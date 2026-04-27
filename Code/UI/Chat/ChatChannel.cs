namespace TTT;

/// <summary>
/// Which channel a chat message goes through. The host uses this plus the
/// speaker's life-bucket to pick the recipient set.
/// </summary>
public enum ChatChannel
{
	/// <summary>Default channel. Alive↔alive during Active, open otherwise.</summary>
	Global,

	/// <summary>Traitor team chat — alive traitors only, regardless of phase.</summary>
	Traitor,

	/// <summary>System / info-feed messages (round results, identification announcements).</summary>
	System,
}
