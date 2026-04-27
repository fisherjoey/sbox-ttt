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
	[Sync( SyncFlags.FromHost )] public WinReason LastWinReason { get; set; } = WinReason.Timeout;
	[Sync( SyncFlags.FromHost )] public int LastRoundDeaths { get; set; }
	[Sync( SyncFlags.FromHost )] public TimeSince LastRoundDuration { get; set; }
	[Sync( SyncFlags.FromHost )] public TimeSince RoundStartedAt { get; set; }

	/// <summary>
	/// All tunable values live on <see cref="GameConfig"/>, which is on the same
	/// GameObject. Created on demand if missing so we never NRE on a fresh scene.
	/// </summary>
	public GameConfig Config => Components.GetOrCreate<GameConfig>();

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

		// NetworkSpawn(channel) assigns ownership at spawn; no follow-up
		// AssignOwnership needed (engine GameObject.Network.cs:177).
		go.NetworkSpawn( channel );

		var player = go.GetComponent<TTTPlayer>() ?? go.Components.Create<TTTPlayer>();

		// Add a TTTVoice component for life-bucket-based voice routing if the
		// prefab didn't include one (issue #6).
		if ( go.GetComponent<TTTVoice>() is null )
			go.Components.Create<TTTVoice>();

		// Spawn a child GameObject for the nameplate above the player's head.
		// In a finished player prefab this would be a designed anchor; here we
		// create it programmatically so the v1 spine works without prefab work.
		if ( go.GetComponentInChildren<UI.Nameplate>() is null )
		{
			var anchor = new GameObject( true, "Nameplate" );
			anchor.SetParent( go );
			anchor.LocalPosition = new Vector3( 0, 0, 80 );    // ~head height; tuneable
			var plate = anchor.Components.Create<UI.Nameplate>();
			plate.Player = player;
		}

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
