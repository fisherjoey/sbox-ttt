using System;
using System.Collections.Generic;

namespace TTT;

/// <summary>
/// Vanilla GMod TTT role assignment.
/// - traitors = clamp(floor(N * 0.25), 1, 32)   (ttt_traitor_pct)
/// - detectives = clamp(floor(N * 0.13), 0, 32) gated by N >= ttt_detective_min_players (10)
/// Source: troubleinterroristtown.com/config/settings/
/// </summary>
public static class RoleAssignment
{
	public const float TraitorPct = 0.25f;
	public const float DetectivePct = 0.13f;
	public const int DetectiveMinPlayers = 10;

	/// <summary>
	/// Karma threshold below which a player can't be selected as Detective. On
	/// the vanilla 0-1000 karma scale the wiki suggests "high karma"; we use 600
	/// — comfortably above the 450 kick threshold without excluding casual players.
	/// </summary>
	public const int MinDetectiveKarma = 600;

	public static void Assign( IList<TTTPlayer> players )
	{
		var n = players.Count;
		var traitorCount = Math.Clamp( (int)MathF.Floor( n * TraitorPct ), 1, 32 );
		var detectiveCount = n >= DetectiveMinPlayers
			? Math.Clamp( (int)MathF.Floor( n * DetectivePct ), 0, 32 )
			: 0;

		players.Shuffle();
		var i = 0;

		while ( traitorCount-- > 0 && i < players.Count )
			players[i++].Role.Type = RoleType.Traitor;

		while ( i < players.Count )
		{
			if ( detectiveCount > 0 && players[i].Role.Karma >= MinDetectiveKarma )
			{
				players[i].Role.Type = RoleType.Detective;
				detectiveCount--;
			}
			else
			{
				players[i].Role.Type = RoleType.Innocent;
			}
			i++;
		}
	}
}
