using ITKamianets.Engine.Net;
using UnityEngine;

namespace Temnorid.Escape
{
	/// <summary>
	/// The other prisoner as this machine sees it: a copy of this machine's own prisoner figure (so it uses the game's own
	/// materials), tinted with the player's colour, that glides to the pose its owner publishes. It has no collider, so it never
	/// blocks anybody or a line of sight; the guard on the host watches it by position.
	/// </summary>
	public sealed class RemotePrisoner : MonoBehaviour
	{
		/// <summary>Pose state bits: hiding behind the barrels, and in a screen (puzzle, reading, panel, menu).</summary>
		public const int HiddenBit = 1;
		public const int BusyBit = 2;

		public ulong Owner;
		public int State { get; private set; }
		public bool HasPose { get; private set; }
		/// <summary>True when the guard may see this prisoner: walking about, not hiding and not in a screen.</summary>
		public bool Exposed => HasPose && (State & (HiddenBit | BusyBit)) == 0;

		Figure _figure;
		TextMesh _name;
		RemoteBody _body;

		public static RemotePrisoner Create(ulong owner, string playerName, Color colour, Player template)
		{
			var go = new GameObject("RemotePrisoner " + playerName);
			var prisoner = go.AddComponent<RemotePrisoner>();
			prisoner.Owner = owner;
			prisoner.Build(playerName, colour, template);
			return prisoner;
		}

		void Build(string playerName, Color colour, Player template)
		{
			_body = gameObject.AddComponent<RemoteBody>();
			var figure = template != null ? template.GetComponentInChildren<Figure>(true) : null;
			if (figure != null)
			{
				var copy = Instantiate(figure.gameObject, transform, false);
				copy.name = "Visual";
				_figure = copy.GetComponent<Figure>();
				foreach (var renderer in copy.GetComponentsInChildren<Renderer>(true))
				{
					renderer.enabled = true;
					var shared = renderer.sharedMaterial;
					if (shared != null && shared.name.Contains("Tunic"))
					{
						var tinted = new Material(shared);
						tinted.color = colour;
						renderer.sharedMaterial = tinted;
					}
				}
				foreach (var collider in copy.GetComponentsInChildren<Collider>(true)) Destroy(collider);
			}

			var label = new GameObject("Name");
			label.transform.SetParent(transform, false);
			label.transform.localPosition = new Vector3(0f, 2.3f, 0f);
			_name = label.AddComponent<TextMesh>();
			_name.text = playerName;
			_name.characterSize = 0.12f;
			_name.fontSize = 48;
			_name.anchor = TextAnchor.MiddleCenter;
			_name.color = Color.white;
		}

		public void SetName(string value)
		{
			if (_name != null && _name.text != value) _name.text = value;
		}

		public void Apply(NetPose pose)
		{
			_body.Apply(pose);
			State = pose.State;
			HasPose = true;
			if (_figure != null) _figure.Speed = pose.Speed;
		}

		void LateUpdate()
		{
			var camera = Camera.main;
			if (camera != null && _name != null) _name.transform.rotation = camera.transform.rotation;
			// A prisoner hiding behind the barrels or in a close-up is not drawn over the other one's view.
			if (_figure != null) _figure.gameObject.SetActive((State & HiddenBit) == 0);
		}
	}
}
