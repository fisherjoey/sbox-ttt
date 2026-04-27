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

	public RoleTeam Team => Type.Team();
}
