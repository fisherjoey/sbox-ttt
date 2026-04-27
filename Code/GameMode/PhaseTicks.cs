using Sandbox;
using System.Collections.Generic;
using System.Linq;

namespace TTT;

public sealed partial class TTTGameMode
{
	private void TickWaiting()
	{
		if ( ConnectedNonSpectators() < Config.MinPlayers ) return;

		var prep = RoundNumber == 0 ? Config.FirstPrepDuration : Config.PrepDuration;
		EnterPhase( RoundPhase.Preparing, prep );

		foreach ( var p in AllPlayers() )
		{
			p.Role.Type = RoleType.Innocent;
			p.Role.Credits = 0;
			p.Respawn();
		}
	}

	private void TickPreparing()
	{
		if ( ConnectedNonSpectators() < Config.MinPlayers )
		{
			EnterPhase( RoundPhase.WaitingForPlayers, 0f );
			return;
		}

		if ( PhaseEndsAt > 0 ) return;

		StartActiveRound();
	}

	private void StartActiveRound()
	{
		_alivePlayers.Clear();
		_spectators.Clear();

		foreach ( var p in AllPlayers() )
		{
			if ( p.Status == PlayerStatus.Spectator )
			{
				_spectators.Add( p );
				continue;
			}

			p.Health = Config.BaseHealth;
			p.Status = PlayerStatus.Alive;
			_alivePlayers.Add( p );
		}

		RoleAssignment.Assign( _alivePlayers, Config );
		RoleVisibility.RevealToClients( _alivePlayers );

		// Snapshot karma so damage scaling uses a stable base for the round, and
		// reset the clean-round flag for everyone going into the round.
		foreach ( var p in _alivePlayers )
		{
			p.Role.BaseKarma = p.Role.Karma;
			p.Role.WasCleanThisRound = true;
		}

		RoundNumber++;
		EnterPhase( RoundPhase.Active, Config.ActiveDuration );
	}

	private void TickActive()
	{
		var alive = _alivePlayers.Where( p => p.IsValid() && p.IsAlive ).ToList();
		var aliveTeams = alive.Select( p => p.Role.Type.Team() ).Distinct().ToList();

		if ( aliveTeams.Count == 0 )
		{
			// Everyone dead. Treat as Traitor win.
			EndRound( RoleTeam.Traitors );
			return;
		}

		if ( aliveTeams.Count == 1 && aliveTeams[0] == RoleTeam.Innocents )
		{
			EndRound( RoleTeam.Innocents );
			return;
		}

		if ( aliveTeams.Count == 1 && aliveTeams[0] == RoleTeam.Traitors )
		{
			EndRound( RoleTeam.Traitors );
			return;
		}

		if ( PhaseEndsAt <= 0 )
			EndRound( RoleTeam.Innocents );
	}

	private void EndRound( RoleTeam winner )
	{
		LastWinner = winner;

		ApplyRoundEndKarma();

		RoleVisibility.Clear();
		EnterPhase( RoundPhase.PostRound, Config.PostRoundDuration );
	}

	private void ApplyRoundEndKarma()
	{
		var config = Config;
		if ( config.KarmaMode == KarmaMode.Off ) return;

		// Recovery + clean bonus for everyone who participated this round (alive
		// or spectating, doesn't matter — players who died still get the round
		// recovery in vanilla, and the clean flag tracks whether they damaged a
		// teammate, not whether they survived).
		foreach ( var p in AllPlayers() )
		{
			if ( !p.IsValid() ) continue;
			p.Role.Karma = KarmaSystem.ApplyRoundEnd(
				p.Role.Karma,
				p.Role.WasCleanThisRound,
				config.RecoveryPerRound,
				config.CleanRoundBonus,
				config.StartingKarma
			);
		}

		if ( !config.KarmaAutoKick ) return;

		foreach ( var p in AllPlayers() )
		{
			if ( !p.IsValid() ) continue;
			if ( !KarmaSystem.ShouldKick( p.Role.Karma, config.KickThreshold, config.KarmaMode ) ) continue;

			p.Network.Owner?.Kick( $"Karma below {config.KickThreshold}" );
		}
	}

	/// <summary>
	/// Hook called by the (future) damage / kill system when an alive player
	/// transitions to dead. Extends the active phase timer if haste mode is on.
	/// Host-only.
	/// </summary>
	public void OnPlayerKilled( TTTPlayer victim )
	{
		if ( !Networking.IsHost ) return;
		if ( Phase != RoundPhase.Active ) return;

		_alivePlayers.Remove( victim );
		_spectators.Add( victim );

		if ( Config.HasteMode )
			PhaseEndsAt = (float)PhaseEndsAt + Config.HasteSecondsPerDeath;
	}

	private void TickPostRound()
	{
		if ( PhaseEndsAt > 0 ) return;

		EnterPhase( ConnectedNonSpectators() >= Config.MinPlayers
			? RoundPhase.Preparing
			: RoundPhase.WaitingForPlayers, Config.PrepDuration );
	}
}
