using System;
using System.Text;
using ITKamianets.Engine.Net;
using ITKamianets.Engine.Shell;
using ITKamianets.Engine.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Temnorid.Escape
{
	/// <summary>All the screens, drawn with plain uGUI built in code, in Ukrainian. It only shows what <see cref="EscapeGame"/> says and calls it when a button is pressed.</summary>
	public sealed class EscapeHud : MonoBehaviour
	{
		public EscapeGame Game;

		static readonly Color Panel = new Color(0.07f, 0.06f, 0.06f, 0.9f);
		static readonly Color Gold = new Color(0.95f, 0.78f, 0.35f);
		static readonly Color Cream = new Color(0.95f, 0.91f, 0.82f);
		static readonly Color Red = new Color(0.85f, 0.25f, 0.2f);

		UiBuilder _ui;
		GameObject _title, _intro, _hud, _latch, _cabinet, _reading, _inventory, _journal, _pause, _settings, _caught, _victory;
		Image _fade, _awareness;
		GameObject _awarenessBack, _promptBox, _toastBox, _hintBox, _combine, _titleContinue;
		Text _objective, _prompt, _toast, _hint, _titleNote, _latchDots, _latchMessage, _cabinetMessage, _readingText, _inventoryText, _journalText, _journalHint;
		Text _masterText, _sfxText, _fullscreenText, _victoryTime, _victoryDetections, _victoryReason, _caughtSub, _pauseTitle;
		Text[] _slotTexts;
		Mode _shown = (Mode)(-1);

		ShellChrome _chrome;
		GameObject _touch, _checkpointButton, _replayButton;
		TouchStick _stick;
		TouchButton _act, _run, _pauseTouch;
		float _statusAt;

		void Start()
		{
			_ui = new UiBuilder
			{
				ButtonColour = new Color(0.22f, 0.18f, 0.14f, 1f),
				ButtonHighlight = new Color(1f, 0.9f, 0.6f),
				ButtonSelected = new Color(1f, 0.85f, 0.45f),
				ButtonPressed = new Color(0.8f, 0.65f, 0.3f),
			};
			UiBuilder.EnsureEventSystem(transform);
			var canvas = UiBuilder.CreateCanvas(transform);
			_chrome = ShellChrome.Create(transform);
			_chrome.PlaceSelected += OnPlace;

			// Every screen lives in the frame the shell's bars leave free.
			var frame = new GameObject("Frame", typeof(RectTransform));
			frame.transform.SetParent(canvas.transform, false);
			var frameRect = (RectTransform)frame.transform;
			frameRect.anchorMin = new Vector2(0f, _chrome.BottomFraction);
			frameRect.anchorMax = new Vector2(1f, 1f - _chrome.TopFraction);
			frameRect.offsetMin = Vector2.zero;
			frameRect.offsetMax = Vector2.zero;
			var root = frame.transform;
			BuildHud(root);
			BuildLatch(root);
			BuildCabinet(root);
			BuildReading(root);
			BuildInventory(root);
			BuildJournal(root);
			BuildPause(root);
			BuildSettings(root);
			BuildCaught(root);
			BuildVictory(root);
			BuildIntro(root);
			BuildTitle(root);
			var fade = UiBuilder.Box("Fade", canvas.transform, Vector2.zero, Vector2.one, Color.black);
			_fade = fade.GetComponent<Image>();
			_fade.raycastTarget = false;
		}

		// ---- building blocks ----

		Button Btn(Transform parent, string text, float x0, float y0, float x1, float y1, Action onClick, int size = 34)
		{
			var button = _ui.Button(text, parent, new Vector2(x0, y0), new Vector2(x1, y1), () =>
			{
				if (Game != null) Game.Audio.Play(Sfx.Click, 0.6f);
				onClick();
			});
			button.GetComponentInChildren<Text>().fontSize = size;
			return button;
		}

		Text Lbl(Transform parent, string text, int size, TextAnchor anchor, float x0, float y0, float x1, float y1, Color colour, bool wrap = false)
			=> _ui.Label("Label", parent, text, size, anchor, new Vector2(x0, y0), new Vector2(x1, y1), colour, wrap);

		static GameObject Screen(string name, Transform root, Color colour, float x0 = 0f, float y0 = 0f, float x1 = 1f, float y1 = 1f)
			=> UiBuilder.Box(name, root, new Vector2(x0, y0), new Vector2(x1, y1), colour);

		// ---- screens ----

		void BuildTitle(Transform root)
		{
			_title = Screen("Title", root, new Color(0.04f, 0.035f, 0.04f, 0.94f));
			var t = _title.transform;
			Lbl(t, Loc.T("subtitle"), 64, TextAnchor.MiddleCenter, 0.1f, 0.68f, 0.9f, 0.86f, Gold);
			Btn(t, Loc.T("menu.new"), 0.35f, 0.46f, 0.65f, 0.55f, () => Game.NewGame());
			var cont = Btn(t, Loc.T("menu.continue"), 0.35f, 0.35f, 0.65f, 0.44f, () => Game.ContinueGame());
			_titleContinue = cont.gameObject;
			Btn(t, Loc.T("menu.settings"), 0.35f, 0.24f, 0.65f, 0.33f, () => Game.OpenSettings());
			Btn(t, Loc.T("menu"), 0.35f, 0.13f, 0.65f, 0.22f, () => ShellFlow.QuitToMenu());
			_titleNote = Lbl(t, "", 30, TextAnchor.MiddleCenter, 0.1f, 0.05f, 0.9f, 0.12f, new Color(1f, 0.6f, 0.5f));
			Lbl(t, Loc.T("menu.demo"), 26, TextAnchor.LowerCenter, 0.1f, 0.0f, 0.9f, 0.05f, new Color(0.7f, 0.68f, 0.62f));
		}

		void BuildIntro(Transform root)
		{
			_intro = Screen("Intro", root, new Color(0.03f, 0.03f, 0.04f, 0.96f));
			var t = _intro.transform;
			Lbl(t, Loc.T("subtitle"), 56, TextAnchor.MiddleCenter, 0.1f, 0.68f, 0.9f, 0.82f, Gold);
			Lbl(t, Loc.T("intro"), 40, TextAnchor.MiddleCenter, 0.18f, 0.35f, 0.82f, 0.68f, Cream, true);
			Btn(t, Loc.T("intro.go"), 0.4f, 0.18f, 0.6f, 0.28f, () => Game.BeginPlay());
		}

		void BuildHud(Transform root)
		{
			_hud = Screen("Hud", root, Color.clear);
			var t = _hud.transform;
			var obj = UiBuilder.Box("ObjectiveBox", t, new Vector2(0.015f, 0.9f), new Vector2(0.42f, 0.985f), new Color(0f, 0f, 0f, 0.6f));
			_objective = Lbl(obj.transform, "", 32, TextAnchor.MiddleLeft, 0.03f, 0f, 0.98f, 1f, Cream, true);
			if (!EscapeGame.TouchUi)
				Lbl(t, "Tab — " + Loc.T("inventory") + "    J — " + Loc.T("journal") + "    Esc — " + Loc.T("pause"), 24, TextAnchor.UpperRight, 0.5f, 0.94f, 0.985f, 0.99f, new Color(0.85f, 0.82f, 0.74f, 0.8f));

			_promptBox = UiBuilder.Box("PromptBox", t, new Vector2(0.3f, 0.07f), new Vector2(0.7f, 0.14f), new Color(0f, 0f, 0f, 0.7f));
			_prompt = Lbl(_promptBox.transform, "", 36, TextAnchor.MiddleCenter, 0f, 0f, 1f, 1f, Gold);

			_toastBox = UiBuilder.Box("ToastBox", t, new Vector2(0.2f, 0.17f), new Vector2(0.8f, 0.27f), new Color(0f, 0f, 0f, 0.7f));
			_toast = Lbl(_toastBox.transform, "", 32, TextAnchor.MiddleCenter, 0.02f, 0f, 0.98f, 1f, Cream, true);

			_hintBox = UiBuilder.Box("HintBox", t, new Vector2(0.015f, 0.82f), new Vector2(0.42f, 0.895f), new Color(0.15f, 0.2f, 0.3f, 0.75f));
			_hint = Lbl(_hintBox.transform, "", 28, TextAnchor.MiddleLeft, 0.03f, 0f, 0.98f, 1f, new Color(0.75f, 0.88f, 1f), true);

			_awarenessBack = UiBuilder.Box("AwarenessBack", t, new Vector2(0.4f, 0.03f), new Vector2(0.6f, 0.05f), new Color(0f, 0f, 0f, 0.6f));
			_awareness = UiBuilder.Box("AwarenessFill", _awarenessBack.transform, Vector2.zero, Vector2.one, Red).GetComponent<Image>();
			if (EscapeGame.TouchUi) BuildTouch(t);
		}

		// On a phone: a thumb stick on the left; an action button (use, take, hide), a run button and a pause button on the right.
		void BuildTouch(Transform parent)
		{
			_touch = new GameObject("Touch", typeof(RectTransform));
			_touch.transform.SetParent(parent, false);
			var rect = (RectTransform)_touch.transform;
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;
			_stick = TouchStick.Create(_touch.transform, new Vector2(0.02f, 0.1f), new Vector2(0.22f, 0.55f));
			_act = TouchButton.Create(_ui, _touch.transform, Loc.T("touch.act"), new Vector2(0.78f, 0.1f), new Vector2(0.98f, 0.38f), 40);
			_run = TouchButton.Create(_ui, _touch.transform, Loc.T("touch.run"), new Vector2(0.82f, 0.42f), new Vector2(0.96f, 0.6f), 32);
			_pauseTouch = TouchButton.Create(_ui, _touch.transform, "II", new Vector2(0.92f, 0.86f), new Vector2(0.99f, 0.99f), 36);
		}

		void UpdateTouch()
		{
			var player = Game.Player;
			var playing = Game.Mode == Mode.Playing && _chrome != null && !_chrome.ProfileOpen;
			_touch.SetActive(playing);
			player.TouchMove = playing && _stick != null ? _stick.Value : Vector2.zero;
			player.TouchRun = playing && _run != null && _run.Held;
			if (!playing) return;
			if (_act != null && _act.ConsumePress()) Game.Interact();
			if (_pauseTouch != null && _pauseTouch.ConsumePress()) Game.Pause();
		}

		void OnPlace(string id)
		{
			if (Game == null) return;
			switch (id)
			{
				case EscapeShell.PlacePrison:
					if (Game.Mode == Mode.Inventory || Game.Mode == Mode.Journal) Game.ClosePanel();
					break;
				case EscapeShell.PlaceInventory:
					Game.OpenPanel(Mode.Inventory);
					break;
				case EscapeShell.PlaceJournal:
					Game.OpenPanel(Mode.Journal);
					break;
			}
		}

		void BuildLatch(Transform root)
		{
			_latch = Screen("Latch", root, new Color(0f, 0f, 0f, 0.15f));
			var panel = UiBuilder.Box("Panel", _latch.transform, new Vector2(0.28f, 0.03f), new Vector2(0.72f, 0.4f), Panel);
			var t = panel.transform;
			Lbl(t, Loc.T("latch.title"), 40, TextAnchor.MiddleCenter, 0.05f, 0.8f, 0.95f, 0.98f, Gold);
			_latchDots = Lbl(t, "", 44, TextAnchor.MiddleCenter, 0.05f, 0.62f, 0.95f, 0.8f, Cream);
			_latchMessage = Lbl(t, "", 30, TextAnchor.MiddleCenter, 0.05f, 0.5f, 0.95f, 0.64f, new Color(0.85f, 0.85f, 0.7f));
			Btn(t, Loc.T("latch.left"), 0.04f, 0.2f, 0.34f, 0.48f, () => Game.LatchPress(LatchStep.Left));
			Btn(t, Loc.T("latch.right"), 0.35f, 0.2f, 0.65f, 0.48f, () => Game.LatchPress(LatchStep.Right));
			Btn(t, Loc.T("latch.lift"), 0.66f, 0.2f, 0.96f, 0.48f, () => Game.LatchPress(LatchStep.Lift));
			Btn(t, Loc.T("back"), 0.35f, 0.03f, 0.65f, 0.17f, () => Game.ClosePuzzle(), 30);
		}

		void BuildCabinet(Transform root)
		{
			_cabinet = Screen("Cabinet", root, new Color(0f, 0f, 0f, 0.15f));
			var panel = UiBuilder.Box("Panel", _cabinet.transform, new Vector2(0.24f, 0.03f), new Vector2(0.76f, 0.42f), Panel);
			var t = panel.transform;
			Lbl(t, Loc.T("cabinet.title"), 40, TextAnchor.MiddleCenter, 0.05f, 0.82f, 0.95f, 0.98f, Gold);
			_slotTexts = new Text[3];
			for (var i = 0; i < 3; i++)
			{
				var slot = i;
				var x0 = 0.04f + i * 0.32f;
				var b = Btn(t, "", x0, 0.42f, x0 + 0.3f, 0.78f, () => Game.CabinetCycle(slot), 34);
				_slotTexts[i] = b.GetComponentInChildren<Text>();
			}
			_cabinetMessage = Lbl(t, "", 30, TextAnchor.MiddleCenter, 0.05f, 0.3f, 0.95f, 0.42f, new Color(1f, 0.7f, 0.55f));
			Btn(t, Loc.T("confirm"), 0.08f, 0.04f, 0.48f, 0.28f, () => Game.CabinetConfirm(), 32);
			Btn(t, Loc.T("back"), 0.52f, 0.04f, 0.92f, 0.28f, () => Game.ClosePuzzle(), 32);
		}

		void BuildReading(Transform root)
		{
			_reading = Screen("Reading", root, new Color(0f, 0f, 0f, 0.5f));
			var panel = UiBuilder.Box("Panel", _reading.transform, new Vector2(0.2f, 0.25f), new Vector2(0.8f, 0.75f), Panel);
			_readingText = Lbl(panel.transform, "", 38, TextAnchor.MiddleCenter, 0.06f, 0.28f, 0.94f, 0.95f, Cream, true);
			Btn(panel.transform, Loc.T("close") + (EscapeGame.TouchUi ? "" : " (E)"), 0.35f, 0.06f, 0.65f, 0.22f, () => Game.CloseReading(), 32);
		}

		void BuildInventory(Transform root)
		{
			_inventory = Screen("Inventory", root, new Color(0f, 0f, 0f, 0.5f));
			var panel = UiBuilder.Box("Panel", _inventory.transform, new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.8f), Panel);
			var t = panel.transform;
			Lbl(t, Loc.T("inventory"), 48, TextAnchor.MiddleCenter, 0.05f, 0.86f, 0.95f, 0.98f, Gold);
			_inventoryText = Lbl(t, "", 32, TextAnchor.UpperLeft, 0.06f, 0.32f, 0.94f, 0.85f, Cream, true);
			_combine = Btn(t, Loc.T("combine"), 0.1f, 0.17f, 0.9f, 0.3f, () => Game.Combine(), 32).gameObject;
			Btn(t, Loc.T("close"), 0.35f, 0.03f, 0.65f, 0.15f, () => Game.ClosePanel(), 30);
		}

		void BuildJournal(Transform root)
		{
			_journal = Screen("Journal", root, new Color(0f, 0f, 0f, 0.5f));
			var panel = UiBuilder.Box("Panel", _journal.transform, new Vector2(0.18f, 0.12f), new Vector2(0.82f, 0.88f), Panel);
			var t = panel.transform;
			Lbl(t, Loc.T("journal"), 48, TextAnchor.MiddleCenter, 0.05f, 0.9f, 0.95f, 0.99f, Gold);
			_journalText = Lbl(t, "", 30, TextAnchor.UpperLeft, 0.06f, 0.3f, 0.94f, 0.89f, Cream, true);
			_journalHint = Lbl(t, "", 30, TextAnchor.MiddleLeft, 0.06f, 0.18f, 0.94f, 0.3f, new Color(0.75f, 0.88f, 1f), true);
			Btn(t, Loc.T("hint"), 0.08f, 0.03f, 0.46f, 0.16f, () => Game.RequestHint(), 30);
			Btn(t, Loc.T("close"), 0.54f, 0.03f, 0.92f, 0.16f, () => Game.ClosePanel(), 30);
		}

		void BuildPause(Transform root)
		{
			_pause = Screen("Pause", root, new Color(0f, 0f, 0f, 0.6f));
			var panel = UiBuilder.Box("Panel", _pause.transform, new Vector2(0.33f, 0.14f), new Vector2(0.67f, 0.86f), Panel);
			var t = panel.transform;
			_pauseTitle = Lbl(t, Loc.T("pause"), 52, TextAnchor.MiddleCenter, 0.05f, 0.86f, 0.95f, 0.98f, Gold);
			Btn(t, Loc.T("resume"), 0.1f, 0.71f, 0.9f, 0.84f, () => Game.Resume());
			Btn(t, Loc.T("journal"), 0.1f, 0.56f, 0.9f, 0.69f, () => Game.OpenPanel(Mode.Journal));
			_checkpointButton = Btn(t, Loc.T("checkpoint"), 0.1f, 0.41f, 0.9f, 0.54f, () => Game.RestartFromCheckpoint(), 30).gameObject;
			Btn(t, Loc.T("menu.settings"), 0.1f, 0.26f, 0.9f, 0.39f, () => Game.OpenSettings());
			Btn(t, Loc.T("menu"), 0.1f, 0.08f, 0.9f, 0.21f, () => ShellFlow.QuitToMenu());
		}

		void BuildSettings(Transform root)
		{
			_settings = Screen("Settings", root, new Color(0.04f, 0.035f, 0.04f, 0.97f));
			var t = _settings.transform;
			Lbl(t, Loc.T("menu.settings"), 64, TextAnchor.MiddleCenter, 0.1f, 0.84f, 0.9f, 0.96f, Gold);
			_masterText = Lbl(t, "", 38, TextAnchor.MiddleRight, 0.1f, 0.7f, 0.5f, 0.8f, Cream);
			Btn(t, "−", 0.52f, 0.7f, 0.58f, 0.8f, () => Game.SetVolumes(Game.Settings.masterVolume - 0.1f, Game.Settings.sfxVolume));
			Btn(t, "+", 0.6f, 0.7f, 0.66f, 0.8f, () => Game.SetVolumes(Game.Settings.masterVolume + 0.1f, Game.Settings.sfxVolume));
			_sfxText = Lbl(t, "", 38, TextAnchor.MiddleRight, 0.1f, 0.57f, 0.5f, 0.67f, Cream);
			Btn(t, "−", 0.52f, 0.57f, 0.58f, 0.67f, () => Game.SetVolumes(Game.Settings.masterVolume, Game.Settings.sfxVolume - 0.1f));
			Btn(t, "+", 0.6f, 0.57f, 0.66f, 0.67f, () => Game.SetVolumes(Game.Settings.masterVolume, Game.Settings.sfxVolume + 0.1f));
			var fs = Btn(t, "", 0.3f, 0.44f, 0.7f, 0.54f, () => Game.SetFullscreen(!Game.Settings.fullscreen), 34);
			_fullscreenText = fs.GetComponentInChildren<Text>();
			Lbl(t, Loc.T(EscapeGame.TouchUi ? "controls.touch" : "controls"), 30, TextAnchor.MiddleCenter, 0.12f, 0.22f, 0.88f, 0.4f, new Color(0.85f, 0.82f, 0.74f), true);
			Btn(t, Loc.T("back"), 0.38f, 0.06f, 0.62f, 0.16f, () => Game.CloseSettings());
		}

		void BuildCaught(Transform root)
		{
			_caught = Screen("Caught", root, new Color(0.25f, 0f, 0f, 0.45f));
			Lbl(_caught.transform, Loc.T("caught"), 100, TextAnchor.MiddleCenter, 0.1f, 0.5f, 0.9f, 0.72f, new Color(1f, 0.8f, 0.7f));
			_caughtSub = Lbl(_caught.transform, Loc.T("caught.sub"), 36, TextAnchor.MiddleCenter, 0.1f, 0.4f, 0.9f, 0.5f, Cream);
		}

		void BuildVictory(Transform root)
		{
			_victory = Screen("Victory", root, new Color(0.03f, 0.04f, 0.06f, 0.95f));
			var t = _victory.transform;
			Lbl(t, Loc.T("escaped"), 96, TextAnchor.MiddleCenter, 0.1f, 0.72f, 0.9f, 0.9f, Gold);
			_victoryTime = Lbl(t, "", 44, TextAnchor.MiddleCenter, 0.1f, 0.62f, 0.9f, 0.72f, Cream);
			_victoryDetections = Lbl(t, "", 44, TextAnchor.MiddleCenter, 0.1f, 0.53f, 0.9f, 0.62f, Cream);
			_victoryReason = Lbl(t, "", 30, TextAnchor.MiddleCenter, 0.1f, 0.47f, 0.9f, 0.53f, new Color(0.75f, 0.72f, 0.66f));
			Lbl(t, Loc.T("lords.mission"), 32, TextAnchor.MiddleCenter, 0.15f, 0.36f, 0.85f, 0.46f, new Color(0.8f, 0.9f, 1f), true);
			Lbl(t, Loc.T("lords.demo"), 26, TextAnchor.MiddleCenter, 0.15f, 0.26f, 0.85f, 0.36f, new Color(0.65f, 0.7f, 0.76f), true);
			_replayButton = Btn(t, Loc.T("replay"), 0.25f, 0.1f, 0.48f, 0.21f, () => Game.Replay()).gameObject;
			Btn(t, Loc.T("menu"), 0.52f, 0.1f, 0.75f, 0.21f, () => ShellFlow.QuitToMenu());
		}

		// ---- showing ----

		void Update()
		{
			if (Game == null || _fade == null) return;
			_fade.color = new Color(0f, 0f, 0f, Game.Fade);
			var mode = Game.Mode;
			if (mode != _shown)
			{
				Show(mode);
				_shown = mode;
			}
			Refresh(mode);
			SyncChrome(mode);
			if (_touch != null) UpdateTouch();
		}

		/// <summary>The tab bar follows the screen the game is on, and the bar shows who plays along.</summary>
		void SyncChrome(Mode mode)
		{
			if (_chrome == null) return;
			if (!_chrome.ProfileOpen)
			{
				var want = mode == Mode.Inventory ? EscapeShell.PlaceInventory : mode == Mode.Journal ? EscapeShell.PlaceJournal : EscapeShell.PlacePrison;
				if (_chrome.CurrentPlace != want) _chrome.Select(want, false);
			}
			if (Time.unscaledTime < _statusAt) return;
			_statusAt = Time.unscaledTime + 0.5f;
			var net = Game.Net;
			if (net == null) _chrome.SetStatus("");
			else _chrome.SetStatus(net.Others.Count > 0 ? Loc.T("net.with", FirstOtherName(net)) : Loc.T("net.alone"));
		}

		static string FirstOtherName(EscapeNet net)
		{
			foreach (var player in net.Session.Players)
				if (player.Id != net.Session.LocalId) return player.Name;
			return "";
		}

		void Show(Mode mode)
		{
			if (_checkpointButton != null) _checkpointButton.SetActive(Game.Net == null);
			if (_replayButton != null) _replayButton.SetActive(Game.Net == null);
			_title.SetActive(mode == Mode.Title);
			_intro.SetActive(mode == Mode.Intro);
			_hud.SetActive(mode == Mode.Playing || mode == Mode.Caught || mode == Mode.Latch || mode == Mode.Cabinet);
			_latch.SetActive(mode == Mode.Latch);
			_cabinet.SetActive(mode == Mode.Cabinet);
			_reading.SetActive(mode == Mode.Reading);
			_inventory.SetActive(mode == Mode.Inventory);
			_journal.SetActive(mode == Mode.Journal);
			_pause.SetActive(mode == Mode.Pause);
			_settings.SetActive(mode == Mode.Settings);
			_caught.SetActive(mode == Mode.Caught);
			_victory.SetActive(mode == Mode.Victory);

			GameObject screen = null;
			switch (mode)
			{
				case Mode.Title: screen = _title; break;
				case Mode.Intro: screen = _intro; break;
				case Mode.Latch: screen = _latch; break;
				case Mode.Cabinet: screen = _cabinet; break;
				case Mode.Reading: screen = _reading; break;
				case Mode.Inventory: screen = _inventory; break;
				case Mode.Journal: screen = _journal; break;
				case Mode.Pause: screen = _pause; break;
				case Mode.Settings: screen = _settings; break;
				case Mode.Victory: screen = _victory; break;
			}
			if (screen != null) UiBuilder.Select(screen);
		}

		void Refresh(Mode mode)
		{
			var state = Game.State;
			switch (mode)
			{
				case Mode.Title:
					_titleContinue.SetActive(Game.HasSave);
					UiBuilder.SetText(_titleNote, Game.SaveProblem ? Loc.T("menu.corrupt") : "");
					break;
				case Mode.Playing:
				case Mode.Caught:
				case Mode.Latch:
				case Mode.Cabinet:
					UiBuilder.SetText(_objective, Game.Objective);
					var prompt = Game.PromptText;
					_promptBox.SetActive(mode == Mode.Playing && prompt != "");
					UiBuilder.SetText(_prompt, prompt);
					var toast = Game.ToastActive && mode != Mode.Caught;
					_toastBox.SetActive(toast);
					if (toast) UiBuilder.SetText(_toast, Game.ToastText);
					var hint = Game.HintText;
					_hintBox.SetActive(hint != "");
					UiBuilder.SetText(_hint, Loc.T("hint") + ": " + hint);
					var awareness = Game.GuardAwareness;
					_awarenessBack.SetActive(awareness > 0.04f && mode == Mode.Playing);
					UiBuilder.SetFill(_awareness, awareness);
					if (mode == Mode.Latch)
					{
						var dots = new StringBuilder();
						for (var i = 0; i < 4; i++) dots.Append(i < state.LatchProgress ? "● " : "○ ");
						UiBuilder.SetText(_latchDots, dots.ToString().TrimEnd());
						UiBuilder.SetText(_latchMessage, Game.LatchMessage);
					}
					if (mode == Mode.Cabinet)
					{
						for (var i = 0; i < 3; i++) UiBuilder.SetText(_slotTexts[i], Loc.T("s." + state.Slot(i)));
						UiBuilder.SetText(_cabinetMessage, Game.CabinetMessage);
					}
					break;
				case Mode.Reading:
					UiBuilder.SetText(_readingText, Game.ReadingText);
					break;
				case Mode.Inventory:
					var inventory = new StringBuilder();
					foreach (var item in state.Items) inventory.Append("• ").Append(Loc.T("i." + item)).Append('\n').Append("   ").Append(Loc.T("i." + item + ".d")).Append("\n\n");
					if (state.Items.Count == 0) inventory.Append(Loc.T("empty"));
					UiBuilder.SetText(_inventoryText, inventory.ToString());
					_combine.SetActive(state.CanCombine);
					break;
				case Mode.Journal:
					var journal = new StringBuilder();
					journal.Append(Game.Objective).Append("\n\n").Append(Loc.T("clues")).Append(":\n");
					foreach (var clue in state.Clues) journal.Append("• ").Append(Loc.T(clue)).Append('\n');
					if (state.Clues.Count == 0) journal.Append(Loc.T("no.clues"));
					UiBuilder.SetText(_journalText, journal.ToString());
					UiBuilder.SetText(_journalHint, Game.HintText);
					break;
				case Mode.Settings:
					UiBuilder.SetText(_masterText, Loc.T("volume.master") + ": " + Mathf.RoundToInt(Game.Settings.masterVolume * 100f) + "%");
					UiBuilder.SetText(_sfxText, Loc.T("volume.sfx") + ": " + Mathf.RoundToInt(Game.Settings.sfxVolume * 100f) + "%");
					UiBuilder.SetText(_fullscreenText, Loc.T("fullscreen") + ": " + (Game.Settings.fullscreen ? "✓" : "—"));
					break;
				case Mode.Victory:
					UiBuilder.SetText(_victoryTime, Loc.T("time") + ": " + Game.ElapsedText);
					UiBuilder.SetText(_victoryDetections, Loc.T("detections") + ": " + state.Detections);
					UiBuilder.SetText(_victoryReason, Loc.T("village") + ": " + Game.Capture.VillageName + " · " + Loc.T("reason." + Game.Capture.CaptureReason));
					break;
			}
		}
	}
}
