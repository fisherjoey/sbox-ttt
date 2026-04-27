using Sandbox;
using System;
using System.Linq;

namespace TTT;

public sealed partial class TTTPlayer
{
	/// <summary>How far forward to trace when looking at a corpse to identify.</summary>
	public const float UseRange = 96f;

	protected override void OnUpdate()
	{
		base.OnUpdate();

		// Local-only input handling; ownership check first so this only fires
		// for the connection that owns this Player.
		if ( !IsLocalPlayer ) return;
		if ( !IsAlive ) return;

		if ( Input.Pressed( "use" ) )
			TryUseTrace();

		if ( Input.Pressed( "toggle_disguise" ) )
			RequestToggleDisguise();

		if ( Input.Pressed( "attack1" ) )
			TryFireWeapon();
	}

	private void TryFireWeapon()
	{
		var weapon = GameObject.GetComponent<Weapon>();
		if ( !weapon.IsValid() ) return;

		var ray = new Ray( WorldPosition + Vector3.Up * 64f, EyeForward() );
		RequestFire( ray );
	}

	[Rpc.Host]
	private void RequestFire( Ray eyeRay )
	{
		if ( !Networking.IsHost ) return;

		var caller = TTTGameMode.Current?.AllPlayers().FirstOrDefault( p => p.Network.Owner == Rpc.Caller );
		if ( !caller.IsValid() ) return;

		var weapon = caller.GameObject.GetComponent<Weapon>();
		weapon?.Fire( eyeRay );
	}

	private void TryUseTrace()
	{
		var ray = new Ray( WorldPosition + Vector3.Up * 64f, EyeForward() );
		var trace = Scene.Trace.Ray( ray, UseRange )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();

		if ( !trace.Hit ) return;

		var corpse = trace.GameObject?.GetComponentInParent<Corpse>();
		if ( corpse.IsValid() )
		{
			IdentifyCorpse( corpse.GameObject.Id );
		}
	}

	private Vector3 EyeForward()
	{
		// Until we have a proper camera component, just use facing direction.
		// The player controller (separate issue) will replace this.
		return WorldRotation.Forward;
	}

	[Rpc.Host]
	private void IdentifyCorpse( Guid corpseId )
	{
		if ( !Networking.IsHost ) return;

		var corpse = Scene.Directory.FindByGuid( corpseId )?.GetComponent<Corpse>();
		if ( !corpse.IsValid() ) return;

		// Use the calling player as the identifier. Rpc.Caller is the connection
		// that initiated the RPC; resolve to their TTTPlayer.
		var identifier = TTTGameMode.Current?.AllPlayers().FirstOrDefault( p => p.Network.Owner == Rpc.Caller );
		corpse.Identify( identifier );
	}

	[Rpc.Host]
	private void RequestToggleDisguise()
	{
		if ( !Networking.IsHost ) return;

		var player = TTTGameMode.Current?.AllPlayers().FirstOrDefault( p => p.Network.Owner == Rpc.Caller );
		if ( player.IsValid() && player.Role.HasDisguiser )
			Disguiser.Toggle( player );
	}
}
