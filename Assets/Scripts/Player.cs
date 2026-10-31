using ITKamianets.Engine.FixedCamera;
using ITKamianets.Engine.Input;
using UnityEngine;

namespace Temnorid.Escape
{
	/// <summary>
	/// The prisoner. Moves relative to the camera as it was when the key went down, so a camera cut never turns held
	/// input around. All device input comes through <see cref="EngineInput"/>; <see cref="ScriptedInput"/> stands in for it in tests.
	/// </summary>
	[RequireComponent(typeof(CharacterController))]
	[DisallowMultipleComponent]
	public sealed class Player : MonoBehaviour
	{
		public FixedViewCamera Rig;
		public bool Frozen;

		/// <summary>Input in camera space (x right, y forward), used instead of the devices when set.</summary>
		public Vector2? ScriptedInput;
		public bool ScriptedRun;

		/// <summary>The thumb stick of a phone (x right, y forward), used when the keys and the pad say nothing.</summary>
		public Vector2 TouchMove;
		public bool TouchRun;

		CharacterController _body;
		Figure _figure;
		float _vy;
		Vector3 _safe;
		float _nextSafe;

		public float Speed { get; private set; }
		public Vector2 Position2D => new Vector2(transform.position.x, transform.position.z);
		public float Yaw => transform.eulerAngles.y;

		void Awake()
		{
			_body = GetComponent<CharacterController>();
			_body.minMoveDistance = 0f; // at a very high frame rate one step is under the default 1 mm and would be dropped
			_figure = GetComponentInChildren<Figure>();
			_safe = transform.position;
		}

		/// <summary>Walks toward a point on the floor, as a bot or a cutscene would. Returns the distance left.</summary>
		public float ScriptedWalkTo(Vector2 target, bool run)
		{
			var to = target - Position2D;
			var distance = to.magnitude;
			if (distance < 0.15f)
			{
				ScriptedInput = null;
				return distance;
			}
			var world = new Vector3(to.x, 0f, to.y).normalized;
			var yaw = Rig != null ? Rig.MovementYaw : 0f;
			var local = Quaternion.Euler(0f, -yaw, 0f) * world;
			ScriptedInput = new Vector2(local.x, local.z);
			ScriptedRun = run;
			return distance;
		}

		/// <summary>Hides or shows the prisoner, so a close-up on a lock or a cabinet is not blocked by the player.</summary>
		public void SetVisible(bool visible)
		{
			foreach (var renderer in GetComponentsInChildren<Renderer>(true)) renderer.enabled = visible;
		}

		public void Teleport(Vector2 position, float yawDegrees)
		{
			var enabled = _body.enabled;
			_body.enabled = false;
			transform.SetPositionAndRotation(new Vector3(position.x, 0.05f, position.y), Quaternion.Euler(0f, yawDegrees, 0f));
			_body.enabled = enabled;
			_vy = 0f;
			_safe = transform.position;
			Speed = 0f;
		}

		void Update()
		{
			var dt = Time.deltaTime;
			var input = Vector2.zero;
			var run = false;
			if (!Frozen)
			{
				if (ScriptedInput.HasValue)
				{
					input = ScriptedInput.Value;
					run = ScriptedRun;
				}
				else
				{
					var actions = EngineInput.Default.Player;
					input = actions.Move.ReadValue<Vector2>();
					run = actions.Sprint.IsPressed();
					if (input.sqrMagnitude < 0.01f && TouchMove.sqrMagnitude > 0.01f)
					{
						input = TouchMove;
						run = TouchRun;
					}
				}
			}

			var moving = input.sqrMagnitude > 0.01f;
			if (Rig != null) Rig.HoldBasis(moving);
			var yaw = Rig != null ? Rig.MovementYaw : 0f;
			var heading = Quaternion.Euler(0f, yaw, 0f) * new Vector3(input.x, 0f, input.y);
			if (heading.sqrMagnitude > 1f) heading.Normalize();

			var speed = run ? EscapeConfig.FastSpeed : EscapeConfig.MoveSpeed;
			var velocity = Frozen ? Vector3.zero : heading * speed;
			if (moving && !Frozen)
				transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(new Vector3(heading.x, 0f, heading.z)), Mathf.Clamp01(14f * dt));

			if (_body.isGrounded && transform.position.y < 1f && Time.time >= _nextSafe)
			{
				_safe = transform.position;
				_nextSafe = Time.time + 0.5f;
			}
			if (transform.position.y < -20f) Teleport(new Vector2(_safe.x, _safe.z), transform.eulerAngles.y);

			if (_body.isGrounded && _vy < 0f) _vy = -2f;
			_vy -= 20f * dt;
			velocity.y = _vy;
			if (_body.enabled) _body.Move(velocity * dt);

			Speed = Frozen ? 0f : heading.magnitude * speed;
			if (_figure != null) _figure.Speed = Speed;
		}
	}
}
