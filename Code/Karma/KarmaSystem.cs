using System;

namespace TTT;

/// <summary>
/// Pure-function karma calculations. Formulas pulled verbatim from the canonical
/// GMod source at <c>Facepunch/garrysmod/gamemodes/terrortown/gamemode/karma.lua</c>.
///
/// All functions are stateless and side-effect-free so the caller decides when
/// to read live karma vs base karma, when to commit changes, etc. The caller is
/// expected to be host-authoritative — this module does not know about networking.
/// </summary>
public static class KarmaSystem
{
	// ----- Tunables hardcoded from karma.lua. The mode (Off/Lenient/Strict) and
	// kick threshold are exposed via GameConfig; these inner ratios stay private
	// because changing them rebalances the whole game in non-obvious ways. -----

	/// <summary>Damage→penalty ratio when an innocent hurts another innocent. <c>ttt_karma_ratio</c>.</summary>
	public const float HurtPenaltyRatio = 0.001f;

	/// <summary>Treated as "extra damage" when computing the kill penalty. <c>ttt_karma_kill_penalty</c>.</summary>
	public const float KillPenaltyDamage = 15f;

	/// <summary>Damage→reward ratio when a non-traitor hurts a traitor. <c>ttt_karma_traitordmg_ratio</c>.</summary>
	public const float TraitorHurtRewardRatio = 0.0003f;

	/// <summary>Treated as "extra damage" when computing the traitor-kill reward. <c>ttt_karma_traitorkill_bonus</c>.</summary>
	public const float TraitorKillBonus = 40f;

	// =========================================================================
	// Damage scaling — applied to outgoing damage based on attacker's karma.
	// =========================================================================

	/// <summary>
	/// Returns the multiplier to apply to outgoing damage based on the attacker's
	/// base karma. Karma at or above start gives 1.0; karma below scales down per
	/// the strict/lenient curve from <c>karma.lua : KARMA.ApplyKarma</c>.
	/// Result is always clamped to [0.1, 1.0].
	/// </summary>
	public static float GetDamageFactor( int baseKarma, KarmaMode mode, int startKarma = 1000 )
	{
		if ( mode == KarmaMode.Off ) return 1f;
		if ( baseKarma >= startKarma ) return 1f;

		var k = (float)(baseKarma - startKarma);
		var df = mode == KarmaMode.Strict
			? 1f + 0.0007f * k - 0.000002f * k * k
			: 1f - 0.0000025f * k * k;

		return Math.Clamp( df, 0.1f, 1.0f );
	}

	// =========================================================================
	// Hurt / kill penalties — applied to attacker's karma when they damage
	// or kill an innocent (or any teammate, depending on context).
	// =========================================================================

	/// <summary>
	/// Penalty for hurting a teammate. <c>victim_karma * clamp(damage * 0.001, 0, 1)</c>
	/// — so hurting a high-karma teammate costs more, and the per-damage hit
	/// saturates at 1000 damage. Damage is the post-scaling number that actually
	/// landed on the victim.
	/// </summary>
	public static float GetHurtPenalty( int victimKarma, float damage )
	{
		var ratio = Math.Clamp( damage * HurtPenaltyRatio, 0f, 1f );
		return victimKarma * ratio;
	}

	/// <summary>Penalty for killing a teammate — equivalent to hurting them for <see cref="KillPenaltyDamage"/> damage.</summary>
	public static float GetKillPenalty( int victimKarma ) =>
		GetHurtPenalty( victimKarma, KillPenaltyDamage );

	// =========================================================================
	// Hurt / kill rewards — granted when a non-traitor damages or kills a
	// traitor. Encourages innocents to actually shoot traitors.
	// =========================================================================

	/// <summary>Reward for hurting a traitor. Scales with damage, capped at full max-karma at high damage.</summary>
	public static float GetHurtReward( float damage, int maxKarma = 1000 )
	{
		var ratio = Math.Clamp( damage * TraitorHurtRewardRatio, 0f, 1f );
		return maxKarma * ratio;
	}

	/// <summary>Reward for killing a traitor — equivalent to hurting them for <see cref="TraitorKillBonus"/> damage.</summary>
	public static float GetKillReward( int maxKarma = 1000 ) =>
		GetHurtReward( TraitorKillBonus, maxKarma );

	// =========================================================================
	// Round-end karma adjustment — recovery + clean-round bonus.
	// =========================================================================

	/// <summary>
	/// Compute the karma value at round end given the player's current value
	/// and whether they had a "clean" round (no teammate damage).
	/// Result is clamped to [0, max].
	/// </summary>
	public static int ApplyRoundEnd( int currentKarma, bool wasClean, int recoveryPerRound, int cleanBonus, int maxKarma = 1000 )
	{
		var bonus = wasClean ? cleanBonus : 0;
		return Math.Clamp( currentKarma + recoveryPerRound + bonus, 0, maxKarma );
	}

	// =========================================================================
	// Kick check.
	// =========================================================================

	/// <summary>
	/// True if the player's karma is at or below the kick threshold and karma
	/// is enabled. Caller decides what to do (kick, autoban, log, ignore).
	/// </summary>
	public static bool ShouldKick( int karma, int kickThreshold, KarmaMode mode ) =>
		mode != KarmaMode.Off && karma <= kickThreshold;
}
