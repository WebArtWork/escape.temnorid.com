using UnityEngine;

namespace Temnorid.Escape
{
	/// <summary>Makes a torch light waver a little.</summary>
	[RequireComponent(typeof(Light))]
	public sealed class TorchFlicker : MonoBehaviour
	{
		Light _light;
		float _base;
		float _seed;

		void Awake()
		{
			_light = GetComponent<Light>();
			_base = _light.intensity;
			_seed = Random.value * 50f;
		}

		void Update()
		{
			_light.intensity = _base * (0.88f + 0.12f * Mathf.PerlinNoise(Time.time * 5f, _seed)) * (0.96f + 0.04f * Mathf.Sin(Time.time * 19f + _seed));
		}
	}
}
