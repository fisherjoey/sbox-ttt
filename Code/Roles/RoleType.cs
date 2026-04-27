namespace TTT;

public enum RoleType
{
	Innocent,
	Detective,
	Traitor,
}

public enum RoleTeam
{
	Innocents,
	Traitors,
}

public static class RoleTypeExtensions
{
	public static RoleTeam Team( this RoleType type ) => type switch
	{
		RoleType.Traitor => RoleTeam.Traitors,
		_ => RoleTeam.Innocents,
	};
}
