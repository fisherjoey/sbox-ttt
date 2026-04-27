using Sandbox;
using System;
using System.Collections.Generic;

namespace TTT;

/// <summary>
/// Persistent body left when a TTTPlayer dies. Holds the role, credits, kill info,
/// and DNA samples needed for body identification and the detective DNA scanner.
/// </summary>
[Title( "TTT Corpse" ), Icon( "person_off" )]
public sealed class Corpse : Component
{
	[Sync] public string PlayerName { get; set; }
	[Sync] public Guid VictimId { get; set; }

	/// <summary>
	/// Role of the dead player. ONLY revealed after identification — never [Sync]'d
	/// blindly because clients shouldn't see traitor roles before ID.
	/// </summary>
	public RoleType VictimRole { get; set; }

	[Sync] public int Credits { get; set; }
	[Sync] public Guid? KillerId { get; set; }
	[Sync] public string KillerWeapon { get; set; }
	[Sync] public TimeSince TimeSinceDeath { get; set; }

	/// <summary>True once any non-spectator has interacted with this body.</summary>
	[Sync( SyncFlags.FromHost )] public bool Identified { get; set; }

	/// <summary>
	/// DNA samples present on this body — typically the killer plus anyone who
	/// physically touched the victim within a short window before death.
	/// Cleaned up after a duration so detectives can't scan ancient corpses.
	/// </summary>
	public List<DnaSample> DnaSamples { get; } = new();

	public record struct DnaSample( Guid PlayerId, RealTimeUntil ExpiresAt );

	/// <summary>
	/// Called when a player interacts with the corpse. Host-side: reveal the role,
	/// credit transfer, broadcast the identification event.
	/// </summary>
	public void Identify( TTTPlayer identifier )
	{
		if ( !Networking.IsHost ) return;
		if ( Identified ) return;

		Identified = true;

		// Credit transfer — innocents get credits dropped on detective bodies; traitors
		// get credits dropped on traitor bodies. Detail TBD.
		if ( Credits > 0 && identifier.IsValid() )
		{
			identifier.Role.Credits += Credits;
			Credits = 0;
		}

		// TODO: broadcast info-feed entry naming the victim and revealing the role.
	}
}
