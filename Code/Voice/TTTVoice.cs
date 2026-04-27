using Sandbox;
using System.Collections.Generic;

namespace TTT;

/// <summary>
/// Voice routing for TTT. During the Active phase, alive players only hear
/// other alive players, and dead players only hear other dead players. Outside
/// Active (Prep, PostRound, Waiting) the partition drops and everyone hears
/// everyone — gives the round-end celebration / pre-round chat an open channel.
///
/// Engine reference: VoiceComponent.cs:281-295. Both ExcludeFilter (transmit
/// gate) and ShouldHearVoice (receive gate) fire, so we set both for defense
/// in depth.
///
/// Issue #6 also covers text chat — voice is the easier half because the
/// engine ships the hooks. Text chat needs a custom Razor panel and is a
/// follow-up.
/// </summary>
[Title( "TTT Voice" ), Icon( "record_voice_over" )]
public sealed class TTTVoice : Voice
{
	private static bool IsActivePhase => TTTGameMode.Current?.Phase == RoundPhase.Active;

	private static TTTPlayer ForConnection( Connection conn )
	{
		if ( conn is null ) return null;
		foreach ( var p in Game.ActiveScene.GetAllComponents<TTTPlayer>() )
		{
			if ( p.IsValid() && p.Network.Owner == conn ) return p;
		}
		return null;
	}

	private static bool SameLifeBucket( TTTPlayer a, TTTPlayer b )
	{
		// "Alive" vs "not alive" — Spectator and Dead share the dead-chat bucket.
		return a.IsAlive == b.IsAlive;
	}

	protected override bool ShouldHearVoice( Connection connection )
	{
		// Open channel outside the Active phase.
		if ( !IsActivePhase ) return true;

		var speaker = ForConnection( connection );
		var self = ForConnection( Network.Owner );

		// Unknown connection (admin overlay, joining player not yet spawned, etc.):
		// default to letting it through. The transmit-side gate still applies.
		if ( speaker is null || self is null ) return true;

		return SameLifeBucket( self, speaker );
	}

	protected override IEnumerable<Connection> ExcludeFilter()
	{
		if ( !IsActivePhase ) yield break;

		var self = ForConnection( Network.Owner );
		if ( self is null ) yield break;

		foreach ( var p in Game.ActiveScene.GetAllComponents<TTTPlayer>() )
		{
			if ( !p.IsValid() ) continue;
			if ( p.Network.Owner == Network.Owner ) continue;     // don't exclude self
			if ( !SameLifeBucket( self, p ) )
				yield return p.Network.Owner;
		}
	}
}
