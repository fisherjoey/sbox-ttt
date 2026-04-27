using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TTT;

/// <summary>
/// Motion → second → vote state machine for player-driven config changes.
/// Design lives in CONFIG.md. This is the v1 of voting: state machine + UI
/// hooks. Integration with actual config changes is the v2 of voting work.
///
/// Flow:
///   Idle      ── any player calls RequestMotion ──► Motion (30s gather seconds)
///   Motion    ── seconds ≥ 25% non-spectators  ──► Vote (60s yes/no)
///   Motion    ── timer expires, quorum unmet   ──► Cooldown (rest of round)
///   Vote      ── yes/(yes+no) ≥ 60%            ──► apply, Idle
///   Vote      ── below threshold or timer up   ──► Cooldown
///   Cooldown  ── round ends                    ──► Idle
///
/// Anti-grief: motion-er can't second self; max 2 motions per map regardless
/// of outcome; cooldown wipes on round end so each round gets a fresh budget.
/// </summary>
[Title( "TTT Voting System" ), Icon( "how_to_vote" )]
public sealed class VotingSystem : Component
{
	public static VotingSystem Current => Game.ActiveScene?.GetAllComponents<VotingSystem>().FirstOrDefault();

	// --- Tunable thresholds (host-synced, mirrors CONFIG.md defaults) ---
	[Property, Sync( SyncFlags.FromHost )] public float MotionDuration { get; set; } = 30f;
	[Property, Sync( SyncFlags.FromHost )] public float MotionQuorumPct { get; set; } = 0.25f;
	[Property, Sync( SyncFlags.FromHost )] public float VoteDuration { get; set; } = 60f;
	[Property, Sync( SyncFlags.FromHost )] public float VotePassThreshold { get; set; } = 0.60f;
	[Property, Sync( SyncFlags.FromHost )] public int MaxMotionsPerMap { get; set; } = 2;

	// --- Live state (host-synced) ---
	[Sync( SyncFlags.FromHost )] public VotePhase Phase { get; private set; } = VotePhase.Idle;
	[Sync( SyncFlags.FromHost )] public string Subject { get; private set; } = "";
	[Sync( SyncFlags.FromHost )] public Guid MotionerId { get; private set; }
	[Sync( SyncFlags.FromHost )] public TimeUntil PhaseEndsAt { get; private set; }
	[Sync( SyncFlags.FromHost )] public int MotionsUsedThisMap { get; private set; }

	// Per-phase participation. Host-only; clients see counts via the synced
	// Counts properties below.
	private readonly HashSet<Guid> _seconds = new();
	private readonly HashSet<Guid> _yesVotes = new();
	private readonly HashSet<Guid> _noVotes = new();

	[Sync( SyncFlags.FromHost )] public int SecondsCount { get; private set; }
	[Sync( SyncFlags.FromHost )] public int YesCount { get; private set; }
	[Sync( SyncFlags.FromHost )] public int NoCount { get; private set; }

	public static VotingSystem EnsureExists()
	{
		if ( !Networking.IsHost ) return Current;
		if ( Current is { } existing ) return existing;

		var go = new GameObject( true, "VotingSystem" );
		var v = go.Components.Create<VotingSystem>();
		go.NetworkSpawn( true, null );
		return v;
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost ) return;

