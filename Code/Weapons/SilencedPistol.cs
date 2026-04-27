using Sandbox;

namespace TTT;

/// <summary>
/// Vanilla TTT silenced pistol: traitor sidearm with no fire sound, slightly
/// quieter ballistics, same damage as the standard pistol. Distinguishing
/// feature is that other players don't hear you fire it.
/// </summary>
[Title( "Silenced Pistol" ), Icon( "volume_off" )]
public sealed class SilencedPistol : Weapon
{
	public SilencedPistol()
	{
		Damage = 18f;
		Range = 4096f;
		FireRate = 0.4f;
		IsHeadshotCapable = true;
		FireSound = null;     // silent
	}
}
