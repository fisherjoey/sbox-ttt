using System.Collections.Generic;
using System.Linq;

namespace TTT;

/// <summary>
/// Pure win-condition decision. Given the set of teams that still have at
/// least one alive player and whether the active-phase timer expired, decide
/// whether the round is over and who won.
///
/// Vanilla TTT rules:
/// - Traitors win when no non-traitor is alive.
/// - Innocents win when no traitor is alive.
/// - Innocents win on timeout (no separate draw screen).
/// - "Everyone dead" — neither team has anyone alive — is treated as a
///   Traitor win (their win condition was already met).
/// </summary>
public static class WinCondition
{
	public readonly record struct Result( RoleTeam Winner, WinReason Reason );

	/// <summary>
	/// Evaluate the round state. Returns null if the round should keep going.
	/// </summary>
	public static Result? Evaluate( IEnumerable<RoleTeam> aliveTeams, bool timerExpired )
	{
		var distinctTeams = aliveTeams.Distinct().ToList();

		if ( distinctTeams.Count == 0 )
			return new Result( RoleTeam.Traitors, WinReason.Elimination );

		if ( distinctTeams.Count == 1 )
			return new Result( distinctTeams[0], WinReason.Elimination );

		if ( timerExpired )
			return new Result( RoleTeam.Innocents, WinReason.Timeout );

		return null;
	}
}
