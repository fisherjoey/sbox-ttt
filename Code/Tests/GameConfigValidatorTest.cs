using TTT;

namespace TTT.Tests;

[TestClass]
public class GameConfigValidatorTest
{
	[TestMethod]
	public void DefaultsAreValid()
	{
		var errors = GameConfigValidator.Validate( GameConfigSnapshot.Default );
		Assert.AreEqual( 0, errors.Count, $"vanilla defaults should be valid; got: {string.Join( "; ", errors )}" );
	}

	// --- Liveness ---

	[TestMethod]
	public void TraitorPctTooLowAtMinPlayers_FailsLiveness()
	{
		var c = GameConfigSnapshot.Default with { TraitorPct = 0.1f, MinPlayers = 4 };
		var errors = GameConfigValidator.Validate( c );
		// floor(4 * 0.1) = 0, no traitors — round can't start.
		Assert.IsTrue( ContainsErrorAbout( errors, "produces 0 traitors" ),
			$"expected traitors=0 error, got: {string.Join( "; ", errors )}" );
	}

	[TestMethod]
	public void TraitorPlusDetectiveAt100Pct_FailsInnocentsExist()
	{
		var c = GameConfigSnapshot.Default with { TraitorPct = 0.5f, DetectivePct = 0.5f, DetectiveMinPlayers = 2 };
		var errors = GameConfigValidator.Validate( c );
		Assert.IsTrue( ContainsErrorAbout( errors, "Innocents must always exist" ) );
	}

	[TestMethod]
	public void HasteMode_TimerCantGrowPast30Min()
	{
		var c = GameConfigSnapshot.Default with { ActiveDuration = 1800f, HasteSecondsPerDeath = 60f, MinPlayers = 16 };
		var errors = GameConfigValidator.Validate( c );
		Assert.IsTrue( ContainsErrorAbout( errors, "exceeds 30 min cap" ) );
	}

	// --- Pace floors ---

	[TestMethod]
	public void PrepDurationTooShort_Fails()
	{
		var c = GameConfigSnapshot.Default with { PrepDuration = 5f };
		var errors = GameConfigValidator.Validate( c );
		Assert.IsTrue( ContainsErrorAbout( errors, "PrepDuration must be at least" ) );
	}

	[TestMethod]
	public void ActiveDurationTooShort_Fails()
	{
		var c = GameConfigSnapshot.Default with { ActiveDuration = 30f };
		var errors = GameConfigValidator.Validate( c );
		Assert.IsTrue( ContainsErrorAbout( errors, "ActiveDuration must be at least" ) );
	}

	// --- Karma sanity ---

	[TestMethod]
	public void KickThresholdAboveStartKarma_NeverKicks()
	{
		var c = GameConfigSnapshot.Default with { KickThreshold = 1100, StartingKarma = 1000 };
		var errors = GameConfigValidator.Validate( c );
		Assert.IsTrue( ContainsErrorAbout( errors, "No one would ever get kicked" ) );
	}

	[TestMethod]
	public void RecoveryPerRoundZeroWithKarmaOn_Fails()
	{
		var c = GameConfigSnapshot.Default with { RecoveryPerRound = 0 };
		var errors = GameConfigValidator.Validate( c );
		Assert.IsTrue( ContainsErrorAbout( errors, "RecoveryPerRound must be > 0" ) );
	}

	[TestMethod]
	public void KarmaOff_KarmaRulesNotEnforced()
	{
		// With karma off, KickThreshold and RecoveryPerRound aren't checked.
		var c = GameConfigSnapshot.Default with
		{
			KarmaMode = KarmaMode.Off,
			KickThreshold = 5000,
			RecoveryPerRound = 0,
		};
		var errors = GameConfigValidator.Validate( c );
		Assert.IsFalse( ContainsErrorAbout( errors, "kicked" ),
			"karma rules shouldn't fire when KarmaMode is Off" );
		Assert.IsFalse( ContainsErrorAbout( errors, "RecoveryPerRound" ) );
	}

	// --- Economy ---

	[TestMethod]
	public void MaxCreditsBelowStarting_Fails()
	{
		var c = GameConfigSnapshot.Default with { MaxCredits = 1, StartingCreditsTraitor = 5 };
		var errors = GameConfigValidator.Validate( c );
		Assert.IsTrue( ContainsErrorAbout( errors, "MaxCredits" ) );
	}

	// --- Detective gating ---

	[TestMethod]
	public void DetectiveMinPlayersTooLowForPct_Fails()
	{
		// 10% detective needs ceil(1/0.1) = 10 players to round to 1.
		// Setting min to 5 → detective gate fires for sub-10 games but never produces one.
		var c = GameConfigSnapshot.Default with { DetectivePct = 0.1f, DetectiveMinPlayers = 5 };
		var errors = GameConfigValidator.Validate( c );
		Assert.IsTrue( ContainsErrorAbout( errors, "DetectiveMinPlayers" ) );
	}

	// --- Combat ---

	[TestMethod]
	public void BaseHealthTooLow_Fails()
	{
		var c = GameConfigSnapshot.Default with { BaseHealth = 30f };
		var errors = GameConfigValidator.Validate( c );
		Assert.IsTrue( ContainsErrorAbout( errors, "BaseHealth" ) );
	}

	[TestMethod]
	public void HeadshotMultiplierOutOfRange_Fails()
	{
		var c1 = GameConfigSnapshot.Default with { HeadshotMultiplier = 0.1f };
		Assert.IsTrue( ContainsErrorAbout( GameConfigValidator.Validate( c1 ), "HeadshotMultiplier" ) );

		var c2 = GameConfigSnapshot.Default with { HeadshotMultiplier = 10f };
		Assert.IsTrue( ContainsErrorAbout( GameConfigValidator.Validate( c2 ), "HeadshotMultiplier" ) );
	}

	// --- Map ---

	[TestMethod]
	public void MinPlayersBelowTwo_Fails()
	{
		var c = GameConfigSnapshot.Default with { MinPlayers = 1 };
		var errors = GameConfigValidator.Validate( c );
		Assert.IsTrue( ContainsErrorAbout( errors, "MinPlayers must be at least 2" ) );
	}

	// --- Helpers ---

	static bool ContainsErrorAbout( System.Collections.Generic.IReadOnlyList<string> errors, string substring )
	{
		foreach ( var e in errors )
			if ( e.Contains( substring ) ) return true;
		return false;
	}
}
