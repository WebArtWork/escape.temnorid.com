using System;
using System.Collections.Generic;
using ITKamianets.Engine.FixedCamera;
using ITKamianets.Engine.Input;
using ITKamianets.Engine.Net;
using ITKamianets.Engine.Save;
using ITKamianets.Engine.Shell;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Temnorid.Escape
{
	public enum Mode
	{
		Title,
		Intro,
		Playing,
		Latch,
		Cabinet,
		Reading,
		Inventory,
		Journal,
		Pause,
		Settings,
		Caught,
		Victory,
	}

	/// <summary>
	/// The episode: it owns the <see cref="PrisonState"/>, decides what every object does, runs the puzzle screens,
	/// the guard's capture and the saving. Scene objects only show what it says, and <see cref="EscapeHud"/> only shows
	/// the screens, so a test (or a bot) can play the whole game through this class.
	/// </summary>
	[DefaultExecutionOrder(-100)]
	public sealed class EscapeGame : MonoBehaviour
	{
		public static EscapeGame Instance { get; private set; }
		/// <summary>Tests point the save somewhere else.</summary>
		public static string SaveRootOverride;
		/// <summary>Tests (and the editor) can ask for the phone controls and texts.</summary>
		public static bool ForceTouch;
		public static bool TouchUi => ForceTouch || TouchButton.IsNeeded;

		/// <summary>False in an online game: the shared prison never overwrites anybody's own offline save.</summary>
		public bool SaveEnabled = true;
		/// <summary>The link to the other prisoner; null when playing alone.</summary>
		public EscapeNet Net { get; private set; }
		public bool IsGuest => Net != null && Net.IsClient;
		public bool IsHostOnline => Net != null && Net.IsHost;
		/// <summary>True when this machine is in a screen (puzzle, text, panel, menu) or has the profile open: the guard cannot see it.</summary>
		public bool LocalBusy => Mode != Mode.Playing || ProfileOpen;
		bool ProfileOpen => ShellChrome.Current != null && ShellChrome.Current.ProfileOpen;
		/// <summary>Who an action is being done for on the host: null is this machine, otherwise the guest's id.</summary>
		ulong? _actor;

		[Header("Scene")]
		public Player Player;
		public GuardPatrol Guard;
		public FixedViewCamera Rig;
		public EscapeAudio Audio;
		public Mover CellDoor, Gate, CabinetLid, LeverUpper, LeverLower, Cup;
		public GameObject WireObject, PouchObject, HalfBObject, KeyInLock;
		public Transform LatchCamera, LatchLook, CabinetCamera, CabinetLook;
		public TextMesh[] CabinetLabels;

		public PrisonState State { get; private set; } = new PrisonState();
		public CaptureContext Capture { get; private set; } = CaptureContext.Default();
		public HintTimer Hint { get; } = new HintTimer();
		public EscapeSettings Settings { get; private set; } = new EscapeSettings();
		public EscapeSaveStore Store { get; private set; }

		public Mode Mode { get; private set; } = Mode.Title;
		public Mode PanelReturn { get; private set; } = Mode.Playing;
		public bool HasSave { get; private set; }
		public bool SaveProblem { get; private set; }
		public bool Hidden { get; private set; }
		public Interactable Target { get; private set; }
		public string PromptText { get; private set; } = "";
		public string ToastText { get; private set; } = "";
		public float ToastUntil { get; private set; }
		public string ReadingText { get; private set; } = "";
		public string LatchMessage { get; private set; } = "";
		public string CabinetMessage { get; private set; } = "";
		/// <summary>0 clear, 1 black. The HUD draws it.</summary>
		public float Fade { get; private set; }
		public int Saves { get; private set; }

		float _modeTimer;
		float _fadeTarget;
		int _requestedHint;
		string _requestedFor = "";
		InputAction _inventoryKey, _journalKey;

		public string Objective
		{
			get
			{
				switch (State.Milestone)
				{
					case Milestone.CellLocked: return Loc.T("obj.cell");
					case Milestone.CellOpen:
					case Milestone.CorridorAccess: return Loc.T("obj.guard");
					case Milestone.CabinetOpen: return Loc.T("obj.key");
					case Milestone.KeyAssembled:
					case Milestone.GateUnlocked: return Loc.T("obj.gate");
					default: return Loc.T("obj.out");
				}
			}
		}

		/// <summary>The hint to show now: the one earned by waiting, or the one asked for in the journal, whichever is stronger.</summary>
		public int HintLevel => Mathf.Max(Hint.OfferedLevel, _requestedFor == State.CurrentPuzzle ? _requestedHint : 0);

		public string HintText => HintLevel > 0 ? Hints.Text(State.CurrentPuzzle, HintLevel) : "";

		public float GuardAwareness => Guard != null ? Guard.Awareness : 0f;

		bool Busy => Mode != Mode.Playing;

		void Awake()
		{
			Instance = this;
			Time.timeScale = 1f;
			EngineInput.Default.PlayerEnabled = true;

			var map = EngineInput.Default.Asset.FindActionMap("Escape")
				?? EngineInput.Default.AddMap("Escape", m =>
				{
					var inventory = m.AddAction("Inventory", InputActionType.Button, "<Keyboard>/i", groups: EngineInputActions.Groups.KeyboardMouse);
					inventory.AddBinding("<Keyboard>/tab", groups: EngineInputActions.Groups.KeyboardMouse);
					m.AddAction("Journal", InputActionType.Button, "<Keyboard>/j", groups: EngineInputActions.Groups.KeyboardMouse);
				});
			_inventoryKey = map.FindAction("Inventory");
			_journalKey = map.FindAction("Journal");

			Store = new EscapeSaveStore(SaveRootOverride);
			Settings = Store.LoadSettings();
			ApplySettings();
			RefreshSave();
			if (NetSession.Instance.IsOnline) SaveEnabled = false;
			ShellFlow.BeforeQuit += OnBeforeQuit;
		}

		void OnBeforeQuit()
		{
			if (Mode == Mode.Pause || Mode == Mode.Playing) SaveNow();
		}

		/// <summary>Where this prisoner stands: the second prisoner of an online game a step beside the first.</summary>
		Vector2 At(Vector2 point) => point + (Net != null && Net.Slot > 0 ? new Vector2(0f, 1f) : Vector2.zero);

		void Start()
		{
			Rig.Subject = Player.transform;
			Guard.Target = Player;
			Guard.Detected += OnDetected;
			Player.Rig = Rig;
			ApplyWorld(true);
			Guard.ResetPatrol(0f);
			Guard.Footstep += volume => Audio.Play(Sfx.Footstep, volume * 0.5f);
			Player.Teleport(PrisonLayout.CellStart, 90f);
			Rig.Snap();

			if (NetSession.Instance.IsOnline)
			{
				SaveEnabled = false;
				Net = gameObject.AddComponent<EscapeNet>();
				Net.Init(this);
				if (Net.IsClient)
				{
					// The host owns the story: wait for its state, and start playing when the opening text is closed.
					Player.Teleport(At(PrisonLayout.CellStart), 90f);
					Rig.Snap();
					Hint.Tick(0f, "");
					Mode = Mode.Intro;
				}
				else NewGame();
			}
		}

		void OnDestroy()
		{
			ShellFlow.BeforeQuit -= OnBeforeQuit;
			if (Instance == this) Instance = null;
		}

		// ---- starting, saving ----

		/// <summary>Reads the save slot to decide whether Continue is offered.</summary>
		public void RefreshSave()
		{
			var saved = Store.Load(out var status);
			SaveProblem = status == SaveStatus.Corrupt || status == SaveStatus.UnsupportedVersion || status == SaveStatus.IoError;
			HasSave = false;
			if (saved == null) return;
			if (saved.escaped) return;
			if (new PrisonState().Load(saved)) HasSave = true;
			else SaveProblem = true;
		}

		public void NewGame(bool theft = false)
		{
			State = new PrisonState();
			Player.SetVisible(true);
			Capture = CaptureContext.Default(theft);
			Hint.Tick(0f, "");
			_requestedHint = 0;
			Hidden = false;
			Guard.TargetHidden = false;
			ApplyWorld(true);
			Player.Teleport(At(PrisonLayout.CellStart), 90f);
			Guard.ResetPatrol(0f);
			Rig.Snap();
			Mode = Mode.Intro;
			SaveNow();
			Net?.MarkDirty();
		}

		/// <summary>Leaves the opening text and starts playing.</summary>
		public void BeginPlay()
		{
			if (Mode != Mode.Intro) return;
			Mode = Mode.Playing;
		}

		public bool ContinueGame()
		{
			var saved = Store.Load(out _);
			var state = new PrisonState();
			if (saved == null || !state.Load(saved))
			{
				SaveProblem = true;
				HasSave = false;
				return false;
			}
			State = state;
			Player.SetVisible(true);
			Capture = CaptureContext.Default();
			Hidden = false;
			Guard.TargetHidden = false;
			Hint.Tick(0f, "");
			ApplyWorld(true);
			Player.Teleport(At(PrisonLayout.CheckpointPosition(State.Checkpoint)), 90f);
			Guard.ResetPatrol(EscapeConfig.GraceSeconds);
			Rig.Snap();
			Mode = Mode.Playing;
			return true;
		}

		/// <summary>Writes the save. Never called while a puzzle screen is open, so no half-entered input is stored.</summary>
		public void SaveNow()
		{
			if (!SaveEnabled) return;
			var save = State.ToSave(Player != null ? Player.Position2D : Vector2.zero, Player != null ? Player.Yaw : 0f);
			if (Store.Save(save) == SaveStatus.Ok) Saves++;
			RefreshSave();
		}

		void Autosave()
		{
			if (Mode == Mode.Latch || Mode == Mode.Cabinet) return;
			SaveNow();
		}

		void ApplySettings()
		{
			if (Audio != null) Audio.SetVolumes(Settings.masterVolume, Settings.sfxVolume);
			if (!Application.isEditor && Screen.fullScreen != Settings.fullscreen) Screen.fullScreen = Settings.fullscreen;
		}

		public void SetVolumes(float master, float sfx)
		{
			Settings.masterVolume = Mathf.Clamp01(master);
			Settings.sfxVolume = Mathf.Clamp01(sfx);
			ApplySettings();
			Store.SaveSettings(Settings);
		}

		public void SetFullscreen(bool on)
		{
			Settings.fullscreen = on;
			ApplySettings();
			Store.SaveSettings(Settings);
		}

		// ---- the world follows the state ----

		void ApplyWorld(bool instant)
		{
			CellDoor.SetOpen(State.CellLatchUnlocked, instant);
			Cup.SetOpen(State.CupMoved, instant);
			WireObject.SetActive(State.CupMoved && !State.WireTaken);
			CabinetLid.SetOpen(State.CabinetOpen, instant);
			PouchObject.SetActive(State.CabinetOpen && !State.PouchTaken);
			HalfBObject.SetActive(!State.HalfBTaken);
			KeyInLock.SetActive(State.GateKeyInserted);
			LeverUpper.SetOpen(State.UpperLeverDone, instant);
			LeverLower.SetOpen(State.LowerLeverDone, instant);
			Gate.SetOpen(State.ServiceGateOpen, instant);
			RefreshLabels();
		}

		void RefreshLabels()
		{
			if (CabinetLabels == null) return;
			for (var i = 0; i < CabinetLabels.Length && i < 3; i++)
				if (CabinetLabels[i] != null) CabinetLabels[i].text = Loc.T("s." + State.Slot(i));
		}

		// ---- the frame ----

		void Update()
		{
			var dt = Time.deltaTime;
			Fade = Mathf.MoveTowards(Fade, _fadeTarget, dt * 2.5f);
			Player.Frozen = Busy || Hidden || ProfileOpen;
			var input = EngineInput.Default;
			var back = input.UI.Cancel.WasPressedThisFrame();

			// Online the world belongs to the host and keeps running while its own player is in a screen.
			if (IsHostOnline && Mode != Mode.Playing && WorldRunsInScreen(Mode)) HostWorldStep(dt);
			// A puzzle somebody else solved closes on the screen of the one still working at it.
			if (Net != null && ((Mode == Mode.Latch && !State.CanOpenLatch) || (Mode == Mode.Cabinet && State.CabinetOpen))) ClosePuzzle();

			switch (Mode)
			{
				case Mode.Playing:
					PlayingFrame(dt, input, back);
					break;
				case Mode.Intro:
					if (input.Player.Interact.WasPressedThisFrame() || input.UI.Submit.WasPressedThisFrame()) BeginPlay();
					break;
				case Mode.Latch:
				case Mode.Cabinet:
					if (back) ClosePuzzle();
					break;
				case Mode.Reading:
					if (back || input.Player.Interact.WasPressedThisFrame()) CloseReading();
					break;
				case Mode.Inventory:
					if (back || _inventoryKey.WasPressedThisFrame()) ClosePanel();
					else if (_journalKey.WasPressedThisFrame()) OpenPanel(Mode.Journal);
					break;
				case Mode.Journal:
					if (back || _journalKey.WasPressedThisFrame()) ClosePanel();
					else if (_inventoryKey.WasPressedThisFrame()) OpenPanel(Mode.Inventory);
					break;
				case Mode.Pause:
					if (back) Resume();
					break;
				case Mode.Settings:
					if (back) CloseSettings();
					break;
				case Mode.Caught:
					CaughtFrame(dt);
					break;
			}
		}

		static bool WorldRunsInScreen(Mode mode)
		{
			switch (mode)
			{
				case Mode.Latch:
				case Mode.Cabinet:
				case Mode.Reading:
				case Mode.Inventory:
				case Mode.Journal:
				case Mode.Pause:
				case Mode.Settings:
					return true;
				default:
					return false;
			}
		}

		/// <summary>Host, online: the guard and the clock go on while the host's own prisoner is in a screen (and so cannot be seen).</summary>
		void HostWorldStep(float dt)
		{
			State.ElapsedSeconds += dt;
			Guard.TargetHidden = true;
			WatchOther();
			Guard.Step(dt);
			if (Mode == Mode.Caught || Mode == Mode.Victory) return;
			WatchPlaces();
		}

		/// <summary>Host: tells the guard where the other prisoner is and whether it may see them.</summary>
		void WatchOther()
		{
			if (Net != null && Net.IsHost && Net.TryGetOther(out var other))
			{
				Guard.Other = other.transform;
				Guard.OtherHidden = !other.Exposed;
			}
			else
			{
				Guard.Other = null;
				Guard.OtherHidden = true;
			}
		}

		void PlayingFrame(float dt, EngineInput input, bool back)
		{
			var profile = ProfileOpen;
			if (profile && Net == null) return; // alone, the profile panel stops the game like a pause
			State.ElapsedSeconds += dt;
			Hint.Tick(dt, State.CurrentPuzzle);
			if (_requestedFor != State.CurrentPuzzle)
			{
				_requestedFor = State.CurrentPuzzle;
				_requestedHint = 0;
			}

			if (!IsGuest)
			{
				Guard.TargetHidden = Hidden || profile;
				WatchOther();
				Guard.Step(dt);
				if (Mode != Mode.Playing) return; // the guard caught the player this frame
			}
			if (profile)
			{
				WatchPlaces();
				return;
			}

			Target = Interactable.Pick(Player.transform.position, Player.transform.forward, EscapeConfig.InteractRange, Available);
			PromptText = Target != null ? Prompt(Target) : "";

			if (back)
			{
				Pause();
				return;
			}
			if (_inventoryKey.WasPressedThisFrame())
			{
				OpenPanel(Mode.Inventory);
				return;
			}
			if (_journalKey.WasPressedThisFrame())
			{
				OpenPanel(Mode.Journal);
				return;
			}
			if (Hidden && (input.Player.Move.ReadValue<Vector2>().sqrMagnitude > 0.25f || Player.TouchMove.sqrMagnitude > 0.25f)) Unhide();
			else if (input.Player.Interact.WasPressedThisFrame()) Interact();

			WatchPlaces();
		}

		/// <summary>Checkpoints and the way out, by where the prisoners stand. Only the host (or a lone player) decides them.</summary>
		void WatchPlaces()
		{
			if (IsGuest) return;
			Watch(Player.Position2D);
			if (IsHostOnline && Mode != Mode.Victory)
				foreach (var other in Net.OtherPositions()) Watch(other);
		}

		void Watch(Vector2 p)
		{
			if (State.CellLatchUnlocked && !State.CorridorReached && p.x > -7.2f && p.y > -2f && p.y < 2f)
			{
				if (State.ReachCorridor()) Autosave();
			}
			if (State.CorridorReached && p.y < -3.5f && p.x > 3f && p.x < 13f)
			{
				if (State.ReachStore()) Autosave();
			}
			if (State.ServiceGateOpen && p.x > PrisonLayout.ExitLineX) Win();
		}

		void Win()
		{
			if (!State.TryEscape()) return;
			AbortScreens();
			Mode = Mode.Victory;
			Audio.Play(Sfx.Success);
			SaveNow();
			Net?.AnnounceWin();
		}

		/// <summary>Closes a puzzle screen, a text or a hiding place before something takes over the whole screen (being caught, the escape).</summary>
		void AbortScreens()
		{
			if (Mode == Mode.Latch || Mode == Mode.Cabinet)
			{
				Rig.PopOverride();
				Player.SetVisible(true);
				State.ResetLatch();
			}
			Hidden = false;
			Guard.TargetHidden = false;
		}

		// ---- interactions ----

		bool Available(Interactable item)
		{
			if (Hidden) return item.Id == Ids.CorridorHidingSpot;
			return StateAvailable(item);
		}

		/// <summary>Whether the state lets anybody use the object now (hiding is a matter of one prisoner and is checked apart).</summary>
		bool StateAvailable(Interactable item)
		{
			switch (item.Id)
			{
				case Ids.CellWire: return State.CupMoved && !State.WireTaken;
				case Ids.CellLatch: return !State.CellLatchUnlocked;
				case Ids.RecordsCabinet: return !State.CabinetOpen;
				case Ids.ConfiscatedPouch: return State.CabinetOpen && !State.PouchTaken;
				case Ids.WorkbenchKeyPiece: return !State.HalfBTaken;
				case Ids.GateKeyhole: return !State.GateKeyInserted;
				case Ids.LeverUpper:
				case Ids.LeverLower: return !State.ServiceGateOpen;
				default: return true;
			}
		}

		string Prompt(Interactable item)
		{
			var id = item.Id;
			var key = TouchUi ? "" : "E — ";
			if (id == Ids.GateKeyhole && State.CanCombine) return key + Loc.T("combine");
			string verb;
			switch (id)
			{
				case Ids.CellWire:
				case Ids.ConfiscatedPouch:
				case Ids.WorkbenchKeyPiece:
					verb = Loc.T("verb.take");
					break;
				case Ids.CellLatch:
				case Ids.RecordsCabinet:
				case Ids.GateKeyhole:
					verb = Loc.T("verb.use");
					break;
				case Ids.LeverUpper:
				case Ids.LeverLower:
					verb = Loc.T("verb.pull");
					break;
				case Ids.CorridorHidingSpot:
					verb = Hidden ? Loc.T("verb.leave") : Loc.T("verb.hide");
					break;
				default:
					verb = Loc.T("verb.examine");
					break;
			}
			return key + verb + " " + Loc.T("o." + id);
		}

		/// <summary>Uses the object in front of the player. Returns whether anything was done.</summary>
		public bool Interact()
		{
			if (Mode != Mode.Playing || Target == null) return false;
			// A guest asks the host; hiding behind the barrels is each prisoner's own business.
			if (IsGuest && Target.Id != Ids.CorridorHidingSpot) Net.SendUse(Target.Id);
			else Use(Target.Id);
			return true;
		}

		/// <summary>Host: a request from the other prisoner (already checked to be close enough). The rules decide; the answer goes back to them.</summary>
		public void HostUse(ulong who, string id)
		{
			if (!IsHostOnline || id == Ids.CorridorHidingSpot || Mode == Mode.Caught || Mode == Mode.Victory || State.Escaped) return;
			Interactable item = null;
			foreach (var candidate in Interactable.All)
				if (candidate.Id == id) { item = candidate; break; }
			if (item == null || !StateAvailable(item)) return;
			DoFor(who, () => Use(id));
		}

		void DoFor(ulong who, Action action)
		{
			_actor = who;
			try { action(); }
			finally { _actor = null; }
			Net.MarkDirty();
		}

		/// <summary>The effect of using an object, by its stable id.</summary>
		public void Use(string id)
		{
			switch (id)
			{
				case Ids.CellBed:
					Toast(Loc.T("t.bed"));
					break;
				case Ids.CellWallClue:
					ReadClue(Ids.ClueWall, Loc.T("t.wall"));
					break;
				case Ids.CellCup:
					if (State.MoveCup())
					{
						Cup.SetOpen(true);
						WireObject.SetActive(true);
						Sound(Sfx.Click);
						Toast(Loc.T("t.cup.moved"));
					}
					else Toast(Loc.T("t.cup.done"));
					break;
				case Ids.CellWire:
					if (State.TakeWire())
					{
						WireObject.SetActive(false);
						Sound(Sfx.Pickup);
						Toast(Loc.T("t.wire"));
						Autosave();
					}
					break;
				case Ids.CellLatch:
					if (State.CanOpenLatch)
					{
						if (_actor.HasValue)
						{
							State.ResetLatch();
							Net.Fx(_actor.Value, "latch");
						}
						else OpenLatch();
					}
					else Toast(Loc.T("t.latch.nowire"));
					break;
				case Ids.CorridorHidingSpot:
					if (Hidden) Unhide();
					else Hide();
					break;
				case Ids.StoreroomLedger:
					ReadClue(Ids.ClueLedger, Loc.T("t.ledger"));
					break;
				case Ids.StoreShelf:
					Toast(Loc.T("t.shelf"));
					break;
				case Ids.RecordsCabinet:
					if (!State.CabinetOpen)
					{
						if (_actor.HasValue) Net.Fx(_actor.Value, "cabinet");
						else OpenCabinet();
					}
					break;
				case Ids.ConfiscatedPouch:
					if (State.TakePouch())
					{
						PouchObject.SetActive(false);
						Sound(Sfx.Pickup);
						State.Read(Ids.ClueNote);
						ShowReading(Loc.T("t.pouch") + "\n\n" + Loc.T("t.note"));
						Autosave();
					}
					break;
				case Ids.WorkbenchKeyPiece:
					if (State.TakeHalfB())
					{
						HalfBObject.SetActive(false);
						Sound(Sfx.Pickup);
						Toast(Loc.T("t.half.b"));
						Autosave();
					}
					break;
				case Ids.GatePlaque:
					ReadClue(Ids.CluePlaque, Loc.T("t.plaque"));
					break;
				case Ids.GateKeyhole:
					UseKeyhole();
					break;
				case Ids.LeverUpper:
					Pull(Lever.Upper);
					break;
				case Ids.LeverLower:
					Pull(Lever.Lower);
					break;
			}
			Net?.MarkDirty();
		}

		void UseKeyhole()
		{
			if (State.CanCombine)
			{
				Combine();
				return;
			}
			if (State.InsertKey())
			{
				KeyInLock.SetActive(true);
				Sound(Sfx.Metal);
				Toast(Loc.T("t.key.in"));
				Autosave();
			}
			else Toast(Loc.T("t.key.missing"));
		}

		/// <summary>Joins the two key halves (from the inventory or at the gate).</summary>
		public bool Combine()
		{
			if (IsGuest)
			{
				Net.SendCombine();
				return true;
			}
			if (!State.CombineKey()) return false;
			Sound(Sfx.Pickup);
			ToastAll(Loc.T("t.combined"));
			Autosave();
			Net?.MarkDirty();
			return true;
		}

		/// <summary>Host: the other prisoner joins the key halves from their inventory.</summary>
		public void HostCombine(ulong who)
		{
			if (!IsHostOnline || Mode == Mode.Caught || Mode == Mode.Victory) return;
			DoFor(who, () => Combine());
		}

		void Pull(Lever lever)
		{
			var result = State.PullLever(lever);
			switch (result)
			{
				case LeverResult.Blocked:
					Sound(Sfx.Dull);
					Toast(Loc.T("t.lever.blocked"));
					break;
				case LeverResult.UpperRaised:
					LeverUpper.SetOpen(true);
					Sound(Sfx.Lever);
					Toast(Loc.T("t.lever.upper"));
					break;
				case LeverResult.WrongOrder:
					LeverUpper.SetOpen(false);
					LeverLower.SetOpen(false);
					Sound(Sfx.Dull);
					Toast(Loc.T("t.lever.wrong"));
					break;
				case LeverResult.GateOpened:
					LeverLower.SetOpen(true);
					Gate.SetOpen(true);
					Sound(Sfx.Lever);
					Sound(Sfx.Door);
					ToastAll(Loc.T("t.lever.open"));
					Autosave();
					break;
				case LeverResult.AlreadyDone:
					Toast(Loc.T("t.lever.done"));
					break;
			}
		}

		void ReadClue(string clue, string text)
		{
			State.Read(clue);
			ShowReading(text);
		}

		void ShowReading(string text)
		{
			if (_actor.HasValue)
			{
				Net.Fx(_actor.Value, "read", text);
				return;
			}
			ReadingText = text;
			Mode = Mode.Reading;
		}

		/// <summary>A sound for the shared world: played here and, online, sent to the other prisoner too.</summary>
		void Sound(Sfx sfx, float volume = 1f)
		{
			Audio.Play(sfx, volume);
			if (IsHostOnline) Net.Sound(sfx, volume);
		}

		/// <summary>A line for everybody (the host shows it and tells the guest).</summary>
		void ToastAll(string text)
		{
			var actor = _actor;
			_actor = null;
			Toast(text);
			_actor = actor;
			if (IsHostOnline) Net.FxAll("toast", text);
		}

		public void CloseReading()
		{
			if (Mode == Mode.Reading) Mode = Mode.Playing;
		}

		public void Toast(string text, float seconds = 3.2f)
		{
			if (_actor.HasValue)
			{
				Net.Fx(_actor.Value, "toast", text);
				return;
			}
			ToastText = text;
			ToastUntil = Time.unscaledTime + seconds;
		}

		public bool ToastActive => Time.unscaledTime < ToastUntil;

		// ---- hiding ----

		void Hide()
		{
			Hidden = true;
			Player.Teleport(PrisonLayout.HidePoint, 180f);
			Guard.TargetHidden = true;
			Audio.Play(Sfx.Click);
			Toast(Loc.T("t.hide"), 2f);
		}

		void Unhide()
		{
			if (!Hidden) return;
			Hidden = false;
			Guard.TargetHidden = false;
			Player.Teleport(PrisonLayout.HideExit, 0f);
		}

		// ---- puzzles ----

		public void OpenLatch()
		{
			if (Mode != Mode.Playing || !State.CanOpenLatch) return;
			if (!IsGuest) State.ResetLatch();
			LatchMessage = "";
			Mode = Mode.Latch;
			Player.SetVisible(false);
			Rig.PushOverride(LatchCamera, LatchLook, 38f);
		}

		/// <summary>A line on the latch screen: here, or on the screen of the prisoner the action is for.</summary>
		void LatchLine(string text)
		{
			if (_actor.HasValue) Net.Fx(_actor.Value, "latchmsg", text);
			else LatchMessage = text;
		}

		void CabinetLine(string text)
		{
			if (_actor.HasValue) Net.Fx(_actor.Value, "cabmsg", text);
			else CabinetMessage = text;
		}

		public PuzzleResult LatchPress(LatchStep step)
		{
			if (Mode != Mode.Latch) return PuzzleResult.Blocked;
			if (IsGuest)
			{
				Net.SendLatch(step);
				return PuzzleResult.Blocked; // the host decides; the answer comes back as a line and the state
			}
			return ApplyLatch(step);
		}

		PuzzleResult ApplyLatch(LatchStep step)
		{
			var result = State.PressLatch(step);
			switch (result)
			{
				case PuzzleResult.Progress:
					Sound(Sfx.Metal);
					LatchLine(Loc.T("t.latch.step"));
					break;
				case PuzzleResult.Wrong:
					Sound(Sfx.Dull);
					LatchLine(Loc.T("t.latch.wrong"));
					break;
				case PuzzleResult.Solved:
					Sound(Sfx.Metal);
					Sound(Sfx.Door);
					CellDoor.SetOpen(true);
					EndPuzzleScreen();
					ToastAll(Loc.T("t.latch.open"));
					Autosave();
					break;
			}
			Net?.MarkDirty();
			return result;
		}

		/// <summary>Host: a latch step from the other prisoner.</summary>
		public void HostLatch(ulong who, LatchStep step)
		{
			if (!IsHostOnline || !State.CanOpenLatch || Mode == Mode.Caught || Mode == Mode.Victory) return;
			DoFor(who, () => ApplyLatch(step));
		}

		/// <summary>The puzzle screen is done: close this machine's, or the one of the prisoner the action is for.</summary>
		void EndPuzzleScreen()
		{
			if (_actor.HasValue) Net.Fx(_actor.Value, "close");
			else ClosePuzzle();
		}

		public void OpenCabinet()
		{
			if (Mode != Mode.Playing || State.CabinetOpen) return;
			CabinetMessage = "";
			Mode = Mode.Cabinet;
			Player.SetVisible(false);
			Rig.PushOverride(CabinetCamera, CabinetLook, 38f);
		}

		public Symbol CabinetCycle(int slot)
		{
			if (Mode != Mode.Cabinet) return State.Slot(slot);
			if (IsGuest)
			{
				Net.SendCycle(slot);
				CabinetMessage = "";
				return State.Slot(slot);
			}
			var symbol = State.CycleSlot(slot);
			Sound(Sfx.Click);
			CabinetMessage = "";
			RefreshLabels();
			Net?.MarkDirty();
			return symbol;
		}

		/// <summary>Host: a symbol turned by the other prisoner.</summary>
		public void HostCycle(ulong who, int slot)
		{
			if (!IsHostOnline || State.CabinetOpen || Mode == Mode.Caught || Mode == Mode.Victory) return;
			State.CycleSlot(slot);
			Sound(Sfx.Click);
			RefreshLabels();
			Net.Fx(who, "cabmsg", "");
			Net.MarkDirty();
		}

		public PuzzleResult CabinetConfirm()
		{
			if (Mode != Mode.Cabinet) return PuzzleResult.Blocked;
			if (IsGuest)
			{
				Net.SendConfirm();
				return PuzzleResult.Blocked;
			}
			return ApplyConfirm();
		}

		PuzzleResult ApplyConfirm()
		{
			var result = State.ConfirmCabinet();
			if (result == PuzzleResult.Wrong)
			{
				Sound(Sfx.Dull);
				CabinetLine(Loc.T("t.cabinet.wrong"));
			}
			else if (result == PuzzleResult.Solved)
			{
				CabinetLid.SetOpen(true);
				PouchObject.SetActive(true);
				Sound(Sfx.Metal);
				Sound(Sfx.Door);
				EndPuzzleScreen();
				ToastAll(Loc.T("t.cabinet.open"));
				Autosave();
			}
			Net?.MarkDirty();
			return result;
		}

		/// <summary>Host: the other prisoner tries the three symbols.</summary>
		public void HostConfirm(ulong who)
		{
			if (!IsHostOnline || State.CabinetOpen || Mode == Mode.Caught || Mode == Mode.Victory) return;
			DoFor(who, () => ApplyConfirm());
		}

		/// <summary>Leaves a puzzle screen (Back). Nothing is lost; the camera goes back to the shot it had.</summary>
		public void ClosePuzzle()
		{
			if (Mode != Mode.Latch && Mode != Mode.Cabinet) return;
			Rig.PopOverride();
			Player.SetVisible(true);
			if (IsGuest) Net.SendClose();
			else State.ResetLatch();
			Mode = Mode.Playing;
		}

		/// <summary>Host: the other prisoner left a puzzle screen; the dots start over.</summary>
		public void HostClosePuzzle()
		{
			if (!IsHostOnline) return;
			State.ResetLatch();
			Net.MarkDirty();
		}

		// ---- panels, pause ----

		public void OpenPanel(Mode panel)
		{
			if (Mode != Mode.Playing && Mode != Mode.Inventory && Mode != Mode.Journal && Mode != Mode.Pause) return;
			if (Mode != Mode.Inventory && Mode != Mode.Journal) PanelReturn = Mode == Mode.Pause ? Mode.Pause : Mode.Playing;
			Mode = panel;
		}

		public void ClosePanel()
		{
			if (Mode != Mode.Inventory && Mode != Mode.Journal) return;
			Mode = PanelReturn;
		}

		/// <summary>Asks for the next hint for the current puzzle from the journal.</summary>
		public void RequestHint()
		{
			var puzzle = State.CurrentPuzzle;
			if (puzzle == "") return;
			if (_requestedFor != puzzle)
			{
				_requestedFor = puzzle;
				_requestedHint = 0;
			}
			_requestedHint = Mathf.Min(2, Mathf.Max(_requestedHint, Hint.OfferedLevel) + 1);
		}

		public void Pause()
		{
			if (Mode != Mode.Playing) return;
			Autosave();
			Mode = Mode.Pause;
		}

		public void Resume()
		{
			if (Mode == Mode.Pause) Mode = Mode.Playing;
		}

		public void OpenSettings()
		{
			PanelReturn = Mode;
			Mode = Mode.Settings;
		}

		public void CloseSettings()
		{
			if (Mode == Mode.Settings) Mode = PanelReturn == Mode.Pause ? Mode.Pause : Mode.Title;
		}

		/// <summary>Back to the last checkpoint (from the pause menu). Solved puzzles and items stay.</summary>
		public void RestartFromCheckpoint()
		{
			if (Mode != Mode.Pause || Net != null) return; // online the guard is shared: nobody restarts it alone
			Hidden = false;
			Guard.TargetHidden = false;
			Player.Teleport(At(PrisonLayout.CheckpointPosition(State.Checkpoint)), 90f);
			Guard.ResetPatrol(EscapeConfig.GraceSeconds);
			Rig.Snap();
			Mode = Mode.Playing;
		}

		public void ToTitle()
		{
			if (Mode == Mode.Pause || Mode == Mode.Playing) SaveNow();
			RefreshSave();
			Hidden = false;
			Mode = Mode.Title;
		}

		public void Replay() => NewGame(Capture.CaptureReason == "theft");

		// ---- being caught ----

		void OnDetected()
		{
			if (IsGuest || Mode == Mode.Caught || Mode == Mode.Victory) return;
			// Alone the guard only watches while the player plays; online it watches whoever is out in the open, whatever the other one is doing.
			if (Net == null && Mode != Mode.Playing) return;
			if (Net != null && !WorldRunsInScreen(Mode) && Mode != Mode.Playing) return;
			State.Caught();
			AbortScreens();
			Mode = Mode.Caught;
			_modeTimer = 2f;
			Audio.Play(Sfx.Alert);
			if (IsHostOnline)
			{
				Net.AnnounceCaught();
				Net.MarkDirty();
			}
		}

		/// <summary>Guest: the host says the guard caught a prisoner. Both go back to the corridor.</summary>
		public void CaughtRemote()
		{
			if (!IsGuest || Mode == Mode.Caught || Mode == Mode.Victory || Mode == Mode.Title || Mode == Mode.Intro) return;
			AbortScreens();
			Mode = Mode.Caught;
			_modeTimer = 2f;
			Audio.Play(Sfx.Alert);
		}

		/// <summary>Guest: the host says a prisoner went out through the gate. The episode is over for both.</summary>
		public void WinRemote()
		{
			if (!IsGuest || Mode == Mode.Victory) return;
			State.TryEscape();
			AbortScreens();
			Mode = Mode.Victory;
			Audio.Play(Sfx.Success);
		}

		/// <summary>Guest: the host's state of the prison. The world follows it.</summary>
		public void ApplyState(EscapeStatePacket packet)
		{
			if (packet == null || packet.save == null) return;
			var state = new PrisonState();
			if (!state.Load(packet.save))
			{
				Debug.LogWarning("[Escape] The host's state was refused.");
				return;
			}
			var slots = packet.slots != null && packet.slots.Length >= 3 ? packet.slots : new[] { (int)Symbol.Tree, (int)Symbol.Tree, (int)Symbol.Tree };
			state.SetPuzzleView(packet.latch, slots[0], slots[1], slots[2]);
			if (packet.escaped) state.TryEscape();
			State = state;
			ApplyWorld(false);
		}

		/// <summary>Guest: a line, a text or a puzzle screen the host sends to this prisoner only.</summary>
		public void ApplyFx(string kind, string text)
		{
			switch (kind)
			{
				case "toast":
					Toast(text);
					break;
				case "read":
					if (Mode == Mode.Playing) ShowReading(text);
					else Toast(text);
					break;
				case "latch":
					OpenLatch();
					break;
				case "cabinet":
					OpenCabinet();
					break;
				case "latchmsg":
					LatchMessage = text;
					break;
				case "cabmsg":
					CabinetMessage = text;
					break;
				case "close":
					if (Mode == Mode.Latch || Mode == Mode.Cabinet) ClosePuzzle();
					break;
			}
		}

		void CaughtFrame(float dt)
		{
			_modeTimer -= dt;
			if (_modeTimer < 0.5f) _fadeTarget = 1f;
			if (_modeTimer > 0f) return;
			Hidden = false;
			Guard.TargetHidden = false;
			Player.Teleport(At(PrisonLayout.CorridorCheckpoint), 90f);
			if (!IsGuest) Guard.ResetPatrol(EscapeConfig.GraceSeconds);
			Rig.Snap();
			_fadeTarget = 0f;
			Mode = Mode.Playing;
			Autosave();
		}

		public string ElapsedText
		{
			get
			{
				var total = Mathf.FloorToInt(State.ElapsedSeconds);
				return (total / 60) + ":" + (total % 60).ToString("00");
			}
		}

		public IEnumerable<string> ItemNames()
		{
			foreach (var item in State.Items) yield return Loc.T("i." + item);
		}
	}
}
