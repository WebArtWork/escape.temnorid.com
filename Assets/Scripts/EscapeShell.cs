using ITKamianets.Engine.Shell;
using UnityEngine;

namespace Temnorid.Escape
{
	/// <summary>
	/// What the shared shell (splash, landing, lobby, top and bottom bars) needs to know about this game. The shell's own
	/// screens are in English; the places of the in-game bars are in Ukrainian like the rest of the game.
	/// </summary>
	public static class EscapeShell
	{
		public const string Repo = "WebArtWork/escape.temnorid.com";

		/// <summary>The place ids of the in-game bar. The first is the prison itself.</summary>
		public const string PlacePrison = "prison";
		public const string PlaceInventory = "inventory";
		public const string PlaceJournal = "journal";

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		static void Configure()
		{
			ShellConfig.Current = new ShellConfig
			{
				Title = "Temnorid Escape",
				Tagline = "Escape from the watchtower prison",
				GameId = "escape",
				Repo = Repo,
				InstallerSuffix = "-Setup.exe",
				MaxPlayers = 2,
				Multiplayer = true,
				GameScene = "Prison",
				Accent = new Color(0.95f, 0.78f, 0.35f),
				Places =
				{
					new ShellPlace(PlacePrison, Loc.T("place.prison"), "В"),
					new ShellPlace(PlaceInventory, Loc.T("inventory"), "І"),
					new ShellPlace(PlaceJournal, Loc.T("journal"), "Щ"),
				},
			};
		}
	}
}
