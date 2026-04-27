using Sandbox;

namespace TTT;

public enum PlayerStatus
{
	Alive,
	Dead,
	Spectator,
}

[Title( "TTT Player" ), Icon( "person" )]
public sealed partial class TTTPlayer : Component
{
	[Sync] public PlayerStatus Status { get; set; } = PlayerStatus.Alive;
	[Sync] public float Health { get; set; } = MaxHealth;

	public const float MaxHealth = 100f;

	public PlayerRole Role => GameObject.Components.GetOrCreate<PlayerRole>();

	public bool IsAlive => Status == PlayerStatus.Alive;
	public bool IsSpectator => Status == PlayerStatus.Spectator;

	public Connection Owner => Network.Owner;

	public void OnConnectionActive( Connection channel )
	{
		Network.AssignOwnership( channel );
	}
}
