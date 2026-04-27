using Sandbox;
using System.Collections.Generic;
using System.Linq;

namespace TTT;

public sealed partial class TTTGameMode
{
	private void TickWaiting()
	{
		if ( ConnectedNonSpectators() < MinPlayers ) return;

		var prep = RoundNumber == 0 ? FirstPreparingDuration : PreparingDuration;
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
		if ( ConnectedNonSpectators() < MinPlayers )
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

			p.Health = TTTPlayer.MaxHealth;
			p.Status = PlayerStatus.Alive;
			_alivePlayers.Add( p );
		}

		RoleAssignment.Assign( _alivePlayers );
		RoleVisibility.RevealToClients( _alivePlayers );

		RoundNumber++;
		EnterPhase( RoundPhase.Active, ActiveDuration );
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
		RoleVisibility.Clear();
		EnterPhase( RoundPhase.PostRound, PostRoundDuration );
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

		if ( HasteMode )
			PhaseEndsAt = (float)PhaseEndsAt + HasteSecondsPerDeath;
	}

	private void TickPostRound()
	{
		if ( PhaseEndsAt > 0 ) return;

		EnterPhase( ConnectedNonSpectators() >= MinPlayers
			? RoundPhase.Preparing
			: RoundPhase.WaitingForPlayers, PreparingDuration );
	}
}
