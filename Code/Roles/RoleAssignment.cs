using System;
using System.Collections.Generic;

namespace TTT;

/// <summary>
/// Vanilla GMod TTT role assignment, parameterised by <see cref="GameConfig"/>.
/// - traitors = clamp(floor(N * TraitorPct), 1, 32)
/// - detectives = clamp(floor(N * DetectivePct), 0, 32) gated by N >= DetectiveMinPlayers
/// Source: troubleinterroristtown.com/config/settings/
/// </summary>
public static class RoleAssignment
{
	public static void Assign( IList<TTTPlayer> players, GameConfig config )
	{
		var n = players.Count;
		var traitorCount = Math.Clamp( (int)MathF.Floor( n * config.TraitorPct ), 1, 32 );
		var detectiveCount = n >= config.DetectiveMinPlayers
			? Math.Clamp( (int)MathF.Floor( n * config.DetectivePct ), 0, 32 )
			: 0;

		players.Shuffle();
		var i = 0;

		while ( traitorCount-- > 0 && i < players.Count )
		{
			var p = players[i++];
			p.Role.Type = RoleType.Traitor;
			p.Role.Credits = config.StartingCreditsTraitor;
		}

		while ( i < players.Count )
		{
			var p = players[i];
			if ( detectiveCount > 0 && p.Role.Karma >= config.MinDetectiveKarma )
			{
				p.Role.Type = RoleType.Detective;
				p.Role.Credits = config.StartingCreditsDetective;
				detectiveCount--;
			}
			else
			{
				p.Role.Type = RoleType.Innocent;
				p.Role.Credits = 0;
			}
			i++;
		}
	}
}
