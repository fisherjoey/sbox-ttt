using System;
using System.Linq;
using TTT;

namespace TTT.Tests;

[TestClass]
public class WinConditionTest
{
	[TestMethod]
	public void NoTeamsAlive_TraitorsWinByElimination()
	{
		var result = WinCondition.Evaluate( Array.Empty<RoleTeam>(), timerExpired: false );
		Assert.IsNotNull( result );
		Assert.AreEqual( RoleTeam.Traitors, result.Value.Winner );
		Assert.AreEqual( WinReason.Elimination, result.Value.Reason );
	}

	[TestMethod]
	public void OnlyInnocentsAlive_InnocentsWinByElimination()
	{
		var result = WinCondition.Evaluate( new[] { RoleTeam.Innocents }, timerExpired: false );
		Assert.IsNotNull( result );
		Assert.AreEqual( RoleTeam.Innocents, result.Value.Winner );
		Assert.AreEqual( WinReason.Elimination, result.Value.Reason );
	}

	[TestMethod]
	public void OnlyTraitorsAlive_TraitorsWinByElimination()
	{
		var result = WinCondition.Evaluate( new[] { RoleTeam.Traitors }, timerExpired: false );
		Assert.IsNotNull( result );
		Assert.AreEqual( RoleTeam.Traitors, result.Value.Winner );
		Assert.AreEqual( WinReason.Elimination, result.Value.Reason );
	}

	[TestMethod]
	public void DuplicatesInTeamList_StillCountsAsOneTeam()
	{
		// Multiple traitors alive — Distinct() collapses them.
		var result = WinCondition.Evaluate( new[] { RoleTeam.Traitors, RoleTeam.Traitors, RoleTeam.Traitors }, timerExpired: false );
		Assert.IsNotNull( result );
		Assert.AreEqual( RoleTeam.Traitors, result.Value.Winner );
	}

	[TestMethod]
	public void BothTeamsAlive_NoWin()
	{
		var result = WinCondition.Evaluate( new[] { RoleTeam.Innocents, RoleTeam.Traitors }, timerExpired: false );
		Assert.IsNull( result );
	}

	[TestMethod]
	public void BothTeamsAliveAndTimerExpired_InnocentsWinByTimeout()
	{
		var result = WinCondition.Evaluate( new[] { RoleTeam.Innocents, RoleTeam.Traitors }, timerExpired: true );
		Assert.IsNotNull( result );
		Assert.AreEqual( RoleTeam.Innocents, result.Value.Winner );
		Assert.AreEqual( WinReason.Timeout, result.Value.Reason );
	}

	[TestMethod]
	public void TimerExpiredButOneTeamLeft_StillElimination()
	{
		// Timer can expire on the same tick someone dies. Elimination wins
		// over timeout because the round is decisively over.
		var result = WinCondition.Evaluate( new[] { RoleTeam.Traitors }, timerExpired: true );
		Assert.IsNotNull( result );
		Assert.AreEqual( RoleTeam.Traitors, result.Value.Winner );
		Assert.AreEqual( WinReason.Elimination, result.Value.Reason );
	}

	[TestMethod]
	public void NoTeamsAndTimerExpired_StillTraitorElimination()
	{
		// Edge case: everyone died on the last tick. Traitors win by their
		// elimination condition; timeout is not consulted.
		var result = WinCondition.Evaluate( Array.Empty<RoleTeam>(), timerExpired: true );
		Assert.IsNotNull( result );
		Assert.AreEqual( RoleTeam.Traitors, result.Value.Winner );
		Assert.AreEqual( WinReason.Elimination, result.Value.Reason );
	}
}
