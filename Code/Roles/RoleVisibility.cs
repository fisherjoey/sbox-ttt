using Sandbox;
using System;
using System.Collections.Generic;

namespace TTT;

/// <summary>
/// Decides who-knows-who-is-what at round start and pushes the result to each
/// client via targeted RPC. We do NOT [Sync] PlayerRole.Type — replicated state
/// would leak to every peer regardless of UI hiding.
/// </summary>
public static class RoleVisibility
{
	private static readonly Dictionary<Guid, RoleType> _knownRoles = new();

	/// <summary>
	/// Lookup what role THIS client believes a target player has. Innocents only
	/// see their own role and detectives; traitors additionally see other traitors.
	/// </summary>
	public static RoleType? GetKnownRole( Guid playerObjectId ) =>
		_knownRoles.TryGetValue( playerObjectId, out var role ) ? role : null;

	public static void RevealToClients( IList<TTTPlayer> all )
	{
		if ( !Networking.IsHost ) return;

		foreach ( var viewer in all )
		{
			if ( !viewer.IsValid() ) continue;
			var viewerConn = viewer.Network.Owner;
			if ( viewerConn is null ) continue;

			foreach ( var target in all )
			{
				if ( !target.IsValid() ) continue;
				if ( !ShouldReveal( viewer.Role.Type, target.Role.Type, viewer == target ) )
					continue;

				// Targeted RPC — only viewerConn's peer runs SendRoleReveal.
				// Engine pattern via Rpc.FilterInclude (Sandbox.Engine/Scene/Networking/Rpc.cs:217),
				// production reference: Code/UI/Notices/Notices.cs:52 in Facepunch/sandbox.
				using ( Rpc.FilterInclude( viewerConn ) )
				{
					SendRoleReveal( target.GameObject.Id, target.Role.Type );
				}
			}
		}
	}

	private static bool ShouldReveal( RoleType viewer, RoleType target, bool sameObject )
	{
		if ( sameObject ) return true;                                 // see your own role
		if ( target == RoleType.Detective ) return true;               // detectives are public
		if ( viewer == RoleType.Traitor && target == RoleType.Traitor )
			return true;                                                // traitors see traitors
		return false;
	}

	[Rpc.Broadcast]
	private static void SendRoleReveal( Guid targetObjectId, RoleType role )
	{
		_knownRoles[targetObjectId] = role;
	}

	/// <summary>
	/// Wipe the per-client cache. Called between rounds.
	/// </summary>
	public static void Clear() => _knownRoles.Clear();
}
