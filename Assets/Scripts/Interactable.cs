using System;
using System.Collections.Generic;
using UnityEngine;

namespace Temnorid.Escape
{
	/// <summary>
	/// Something the player can use with the interact key. It only carries a stable object id; what happens, and
	/// whether it is available now, is decided by <see cref="EscapeGame"/>, so the whole story lives in one place.
	/// </summary>
	public sealed class Interactable : MonoBehaviour
	{
		public static readonly List<Interactable> All = new List<Interactable>();

		public string Id;
		/// <summary>Where a line of sight to this object is aimed, relative to its transform.</summary>
		public Vector3 FocusOffset = new Vector3(0f, 0.8f, 0f);
		/// <summary>True for the hiding spot: the player stands behind it, so a wall of barrels must not hide it from the player.</summary>
		public bool IgnoreSight;

		void OnEnable() => All.Add(this);
		void OnDisable() => All.Remove(this);

		public Vector3 Focus => transform.TransformPoint(FocusOffset);

		/// <summary>
		/// The one object to offer: within <paramref name="range"/> metres on the ground, in plain view (no wall between), and available.
		/// Objects the player faces win over nearer ones behind them; among equals the nearest wins.
		/// </summary>
		public static Interactable Pick(Vector3 from, Vector3 forward, float range, Func<Interactable, bool> available)
		{
			Interactable best = null;
			var bestScore = float.MaxValue;
			forward.y = 0f;
			foreach (var item in All)
			{
				if (!available(item)) continue;
				var to = item.transform.position - from;
				to.y = 0f;
				var distance = to.magnitude;
				if (distance > range) continue;
				if (!item.IgnoreSight && !Visible(from, item)) continue;
				var facing = distance < 0.4f || Vector3.Angle(forward, to) <= 70f;
				var score = distance + (facing ? 0f : 10f);
				if (score < bestScore)
				{
					best = item;
					bestScore = score;
				}
			}
			return best;
		}

		static bool Visible(Vector3 from, Interactable item)
		{
			var eye = from + Vector3.up * 1.0f;
			var target = item.Focus;
			if (!Physics.Linecast(eye, target, out var hit, ~0, QueryTriggerInteraction.Ignore)) return true;
			if (hit.collider.GetComponentInParent<Player>() != null) return true;
			var found = hit.collider.GetComponentInParent<Interactable>();
			return found == item;
		}
	}
}
