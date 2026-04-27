using System;

namespace TTT;

/// <summary>
/// Pure resolution logic for the motion → vote state machine. Given the
/// current participation counts and tunable thresholds, decide what the next
/// state should be. The component-side <see cref="VotingSystem"/> calls into
/// this; tests exercise it directly.
/// </summary>
public static class VotingResolution
{
	/// <summary>
	/// At motion timeout: did enough non-spectators second the motion to
	/// proceed to a vote, or does it die in cooldown?
	/// </summary>
	public static VotePhase ResolveMotion( int seconds, int nonSpectators, float quorumPct )
	{
		if ( nonSpectators <= 0 ) return VotePhase.Cooldown;

		var quorum = (int)MathF.Ceiling( nonSpectators * quorumPct );
		return seconds >= quorum ? VotePhase.Vote : VotePhase.Cooldown;
	}

	/// <summary>
	/// At vote timeout: did the yes side hit the pass threshold?
	/// Returns Idle for pass (motion applied, back to idle) or Cooldown for fail.
	/// </summary>
	public static VotePhase ResolveVote( int yes, int no, float passThreshold )
	{
		var total = yes + no;
		if ( total <= 0 ) return VotePhase.Cooldown;     // no participation = fail

		var yesRatio = yes / (float)total;
		return yesRatio >= passThreshold ? VotePhase.Idle : VotePhase.Cooldown;
	}

	/// <summary>The seconds count needed to advance from Motion to Vote.</summary>
	public static int RequiredQuorum( int nonSpectators, float quorumPct )
	{
		if ( nonSpectators <= 0 ) return int.MaxValue;
		return (int)MathF.Ceiling( nonSpectators * quorumPct );
	}
}
