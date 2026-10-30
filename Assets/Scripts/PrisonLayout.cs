using UnityEngine;

namespace Temnorid.Escape
{
	/// <summary>
	/// Where things are in the prison, as plain numbers (x east, z north, metres). The scene builder, the game and
	/// the tests all read this, so the guard's route and the places the player must reach cannot drift apart.
	///
	/// <code>
	///  z
	///  4  [CELL x-16..-8 ]
	///  2  [ door  ]=========== CORRIDOR x-8..12, z-2..2 ===========[end door]
	/// -2             [alcove x-5..-2]       [store door x7..9]
	/// -4                                    [STOREROOM x2..14, z-12..-2] --door--> [SERVICE PASSAGE x14..26, z-8..-4] --gate--> [COURTYARD x26..40]
	/// </code>
	/// </summary>
	public static class PrisonLayout
	{
		public const float WallHeight = 3.2f;

		// Cell.
		public static readonly Vector2 CellStart = new Vector2(-12.5f, -1.5f);
		public static readonly Vector2 CellDoor = new Vector2(-8f, 0f);

		// Corridor and guard.
		public static readonly Vector2 Desk = new Vector2(-5f, 0f);
		public static readonly Vector2 Window = new Vector2(3f, 0f);
		public static readonly Vector2 EndDoor = new Vector2(9f, 0f);
		public static readonly Vector2 CorridorCheckpoint = new Vector2(-6.6f, -1.1f);
		public static readonly Vector2 HidePoint = new Vector2(-3.5f, -3.3f);
		public static readonly Vector2 HideExit = new Vector2(-3.5f, -1.3f);
		public static readonly Vector2 StoreDoorway = new Vector2(8f, -2f);

		// Storeroom.
		public static readonly Vector2 StoreCheckpoint = new Vector2(8f, -4.5f);
		public static readonly Vector2 LedgerSpot = new Vector2(3.4f, -10.4f);
		public static readonly Vector2 CabinetSpot = new Vector2(12.6f, -10.2f);
		public static readonly Vector2 StoreEastDoor = new Vector2(14f, -6f);

		// Service passage and courtyard.
		public static readonly Vector2 WorkbenchSpot = new Vector2(16.5f, -7.2f);
		public static readonly Vector2 GateCentre = new Vector2(26f, -6f);
		public const float ExitLineX = 36f;

		public static PatrolSchedule CreateSchedule()
		{
			return new PatrolSchedule(new[]
			{
				new Waypoint("Desk", Desk, Vector2.up, 4f),
				new Waypoint("Window", Window, Vector2.up, 5f),
				new Waypoint("EndDoor", EndDoor, Vector2.right, 2f),
			}, EscapeConfig.GuardSpeed);
		}

		public static Vector2 CheckpointPosition(string checkpoint)
		{
			switch (checkpoint)
			{
				case Ids.CheckpointStore: return StoreCheckpoint;
				case Ids.CheckpointCorridor: return CorridorCheckpoint;
				default: return CellStart;
			}
		}
	}
}
