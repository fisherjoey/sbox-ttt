using Sandbox;

namespace TTT;

[Title( "TTT Player Role" ), Icon( "badge" )]
public sealed class PlayerRole : Component
{
	public RoleType Type { get; set; } = RoleType.Innocent;

	public int Credits { get; set; }

	public float Karma { get; set; } = 1.0f;

	public RoleTeam Team => Type.Team();
}
