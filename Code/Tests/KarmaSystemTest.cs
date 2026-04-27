using TTT;

namespace TTT.Tests;

/// <summary>
/// Unit tests for <see cref="KarmaSystem"/>. Expected values cross-checked
/// against the canonical GMod karma.lua. Run via the s&box editor's test tab.
///
/// Convention follows engine tests in <c>Sandbox.Test.Unit</c> (MSTest-style).
/// </summary>
[TestClass]
public class KarmaSystemTest
{
	// =========================================================================
	// GetDamageFactor
	// =========================================================================

	[TestMethod]
	[DataRow( 1000, 1.0f )]   // at-start = full damage
	[DataRow( 1500, 1.0f )]   // above-start = full damage (clamped)
	public void DamageFactorAtOrAboveStart( int karma, float expected )
	{
		var df = KarmaSystem.GetDamageFactor( karma, KarmaMode.Strict );
		Assert.AreEqual( expected, df, 0.001f );
	}

	[TestMethod]
	public void DamageFactorOffModeAlwaysFull()
	{
		// Even at karma=0, mode=Off should never scale damage.
		Assert.AreEqual( 1.0f, KarmaSystem.GetDamageFactor( 0, KarmaMode.Off ), 0.001f );
		Assert.AreEqual( 1.0f, KarmaSystem.GetDamageFactor( 500, KarmaMode.Off ), 0.001f );
	}

	[TestMethod]
	// k = karma - 1000, df = 1 + 0.0007k - 0.000002k², clamped [0.1, 1.0]
	[DataRow( 800, 0.78f )]   // k=-200: 1 - 0.14 - 0.08 = 0.78
	[DataRow( 600, 0.40f )]   // k=-400: 1 - 0.28 - 0.32 = 0.40
	[DataRow( 450, 0.1f )]    // k=-550: 1 - 0.385 - 0.605 = 0.01 → clamped to 0.1
	[DataRow( 0,   0.1f )]    // floor
	public void DamageFactorStrictCurve( int karma, float expected )
	{
		var df = KarmaSystem.GetDamageFactor( karma, KarmaMode.Strict );
		Assert.AreEqual( expected, df, 0.01f, $"karma={karma}" );
	}

	[TestMethod]
	public void DamageFactorAlwaysClampedToFloor()
	{
		// Both modes must respect the [0.1, 1.0] clamp from karma.lua.
		for ( var k = 0; k <= 2000; k += 100 )
		{
			var strict = KarmaSystem.GetDamageFactor( k, KarmaMode.Strict );
			var lenient = KarmaSystem.GetDamageFactor( k, KarmaMode.Lenient );
			Assert.IsTrue( strict >= 0.1f && strict <= 1.0f, $"strict at karma={k} is {strict}" );
			Assert.IsTrue( lenient >= 0.1f && lenient <= 1.0f, $"lenient at karma={k} is {lenient}" );
		}
	}

	[TestMethod]
	public void DamageFactorStrictDecaysFasterThanLenient()
	{
		// At karma 800 (k=-200), strict should drop more than lenient.
		// Lenient: df = 1 + -0.0000025 * 40000 = 1 - 0.1 = 0.9
		// Strict:  df = 1 - 0.14 - 0.08 = 0.78
		var strict = KarmaSystem.GetDamageFactor( 800, KarmaMode.Strict );
		var lenient = KarmaSystem.GetDamageFactor( 800, KarmaMode.Lenient );
		Assert.IsTrue( strict < lenient, $"strict={strict} should be less than lenient={lenient}" );
	}

	// =========================================================================
	// GetHurtPenalty / GetKillPenalty
	// =========================================================================

	[TestMethod]
	[DataRow( 1000, 0f, 0f )]      // 0 damage = 0 penalty
	[DataRow( 1000, 100f, 100f )]  // 1000 * clamp(100 * 0.001, 0, 1) = 1000 * 0.1 = 100
	[DataRow( 1000, 1000f, 1000f )] // saturated: 1000 * clamp(1.0) = 1000
	[DataRow( 1000, 5000f, 1000f )] // saturated past 1000 dmg
	[DataRow( 500, 100f, 50f )]    // half the karma = half the penalty
	public void HurtPenaltyMatchesCanonicalFormula( int victimKarma, float damage, float expected )
	{
		var penalty = KarmaSystem.GetHurtPenalty( victimKarma, damage );
		Assert.AreEqual( expected, penalty, 0.01f );
	}

