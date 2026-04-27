using Sandbox;

namespace TTT;

/// <summary>
/// Vanilla TTT body armor: halves incoming non-headshot damage for the rest of
/// the round. Available to traitors and detectives, 1 credit, max 1 per round.
/// </summary>
[Title( "Body Armor" ), Icon( "shield" )]
public sealed class BodyArmor : Equipment
{
	public BodyArmor()
	{
		Title = "Body Armor";
		IconName = "shield";
		Cost = 1;
		LimitPerRound = 1;
		AllowedRoles = new[] { RoleType.Traitor, RoleType.Detective };
	}

	protected override void OnPurchased( TTTPlayer buyer )
	{
		if ( !Networking.IsHost ) return;
		if ( !buyer.IsValid() ) return;

		buyer.Role.HasBodyArmor = true;
	}
}
