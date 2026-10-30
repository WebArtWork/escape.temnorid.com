using ITKamianets.Engine.Synth;
using UnityEngine;

namespace Temnorid.Escape
{
	public enum Sfx
	{
		Click,
		/// <summary>A correct latch step: a quiet metal click.</summary>
		Metal,
		/// <summary>A wrong step: a dull tap.</summary>
		Dull,
		Door,
		Alert,
		Footstep,
		Pickup,
		Success,
		Lever,
	}

	/// <summary>
	/// The sounds, made from simple waves and noise when the game starts, so no audio files are needed: clicks, a door,
	/// footsteps, an alert and a dripping, windy ambience that loops under everything.
	/// </summary>
	public sealed class EscapeAudio : MonoBehaviour
	{
		SoundBoard _boardCache;

		/// <summary>The game's Awake may set the volumes before this component has woken up, so the board is made on first use.</summary>
		SoundBoard Board => _boardCache != null ? _boardCache : (_boardCache = gameObject.AddComponent<SoundBoard>());

		public float MasterVolume => Board.MasterVolume;
		public float SfxVolume => Board.SfxVolume;

		void Awake()
		{
			var kinds = System.Enum.GetValues(typeof(Sfx));
			var clips = new AudioClip[kinds.Length];
			foreach (Sfx kind in kinds) clips[(int)kind] = Make(kind);
			Board.Init(Ambience(), clips, 0.3f);
		}

		public void SetVolumes(float master, float sfx) => Board.SetVolumes(master, sfx);

		public void Play(Sfx kind, float volume = 1f) => Board.Play((int)kind, volume);

		static AudioClip Make(Sfx kind)
		{
			var random = new System.Random((int)kind * 131 + 7);
			float Noise() => Tone.Noise(random);
			switch (kind)
			{
				case Sfx.Click:
					return Tone.Build("click", 0.05f, t => Mathf.Sin(t * 2f * Mathf.PI * 1100f) * Tone.Envelope(t, 0.05f) * 0.45f);
				case Sfx.Metal:
					return Tone.Build("metal", 0.16f, t => (Mathf.Sin(t * 2f * Mathf.PI * 2300f) * 0.6f + Mathf.Sin(t * 2f * Mathf.PI * 3700f) * 0.3f) * Tone.Envelope(t, 0.16f, 0.001f) * 0.4f);
				case Sfx.Dull:
					return Tone.Build("dull", 0.14f, t => (Mathf.Sin(t * 2f * Mathf.PI * 110f) * 0.8f + Noise() * 0.15f) * Tone.Envelope(t, 0.14f, 0.002f) * 0.7f);
				case Sfx.Door:
					return Tone.Build("door", 0.7f, t => (Noise() * 0.35f + Mathf.Sin(t * 2f * Mathf.PI * (70f - 30f * t)) * 0.6f) * Tone.Envelope(t, 0.7f, 0.05f) * 0.6f);
				case Sfx.Alert:
					return Tone.Build("alert", 0.45f, t => Mathf.Sin(t * 2f * Mathf.PI * (t < 0.2f ? 520f : 740f)) * Tone.Envelope(t, 0.45f, 0.01f) * 0.5f);
				case Sfx.Footstep:
					return Tone.Build("step", 0.12f, t => (Noise() * 0.4f + Mathf.Sin(t * 2f * Mathf.PI * 85f) * 0.7f) * Tone.Envelope(t, 0.12f, 0.002f) * 0.6f);
				case Sfx.Pickup:
					return Tone.Build("pickup", 0.2f, t => Mathf.Sin(t * 2f * Mathf.PI * (660f + 880f * t)) * Tone.Envelope(t, 0.2f) * 0.4f);
				case Sfx.Success:
					return Tone.Build("success", 0.6f, t => (Mathf.Sin(t * 2f * Mathf.PI * (t < 0.2f ? 523f : t < 0.4f ? 659f : 784f))) * Tone.Envelope(t, 0.6f, 0.01f) * 0.45f);
				default:
					return Tone.Build("lever", 0.4f, t => (Noise() * 0.3f + Mathf.Sin(t * 2f * Mathf.PI * (140f - 60f * t)) * 0.7f) * Tone.Envelope(t, 0.4f, 0.01f) * 0.6f);
			}
		}

		static AudioClip Ambience()
		{
			var random = new System.Random(5);
			var smooth = 0f;
			const float length = 6f;
			return Tone.Build("ambience", length, t =>
			{
				smooth += ((float)(random.NextDouble() * 2.0 - 1.0) - smooth) * 0.02f;
				var drip = t % 2.3f < 0.04f ? Mathf.Sin(t * 2f * Mathf.PI * 1800f) * (1f - (t % 2.3f) / 0.04f) * 0.35f : 0f;
				var edge = Mathf.Clamp01(Mathf.Min(t, length - t) / 0.2f);
				return (smooth * 1.6f + drip + Mathf.Sin(t * 2f * Mathf.PI * 55f) * 0.05f) * edge;
			});
		}
	}
}
