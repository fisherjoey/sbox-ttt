using Sandbox;

namespace TTT;

[Title( "TTT Player Role" ), Icon( "badge" )]
public sealed class PlayerRole : Component
{
	public RoleType Type { get; set; } = RoleType.Innocent;

	public int Credits { get; set; }

	/// <summary>
	/// Live karma. Mutated during the round by hurt penalties / traitor-kill
	/// rewards. Round-end recovery + clean bonus also write here.
	/// </summary>
	public int Karma { get; set; } = 1000;

	/// <summary>
	/// Karma snapshot at round start. Used for outgoing damage scaling so a
	/// single bad round can't insta-cripple a player mid-round (vanilla
	/// behaviour from karma.lua: GetBaseKarma vs GetLiveKarma).
	/// </summary>
	public int BaseKarma { get; set; } = 1000;

	/// <summary>
	/// True if this player hasn't damaged a teammate this round. Reset to true
	/// at round start, flipped to false when a hurt-teammate penalty fires.
	/// Required for the clean-round karma bonus.
	/// </summary>
	public bool WasCleanThisRound { get; set; } = true;

	/// <summary>
	/// Body armor — passive flag set when the player buys it from the shop.
	/// Halves incoming non-headshot damage. Wears off at round start.
	/// Vanilla limits to 1 per round (handled via Equipment.LimitPerRound).
	/// </summary>
	[Sync] public bool HasBodyArmor { get; set; }

	/// <summary>
	/// Disguiser — traitor item that suppresses your name on other players'
	/// nameplates. Owned (always purchased) vs Active (toggled on/off). Vanilla
	/// uses Q to toggle in-game. Both flags reset at round start.
	/// </summary>
	[Sync] public bool HasDisguiser { get; set; }

	[Sync] public bool IsDisguised { get; set; }

	public RoleTeam Team => Type.Team();
}
