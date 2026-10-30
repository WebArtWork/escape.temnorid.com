using UnityEngine;

namespace Temnorid.Escape
{
	/// <summary>Swings the arms and legs of a primitive person while it walks. Pure decoration.</summary>
	public sealed class Figure : MonoBehaviour
	{
		public Transform LegL, LegR, ArmL, ArmR, Body;
		/// <summary>How fast the figure is moving now, in metres per second.</summary>
		public float Speed;
		public float Crouch;

		float _phase;
		Vector3 _bodyStart;

		void Start()
		{
			if (Body != null) _bodyStart = Body.localPosition;
		}

		void Update()
		{
			var amount = Mathf.Clamp01(Speed / 2.5f);
			_phase += Speed * Time.deltaTime * 3.2f;
			var swing = Mathf.Sin(_phase) * 38f * amount;
			if (LegL != null) LegL.localRotation = Quaternion.Euler(swing, 0f, 0f);
			if (LegR != null) LegR.localRotation = Quaternion.Euler(-swing, 0f, 0f);
			if (ArmL != null) ArmL.localRotation = Quaternion.Euler(-swing * 0.8f, 0f, 0f);
			if (ArmR != null) ArmR.localRotation = Quaternion.Euler(swing * 0.8f, 0f, 0f);
			if (Body != null) Body.localPosition = _bodyStart + Vector3.up * (Mathf.Abs(Mathf.Sin(_phase)) * 0.04f * amount - Crouch * 0.25f);
		}
	}
}
