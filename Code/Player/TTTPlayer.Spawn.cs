using Sandbox;
using System.Linq;

namespace TTT;

public sealed partial class TTTPlayer
{
	public void Respawn()
	{
		if ( !Networking.IsHost ) return;

		Status = PlayerStatus.Alive;
		Health = MaxHealth;
		MoveToSpawnpoint();
	}

	public void MakeSpectator()
	{
		if ( !Networking.IsHost ) return;

		Status = PlayerStatus.Spectator;
		Health = 0f;
	}

	public void MoveToSpawnpoint()
	{
		var spawns = Game.ActiveScene.GetAllComponents<SpawnPoint>().ToList();
		if ( spawns.Count == 0 ) return;

		var sp = Game.Random.FromList( spawns );
		WorldPosition = sp.WorldPosition;
		WorldRotation = sp.WorldRotation;
	}
}
