using System;
using System.Collections.Generic;

namespace TTT;

/// <summary>
/// Static validators for <see cref="GameConfig"/>. See CONFIG.md for the
/// rationale behind each rule. Run on every host-side change before commit.
/// </summary>
public static class GameConfigValidator
{
	public static IReadOnlyList<string> Validate( GameConfig c )
	{
		var errors = new List<string>();

		// --- Liveness ---
		var minTraitors = (int)MathF.Floor( c.MinPlayers * c.TraitorPct );
		if ( minTraitors < 1 )
			errors.Add( $"At {c.MinPlayers} players, traitor% of {c.TraitorPct:P0} produces 0 traitors. Round can't start." );

		if ( c.TraitorPct + c.DetectivePct >= 0.95f )
			errors.Add( $"Traitor% + Detective% = {(c.TraitorPct + c.DetectivePct):P0}. Innocents must always exist." );

		var hasteWorstCase = c.ActiveDuration + c.HasteSecondsPerDeath * c.MinPlayers;
		if ( c.HasteMode && hasteWorstCase > 30 * 60 )
			errors.Add( $"Haste worst-case timer ({hasteWorstCase / 60:F0} min) exceeds 30 min cap. Lower ActiveDuration or HasteSecondsPerDeath." );

		// --- Pace floors ---
		if ( c.PrepDuration < 10 ) errors.Add( "PrepDuration must be at least 10s." );
		if ( c.FirstPrepDuration < 10 ) errors.Add( "FirstPrepDuration must be at least 10s." );
		if ( c.ActiveDuration < 60 ) errors.Add( "ActiveDuration must be at least 60s." );
		if ( c.PostRoundDuration < 5 ) errors.Add( "PostRoundDuration must be at least 5s." );

		// --- Karma ---
		if ( c.KarmaMode != KarmaMode.Off )
		{
			if ( c.KickThreshold >= c.StartingKarma )
				errors.Add( $"KickThreshold ({c.KickThreshold}) >= StartingKarma ({c.StartingKarma}). No one would ever get kicked." );

			if ( c.RecoveryPerRound <= 0 )
				errors.Add( "RecoveryPerRound must be > 0 when karma is enabled, else karma can only decrease." );

			// Damage scale at kick threshold must still produce non-zero damage,
			// otherwise low-karma players become invincible. Formula from karma.lua:
			//   k = karma - 1000;  df = 1 + 0.0007k - 0.000002k²; clamp [0.1, 1.0]
			var k = c.KickThreshold - 1000f;
			var df = MathF.Max( 0.1f, MathF.Min( 1.0f, 1f + 0.0007f * k - 0.000002f * k * k ) );
			if ( df <= 0.05f )
				errors.Add( $"At KickThreshold karma, damage scale is {df:P0}. Players become near-invincible before being kicked." );
		}

		// --- Economy ---
		if ( c.StartingCreditsTraitor + c.KillBonusCredits * 2 < 1 )
			errors.Add( "Traitors can never reach 1 credit. Shop unreachable." );

		if ( c.MaxCredits < c.StartingCreditsTraitor )
			errors.Add( $"MaxCredits ({c.MaxCredits}) < StartingCreditsTraitor ({c.StartingCreditsTraitor}). Credits would be capped below starting." );

		// --- Detective gating ---
		if ( c.DetectivePct > 0f )
		{
			var minForOneDetective = (int)MathF.Ceiling( 1f / c.DetectivePct );
			if ( c.DetectiveMinPlayers < minForOneDetective )
				errors.Add( $"At Detective% of {c.DetectivePct:P0}, you need {minForOneDetective}+ players to round up to 1 detective. Set DetectiveMinPlayers >= {minForOneDetective}." );
		}

		// --- Combat ---
		if ( c.BaseHealth < 50 )
			errors.Add( "BaseHealth below 50 — most weapons become one-shot, gameplay breaks." );

		if ( c.HeadshotMultiplier < 0.5f || c.HeadshotMultiplier > 5f )
			errors.Add( $"HeadshotMultiplier ({c.HeadshotMultiplier}) outside [0.5, 5.0] sane range." );

		// --- Map ---
		if ( c.MinPlayers < 2 )
			errors.Add( "MinPlayers must be at least 2 (TTT is multiplayer)." );

		return errors;
	}

	/// <summary>
	/// Build a "recommended" config for the current player count. Used to populate
	/// the "Reset to recommended" button in the lobby UI (v3+). Vanilla defaults
	/// scale naturally with player count via the percentage formulas; the only
	/// reason to override is for very small games where vanilla feels off.
	/// </summary>
	public static void ApplyRecommendedDefaults( GameConfig c, int playerCount )
	{
		// For now, vanilla defaults are the recommended defaults. This hook
		// exists so we can adjust later without changing callers.
		_ = playerCount;
	}
}
