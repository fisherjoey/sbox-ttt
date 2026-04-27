using System;

namespace TTT;

/// <summary>
/// Pure formula side of <see cref="RoleAssignment"/>. The component-aware
/// Assign method calls into this for the counts; tests exercise this directly
/// without needing a Scene.
///
/// Vanilla TTT (CATALOG.md §2):
/// - traitors  = clamp(floor(N × traitorPct), 1, 32)
/// - detectives = N ≥ detectiveMinPlayers ? clamp(floor(N × detectivePct), 0, 32) : 0
/// </summary>
public static class RoleAssignmentMath
{
	public readonly record struct Counts( int Traitors, int Detectives, int Innocents );

	public static Counts Compute( int playerCount, float traitorPct, float detectivePct, int detectiveMinPlayers )
	{
		if ( playerCount <= 0 ) return new Counts( 0, 0, 0 );

		var traitors = Math.Clamp( (int)MathF.Floor( playerCount * traitorPct ), 1, 32 );
		var detectives = playerCount >= detectiveMinPlayers
			? Math.Clamp( (int)MathF.Floor( playerCount * detectivePct ), 0, 32 )
			: 0;

		// Don't let traitors+detectives exceed playerCount — the percentage form
		// can technically over-allocate at extreme settings.
		if ( traitors + detectives > playerCount )
			detectives = Math.Max( 0, playerCount - traitors );

		var innocents = playerCount - traitors - detectives;
		return new Counts( traitors, detectives, innocents );
	}
}
