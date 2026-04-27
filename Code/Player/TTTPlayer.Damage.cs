using Sandbox;
using System;

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

		var attacker = dmg.Attacker?.GetComponent<TTTPlayer>();
		var isSelf = attacker == this;
		var sameTeam = attacker.IsValid() && !isSelf && attacker.Role.Team == Role.Team;

		// Friendly-fire gate. If the attacker is on our team and FF is off, no-op.
		// Self-damage (fall, fire, own grenade) is never gated.
		if ( sameTeam && config is { FriendlyFire: false } )
			return;

		// Karma damage scaling — outgoing damage is reduced by the attacker's
		// BASE karma (snapshot at round start) so a single bad round can't
		// insta-cripple. Self-damage and damage from non-player sources skip.
		if ( attacker.IsValid() && !isSelf && config is not null )
		{
			damage *= KarmaSystem.GetDamageFactor( attacker.Role.BaseKarma, config.KarmaMode, config.StartingKarma );
		}

		// Karma penalty / reward applied to attacker's LIVE karma based on the
		// damage that actually lands. Same-team is always a penalty (incl.
		// traitor-on-traitor). Non-traitor hitting traitor is a reward.
		if ( attacker.IsValid() && !isSelf && config is not null && config.KarmaMode != KarmaMode.Off )
		{
			ApplyHurtKarma( attacker, this, damage, config );
		}

		Health -= damage;

		if ( Health <= 0 )
			Kill( dmg );
	}

	private void Kill( in DamageInfo dmg )
	{
		if ( !Networking.IsHost ) return;

		// Apply the kill bonus on top of whatever hurt-penalty/reward was applied
		// during the damage tick — vanilla treats kill as "an extra 15 dmg of
		// penalty" or "an extra 40 dmg of reward."
		var attacker = dmg.Attacker?.GetComponent<TTTPlayer>();
		var config = GameConfig.Current;
		if ( attacker.IsValid() && attacker != this && config is not null && config.KarmaMode != KarmaMode.Off )
		{
			ApplyKillKarma( attacker, this, config );
		}

		Status = PlayerStatus.Dead;
		Health = 0;

		// Drop a corpse at the death position so detectives have something to ID.
		Corpse.SpawnFor( this, dmg );

		// Notify the game mode so haste mode extends and the active-phase win
		// condition gets a chance to re-evaluate next tick.
		TTTGameMode.Current?.OnPlayerKilled( this );
	}

	private static void ApplyHurtKarma( TTTPlayer attacker, TTTPlayer victim, float damage, GameConfig config )
	{
		var sameTeam = attacker.Role.Team == victim.Role.Team;

		if ( sameTeam )
		{
			var penalty = (int)KarmaSystem.GetHurtPenalty( victim.Role.Karma, damage );
			attacker.Role.Karma = Math.Max( 0, attacker.Role.Karma - penalty );
			attacker.Role.WasCleanThisRound = false;
		}
		else if ( victim.Role.Type == RoleType.Traitor && attacker.Role.Type != RoleType.Traitor )
		{
			var reward = (int)KarmaSystem.GetHurtReward( damage, config.StartingKarma );
			attacker.Role.Karma = Math.Min( config.StartingKarma, attacker.Role.Karma + reward );
		}
	}

	private static void ApplyKillKarma( TTTPlayer attacker, TTTPlayer victim, GameConfig config )
	{
		var sameTeam = attacker.Role.Team == victim.Role.Team;

		if ( sameTeam )
		{
			var penalty = (int)KarmaSystem.GetKillPenalty( victim.Role.Karma );
			attacker.Role.Karma = Math.Max( 0, attacker.Role.Karma - penalty );
			attacker.Role.WasCleanThisRound = false;
		}
		else if ( victim.Role.Type == RoleType.Traitor && attacker.Role.Type != RoleType.Traitor )
		{
			var reward = (int)KarmaSystem.GetKillReward( config.StartingKarma );
			attacker.Role.Karma = Math.Min( config.StartingKarma, attacker.Role.Karma + reward );
		}
	}
}
