using TTT;

namespace TTT.Tests;

[TestClass]
public class RoleAssignmentMathTest
{
	const float TraitorPct = 0.25f;
	const float DetectivePct = 0.13f;
	const int DetectiveMin = 10;

	[TestMethod]
	[DataRow( 0,  0, 0, 0 )]   // empty — short-circuit, no traitors forced
	[DataRow( 1,  1, 0, 0 )]   // single player — clamp guarantees 1 traitor
	[DataRow( 2,  1, 0, 1 )]   // 2: floor(0.5)=0, clamped up to 1
	[DataRow( 4,  1, 0, 3 )]   // 4: floor(1.0)=1, no detective (under 10)
	[DataRow( 8,  2, 0, 6 )]   // 8: 2 traitors, no detective
	[DataRow( 9,  2, 0, 7 )]   // 9: still under detective gate
	[DataRow( 10, 2, 1, 7 )]   // 10: detective unlocks; floor(1.3)=1
	[DataRow( 12, 3, 1, 8 )]   // 12: 3 traitors, 1 detective
	[DataRow( 16, 4, 2, 10 )]  // 16: 4 traitors, 2 detectives
	[DataRow( 24, 6, 3, 15 )]  // 24: 6 traitors, 3 detectives
	[DataRow( 32, 8, 4, 20 )]  // 32: clamped at top
	public void VanillaCounts( int n, int expectedTraitors, int expectedDetectives, int expectedInnocents )
	{
		var counts = RoleAssignmentMath.Compute( n, TraitorPct, DetectivePct, DetectiveMin );
		Assert.AreEqual( expectedTraitors, counts.Traitors, $"traitors at N={n}" );
		Assert.AreEqual( expectedDetectives, counts.Detectives, $"detectives at N={n}" );
		Assert.AreEqual( expectedInnocents, counts.Innocents, $"innocents at N={n}" );
	}

	[TestMethod]
	public void CountsAlwaysSumToPlayerCount()
	{
		for ( var n = 1; n <= 32; n++ )
		{
			var counts = RoleAssignmentMath.Compute( n, TraitorPct, DetectivePct, DetectiveMin );
			Assert.AreEqual( n, counts.Traitors + counts.Detectives + counts.Innocents,
				$"counts don't sum at N={n}" );
		}
	}

	[TestMethod]
	public void DetectivesAreZeroBelowMinPlayers()
	{
		for ( var n = 1; n < DetectiveMin; n++ )
		{
			var counts = RoleAssignmentMath.Compute( n, TraitorPct, DetectivePct, DetectiveMin );
			Assert.AreEqual( 0, counts.Detectives, $"detectives nonzero at N={n} (below min)" );
		}
	}

	[TestMethod]
	public void TraitorsAlwaysAtLeastOne()
	{
		// Even at very low traitor% the clamp guarantees 1.
		var counts = RoleAssignmentMath.Compute( 4, traitorPct: 0.01f, detectivePct: 0f, detectiveMinPlayers: 100 );
		Assert.AreEqual( 1, counts.Traitors );
	}

	[TestMethod]
	public void ExtremePctsDontOverAllocate()
	{
		// 50% traitor + 50% detective on 10 players would overflow the count.
		// Compute trims detectives so total <= playerCount.
		var counts = RoleAssignmentMath.Compute( 10, traitorPct: 0.5f, detectivePct: 0.5f, detectiveMinPlayers: 4 );
		Assert.IsTrue( counts.Traitors + counts.Detectives + counts.Innocents == 10,
			$"sum != 10: T={counts.Traitors} D={counts.Detectives} I={counts.Innocents}" );
		Assert.IsTrue( counts.Innocents >= 0 );
	}
}
