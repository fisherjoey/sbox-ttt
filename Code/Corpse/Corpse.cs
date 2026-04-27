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
	/// How long DNA samples remain scannable on a corpse. Scanner samples that
	/// expire return "lost." Vanilla doesn't have a single canonical number;
	/// 5 minutes is a reasonable starting point and tunable later.
	/// </summary>
	public const float DnaSampleLifetime = 300f;

	/// <summary>
	/// Spawn a host-owned corpse at the victim's world position carrying the
	/// metadata needed for identification and the DNA scanner. Call from the
	/// host's death handler. Returns the spawned Corpse component.
	///
	/// TODO[v2]: spawn a citizen ragdoll on this GameObject so it renders.
	/// Currently the corpse is an invisible marker — fine for logic testing,
	/// not for actual play.
	/// </summary>
	public static Corpse SpawnFor( TTTPlayer victim, in DamageInfo dmg )
	{
		if ( !Networking.IsHost || !victim.IsValid() )
			return null;

		var go = new GameObject( true, $"Corpse ({victim.Network.Owner?.DisplayName ?? "?"})" );
		go.WorldPosition = victim.WorldPosition;
		go.WorldRotation = victim.WorldRotation;

		var corpse = go.Components.Create<Corpse>();
		corpse.PlayerName = victim.Network.Owner?.DisplayName ?? "Unknown";
		corpse.VictimId = victim.GameObject.Id;
		corpse.VictimRole = victim.Role.Type;
		corpse.Credits = victim.Role.Credits;
		corpse.KillerWeapon = dmg.Weapon?.GameObject?.Name;
		corpse.TimeSinceDeath = 0f;

		var killerPlayer = dmg.Attacker?.GetComponent<TTTPlayer>();
		if ( killerPlayer.IsValid() && killerPlayer != victim )
		{
			corpse.KillerId = killerPlayer.GameObject.Id;
			corpse.DnaSamples.Add( new DnaSample( killerPlayer.GameObject.Id, DnaSampleLifetime ) );
		}

		go.NetworkSpawn( true, null );
		return corpse;
	}

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
