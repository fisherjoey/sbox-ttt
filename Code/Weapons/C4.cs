using Sandbox;
using System.Collections.Generic;

namespace TTT;

/// <summary>
/// Vanilla TTT C4: traitor placeable explosive. Buy → drops at the buyer's
/// feet → ticks for <see cref="Timer"/> seconds → explodes for radius damage.
/// Innocents can defuse by interacting with it within <see cref="Timer"/>.
/// Failed defuse explodes immediately.
///
/// v1 simplifications: no wire colours / defuse roulette UI yet, no visible
/// timer model. The component is the bomb; whether it has a model is a prefab
/// concern handled later.
/// </summary>
[Title( "C4" ), Icon( "do_not_disturb_on" )]
public sealed class C4 : Component
{
	[Property, Sync] public float Timer { get; set; } = 45f;
	[Property] public float Radius { get; set; } = 512f;
	[Property] public float MaxDamage { get; set; } = 250f;

	[Sync] public TimeUntil ExplodesAt { get; private set; }
	[Sync] public Guid PlacerId { get; set; }

	public TTTPlayer Placer =>
		Scene.Directory.FindByGuid( PlacerId )?.GetComponent<TTTPlayer>();

	protected override void OnStart()
	{
		if ( !Networking.IsHost ) return;
		ExplodesAt = Timer;
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost ) return;
		if ( ExplodesAt > 0 ) return;
		Explode();
	}

	/// <summary>Innocent / detective interact path: instant defuse, no roulette.</summary>
	public void Defuse( TTTPlayer defuser )
	{
		if ( !Networking.IsHost ) return;
		if ( !defuser.IsValid() ) return;

		// Vanilla has a 75% chance to fail-explode for non-detectives, plus
		// a wire roulette UI. v1 gives a clean defuse.
		GameObject.Destroy();
	}

	private void Explode()
	{
		if ( !Networking.IsHost ) return;

		var center = WorldPosition;
		var hit = Scene.FindInPhysics( new Sphere( center, Radius ) );
		var done = new HashSet<GameObject>();

		foreach ( var go in hit )
		{
			if ( !go.IsValid() ) continue;
			if ( !done.Add( go ) ) continue;

			var damageable = go.GetComponentInParent<Component.IDamageable>();
			if ( damageable is null ) continue;

			var distance = go.WorldPosition.Distance( center );
			var falloff = MathF.Max( 0f, 1f - distance / Radius );
			var damage = MaxDamage * falloff;

			var info = new DamageInfo( damage, Placer?.GameObject, GameObject )
			{
				Position = go.WorldPosition,
				Origin = center,
			};
			info.Tags.Add( "explosion" );

			damageable.OnDamage( info );
		}

		GameObject.Destroy();
	}
}
