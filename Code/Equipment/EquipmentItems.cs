using Sandbox;

namespace TTT;

/// <summary>
/// Equipment-shop wrappers for each concrete weapon / placeable. On purchase,
/// they instantiate the actual gameplay component on or near the buyer.
/// Visual model / animation work is post-v1 polish.
/// </summary>

[Title( "Knife (shop entry)" ), Icon( "restaurant" )]
public sealed class KnifeEquipment : Equipment
{
	public KnifeEquipment()
	{
		Title = "Knife";
		IconName = "restaurant";
		Cost = 1;
		LimitPerRound = 1;
		AllowedRoles = new[] { RoleType.Traitor };
	}

	protected override void OnPurchased( TTTPlayer buyer )
	{
		if ( !Networking.IsHost || !buyer.IsValid() ) return;

		// Attach the weapon component directly to the buyer. Pickup/equip flow
		// is v2 — for v1 it just becomes the active weapon.
		buyer.GameObject.Components.Create<Knife>();
	}
}

[Title( "Silenced Pistol (shop entry)" ), Icon( "volume_off" )]
public sealed class SilencedPistolEquipment : Equipment
{
	public SilencedPistolEquipment()
	{
		Title = "Silenced Pistol";
		IconName = "volume_off";
		Cost = 1;
		LimitPerRound = 1;
		AllowedRoles = new[] { RoleType.Traitor };
	}

	protected override void OnPurchased( TTTPlayer buyer )
	{
		if ( !Networking.IsHost || !buyer.IsValid() ) return;
		buyer.GameObject.Components.Create<SilencedPistol>();
	}
}

[Title( "C4 (shop entry)" ), Icon( "do_not_disturb_on" )]
public sealed class C4Equipment : Equipment
{
	public C4Equipment()
	{
		Title = "C4";
		IconName = "do_not_disturb_on";
		Cost = 1;
		LimitPerRound = 1;
		AllowedRoles = new[] { RoleType.Traitor };
	}

	protected override void OnPurchased( TTTPlayer buyer )
	{
		if ( !Networking.IsHost || !buyer.IsValid() ) return;

		// v1 simplification: drops at buyer's feet immediately. Polish later
		// gives a "carry, then place on attack1" flow with a visual model.
		var go = new GameObject( true, "C4" );
		go.WorldPosition = buyer.WorldPosition;
		var c4 = go.Components.Create<C4>();
		c4.PlacerId = buyer.GameObject.Id;
		go.NetworkSpawn( true, null );
	}
}

[Title( "Health Station (shop entry)" ), Icon( "medical_services" )]
public sealed class HealthStationEquipment : Equipment
{
	public HealthStationEquipment()
	{
		Title = "Health Station";
		IconName = "medical_services";
		Cost = 1;
		LimitPerRound = 1;
		AllowedRoles = new[] { RoleType.Detective };
	}

	protected override void OnPurchased( TTTPlayer buyer )
	{
		if ( !Networking.IsHost || !buyer.IsValid() ) return;

		var go = new GameObject( true, "HealthStation" );
		go.WorldPosition = buyer.WorldPosition;
		go.Components.Create<HealthStation>();

		// v2: attach a child BoxCollider with IsTrigger=true to define the
		// heal zone. For now players have to overlap the GameObject directly
		// for ITriggerListener to fire — needs a collider on a prefab.
		go.NetworkSpawn( true, null );
	}
}

[Title( "Decoy (shop entry)" ), Icon( "gps_not_fixed" )]
public sealed class DecoyEquipment : Equipment
{
	public DecoyEquipment()
	{
		Title = "Decoy";
		IconName = "gps_not_fixed";
		Cost = 1;
		LimitPerRound = 1;
		AllowedRoles = new[] { RoleType.Traitor };
	}

	protected override void OnPurchased( TTTPlayer buyer )
	{
		if ( !Networking.IsHost || !buyer.IsValid() ) return;

		var go = new GameObject( true, "Decoy" );
		go.WorldPosition = buyer.WorldPosition;
		go.Components.Create<Decoy>();
		go.NetworkSpawn( true, null );
	}
}
