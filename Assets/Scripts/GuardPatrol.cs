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
		public float Awareness => Remote ? _remoteAwareness : _meter.Awareness;
		public bool Sees { get; private set; }

		/// <summary>The other prisoner of an online game, as the host sees them, and whether they are out of sight (hidden or in a screen).</summary>
		public Transform Other;
		public bool OtherHidden;
		/// <summary>On a guest's machine the guard is only shown from what the host publishes; it never patrols or watches here.</summary>
		public bool Remote;
		float _remoteAwareness;
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
			if (Target != null && !TargetHidden && CanSeeAt(Target.transform.position, Target.Position2D)) return true;
			if (Other != null && !OtherHidden && CanSeeAt(Other.position, new Vector2(Other.position.x, Other.position.z))) return true;
			return false;
		}

		bool CanSeeAt(Vector3 position, Vector2 position2D)
		{
			if (!GuardVision.InCone(Sample.Position, Sample.Facing, position2D)) return false;
			var eye = transform.position + Vector3.up * 1.5f;
			var chest = position + Vector3.up * 1.0f;
			if (!Physics.Linecast(eye, chest, out var hit, ~0, QueryTriggerInteraction.Ignore)) return true;
			return hit.collider.GetComponentInParent<Player>() != null;
		}

		/// <summary>A guest's copy: what the host says the guard is doing (awareness, paused, watching). Position comes from a RemoteBody.</summary>
		public void ShowRemote(float awareness, bool paused, bool sees, float distanceToTarget)
		{
			_remoteAwareness = awareness;
			Sees = sees;
			if (Figure != null) Figure.Speed = paused ? 0f : EscapeConfig.GuardSpeed;
			UpdateMark();
			if (!paused)
			{
				_nextStep -= Time.deltaTime;
				if (_nextStep <= 0f)
				{
					_nextStep = 0.55f;
					Footstep?.Invoke(Mathf.Clamp01(1f - distanceToTarget / 16f));
				}
			}
		}

		void UpdateMark()
		{
			if (Mark == null) return;
			var a = Awareness;
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
