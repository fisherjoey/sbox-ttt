using Sandbox;

namespace TTT;

[Title( "TTT Player Role" ), Icon( "badge" )]
public sealed class PlayerRole : Component
{
	public RoleType Type { get; set; } = RoleType.Innocent;

	public int Credits { get; set; }

	/// <summary>
	/// Vanilla TTT karma is an integer scale 0-1000, starts/maxes at 1000, kicks
	/// at 450. Damage scaling is computed from this — see karma.lua reference in
	/// CATALOG.md §3. Karma persists across rounds; visible (effective) karma
	/// only updates at round start so a single bad round can't insta-cripple.
	/// </summary>
	public int Karma { get; set; } = 1000;

	public RoleTeam Team => Type.Team();
}
