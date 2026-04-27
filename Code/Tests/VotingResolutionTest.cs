using TTT;

namespace TTT.Tests;

[TestClass]
public class VotingResolutionTest
{
	// =========================================================================
	// ResolveMotion — quorum gate
	// =========================================================================

	[TestMethod]
	[DataRow( 0, 0, VotePhase.Cooldown )]   // no players → cooldown
	[DataRow( 0, 8, VotePhase.Cooldown )]   // motion-er alone, no seconds → fail
	[DataRow( 1, 8, VotePhase.Cooldown )]   // 1 second, need 2 (25% of 8) → fail
	[DataRow( 2, 8, VotePhase.Vote )]       // 2 seconds, exactly quorum → pass
	[DataRow( 4, 8, VotePhase.Vote )]       // 4 seconds, well above → pass
	[DataRow( 1, 4, VotePhase.Vote )]       // 1 second on 4 players, ceil(1.0) = 1 → pass
	public void MotionResolvesByQuorum( int seconds, int nonSpectators, VotePhase expected )
	{
		var actual = VotingResolution.ResolveMotion( seconds, nonSpectators, quorumPct: 0.25f );
		Assert.AreEqual( expected, actual );
	}

	[TestMethod]
	public void RequiredQuorum_RoundsUp()
	{
		// 25% of 7 = 1.75 → ceil to 2.
		Assert.AreEqual( 2, VotingResolution.RequiredQuorum( 7, 0.25f ) );
		// 25% of 4 = 1.0 → 1.
		Assert.AreEqual( 1, VotingResolution.RequiredQuorum( 4, 0.25f ) );
		// 25% of 16 = 4 → 4.
		Assert.AreEqual( 4, VotingResolution.RequiredQuorum( 16, 0.25f ) );
	}

	[TestMethod]
	public void RequiredQuorum_NoPlayersIsImpossible()
	{
		Assert.AreEqual( int.MaxValue, VotingResolution.RequiredQuorum( 0, 0.25f ) );
	}

	// =========================================================================
	// ResolveVote — pass threshold
	// =========================================================================

	[TestMethod]
	[DataRow( 0, 0, VotePhase.Cooldown )]   // nobody voted → fail
	[DataRow( 6, 4, VotePhase.Idle )]       // 60% yes → pass (idle = applied)
	[DataRow( 5, 5, VotePhase.Cooldown )]   // 50% yes → fail (need 60%)
	[DataRow( 7, 3, VotePhase.Idle )]       // 70% → pass
	[DataRow( 0, 5, VotePhase.Cooldown )]   // unanimous no → fail
	[DataRow( 5, 0, VotePhase.Idle )]       // unanimous yes → pass
	[DataRow( 3, 2, VotePhase.Idle )]       // 60% on small turnout → pass (3/5 = 0.6)
	public void VoteResolvesByThreshold( int yes, int no, VotePhase expected )
	{
		var actual = VotingResolution.ResolveVote( yes, no, passThreshold: 0.6f );
		Assert.AreEqual( expected, actual );
	}

	[TestMethod]
	public void VoteWithLowerThresholdAcceptsMore()
	{
		// Same vote, lower threshold → would pass.
		Assert.AreEqual( VotePhase.Cooldown, VotingResolution.ResolveVote( 5, 5, 0.6f ) );
		Assert.AreEqual( VotePhase.Idle,     VotingResolution.ResolveVote( 5, 5, 0.5f ) );
	}
}
