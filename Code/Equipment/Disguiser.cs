using Sandbox;

namespace TTT;

/// <summary>
/// Vanilla TTT disguiser: traitor item that hides your name on other players'
/// nameplates while active. Toggle on/off via input (vanilla uses Q). Wears
/// off at round start. 1 credit, 1 per round.
///
/// The actual nameplate suppression lives in the nameplate UI (issue #13),
/// which reads <see cref="PlayerRole.IsDisguised"/>. This component only
/// owns the purchase flag and the toggle.
/// </summary>
[Title( "Disguiser" ), Icon( "theater_comedy" )]
public sealed class Disguiser : Equipment
{
	public Disguiser()
	{
		Title = "Disguiser";
		IconName = "theater_comedy";
		Cost = 1;
		LimitPerRound = 1;
		AllowedRoles = new[] { RoleType.Traitor };
	}

	protected override void OnPurchased( TTTPlayer buyer )
	{
		if ( !Networking.IsHost || !buyer.IsValid() ) return;

		buyer.Role.HasDisguiser = true;
		// Don't auto-activate — let the player toggle when they want to use it.
	}

	/// <summary>
	/// Toggle the disguise on or off. Host-only — clients call via RPC or
	/// console command (input binding in v3 work, see issue #11/#9).
	/// </summary>
	public static void Toggle( TTTPlayer player )
	{
		if ( !Networking.IsHost || !player.IsValid() ) return;
		if ( !player.Role.HasDisguiser ) return;

		player.Role.IsDisguised = !player.Role.IsDisguised;
	}
}
