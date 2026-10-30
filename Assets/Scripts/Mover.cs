using UnityEngine;

namespace Temnorid.Escape
{
	/// <summary>
	/// Slides or swings a door, gate, lever or lid between two poses. It is only the picture of a flag in
	/// <see cref="PrisonState"/>; its colliders are off while it is open, so nothing can be trapped in it.
	/// </summary>
	public sealed class Mover : MonoBehaviour
	{
		public Vector3 ClosedPosition;
		public Vector3 OpenPosition;
		public Vector3 ClosedEuler;
		public Vector3 OpenEuler;
		public float Seconds = 0.7f;
		public Collider[] Blockers;

		float _t;
		bool _open;

		public bool IsOpen => _open;
		public float Progress => _t;

		public void SetOpen(bool open, bool instant = false)
		{
			_open = open;
			if (instant) _t = open ? 1f : 0f;
			if (open) SetBlockers(false);
			else if (instant) SetBlockers(true);
			Apply();
		}

		void SetBlockers(bool on)
		{
			if (Blockers == null) return;
			foreach (var blocker in Blockers) if (blocker != null) blocker.enabled = on;
		}

		void Apply()
		{
			var k = _t * _t * (3f - 2f * _t);
			transform.localPosition = Vector3.Lerp(ClosedPosition, OpenPosition, k);
			transform.localRotation = Quaternion.Slerp(Quaternion.Euler(ClosedEuler), Quaternion.Euler(OpenEuler), k);
		}

		void Update()
		{
			var target = _open ? 1f : 0f;
			if (Mathf.Approximately(_t, target)) return;
			_t = Mathf.MoveTowards(_t, target, Time.deltaTime / Mathf.Max(0.01f, Seconds));
			Apply();
			if (!_open && Mathf.Approximately(_t, 0f)) SetBlockers(true);
		}
	}
}
