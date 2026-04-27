namespace TTT;

/// <summary>
/// How the last round ended. Drives the post-round summary copy:
/// "Innocents win by elimination" vs "by timeout".
/// </summary>
public enum WinReason
{
	/// <summary>One team had no living members; the other team was declared the winner.</summary>
	Elimination,

	/// <summary>Active phase timer expired; defaults to innocent win in vanilla TTT.</summary>
	Timeout,
}
