using Sandbox;
using System.Collections.Generic;
using System.Linq;

namespace TTT;

[Title( "TTT Game Mode" ), Icon( "casino" )]
public sealed partial class TTTGameMode : Component, Component.INetworkListener
{
	public static TTTGameMode Current { get; private set; }

	[Sync( SyncFlags.FromHost )] public RoundPhase Phase { get; set; } = RoundPhase.WaitingForPlayers;
	[Sync( SyncFlags.FromHost )] public TimeUntil PhaseEndsAt { get; set; }
	[Sync( SyncFlags.FromHost )] public int RoundNumber { get; set; }
	[Sync( SyncFlags.FromHost )] public RoleTeam LastWinner { get; set; } = RoleTeam.Innocents;

	// Defaults match vanilla GMod TTT convars (see CATALOG.md §1).
	[Property] public int MinPlayers { get; set; } = 2;
	[Property] public float PreparingDuration { get; set; } = 30f;        // ttt_preptime_seconds
	[Property] public float FirstPreparingDuration { get; set; } = 60f;   // ttt_firstpreptime
	[Property] public float ActiveDuration { get; set; } = 300f;          // haste starting minutes (5 min)
	[Property] public float PostRoundDuration { get; set; } = 30f;        // ttt_posttime_seconds

	/// <summary>
	/// Haste mode (default on in vanilla). Each death extends the active timer
	/// by <see cref="HasteSecondsPerDeath"/> to pressure traitors to act.
	/// </summary>
	[Property] public bool HasteMode { get; set; } = true;
	[Property] public float HasteSecondsPerDeath { get; set; } = 30f;     // ttt_haste_minutes_per_death * 60

	[Property, ResourceType( "prefab" )] public PrefabFile PlayerPrefab { get; set; }

	private readonly List<TTTPlayer> _alivePlayers = new();
	private readonly List<TTTPlayer> _spectators = new();

	protected override void OnStart()
	{
		Current = this;

		if ( !Networking.IsHost ) return;

		EnterPhase( RoundPhase.WaitingForPlayers, 0f );
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost ) return;

		switch ( Phase )
		{
			case RoundPhase.WaitingForPlayers: TickWaiting(); break;
			case RoundPhase.Preparing:         TickPreparing(); break;
			case RoundPhase.Active:            TickActive(); break;
			case RoundPhase.PostRound:         TickPostRound(); break;
		}
	}

	internal void EnterPhase( RoundPhase next, float duration )
	{
		Phase = next;
		PhaseEndsAt = duration;
		BroadcastPhaseChanged( next );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	private void BroadcastPhaseChanged( RoundPhase phase )
	{
		// Hook for UI / sounds on every peer.
	}

	internal IEnumerable<TTTPlayer> AllPlayers() =>
		Game.ActiveScene.GetAllComponents<TTTPlayer>();

	internal int ConnectedNonSpectators() =>
		AllPlayers().Count( p => p.Status != PlayerStatus.Spectator );

	// INetworkListener — host only
	void Component.INetworkListener.OnActive( Connection channel )
	{
		if ( !Networking.IsHost ) return;

		var go = PlayerPrefab is not null
			? SceneUtility.GetPrefabScene( PlayerPrefab ).Clone()
			: new GameObject( true, $"Player ({channel.DisplayName})" );

		go.NetworkSpawn( channel );

		var player = go.GetComponent<TTTPlayer>() ?? go.Components.Create<TTTPlayer>();
		player.OnConnectionActive( channel );
		player.Respawn();
	}

	void Component.INetworkListener.OnDisconnected( Connection channel )
	{
		// Find the player game object owned by this connection and clean up.
		var player = AllPlayers().FirstOrDefault( p => p.Owner == channel );
		if ( player is null ) return;

		_alivePlayers.Remove( player );
		_spectators.Remove( player );

		if ( player.GameObject.IsValid() )
			player.GameObject.Destroy();
	}
}
