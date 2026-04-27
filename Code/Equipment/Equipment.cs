using Sandbox;

namespace TTT;

/// <summary>
/// Base class for buyable shop items. The shop spawns these as components on the
/// purchasing player. Concrete subclasses override <see cref="OnPurchased"/> to
/// apply effects (give weapon, attach disguiser, etc).
/// </summary>
public abstract class Equipment : Component
{
	/// <summary>Display name in shop UI.</summary>
	[Property] public string Title { get; set; }

	/// <summary>Material-icon name for shop UI.</summary>
	[Property] public string IconName { get; set; }

	/// <summary>Cost in credits. Default 1 (most T items in vanilla TTT).</summary>
	[Property] public int Cost { get; set; } = 1;

	/// <summary>
	/// Limit per round per player. 0 = unlimited. Most one-shot T items (knife,
	/// jihad bomb, decoy, C4) cap at 1; some (body armor) cap at 1 because it's
	/// a passive flag rather than a stack.
	/// </summary>
	[Property] public int LimitPerRound { get; set; } = 0;

	/// <summary>
	/// Which roles can buy this. Empty = no restriction (intended for shared
	/// detective+traitor items like body armor and radar).
	/// </summary>
	[Property] public RoleType[] AllowedRoles { get; set; } = System.Array.Empty<RoleType>();

	public bool CanPurchase( TTTPlayer player )
	{
		if ( !player.IsValid() ) return false;
		if ( player.Role.Credits < Cost ) return false;
		if ( AllowedRoles.Length > 0 && System.Array.IndexOf( AllowedRoles, player.Role.Type ) < 0 )
			return false;
		return true;
	}

	/// <summary>Called server-side after credits are deducted.</summary>
	protected virtual void OnPurchased( TTTPlayer buyer ) { }

	internal void Purchase( TTTPlayer buyer )
	{
		if ( !Networking.IsHost ) return;
		if ( !CanPurchase( buyer ) ) return;

		buyer.Role.Credits -= Cost;
		OnPurchased( buyer );
	}
}
