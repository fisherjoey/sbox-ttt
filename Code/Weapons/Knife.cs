using Sandbox;

namespace TTT;

/// <summary>
/// Vanilla TTT knife: traitor melee weapon, one-shot one-use insta-kill.
/// Range is short, damage is whatever-it-takes. After firing once, the
/// weapon is consumed (component is destroyed).
/// </summary>
[Title( "Knife" ), Icon( "restaurant" )]
public sealed class Knife : Weapon
{
	public Knife()
	{
		Damage = 9999f;
		Range = 64f;
		FireRate = 0.5f;
		IsHeadshotCapable = false;
	}

	public override bool Fire( Ray eyeRay )
	{
		if ( !base.Fire( eyeRay ) ) return false;
		if ( !Networking.IsHost ) return true;

		// One-shot — consume after firing.
		GameObject.Destroy();
		return true;
	}
}
