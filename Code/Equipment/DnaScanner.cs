using Sandbox;
using System;

namespace TTT;

/// <summary>
/// Detective-only tool. Aim at a corpse and press attack2 to extract a DNA
/// sample. The scanner then points toward where the sampled player was last
/// seen, with accuracy decreasing over distance and time. Sample expires
/// after <see cref="SampleLifetime"/>.
///
/// v1 simplifications: arrow direction is exact (no scrambling at distance);
/// no model attachment, no UI other than the directional arrow handled by
/// `DnaScannerHud.razor`.
/// </summary>
[Title( "DNA Scanner" ), Icon( "biotech" )]
public sealed class DnaScanner : Component
{
	[Property] public float SampleRange { get; set; } = 96f;
	[Property] public float SampleLifetime { get; set; } = 30f;

	[Sync] public Guid CurrentSampleTargetId { get; set; }
	[Sync] public RealTimeUntil SampleExpiresAt { get; set; }

	public bool HasActiveSample => CurrentSampleTargetId != Guid.Empty && SampleExpiresAt > 0;

	public TTTPlayer CurrentSampleTarget =>
		Scene.Directory.FindByGuid( CurrentSampleTargetId )?.GetComponent<TTTPlayer>();

	/// <summary>
	/// Server-side: trace forward from <paramref name="eyeRay"/>, find a
	/// corpse, take its first DNA sample if any. Returns true if a sample
	/// was acquired.
	/// </summary>
	public bool TakeSample( Ray eyeRay )
	{
		if ( !Networking.IsHost ) return false;

		var trace = Scene.Trace.Ray( eyeRay, SampleRange )
			.IgnoreGameObjectHierarchy( GameObject.Root )
			.Run();

		if ( !trace.Hit ) return false;

		var corpse = trace.GameObject?.GetComponentInParent<Corpse>();
		if ( !corpse.IsValid() || corpse.DnaSamples.Count == 0 ) return false;

		var sample = corpse.DnaSamples[0];
		corpse.DnaSamples.RemoveAt( 0 );

		CurrentSampleTargetId = sample.PlayerId;
		SampleExpiresAt = SampleLifetime;
		return true;
	}
}

[Title( "DNA Scanner (shop entry)" ), Icon( "biotech" )]
public sealed class DnaScannerEquipment : Equipment
{
	public DnaScannerEquipment()
	{
		Title = "DNA Scanner";
		IconName = "biotech";
		Cost = 1;
		LimitPerRound = 1;
		AllowedRoles = new[] { RoleType.Detective };
	}

	protected override void OnPurchased( TTTPlayer buyer )
	{
		if ( !Networking.IsHost || !buyer.IsValid() ) return;

		if ( buyer.GameObject.GetComponent<DnaScanner>() is null )
			buyer.GameObject.Components.Create<DnaScanner>();
	}
}
