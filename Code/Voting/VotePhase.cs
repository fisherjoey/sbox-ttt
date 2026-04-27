namespace TTT;

public enum VotePhase
{
	Idle,         // Nothing happening — anyone can motion
	Motion,       // A motion is open for seconds; if quorum hit, transitions to Vote
	Vote,         // Active vote — yes/no being collected
	Cooldown,     // After failed motion or vote; no new motions until next round
}
