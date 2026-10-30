using System;
using UnityEngine;

namespace Temnorid.Escape
{
	/// <summary>
	/// The guard in the scene. The loop and the cone are plain rules (<see cref="PatrolSchedule"/>, <see cref="GuardVision"/>);
	/// this adds what needs the world: a ray so walls and shut doors block the line of sight, the hiding spot, footsteps and the
	/// small "?" and "!" over the guard's head. It does not look at the camera.
	/// </summary>
	public sealed class GuardPatrol : MonoBehaviour
	{
		public Player Target;
		public Figure Figure;
		public TextMesh Mark;

		readonly DetectionMeter _meter = new DetectionMeter();
		PatrolSchedule _schedule;
		float _nextStep;

		/// <summary>The hidden player cannot be seen. Set by the game.</summary>
		public bool TargetHidden;
		public float PatrolTime { get; private set; }
		public float Awareness => _meter.Awareness;
		public bool Sees { get; private set; }
		public PatrolSample Sample { get; private set; }

		public event Action Detected;
		public event Action<float> Footstep;

		public PatrolSchedule Schedule => _schedule ??= PrisonLayout.CreateSchedule();

		/// <summary>Walks a different loop (tests use this to put a guard where they like).</summary>
		public void UseSchedule(PatrolSchedule schedule) => _schedule = schedule;

		/// <summary>Back to the start of the loop, and unable to notice anything for the grace time.</summary>
		public void ResetPatrol(float grace)
		{
			PatrolTime = 0f;
			_meter.Reset(grace);
			Sees = false;
			Place();
		}

		void Place()
		{
			Sample = Schedule.Sample(PatrolTime);
			transform.position = new Vector3(Sample.Position.x, 0.05f, Sample.Position.y);
			transform.rotation = Quaternion.LookRotation(new Vector3(Sample.Facing.x, 0f, Sample.Facing.y));
			if (Figure != null) Figure.Speed = Sample.Paused ? 0f : EscapeConfig.GuardSpeed;
		}

		/// <summary>Advances the patrol and the watching by <paramref name="dt"/> seconds. The game calls this only while playing, so reading or pausing never gets the player caught.</summary>
		public void Step(float dt)
		{
			PatrolTime += dt;
			Place();

			Sees = CanSee();
			_meter.Tick(dt, Sees);
			UpdateMark();
			if (!Sample.Paused && Target != null)
			{
				_nextStep -= dt;
				if (_nextStep <= 0f)
				{
					_nextStep = 0.55f;
					var distance = Vector3.Distance(Target.transform.position, transform.position);
					Footstep?.Invoke(Mathf.Clamp01(1f - distance / 16f));
				}
			}
			if (_meter.Detected) Detected?.Invoke();
		}

		bool CanSee()
		{
			if (Target == null || TargetHidden) return false;
			if (!GuardVision.InCone(Sample.Position, Sample.Facing, Target.Position2D)) return false;
			var eye = transform.position + Vector3.up * 1.5f;
			var chest = Target.transform.position + Vector3.up * 1.0f;
			if (!Physics.Linecast(eye, chest, out var hit, ~0, QueryTriggerInteraction.Ignore)) return true;
			return hit.collider.GetComponentInParent<Player>() != null;
		}

		void UpdateMark()
		{
			if (Mark == null) return;
			var a = _meter.Awareness;
			var show = a > 0.04f;
			if (Mark.gameObject.activeSelf != show) Mark.gameObject.SetActive(show);
			if (!show) return;
			Mark.text = a > 0.6f ? "!" : "?";
			Mark.color = Color.Lerp(new Color(1f, 0.85f, 0.2f), new Color(1f, 0.15f, 0.1f), a);
			Mark.characterSize = 0.04f + 0.02f * a;
			var camera = Camera.main;
			if (camera != null) Mark.transform.rotation = camera.transform.rotation;
		}
	}
}
