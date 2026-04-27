using Sandbox;

namespace TTT;

public sealed partial class TTTPlayer
{
	void Component.IDamageable.OnDamage( in DamageInfo dmg )
	{
		if ( !Networking.IsHost ) return;
		if ( Status != PlayerStatus.Alive ) return;
		if ( Health <= 0 ) return;

		// No PvP outside the Active phase. Prep is for milling around; PostRound
		// is for reading the result. Damage simply doesn't apply.
		if ( TTTGameMode.Current?.Phase != RoundPhase.Active ) return;

		var config = GameConfig.Current;
		var damage = dmg.Damage;

		// Headshot multiplier — same convention as Facepunch/sandbox Player.cs.
		if ( dmg.Tags.Contains( DamageTags.Headshot ) )
			damage *= config?.HeadshotMultiplier ?? 2.0f;

		// Friendly-fire gate. If the attacker is on our team and FF is off, no-op.
		// Self-damage (fall, fire, own grenade) is never gated.
		var attacker = dmg.Attacker?.GetComponent<TTTPlayer>();
		var isSelf = attacker == this;
		var sameTeam = attacker.IsValid() && !isSelf && attacker.Role.Team == Role.Team;
		if ( sameTeam && config is { FriendlyFire: false } )
			return;

		// Karma damage scaling — outgoing damage is reduced by the attacker's
		// damage factor. Self-damage and damage from non-player sources skip this.
		// RDM penalty / traitor-kill reward live in issue #10 and aren't here.
		if ( attacker.IsValid() && !isSelf && config is not null )
		{
			damage *= KarmaSystem.GetDamageFactor( attacker.Role.Karma, config.KarmaMode, config.StartingKarma );
		}

		Health -= damage;

		if ( Health <= 0 )
			Kill( dmg );
	}

	private void Kill( in DamageInfo dmg )
	{
		if ( !Networking.IsHost ) return;

		Status = PlayerStatus.Dead;
		Health = 0;

		// Drop a corpse at the death position so detectives have something to ID.
		Corpse.SpawnFor( this, dmg );

		// Notify the game mode so haste mode extends and the active-phase win
		// condition gets a chance to re-evaluate next tick.
		TTTGameMode.Current?.OnPlayerKilled( this );
	}
}
