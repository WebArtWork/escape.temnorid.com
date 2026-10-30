using ITKamianets.Engine.Save;

namespace Temnorid.Escape
{
	/// <summary>Reads and writes the <see cref="EscapeSave"/> in the single save slot. The write is atomic (the save package swaps a temporary file in).</summary>
	public sealed class EscapeSaveStore
	{
		const string Slot = "default";
		const int PayloadVersion = 1;

		readonly SaveStore _store;

		public EscapeSaveStore(string rootDirectory = null)
		{
			_store = new SaveStore("escape", rootDirectory);
		}

		public string Path => _store.PathFor(Slot);

		/// <summary>The saved game, or null. <paramref name="status"/> says why it is null: no save yet, a damaged file (kept aside, never deleted) or a newer version.</summary>
		public EscapeSave Load(out SaveStatus status)
		{
			var result = _store.Load<EscapeSave>(Slot, PayloadVersion);
			status = result.Status;
			return result.Ok ? result.Value : null;
		}

		public SaveStatus Save(EscapeSave save) => _store.Save(Slot, save, PayloadVersion);

		public void Delete() => _store.Delete(Slot);

		public EscapeSettings LoadSettings()
		{
			var result = _store.Load<EscapeSettings>("settings", PayloadVersion);
			return result.Ok && result.Value != null ? result.Value : new EscapeSettings();
		}

		public void SaveSettings(EscapeSettings settings) => _store.Save("settings", settings, PayloadVersion);
	}
}
