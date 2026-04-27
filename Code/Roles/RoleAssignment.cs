using System;
using System.Collections.Generic;

namespace TTT;

public static class RoleAssignment
{
	/// <summary>
	/// Karma threshold below which a player can't be selected as Detective.
	/// v1 hardcodes karma at 1.0, so this gate is effectively a no-op until karma is implemented.
	/// </summary>
	public const float MinDetectiveKarma = 0.6f;

	public static void Assign( IList<TTTPlayer> players )
	{
		var traitorCount = Math.Max( players.Count >> 2, 1 );
		var detectiveCount = players.Count >> 3;

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
