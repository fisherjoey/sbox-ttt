namespace TTT;

/// <summary>
/// Pure-data snapshot of <see cref="GameConfig"/>. Used by
/// <see cref="GameConfigValidator"/> tests so they can run without spinning
/// up a live Component / Scene context. The Component side fills this from
/// its current properties; validators read from the snapshot.
/// </summary>
public readonly record struct GameConfigSnapshot(
	float PrepDuration,
	float FirstPrepDuration,
	float ActiveDuration,
	float PostRoundDuration,
	bool HasteMode,
	float HasteSecondsPerDeath,

	float TraitorPct,
	float DetectivePct,
	int DetectiveMinPlayers,
	int MinDetectiveKarma,

	float BaseHealth,
	float HeadshotMultiplier,
	bool FriendlyFire,
	bool DyingShot,

	KarmaMode KarmaMode,
	int StartingKarma,
	int KickThreshold,
	int RecoveryPerRound,
	int CleanRoundBonus,
	bool KarmaAutoKick,

	int StartingCreditsTraitor,
	int StartingCreditsDetective,
	int KillBonusCredits,
	int MaxCredits,
	int EquipmentLimitPerRound,

	int RoundLimit,
	int TimeLimitMinutes,
	int MinPlayers,
	int IdleToSpectatorSeconds
)
{
	/// <summary>Vanilla TTT defaults — same as a fresh <see cref="GameConfig"/>.</summary>
	public static GameConfigSnapshot Default => new(
		PrepDuration: 30f,
		FirstPrepDuration: 60f,
		ActiveDuration: 300f,
		PostRoundDuration: 30f,
		HasteMode: true,
		HasteSecondsPerDeath: 30f,

		TraitorPct: 0.25f,
		DetectivePct: 0.13f,
		DetectiveMinPlayers: 10,
		MinDetectiveKarma: 600,

		BaseHealth: 100f,
		HeadshotMultiplier: 2.0f,
		FriendlyFire: true,
		DyingShot: false,

		KarmaMode: KarmaMode.Strict,
		StartingKarma: 1000,
		KickThreshold: 450,
		RecoveryPerRound: 5,
		CleanRoundBonus: 30,
		KarmaAutoKick: true,

		StartingCreditsTraitor: 2,
		StartingCreditsDetective: 1,
		KillBonusCredits: 1,
		MaxCredits: 10,
		EquipmentLimitPerRound: 0,

		RoundLimit: 6,
		TimeLimitMinutes: 75,
		MinPlayers: 2,
		IdleToSpectatorSeconds: 180
	);
}

public static class GameConfigSnapshotExtensions
{
	public static GameConfigSnapshot ToSnapshot( this GameConfig c ) => new(
		PrepDuration: c.PrepDuration,
		FirstPrepDuration: c.FirstPrepDuration,
		ActiveDuration: c.ActiveDuration,
		PostRoundDuration: c.PostRoundDuration,
		HasteMode: c.HasteMode,
		HasteSecondsPerDeath: c.HasteSecondsPerDeath,
		TraitorPct: c.TraitorPct,
		DetectivePct: c.DetectivePct,
		DetectiveMinPlayers: c.DetectiveMinPlayers,
		MinDetectiveKarma: c.MinDetectiveKarma,
		BaseHealth: c.BaseHealth,
		HeadshotMultiplier: c.HeadshotMultiplier,
		FriendlyFire: c.FriendlyFire,
		DyingShot: c.DyingShot,
		KarmaMode: c.KarmaMode,
		StartingKarma: c.StartingKarma,
		KickThreshold: c.KickThreshold,
		RecoveryPerRound: c.RecoveryPerRound,
		CleanRoundBonus: c.CleanRoundBonus,
		KarmaAutoKick: c.KarmaAutoKick,
		StartingCreditsTraitor: c.StartingCreditsTraitor,
		StartingCreditsDetective: c.StartingCreditsDetective,
		KillBonusCredits: c.KillBonusCredits,
		MaxCredits: c.MaxCredits,
		EquipmentLimitPerRound: c.EquipmentLimitPerRound,
		RoundLimit: c.RoundLimit,
		TimeLimitMinutes: c.TimeLimitMinutes,
		MinPlayers: c.MinPlayers,
		IdleToSpectatorSeconds: c.IdleToSpectatorSeconds
	);
}