	[TestMethod]
	public void KillPenaltyEqualsHurtAt15Damage()
	{
		// Per karma.lua: GetKillPenalty = GetHurtPenalty(victim_karma, 15)
		var killPenalty = KarmaSystem.GetKillPenalty( 1000 );
		var hurtAt15 = KarmaSystem.GetHurtPenalty( 1000, 15f );
		Assert.AreEqual( hurtAt15, killPenalty, 0.001f );
		// 1000 * clamp(15 * 0.001, 0, 1) = 1000 * 0.015 = 15
		Assert.AreEqual( 15f, killPenalty, 0.001f );
	}

	// =========================================================================
	// GetHurtReward / GetKillReward
	// =========================================================================

	[TestMethod]
	[DataRow( 100f, 30f )]    // 1000 * clamp(100 * 0.0003, 0, 1) = 1000 * 0.03 = 30
	[DataRow( 1000f, 300f )]  // 1000 * 0.3 = 300
	[DataRow( 4000f, 1000f )] // saturated
	public void HurtRewardMatchesCanonicalFormula( float damage, float expected )
	{
		var reward = KarmaSystem.GetHurtReward( damage );
		Assert.AreEqual( expected, reward, 0.01f );
	}

	[TestMethod]
	public void KillRewardIs12()
	{
		// Per karma.lua: GetKillReward = GetHurtReward(40) = 1000 * 40*0.0003 = 12
		Assert.AreEqual( 12f, KarmaSystem.GetKillReward(), 0.001f );
	}

	// =========================================================================
	// ApplyRoundEnd
	// =========================================================================

	[TestMethod]
	public void RoundEndAddsRecoveryAndCleanBonus()
	{
		// Clean round: +5 recovery + 30 clean bonus = +35
		Assert.AreEqual( 535, KarmaSystem.ApplyRoundEnd( 500, wasClean: true, recoveryPerRound: 5, cleanBonus: 30 ) );
	}

	[TestMethod]
	public void RoundEndDirtyRoundOnlyGetsRecovery()
	{
		// Dirty round: +5 only
		Assert.AreEqual( 505, KarmaSystem.ApplyRoundEnd( 500, wasClean: false, recoveryPerRound: 5, cleanBonus: 30 ) );
	}

	[TestMethod]
	public void RoundEndCapsAtMax()
	{
		Assert.AreEqual( 1000, KarmaSystem.ApplyRoundEnd( 990, wasClean: true, recoveryPerRound: 5, cleanBonus: 30 ) );
	}

	[TestMethod]
	public void RoundEndFloorsAtZero()
	{
		// Negative recovery (someone misconfigured) shouldn't go below 0.
		Assert.AreEqual( 0, KarmaSystem.ApplyRoundEnd( 5, wasClean: false, recoveryPerRound: -100, cleanBonus: 0 ) );
	}

	// =========================================================================
	// ShouldKick
	// =========================================================================

	[TestMethod]
	public void ShouldKickRespectsMode()
	{
		Assert.IsFalse( KarmaSystem.ShouldKick( 100, kickThreshold: 450, mode: KarmaMode.Off ),
			"Off mode never kicks regardless of karma." );
	}

	[TestMethod]
	public void ShouldKickAtOrBelowThreshold()
	{
		Assert.IsTrue( KarmaSystem.ShouldKick( 450, kickThreshold: 450, mode: KarmaMode.Strict ) );
		Assert.IsTrue( KarmaSystem.ShouldKick( 100, kickThreshold: 450, mode: KarmaMode.Strict ) );
		Assert.IsFalse( KarmaSystem.ShouldKick( 451, kickThreshold: 450, mode: KarmaMode.Strict ) );
		Assert.IsFalse( KarmaSystem.ShouldKick( 1000, kickThreshold: 450, mode: KarmaMode.Strict ) );
	}
}
