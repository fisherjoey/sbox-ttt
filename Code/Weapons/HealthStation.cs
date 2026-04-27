using Sandbox;

namespace TTT;

/// <summary>
/// Vanilla TTT health station: detective placeable. Players standing in its
/// trigger zone heal at <see cref="HealPerSecond"/> until the station's pool
/// runs out (<see cref="HealPoolRemaining"/>). When the pool hits zero the
/// station despawns.
///
/// Implementation needs a child <see cref="BoxCollider"/> with IsTrigger=true
/// for the heal zone. v1 ships the logic; the prefab/visual is post-launch.
/// </summary>
[Title( "Health Station" ), Icon( "medical_services" )]
public sealed class HealthStation : Component, Component.ITriggerListener
{
	[Property, Sync] public float HealPerSecond { get; set; } = 5f;
	[Property, Sync] public float HealPoolRemaining { get; set; } = 200f;

	private readonly System.Collections.Generic.HashSet<TTTPlayer> _inRange = new();

	void Component.ITriggerListener.OnTriggerEnter( Collider other )
	{
		var player = other.GameObject?.GetComponentInParent<TTTPlayer>();
		if ( !player.IsValid() ) return;
		_inRange.Add( player );
	}

	void Component.ITriggerListener.OnTriggerExit( Collider other )
	{
		var player = other.GameObject?.GetComponentInParent<TTTPlayer>();
		if ( !player.IsValid() ) return;
		_inRange.Remove( player );
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost ) return;
		if ( HealPoolRemaining <= 0 ) return;

		var dt = Time.Delta;
		foreach ( var p in _inRange )
		{
			if ( !p.IsValid() || !p.IsAlive ) continue;
			if ( p.Health >= p.MaxHealth ) continue;

			var amount = MathF.Min( HealPerSecond * dt, HealPoolRemaining );
			amount = MathF.Min( amount, p.MaxHealth - p.Health );
			if ( amount <= 0 ) continue;

			p.Health += amount;
			HealPoolRemaining -= amount;

			if ( HealPoolRemaining <= 0 )
			{
				GameObject.Destroy();
				return;
			}
		}
	}
}
