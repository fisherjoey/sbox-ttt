using Sandbox;

namespace TTT;

/// <summary>
/// Minimal weapon base. A weapon lives on the wielding player's GameObject and
/// fires hit-traces from the eye when <see cref="Fire"/> is called. Subclasses
/// override damage, range, fire rate, and any extra behaviour. Visual effects,
/// view models, and recoil are post-v1 polish.
///
/// Engine-private `BaseCarryable` / `BaseWeapon` aren't reusable per the API
/// survey, so this is our own stripped-down version. Damage flows through
/// `Component.IDamageable.OnDamage` via `new DamageInfo(damage, attacker, weapon)`.
/// </summary>
public abstract class Weapon : Component
{
	[Property] public float Damage { get; set; } = 10f;
	[Property] public float Range { get; set; } = 4096f;
	[Property] public float FireRate { get; set; } = 0.1f;
	[Property] public bool IsHeadshotCapable { get; set; } = true;

	[Property] public SoundEvent FireSound { get; set; }

	private TimeSince _timeSinceFired = 1000f;

	public TTTPlayer Owner => GameObject.Root.GetComponent<TTTPlayer>();

	/// <summary>
	/// Server-side: do a hit-trace from the player's eye and apply damage to
	/// any IDamageable hit. Called from Player input handling.
	/// </summary>
	public virtual bool Fire( Ray eyeRay )
	{
		if ( !Networking.IsHost ) return false;
		if ( _timeSinceFired < FireRate ) return false;
		_timeSinceFired = 0;

		PlayFireSound();

		var trace = Scene.Trace.Ray( eyeRay, Range )
			.IgnoreGameObjectHierarchy( Owner?.GameObject )
			.Run();

		if ( !trace.Hit ) return true;

		var target = trace.GameObject?.GetComponentInParent<Component.IDamageable>();
		if ( target is null ) return true;

		var info = new DamageInfo( Damage, Owner?.GameObject, GameObject )
		{
			Position = trace.HitPosition,
			Origin = eyeRay.Position,
		};

		// TODO[v2]: detect torso/head hits via trace.Hitbox or bone name once
		// we have a player model with hitboxes wired up. For now flag every
		// hit as a body shot.
		if ( IsHeadshotCapable && IsHeadshotHit( trace ) )
			info.Tags.Add( DamageTags.Headshot );

		target.OnDamage( info );
		return true;
	}

	protected virtual bool IsHeadshotHit( SceneTraceResult trace )
	{
		// Placeholder until hitbox tags are wired in the player prefab.
		return trace.Hitbox?.Tags.Has( "head" ) ?? false;
	}

	[Rpc.Broadcast]
	private void PlayFireSound()
	{
		if ( FireSound is null ) return;
		GameObject.PlaySound( FireSound );
	}
}
