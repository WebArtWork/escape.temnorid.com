using System.Collections.Generic;
using ITKamianets.Engine.FixedCamera;
using ITKamianets.Engine.Input;
using ITKamianets.Engine.Save;
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
		}

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
		}

		void OnDestroy()
		{
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
			Player.Teleport(PrisonLayout.CellStart, 90f);
			Guard.ResetPatrol(0f);
			Rig.Snap();
			Mode = Mode.Intro;
			SaveNow();
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
			Player.Teleport(PrisonLayout.CheckpointPosition(State.Checkpoint), 90f);
			Guard.ResetPatrol(EscapeConfig.GraceSeconds);
			Rig.Snap();
			Mode = Mode.Playing;
			return true;
		}

		/// <summary>Writes the save. Never called while a puzzle screen is open, so no half-entered input is stored.</summary>
		public void SaveNow()
		{
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
			Player.Frozen = Busy || Hidden;
			var input = EngineInput.Default;
			var back = input.UI.Cancel.WasPressedThisFrame();

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

		void PlayingFrame(float dt, EngineInput input, bool back)
		{
			State.ElapsedSeconds += dt;
			Hint.Tick(dt, State.CurrentPuzzle);
			if (_requestedFor != State.CurrentPuzzle)
			{
				_requestedFor = State.CurrentPuzzle;
				_requestedHint = 0;
			}

			Guard.TargetHidden = Hidden;
			Guard.Step(dt);
			if (Mode != Mode.Playing) return; // the guard caught the player this frame

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
			if (Hidden && input.Player.Move.ReadValue<Vector2>().sqrMagnitude > 0.25f) Unhide();
			else if (input.Player.Interact.WasPressedThisFrame()) Interact();

			WatchPlace();
		}

		/// <summary>Checkpoints and the way out, by where the player stands.</summary>
		void WatchPlace()
		{
			var p = Player.Position2D;
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
			Mode = Mode.Victory;
			Audio.Play(Sfx.Success);
			SaveNow();
		}

		// ---- interactions ----

		bool Available(Interactable item)
		{
			if (Hidden) return item.Id == Ids.CorridorHidingSpot;
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
			if (id == Ids.GateKeyhole && State.CanCombine) return "E — " + Loc.T("combine");
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
			return Loc.T("prompt", verb, Loc.T("o." + id));
		}

		/// <summary>Uses the object in front of the player. Returns whether anything was done.</summary>
		public bool Interact()
		{
			if (Mode != Mode.Playing || Target == null) return false;
			Use(Target.Id);
			return true;
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
						Audio.Play(Sfx.Click);
						Toast(Loc.T("t.cup.moved"));
					}
					else Toast(Loc.T("t.cup.done"));
					break;
				case Ids.CellWire:
					if (State.TakeWire())
					{
						WireObject.SetActive(false);
						Audio.Play(Sfx.Pickup);
						Toast(Loc.T("t.wire"));
						Autosave();
					}
					break;
				case Ids.CellLatch:
					if (State.CanOpenLatch) OpenLatch();
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
					if (!State.CabinetOpen) OpenCabinet();
					break;
				case Ids.ConfiscatedPouch:
					if (State.TakePouch())
					{
						PouchObject.SetActive(false);
						Audio.Play(Sfx.Pickup);
						State.Read(Ids.ClueNote);
						ShowReading(Loc.T("t.pouch") + "\n\n" + Loc.T("t.note"));
						Autosave();
					}
					break;
				case Ids.WorkbenchKeyPiece:
					if (State.TakeHalfB())
					{
						HalfBObject.SetActive(false);
						Audio.Play(Sfx.Pickup);
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
				Audio.Play(Sfx.Metal);
				Toast(Loc.T("t.key.in"));
				Autosave();
			}
			else Toast(Loc.T("t.key.missing"));
		}

		/// <summary>Joins the two key halves (from the inventory or at the gate).</summary>
		public bool Combine()
		{
			if (!State.CombineKey()) return false;
			Audio.Play(Sfx.Pickup);
			Toast(Loc.T("t.combined"));
			Autosave();
			return true;
		}

		void Pull(Lever lever)
		{
			var result = State.PullLever(lever);
			switch (result)
			{
				case LeverResult.Blocked:
					Audio.Play(Sfx.Dull);
					Toast(Loc.T("t.lever.blocked"));
					break;
				case LeverResult.UpperRaised:
					LeverUpper.SetOpen(true);
					Audio.Play(Sfx.Lever);
					Toast(Loc.T("t.lever.upper"));
					break;
				case LeverResult.WrongOrder:
					LeverUpper.SetOpen(false);
					LeverLower.SetOpen(false);
					Audio.Play(Sfx.Dull);
					Toast(Loc.T("t.lever.wrong"));
					break;
				case LeverResult.GateOpened:
					LeverLower.SetOpen(true);
					Gate.SetOpen(true);
					Audio.Play(Sfx.Lever);
					Audio.Play(Sfx.Door);
					Toast(Loc.T("t.lever.open"));
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
			ReadingText = text;
			Mode = Mode.Reading;
		}

		public void CloseReading()
		{
			if (Mode == Mode.Reading) Mode = Mode.Playing;
		}

		public void Toast(string text, float seconds = 3.2f)
		{
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
			State.ResetLatch();
			LatchMessage = "";
			Mode = Mode.Latch;
			Player.SetVisible(false);
			Rig.PushOverride(LatchCamera, LatchLook, 38f);
		}

		public PuzzleResult LatchPress(LatchStep step)
		{
			if (Mode != Mode.Latch) return PuzzleResult.Blocked;
			var result = State.PressLatch(step);
			switch (result)
			{
				case PuzzleResult.Progress:
					Audio.Play(Sfx.Metal);
					LatchMessage = Loc.T("t.latch.step");
					break;
				case PuzzleResult.Wrong:
					Audio.Play(Sfx.Dull);
					LatchMessage = Loc.T("t.latch.wrong");
					break;
				case PuzzleResult.Solved:
					Audio.Play(Sfx.Metal);
					Audio.Play(Sfx.Door);
					CellDoor.SetOpen(true);
					ClosePuzzle();
					Toast(Loc.T("t.latch.open"));
					Autosave();
					break;
			}
			return result;
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
			var symbol = State.CycleSlot(slot);
			Audio.Play(Sfx.Click);
			CabinetMessage = "";
			RefreshLabels();
			return symbol;
		}

		public PuzzleResult CabinetConfirm()
		{
			if (Mode != Mode.Cabinet) return PuzzleResult.Blocked;
			var result = State.ConfirmCabinet();
			if (result == PuzzleResult.Wrong)
			{
				Audio.Play(Sfx.Dull);
				CabinetMessage = Loc.T("t.cabinet.wrong");
			}
			else if (result == PuzzleResult.Solved)
			{
				CabinetLid.SetOpen(true);
				PouchObject.SetActive(true);
				Audio.Play(Sfx.Metal);
				Audio.Play(Sfx.Door);
				ClosePuzzle();
				Toast(Loc.T("t.cabinet.open"));
				Autosave();
			}
			return result;
		}

		/// <summary>Leaves a puzzle screen (Back). Nothing is lost; the camera goes back to the shot it had.</summary>
		public void ClosePuzzle()
		{
			if (Mode != Mode.Latch && Mode != Mode.Cabinet) return;
			Rig.PopOverride();
			Player.SetVisible(true);
			State.ResetLatch();
			Mode = Mode.Playing;
		}

		// ---- panels, pause ----

		public void OpenPanel(Mode panel)
		{
			if (Mode != Mode.Playing && Mode != Mode.Inventory && Mode != Mode.Journal && Mode != Mode.Pause) return;
			PanelReturn = Mode == Mode.Pause ? Mode.Pause : Mode.Playing;
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
			if (Mode != Mode.Pause) return;
			Hidden = false;
			Guard.TargetHidden = false;
			Player.Teleport(PrisonLayout.CheckpointPosition(State.Checkpoint), 90f);
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
			if (Mode != Mode.Playing) return;
			State.Caught();
			Mode = Mode.Caught;
			_modeTimer = 2f;
			Audio.Play(Sfx.Alert);
		}

		void CaughtFrame(float dt)
		{
			_modeTimer -= dt;
			if (_modeTimer < 0.5f) _fadeTarget = 1f;
			if (_modeTimer > 0f) return;
			Hidden = false;
			Guard.TargetHidden = false;
			Player.Teleport(PrisonLayout.CorridorCheckpoint, 90f);
			Guard.ResetPatrol(EscapeConfig.GraceSeconds);
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