		switch ( Phase )
		{
			case VotePhase.Motion when PhaseEndsAt <= 0: ResolveMotion(); break;
			case VotePhase.Vote when PhaseEndsAt <= 0:   ResolveVote(); break;
		}
	}

	/// <summary>
	/// Called on round end (any phase): wipe cooldown so each round gets a
	/// fresh motion budget. MotionsUsedThisMap persists until map change.
	/// </summary>
	public void OnRoundEnded()
	{
		if ( !Networking.IsHost ) return;
		if ( Phase == VotePhase.Cooldown )
			TransitionTo( VotePhase.Idle, "" );
	}

	public void OnMapChanged()
	{
		if ( !Networking.IsHost ) return;
		MotionsUsedThisMap = 0;
		TransitionTo( VotePhase.Idle, "" );
	}

	// --- Motion ---

	[Rpc.Host]
	public void RequestMotion( string subject )
	{
		if ( !Networking.IsHost ) return;
		if ( Phase != VotePhase.Idle ) return;
		if ( MotionsUsedThisMap >= MaxMotionsPerMap ) return;

		var caller = TTTGameMode.Current?.AllPlayers().FirstOrDefault( p => p.Network.Owner == Rpc.Caller );
		if ( !caller.IsValid() || caller.IsSpectator ) return;

		_seconds.Clear();
		MotionerId = caller.GameObject.Id;
		Subject = string.IsNullOrWhiteSpace( subject ) ? "Change game settings" : subject;
		MotionsUsedThisMap++;
		TransitionTo( VotePhase.Motion, Subject, MotionDuration );
		RecountParticipation();
	}

	[Rpc.Host]
	public void Second()
	{
		if ( !Networking.IsHost ) return;
		if ( Phase != VotePhase.Motion ) return;

		var caller = TTTGameMode.Current?.AllPlayers().FirstOrDefault( p => p.Network.Owner == Rpc.Caller );
		if ( !caller.IsValid() || caller.IsSpectator ) return;
		if ( caller.GameObject.Id == MotionerId ) return;        // can't second own motion

		_seconds.Add( caller.GameObject.Id );
		RecountParticipation();
	}

	private void ResolveMotion()
	{
		var next = VotingResolution.ResolveMotion( _seconds.Count, ConnectedNonSpectators(), MotionQuorumPct );
		if ( next == VotePhase.Vote )
		{
			_yesVotes.Clear();
			_noVotes.Clear();
			TransitionTo( VotePhase.Vote, Subject, VoteDuration );
		}
		else
		{
			TransitionTo( VotePhase.Cooldown, Subject );
		}
		RecountParticipation();
	}

	// --- Vote ---

	[Rpc.Host]
	public void CastVote( bool yes )
	{
		if ( !Networking.IsHost ) return;
		if ( Phase != VotePhase.Vote ) return;

		var caller = TTTGameMode.Current?.AllPlayers().FirstOrDefault( p => p.Network.Owner == Rpc.Caller );
		if ( !caller.IsValid() || caller.IsSpectator ) return;

		var id = caller.GameObject.Id;
		if ( yes ) { _yesVotes.Add( id ); _noVotes.Remove( id ); }
		else       { _noVotes.Add( id );  _yesVotes.Remove( id ); }
		RecountParticipation();
	}

	private void ResolveVote()
	{
		var next = VotingResolution.ResolveVote( _yesVotes.Count, _noVotes.Count, VotePassThreshold );
		if ( next == VotePhase.Idle )
		{
			ApplyMotion();
			TransitionTo( VotePhase.Idle, "" );
		}
		else
		{
			TransitionTo( VotePhase.Cooldown, Subject );
		}
		RecountParticipation();
	}

	/// <summary>
	/// Hook for the actual config change. v1 of voting only ships the state
	/// machine — integration with GameConfig is post-v1. Subclass or replace
	/// this to wire concrete changes.
	/// </summary>
	private void ApplyMotion()
	{
		// TODO[v2-voting]: deserialize the proposed config diff and apply via
		// GameConfig.TryApply(). For now, log and surface a chat message.
		Log.Info( $"[Voting] Motion passed: {Subject}" );

		var chat = UI.ChatPanel.Current;
		chat?.BroadcastSystem( $"Motion passed: {Subject}" );
	}

	// --- Helpers ---

	private void TransitionTo( VotePhase next, string subject, float duration = 0f )
	{
		Phase = next;
		Subject = subject;
		PhaseEndsAt = duration;
		if ( next == VotePhase.Idle )
		{
			MotionerId = Guid.Empty;
			_seconds.Clear();
			_yesVotes.Clear();
			_noVotes.Clear();
		}
	}

	private void RecountParticipation()
	{
		SecondsCount = _seconds.Count;
		YesCount = _yesVotes.Count;
		NoCount = _noVotes.Count;
	}

	private static int ConnectedNonSpectators() =>
		TTTGameMode.Current?.AllPlayers().Count( p => p.IsValid() && !p.IsSpectator ) ?? 0;

	public int Quorum => VotingResolution.RequiredQuorum( ConnectedNonSpectators(), MotionQuorumPct );
}
