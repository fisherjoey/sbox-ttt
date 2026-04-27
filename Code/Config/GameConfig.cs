using Sandbox;

namespace TTT;

public enum KarmaMode
{
	Off,
	Lenient,
	Strict,
}

/// <summary>
/// Host-authoritative round configuration. Lives as a Component on the same
/// GameObject as <see cref="TTTGameMode"/>. Host edits via inspector (v2);
/// in-game UI + voting come in v3/v4. See CONFIG.md for the full design.
///
/// Defaults match vanilla GMod TTT — see CATALOG.md for source citations.
/// </summary>
[Title( "TTT Game Config" ), Icon( "tune" )]
public sealed class GameConfig : Component
{
	public static GameConfig Current => TTTGameMode.Current?.GetComponent<GameConfig>();

	// --- Pace ---
	[Property, Sync( SyncFlags.FromHost ), Range( 10, 120, 1 ), Group( "Pace" )]
	public float PrepDuration { get; set; } = 30f;

	[Property, Sync( SyncFlags.FromHost ), Range( 10, 240, 1 ), Group( "Pace" )]
	public float FirstPrepDuration { get; set; } = 60f;

	[Property, Sync( SyncFlags.FromHost ), Range( 60, 1800, 5 ), Group( "Pace" )]
	public float ActiveDuration { get; set; } = 300f;

	[Property, Sync( SyncFlags.FromHost ), Range( 5, 120, 1 ), Group( "Pace" )]
	public float PostRoundDuration { get; set; } = 30f;

	[Property, Sync( SyncFlags.FromHost ), Group( "Pace" )]
	public bool HasteMode { get; set; } = true;

	[Property, Sync( SyncFlags.FromHost ), Range( 0, 60, 1 ), Group( "Pace" )]
	public float HasteSecondsPerDeath { get; set; } = 30f;

	// --- Roles ---
	[Property, Sync( SyncFlags.FromHost ), Range( 0.1f, 0.5f, 0.01f ), Group( "Roles" )]
	public float TraitorPct { get; set; } = 0.25f;

	[Property, Sync( SyncFlags.FromHost ), Range( 0f, 0.3f, 0.01f ), Group( "Roles" )]
	public float DetectivePct { get; set; } = 0.13f;

	[Property, Sync( SyncFlags.FromHost ), Range( 4, 32, 1 ), Group( "Roles" )]
	public int DetectiveMinPlayers { get; set; } = 10;

	[Property, Sync( SyncFlags.FromHost ), Range( 0, 1000, 50 ), Group( "Roles" )]
	public int MinDetectiveKarma { get; set; } = 600;

	// --- Combat ---
	[Property, Sync( SyncFlags.FromHost ), Range( 50, 200, 10 ), Group( "Combat" )]
	public float BaseHealth { get; set; } = 100f;

	[Property, Sync( SyncFlags.FromHost ), Range( 0.5f, 5f, 0.1f ), Group( "Combat" )]
	public float HeadshotMultiplier { get; set; } = 2.0f;

	[Property, Sync( SyncFlags.FromHost ), Group( "Combat" )]
	public bool FriendlyFire { get; set; } = true;

	[Property, Sync( SyncFlags.FromHost ), Group( "Combat" )]
	public bool DyingShot { get; set; } = false;

	// --- Karma ---
	[Property, Sync( SyncFlags.FromHost ), Group( "Karma" )]
	public KarmaMode KarmaMode { get; set; } = KarmaMode.Strict;

	[Property, Sync( SyncFlags.FromHost ), Range( 100, 2000, 50 ), Group( "Karma" )]
	public int StartingKarma { get; set; } = 1000;

	[Property, Sync( SyncFlags.FromHost ), Range( 0, 1500, 50 ), Group( "Karma" )]
	public int KickThreshold { get; set; } = 450;

	[Property, Sync( SyncFlags.FromHost ), Range( 0, 50, 1 ), Group( "Karma" )]
	public int RecoveryPerRound { get; set; } = 5;

	[Property, Sync( SyncFlags.FromHost ), Range( 0, 100, 5 ), Group( "Karma" )]
	public int CleanRoundBonus { get; set; } = 30;

	/// <summary>
	/// Auto-kick players whose karma drops at or below <see cref="KickThreshold"/>
	/// at round end. <c>ttt_karma_low_autokick</c> in vanilla, default on.
	/// </summary>
	[Property, Sync( SyncFlags.FromHost ), Group( "Karma" )]
	public bool KarmaAutoKick { get; set; } = true;

	// --- Economy ---
	[Property, Sync( SyncFlags.FromHost ), Range( 0, 10, 1 ), Group( "Economy" )]
	public int StartingCreditsTraitor { get; set; } = 2;

	[Property, Sync( SyncFlags.FromHost ), Range( 0, 10, 1 ), Group( "Economy" )]
	public int StartingCreditsDetective { get; set; } = 1;

	[Property, Sync( SyncFlags.FromHost ), Range( 0, 5, 1 ), Group( "Economy" )]
	public int KillBonusCredits { get; set; } = 1;

	[Property, Sync( SyncFlags.FromHost ), Range( 1, 20, 1 ), Group( "Economy" )]
	public int MaxCredits { get; set; } = 10;

	[Property, Sync( SyncFlags.FromHost ), Range( 0, 10, 1 ), Group( "Economy" )]
	public int EquipmentLimitPerRound { get; set; } = 0;

	// --- Map ---
	[Property, Sync( SyncFlags.FromHost ), Range( 1, 30, 1 ), Group( "Map" )]
	public int RoundLimit { get; set; } = 6;

	[Property, Sync( SyncFlags.FromHost ), Range( 5, 240, 5 ), Group( "Map" )]
	public int TimeLimitMinutes { get; set; } = 75;

	[Property, Sync( SyncFlags.FromHost ), Range( 2, 16, 1 ), Group( "Map" )]
	public int MinPlayers { get; set; } = 2;

	[Property, Sync( SyncFlags.FromHost ), Range( 0, 600, 30 ), Group( "Map" )]
	public int IdleToSpectatorSeconds { get; set; } = 180;

	/// <summary>
	/// Run validators and apply if they pass. Host-only. Returns the list of
	/// validation errors, or empty if applied successfully.
	/// </summary>
	public System.Collections.Generic.IReadOnlyList<string> TryApply()
	{
		if ( !Networking.IsHost ) return new[] { "Only the host may change config." };

		var errors = GameConfigValidator.Validate( this );
		if ( errors.Count > 0 ) return errors;

		// Sync happens automatically via [Sync(SyncFlags.FromHost)] on each property.
		return System.Array.Empty<string>();
	}
}
