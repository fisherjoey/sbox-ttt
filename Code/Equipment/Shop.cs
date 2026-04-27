using Sandbox;
using System;
using System.Linq;

namespace TTT;

/// <summary>
/// Holds the equipment registry for the round and serves as the entry point for
/// client → host purchase requests. One instance lives in the scene; equipment
/// items are children attached to the same GameObject.
/// </summary>
[Title( "TTT Shop" ), Icon( "shopping_cart" )]
public sealed class Shop : Component
{
	public static Shop Current => Game.ActiveScene?.GetAllComponents<Shop>().FirstOrDefault();

	/// <summary>
	/// Make sure a shop exists in the scene with the canonical v1 item set.
	/// Called from <see cref="TTTGameMode.OnStart"/> on the host.
	/// </summary>
	public static Shop EnsureExists()
	{
		if ( !Networking.IsHost ) return Current;
		if ( Current is { } existing ) return existing;

		var go = new GameObject( true, "Shop" );
		var shop = go.Components.Create<Shop>();
		go.Components.Create<BodyArmor>();
		go.Components.Create<Disguiser>();
		go.Components.Create<KnifeEquipment>();
		go.Components.Create<SilencedPistolEquipment>();
		go.Components.Create<C4Equipment>();
		go.Components.Create<DecoyEquipment>();
		go.Components.Create<HealthStationEquipment>();
		go.NetworkSpawn( true, null );

		return shop;
	}

	/// <summary>
	/// Client → host purchase request. Resolves the calling player and the
	/// requested equipment by id, then runs the standard <see cref="Equipment.Purchase"/>
	/// validation. Silently rejects on any mismatch — client UI should already
	/// be filtering, this is the authoritative gate.
	/// </summary>
	[Rpc.Host]
	public void RequestPurchase( Guid equipmentId )
	{
		if ( !Networking.IsHost ) return;

		var equipment = Scene.Directory.FindByGuid( equipmentId )?.GetComponent<Equipment>();
		var buyer = TTTGameMode.Current?.AllPlayers().FirstOrDefault( p => p.Network.Owner == Rpc.Caller );

		if ( !equipment.IsValid() || !buyer.IsValid() ) return;

		equipment.Purchase( buyer );
	}
}
