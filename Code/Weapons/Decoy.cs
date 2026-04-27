using Sandbox;

namespace TTT;

/// <summary>
/// Vanilla TTT decoy: traitor item that places a marker which shows up on the
/// detective radar as a player. Lasts <see cref="Lifetime"/> seconds, then
/// despawns. The radar UI itself is a separate v2 piece — for v1 the decoy
/// just exists as a tagged GameObject so anything reading "is there a player
/// here" via tag matches it.
/// </summary>
[Title( "Decoy" ), Icon( "gps_not_fixed" )]
public sealed class Decoy : Component
{
	[Property, Sync] public float Lifetime { get; set; } = 60f;

	private TimeUntil _despawnAt;

	protected override void OnStart()
	{
		if ( !Networking.IsHost ) return;

		// "player" tag so radar / scanner code that walks player-tagged objects
		// also picks up decoys (which is the whole point — fooling the radar).
		GameObject.Tags.Add( "player" );
		GameObject.Tags.Add( "decoy" );

		_despawnAt = Lifetime;
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost ) return;
		if ( _despawnAt > 0 ) return;
		GameObject.Destroy();
	}
}
