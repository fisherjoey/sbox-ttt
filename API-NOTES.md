# API Notes — s&box reference survey

Purpose: confirm/correct the API patterns in this repo against the engine source
(`Facepunch/sbox-public`) and current production game projects (`Facepunch/sandbox`,
`apetavern/grubs`, `Bolt-Collective/BoltFPS`). Engine source is sparse-checked into
`/tmp/sbox-engine`; refs live under `/home/joey/dev/sbox-ttt-research/refs/`.

Currency check (April 27, 2026 — same day as this audit):
- engine: `Facepunch/sbox-public` HEAD `f11bfb2` 2026-04-27 "Small menu fixes (#4672)"
- sandbox: `Facepunch/sandbox` HEAD `6efdb5a` 2026-04-27
- grubs: `apetavern/grubs` HEAD `6b9c9b1` 2026-03-29
- boltfps: `Bolt-Collective/BoltFPS` HEAD `06b6d42` 2026-04-26

All four are within four weeks of today, so all are post-rebuild and authoritative.
Where multiple match, I cite engine first (highest authority), then production game
projects.

---

## 1. Targeted RPC to a specific connection — **CORRECTED**

Our current pattern (`Code/Roles/RoleVisibility.cs:59-68`) declares
`[Rpc.Broadcast(NetFlags.HostOnly | NetFlags.Reliable)]` and self-filters via
`Connection.Local.Id == recipientId`. This works but is wasteful: every peer
receives the message and the engine pays for serialization on each.

The canonical pattern is `Rpc.FilterInclude(connection)` wrapped around the call
site. The engine defines this in
`/tmp/sbox-engine/engine/Sandbox.Engine/Scene/Networking/Rpc.cs:217-232`:

```csharp
public static IDisposable FilterInclude( Connection connection )
{
    if ( Filter.HasValue )
        throw new InvalidOperationException( "An RPC filter is already active" );

    Filter = new( Connection.Filter.FilterType.Include, c => c == connection );
    return DisposeAction.Create( () => { Filter = null; } );
}
```

Production sandbox uses exactly this for its targeted-notice system in
`/home/joey/dev/sandbox/Code/UI/Notices/Notices.cs:52-60`:

```csharp
public static void SendNotice( Connection target, string icon, Color iconColor, string text, float seconds = 5 )
{
    Assert.True( Networking.IsHost, "Must not be the host" );

    using ( Rpc.FilterInclude( target ) )
    {
        RpcAddNotice( icon, iconColor, text, seconds );
    }
}

[Rpc.Broadcast]
private static void RpcAddNotice( string icon, Color iconColor, string text, float seconds ) { ... }
```

Other in-tree examples confirming the pattern:
- `/home/joey/dev/sandbox/Code/Player/PlayerData.cs:109` — `Rpc.FilterInclude( Connection )`
- `/home/joey/dev/sandbox/Code/GameLoop/GameManager.Achievements.cs:19`
- `/home/joey/dev/sbox-ttt-research/refs/grubs/Code/Drops/Crate.cs:52` —
  `Rpc.FilterInclude( grub.Network.Owner )`

**Action for `RoleVisibility.SendRoleReveal`:** drop the `NetFlags.HostOnly` self-
filter approach. Replace with:

```csharp
using ( Rpc.FilterInclude( viewerConn ) )
{
    SendRoleReveal( target.GameObject.Id, target.Role.Type );
}

[Rpc.Broadcast]
private static void SendRoleReveal( Guid targetObjectId, RoleType role )
{
    _knownRoles[targetObjectId] = role;
}
```

The recipient parameter goes away — only the targeted connection ever runs the
method body, so Caller-side filtering is unnecessary. `[Rpc.Owner]` is a different
mechanism (sends to the owner of the GameObject the RPC is called on); not what
we want here because the host iterates viewers but the RPC is on a static class.

**Caveat:** `Rpc.FilterInclude` throws if a filter is already active in the same
scope (engine line 219). If we ever nest reveals (e.g. send roles to a viewer
during another targeted call), the inner call has to re-check.

---

## 2. Player-owned GameObject spawning — **CORRECTED**

Our code has two calls:
1. `Code/GameMode/TTTGameMode.cs:78` — `go.NetworkSpawn( channel )`
2. `Code/Player/TTTPlayer.cs:27-30` (`OnConnectionActive`) — `Network.AssignOwnership( channel )`

This is redundant. `NetworkSpawn(Connection owner)` already assigns ownership at
spawn time. The engine implements it as:

`/tmp/sbox-engine/engine/Sandbox.Engine/Scene/GameObject/GameObject.Network.cs:187-202`:

```csharp
public bool NetworkSpawn( bool enabled, Connection owner )
{
    var options = new NetworkSpawnOptions { StartEnabled = enabled, Owner = owner };
    return NetworkSpawn( options );
}

public bool NetworkSpawn( Connection owner ) => NetworkSpawn( true, owner );
```

Inside `NetworkSpawn(NetworkSpawnOptions)`, line 177:

```csharp
_net.InitializeForConnection( options.Owner, options.StartEnabled );
```

— ownership is set during initialization, before any peer hears about the object.

Production sandbox confirms this is the canonical shape — it spawns players with
exactly one call:

`/home/joey/dev/sandbox/Code/GameLoop/GameManager.cs:79-85`:

```csharp
var playerGo = GameObject.Clone( "/prefabs/engine/player.prefab", new CloneConfig { ... } );
var player = playerGo.Components.Get<Player>( true );
player.PlayerData = playerData;

var owner = Connection.Find( playerData.PlayerId );
playerGo.NetworkSpawn( owner );
```

No `AssignOwnership` follow-up. `Network.AssignOwnership(channel)` is for *changing*
ownership later (e.g. picking up an item — see
`/home/joey/dev/sandbox/Code/Player/PlayerInventory.cs:255`). At initial spawn it's
a no-op at best, double-message at worst.

**Action:**
- Keep the `go.NetworkSpawn( channel )` call in `TTTGameMode.OnActive`.
- Delete or repurpose the `AssignOwnership` line in `TTTPlayer.OnConnectionActive`.
  If you need a place to wire up per-connection state (cookies, perf counters, etc.)
  that's fine — but rename and don't gate on ownership timing.

**Caveat:** if you ever spawn the player object in two phases (e.g. host-owned
prefab, then transfer to client), order matters: spawn first, then `AssignOwnership`.
That's the SaveSystem pattern at sandbox `Code/Save/SaveSystem.cs:683,687`. Not
what we're doing.

---

## 3. Chat / voice routing — **CONFIRMED with caveats**

### Voice
The engine `Voice` component (`Component`-based, exposed in editor as "Voice
Transmitter") has two virtual methods designed exactly for the alive↔alive /
dead↔dead routing we need:

`/tmp/sbox-engine/engine/Sandbox.Engine/Scene/Components/Audio/VoiceComponent.cs:281-295`:

```csharp
/// <summary>
/// Exclude these connection from hearing our voice.
/// </summary>
protected virtual IEnumerable<Connection> ExcludeFilter()
{
    return Enumerable.Empty<Connection>();
}

/// <summary>
/// Whether we want to hear voice from a particular connection.
/// </summary>
protected virtual bool ShouldHearVoice( Connection connection )
{
    return true;
}
```

The transmit RPC at line 297-313 uses `Rpc.FilterExclude(ExcludeFilter())` around
`Msg_Voice(...)`, and the receiver at line 324 calls `ShouldHearVoice(Rpc.Caller)`
before playing. **Both gates fire**, so we can layer "I won't transmit to dead
players" with "I won't play voice from alive players" defensively.

Production sandbox confirms the override pattern with mute lists:

`/home/joey/dev/sandbox/Code/Player/SandboxVoice.cs:32-35`:

```csharp
protected override bool ShouldHearVoice( Connection connection )
{
    return !MutedList.Contains( connection.SteamId );
}
```

For TTT issue #7 (alive vs dead voice): subclass `Voice`, override
`ShouldHearVoice` and/or `ExcludeFilter` against `TTTPlayer.Status` lookup. Hook
during `Active` phase only; pass-through during prep / postround.

### Chat
There is no engine-level chat system; chat is built per-game. Sandbox's
`Code/UI/Chat.razor` is a `PanelComponent` with its own RPC plumbing:

`/home/joey/dev/sandbox/Code/UI/Chat.razor:84-107`:

```csharp
[Rpc.Broadcast]
public void AddText( string message )
{
    message = message.Truncate( 300 );
    if ( string.IsNullOrWhiteSpace(message) ) return;

    var author = Rpc.Caller.DisplayName;
    var steamid = Rpc.Caller.SteamId;
    // ...
    Entries.Add(new Entry(steamid, filteredName, message, 0.0f));
    StateHasChanged();
}

[Rpc.Broadcast( NetFlags.HostOnly )]
public void AddSystemText( string message, string icon = "ℹ️" ) { ... }
```

The `Say` ConCmd (line 68-82) routes through `Game.ActiveScene.GetAll<Chat>().FirstOrDefault()`
and calls `AddText` (broadcast) or `AddSystemText` (host-only).

For TTT alive↔alive vs dead↔dead chat: sandbox does NOT gate chat — it broadcasts to
everyone. We have two clean options:
1. **Wrap with `Rpc.FilterInclude`** at the `Say` boundary, sending only to peers
   matching the speaker's alive/dead state. Mirrors the Voice exclude pattern.
2. **Multiple RPC methods**: `AddTextLiving`, `AddTextDead`, each gated host-side
   before forwarding. More boilerplate but easier to audit.

I'd go (1) — fewer methods, the filter set is computed at the host where authority
lives.

**Caveat:** I did not find an `IChatEvent` in the engine. The sandbox `Chat` class
is the integration point; if we want a hookable event bus we have to write it.

---

## 4. `Component.IDamageable` — **CONFIRMED**

Engine declaration is exactly what we have:

`/tmp/sbox-engine/engine/Sandbox.Engine/Scene/Components/Markers/IDamageable.cs:1-12`:

```csharp
public abstract partial class Component
{
    public interface IDamageable
    {
        void OnDamage( in DamageInfo damage );
    }
}
```

The engine never auto-invokes this from collisions or traces — **callers explicitly
call `OnDamage`**. The pattern is "find the IDamageable, hand it a populated
DamageInfo." Three concrete invocation sites in the engine:

1. **Touch/area damage** (`FireDamage`, `RadiusDamage`, `TriggerHurt`) walks the
   tree and runs the event:
   `/tmp/sbox-engine/engine/Sandbox.Engine/Scene/Components/Game/FireDamage.cs:44`:
   ```csharp
   GameObject.Root.RunEvent<IDamageable>( x => x.OnDamage( damageInfo ) );
   ```

2. **Rigidbody collision damage** queries `GetComponentInParent<IDamageable>()`
   and calls directly:
   `/tmp/sbox-engine/engine/Sandbox.Engine/Scene/Components/Collider/Rigidbody.cs:454-466`:
   ```csharp
   var otherDamageable = collision.Other.GameObject?.GetComponentInParent<IDamageable>();
   if ( otherDamageable is null ) return;
   var damageInfo = new DamageInfo( damage, GameObject, GameObject ) { Position = ..., Shape = ... };
   damageInfo.Tags.Add( "impact" );
   otherDamageable.OnDamage( damageInfo );
   ```

3. **Trace results** are not auto-routed. If you trace and want to apply damage,
   you take the result, look up `IDamageable` on the hit object, and call
   `OnDamage` yourself — same shape as #2.

So **the engine never calls `OnDamage` for us**; it just provides convenience
helpers. Our weapon/projectile code will need to do the lookup + call. Our
implementation in `Code/Player/TTTPlayer.Damage.cs:8` is a correct receiver.

**Caveat:** signature is `in DamageInfo` (readonly ref). DamageInfo itself is a
**class**, not a struct (see #5), so `in` is just a "no reassign" hint, not a
copy-elide. Don't write `dmg = new DamageInfo(...)` inside the handler.

---

## 5. `DamageInfo` struct fields — **CORRECTED (it's a class)**

The first correction: `DamageInfo` is a **class**, not a struct. From
`/tmp/sbox-engine/engine/Sandbox.Engine/Scene/DamageInfo.cs:9`:

```csharp
[Expose]
public class DamageInfo
{
```

The class comment explains why: "purposefully a class so it can be derived from,
allowing games to create their own special types of damage."

Field list (engine lines 10-49, all `public T { get; set; }`):

| Member | Type | Notes |
|---|---|---|
| `Attacker` | `GameObject` | "Usually a player or Npc" |
| `Weapon` | `GameObject` | "or a vehicle etc" |
| `Hitbox` | `Hitbox` | "if any" |
| `Damage` | `float` | |
| `Origin` | `Vector3` | "shooter's eye position" / explosion center |
| `Position` | `Vector3` | hit location on target |
| `Shape` | `PhysicsShape` | "if any" |
| `Tags` | `TagSet` | initialized to `new()` — never null |

Plus an obsolete `IsExplosion` bool that aliases `Tags.Has("explosion")` — don't
use it; use tags directly.

Constructors:
```csharp
public DamageInfo() {}
[ActionGraphInclude]
public DamageInfo( float damage, GameObject attacker, GameObject weapon );
public DamageInfo( float damage, GameObject attacker, GameObject weapon, Hitbox hitbox );
```

Our code in `TTTPlayer.Damage.cs:22-25` uses `dmg.Damage`, `dmg.Tags`,
`dmg.Attacker` — all correct. We don't currently use `Position`/`Origin` in the
damage handler but they're on the struct as expected.

**Caveat:** because it's a class, every `OnDamage` receiver gets the **same**
instance. If anyone mutates `dmg.Damage` mid-handler chain (we don't, but a
`RadiusDamage` source could), other receivers see the mutation. Our handler
copies into a local `damage` first (line 19), which is the right defensive shape.

**Caveat:** `Tags.Has("headshot")` vs our `dmg.Tags.Contains( DamageTags.Headshot )`
— `TagSet.Contains` exists alongside `Has`; both work. Sandbox uses `Has`
(`Code/Player/Player.cs` line ~210). Either is fine; matching the production repo
would be a tiny consistency win.

---

## 6. `Connection.Kick(string reason)` — **CONFIRMED**

`/tmp/sbox-engine/engine/Sandbox.Engine/Systems/Networking/System/Channel/Connection.cs:219-240`:

```csharp
/// <summary>
/// Kick this <see cref="Connection"/> from the server. Only the host can kick clients.
/// </summary>
/// <param name="reason">The reason to display to this client.</param>
public virtual void Kick( string reason )
{
    Assert.NotNull( System );
    if ( !System.IsHost ) return;
    if ( string.IsNullOrWhiteSpace( reason ) ) reason = "Kicked";
    SendMessage( new KickMsg { Reason = reason } );
}
```

Signature exact match. `System.IsHost` gate means a client calling `Kick` is a
silent no-op — you don't need to wrap in `if (Networking.IsHost)` defensively, but
doing so makes intent obvious. Empty/whitespace reason is replaced with `"Kicked"`.

Our usage in `Code/GameMode/PhaseTicks.cs:136`:

```csharp
p.Network.Owner?.Kick( $"Karma below {config.KickThreshold}" );
```

Correct. The `?.` handles the case of a player whose owner is null
(host-owned, not assigned, etc.) — though after the rebuild in #2 every player
should have a valid owner.

---

## 7. `[GameResource]` attribute — **CONFIRMED with deprecation warning**

`[GameResource("Name", "ext", "desc", Icon = "...")]` is the long-standing form;
the engine still ships it but **marks it `[Obsolete("Use AssetType instead")]`**.

`/tmp/sbox-engine/engine/Sandbox.Engine/Resources/GameResourceAttribute.cs:77-130`:

```csharp
[Obsolete( "Use AssetType instead" )]
public class GameResourceAttribute : AssetTypeAttribute
{
    public string Icon { get; set; } = "question_mark";
    public string IconBgColor { get; set; } = "#67ac5c";
    public string IconFgColor { get; set; } = "#1a2c17";
    [Obsolete] public string Description { get; set; }

    public GameResourceAttribute( string title, string extension, string description )
    { ... Name = title; Description = description; Extension = extension; }
}
```

The current API is `[AssetType(...)]`, used by sandbox in
`/home/joey/dev/sandbox/Code/Game/Weapon/AmmoResource.cs:5`:

```csharp
[AssetType( Name = "Ammo Type", Extension = "ammo", Category = "Sandbox" )]
public class AmmoResource : GameResource { ... }
```

(The base class `GameResource` itself isn't deprecated — only the attribute is.)

`AssetTypeAttribute` properties: `Name`, `Extension`, `Category`, `Flags`. No
positional ctor — use object initializer. Description moved to the XML doc
comment above the class.

Grubs is using the obsolete form
(`/home/joey/dev/sbox-ttt-research/refs/grubs/Code/Equipment/EquipmentResource.cs:3`):

```csharp
[GameResource( "Equipment", "geq", "Describes a piece of equipment", Icon = "hardware" )]
public class EquipmentResource : GameResource
```

**Action:** if/when we add equipment resources for v2, prefer
`[AssetType(Name=..., Extension=..., Category=...)]`. Won't break to use the old
form, but we'll get an obsolete warning.

**Caveat:** the engine `IconBgColor` / `IconFgColor` properties are
`[GameResource]`-only; `[AssetType]` doesn't have them. If we want themed thumbs
we have to stay on the obsolete attribute (or use a custom asset type registration).

---

## 8. `Game.ActiveScene.GetAllComponents<T>()` — **CONFIRMED, allocation-aware**

Engine declaration:
`/tmp/sbox-engine/engine/Sandbox.Engine/Scene/Scene/Scene.Components.cs:30-34`:

```csharp
[Pure]
public IEnumerable<T> GetAllComponents<T>()
{
    return GetAll<T>();
}
```

It delegates to `Scene.GetAll<T>()` at
`/tmp/sbox-engine/engine/Sandbox.Engine/Scene/Scene/Scene.ObjectIndex.cs:148-160`:

```csharp
[Pure]
public IEnumerable<T> GetAll<T>()
{
    if ( !objectIndex.TryGetValue( typeof( T ), out var set ) || set.Count == 0 )
        yield break;

    foreach ( var e in set.EnumerateLocked() )
    {
        T c = (T)e;
        if ( c is null ) continue;
        if ( c is IValid v && !v.IsValid ) continue;
        yield return c;
    }
}
```

Performance shape:
- **No list allocation per call**: it's an iterator (`yield return`) over a
  pre-built `objectIndex` keyed by `typeof(T)`.
- **No linear scan** of the whole scene: `objectIndex` is a `Dictionary` and
  `GetAll<T>` is an O(matching-objects) walk.
- **Per-call cost**: enumerator object allocation + the `IValid` check on each
  element. Fine for once-per-frame; not great for inner loops.

For hot paths there's a list-fill overload that avoids the iterator entirely:
`Scene.ObjectIndex.cs:165-178`:

```csharp
[Pure]
public void GetAll<T>( List<T> target )
```

— pass a reusable buffer. Use this if we ever poll `GetAllComponents<TTTPlayer>()`
inside `OnUpdate`.

Our current calls:
- `TTTGameMode.cs:64` — `Game.ActiveScene.GetAllComponents<TTTPlayer>()` once per
  tick from `AllPlayers()`. Acceptable.
- `RolePlate.razor:26` — local-player lookup once per Razor `BuildHash`. The
  iterator is slightly wasteful here; a single `[Sync]`'d static
  `LocalTTTPlayer` field on `TTTPlayer` itself (set during `OnConnectionActive`
  on the local connection) would be cheaper. Optional micro-opt.

**Caveat:** `GetAll<T>` only returns **enabled** components — disabled ones don't
register in `objectIndex`. That's what the doc comment on `GetAllComponents`
calls out: "This function can only find enabled/active components." Important
when checking dead players whose components might be disabled.

---

## 9. MSTest in game projects — **INCONCLUSIVE / ENGINE-PRIVATE**

Engine tests:
`/tmp/sbox-engine/engine/Sandbox.Test.Unit/Sandbox.Test.Unit.csproj:32-44` declares
the package references:

```xml
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
<PackageReference Include="MSTest.TestAdapter" Version="3.6.4" />
<PackageReference Include="MSTest.TestFramework" Version="3.6.4" />
```

This is a standard `dotnet test`-runnable project with `<IsTestProject>true</IsTestProject>`.
The test runner that picks up these tests is **the standard MSTest adapter
running outside the editor** — i.e. `dotnet test` from the engine source tree.

For game projects (`.sbproj`), I could find **no evidence** that the s&box editor
ships an in-editor MSTest runner, and **none of the production game projects we
have local** (`sandbox`, `grubs`, `boltfps`) ship tests of any kind:

- `find sandbox grubs boltfps -name "*.cs" | xargs grep -l "TestClass\|TestMethod"`
  returns empty.

The `.csproj` for game projects is generated by the editor at build time (per our
own `CLAUDE.md`), and we don't have visibility into whether that generation
includes MSTest packages. The generation logic lives in the closed-source
`Sandbox.Editor` DLL referenced from
`/tmp/sbox-engine/engine/Sandbox.Editor` (sparse-checked in but mostly bindings).

**Verdict:** our `Code/Tests/KarmaSystemTest.cs` using `[TestClass]` / `[TestMethod]`
mirrors engine convention syntactically, but **whether the editor build includes
the MSTest adapter is unverified**. On day one (April 28+) we have to
either:
1. Run the editor and check the test tab — if it picks up `[TestClass]`, we're
   good.
2. Move tests to a separate `.csproj` outside the `.sbproj` and run them with
   `dotnet test` against a manually-referenced `Sandbox.System.dll` /
   `Sandbox.Engine.dll`. Possible but ugly.

The fact that no production game project ships unit tests is a strong signal that
**the editor's test path may not exist for game projects**, and developers test
gameplay manually in-editor. `KarmaSystem` is pure C# math with no engine deps
though, so the test class itself is portable.

**Action items:** mark the tests directory with a `// TODO[verify]` and keep
`KarmaSystem` engine-free so we can move it to a satellite project if needed.

---

## 10. Razor conventions — **CONFIRMED with one fix**

Engine and sandbox conventions (our `Code/UI/RoundTimer.razor`,
`Code/UI/RolePlate.razor` references):

| Item | Status | Notes |
|---|---|---|
| `<root>` wrapper | confirmed | every sandbox panel uses it, e.g. `Code/UI/Vitals/Vitals.razor:9` |
| `BuildHash()` for invalidation | confirmed | `Code/UI/Vitals/Vitals.razor:60-65` reads same fields it displays |
| `@inherits Panel` | partially confirmed | works for nested panels (`SpawnMenuContent.razor`, `StringQueryPopup.razor`); for **scene-level/root HUD components** sandbox uses `@inherits PanelComponent` (`Vitals.razor:3`, `Chat.razor:5`, `Feed.razor`, `Voices.razor`) |
| `@namespace` | confirmed | `Code/UI/Vitals/Vitals.razor:4` etc |
| `.razor.scss` adjacent | confirmed | every sandbox panel ships a sibling `.scss` |

**Action:** consider switching `RoundTimer.razor` and `RolePlate.razor` from
`@inherits Panel` to `@inherits PanelComponent` if they're meant to live as scene
components placed via the editor. `Panel` is fine for runtime-instantiated child
panels; `PanelComponent` is the s&box-component flavor that auto-mounts to
`Game.ActiveScene` like any other component. Most TTT HUD elements likely want
`PanelComponent`.

Standard sandbox razor header (from `Vitals.razor:1-4`):

```razor
@using Sandbox;
@using Sandbox.UI;
@inherits PanelComponent
@namespace Sandbox
```

`BuildHash` rule (also in our root `CLAUDE.md`): hash **every datum the display
reads**. Our `RoundTimer.BuildHash` includes `gm.Phase` and a coarsened
`PhaseEndsAt` — that's fine for second-precision display. Our `RolePlate.BuildHash`
reads `Type, Credits, Phase` — also fine.

**Caveat:** `BuildHash` runs every frame; if it does an
`O(N)` scan (e.g. `LocalPlayer` in `RolePlate.razor:21-32` calls
`GetAllComponents<TTTPlayer>()`), that's per-frame allocation. See item #8.

---

## 11. Input system — **CONFIRMED**

`Input.Pressed("name")` is the canonical pattern, and the "name" refers to a
project-defined InputAction stored in `ProjectSettings/Input.config`.

Engine signature:
`/tmp/sbox-engine/engine/Sandbox.Engine/Systems/Input/Input.Actions.cs:97-103`:

```csharp
[ActionGraphNode( "input.pressed" ), Pure, Category( "Input" ), Icon( "gamepad" )]
public static bool Pressed( [InputAction] string action )
{
    if ( Application.IsHeadless ) return false;
    if ( Suppressed ) return false;
    return !WasDownLastCommand( action ) && Down( action );
}

[ActionGraphNode( "input.down" ), Pure, Category( "Input" ), Icon( "gamepad" )]
public static bool Down( [InputAction] string action, bool complainOnMissing = true )
```

`Released`, `Down` follow the same pattern. The `[InputAction]` parameter
attribute hooks the editor autocomplete to the action list.

**Action registration** lives in
`/home/joey/dev/sandbox/ProjectSettings/Input.config` (JSON, one entry per
action):

```json
{
    "Name": "Jump",
    "GroupName": "Movement",
    "Title": null,
    "KeyboardCode": "space",
    "GamepadCode": "A"
}
```

For TTT we'll need to add equivalents in our own `ProjectSettings/Input.config`
(to be created on first editor run):
- `"shop"` → `"C"` (issue #9)
- `"interact_body"` → `"F"` (issue #4)
- `"traitor_chat"` → `"shift"` (modifier check in code)

Production usage examples:
- `/home/joey/dev/sandbox/Code/Player/Player.cs:280` — `if ( Input.Pressed( "jump" ) )`
- `/home/joey/dev/sandbox/Code/Player/PlayerFlashlight.cs:23` — `if ( !IsProxy && Input.Pressed( "Flashlight" ) )`
- `/home/joey/dev/sandbox/Code/Player/PlayerInventory.cs:493` — `if ( Input.Pressed( "drop" ) )`

There is also a separate `Input.Keyboard.Pressed("a")` namespace
(`Input.Keyboard.cs:26`) for raw keys ignoring the action map. Useful for one-off
debug shortcuts; **don't ship gameplay against it** — players can't rebind raw
keys.

For "hold-Shift for traitor chat," we want either:
- A dedicated `traitor_chat` InputAction bound to Shift by default, queried with
  `Input.Down("traitor_chat")`, OR
- Read `Input.Keyboard.Down("shift")` as a modifier alongside `Input.Pressed("chat")`.
  Less rebindable but matches how chat modifiers usually work.

**Caveat:** `Input.Suppressed` (line 101) is true while the editor or a UI panel
has focus — calling `Input.Pressed` during a `TextEntry` will return false. Good
for us (won't fire "interact_body" while typing in chat) but worth knowing.

---

## Bonus

### Built-in inventory / weapon base — **partially confirmed, sandbox-private**

There is no engine-level `BaseWeapon` or `Inventory`. The `BaseCarryable` /
`BaseWeapon` hierarchy lives entirely in the **sandbox game project**, not the
engine:

- `/home/joey/dev/sandbox/Code/Game/Weapon/BaseCarryable/BaseCarryable.cs:29` —
  `public partial class BaseCarryable : Component, IKillIcon`
- `/home/joey/dev/sandbox/Code/Game/Weapon/BaseWeapon/BaseWeapon.cs:3` —
  `public partial class BaseWeapon : BaseCarryable, IPlayerControllable`
- `/home/joey/dev/sandbox/Code/Player/PlayerInventory.cs` — `Component`-based
  inventory wired to `BaseCarryable.ActiveWeapon`

So if we want their weapon framework we'd either:
1. **Vendor it** (copy `Code/Game/Weapon/` into TTT) — sandbox is MIT, this is
   the path the grubs / boltfps repos took.
2. **Subclass** by adding sandbox as a `Mount` in `ttt.sbproj`. The `Mounts: []`
   array currently empty would take a sandbox package reference. Riskier — we
   inherit their HUD assumptions.
3. **Roll our own.** Equipment is small enough (a dozen items in v2) that this
   may be cleanest.

Grubs ships its own `Equipment` component layer and an `EquipmentResource`
GameResource (see `/home/joey/dev/sbox-ttt-research/refs/grubs/Code/Equipment/`).
Likely the closest reference for what TTT needs.

### Ragdoll spawn pattern — **CONFIRMED**

The engine's `PlayerController` has a built-in `CreateRagdoll()` helper:

`/tmp/sbox-engine/engine/Sandbox.Engine/Scene/Components/Game/PlayerController/PlayerController.Utility.cs:1-43`:

```csharp
public sealed partial class PlayerController : Component
{
    public GameObject CreateRagdoll( string name = "Ragdoll" )
    {
        var go = new GameObject( true, name );
        go.Tags.Add( "ragdoll" );
        go.WorldTransform = WorldTransform;

        var originalBody = Renderer.Components.Get<SkinnedModelRenderer>();
        if ( !originalBody.IsValid() ) return go;

        var mainBody = go.Components.Create<SkinnedModelRenderer>();
        mainBody.CopyFrom( originalBody );
        mainBody.UseAnimGraph = false;

        // ...copies clothing, BoneMergeTarget chain...

        var physics = go.Components.Create<ModelPhysics>();
        physics.Model = mainBody.Model;
        physics.Renderer = mainBody;
        physics.CopyBonesFrom( originalBody, true );

        return go;
    }
}
```

For `Code/Corpse/Corpse.cs` (currently invisible marker): on death, call
`playerController.CreateRagdoll()`, parent the corpse marker to it (or store the
GameObject ref), `NetworkSpawn(null)` so all clients see it. The "preserve role
identity" data lives on our `Corpse` component, which sits as a sibling on the
ragdoll GameObject. Detective `Identify()` then queries the sibling.

**Caveat:** `CreateRagdoll` is on `PlayerController`. If our `TTTPlayer` doesn't
use `PlayerController` (we'd need to confirm in `TTTPlayer.Spawn.cs` once we have
a player prefab), we'll have to replicate the helper or attach `PlayerController`
to player prefabs. Sandbox uses it; grubs has its own grub controller.

### In-game lobby / host config UI — **NOT FOUND in sandbox**

I grep'd `/home/joey/dev/sandbox/Code/` for `GameSettings`, `HostConfig`,
`LobbyUI`, `LobbyConfig` in UI files. The only `LobbyConfig` reference is the
single host-init line:

`/home/joey/dev/sandbox/Code/GameLoop/GameManager.cs:13`:

```csharp
Networking.CreateLobby( new Sandbox.Network.LobbyConfig() {
    Privacy = Sandbox.Network.LobbyPrivacy.Public,
    MaxPlayers = 32,
    Name = "Sandbox",
    DestroyWhenHostLeaves = true } );
```

— no in-game UI to edit it. **Sandbox doesn't ship a host config screen**; it
hardcodes the lobby and exposes nothing in-game. For TTT v3 we'll be drawing this
UI ourselves; the engine `Lobby` system at
`/tmp/sbox-engine/engine/Sandbox.Engine/Game/Lobby/LobbyManager.cs` is the
underlying API but it's `internal static class LobbyManager` — we use the public
`Networking.CreateLobby(LobbyConfig)` and surface `LobbyConfig` fields in our own
Razor UI.

So there is no convention to match — we set the convention.

---

## Inconclusive / engine-private

- **Engine-private DLLs:** the editor source (`Sandbox.Editor`, in `/tmp/sbox-engine/engine/Sandbox.Editor/` after sparse-add) ships mostly as compiled bindings. Anything related to:
  - in-editor MSTest discovery for `.sbproj`
  - `.csproj` generation logic for game projects
  - The editor's "Tests" tab (if it exists)
  ...is not in the sparse checkout's source files. We can verify on first editor
  run April 28+. Item #9 above tracks this.

- **Targeted RPC over multiple connections at once:** I confirmed
  `Rpc.FilterInclude(IEnumerable<Connection>)` and `(Predicate<Connection>)`
  exist (`Rpc.cs:187,204`). For role visibility we use single-connection form per
  reveal; if perf requires batching all reveals to one viewer we can collect
  targets and broadcast once. Untested — flag as `// TODO[verify]` in code.

- **`[Sync]` on struct contents:** the PhysGun comment in our sandbox notes
  flagged `// State needs to reset for sync to detect a change, bug or how it's
  meant to work?` (`Code/Weapons/PhysGun/Physgun.cs:197`). I didn't dig into
  whether `[Sync]`'d struct fields trigger replication on inner mutation or only
  on whole-struct reassignment. Doesn't affect TTT today (we sync simple value
  types and enums) but worth knowing if we add a `[Sync]`'d struct later.

- **`Voice` permissions / cross-team transmission timing:** I confirmed
  `ShouldHearVoice` / `ExcludeFilter` exist on `Voice` and how they're wired into
  the OPUS RPC path. I did **not** verify whether they're called every frame or
  cached across the connection lifetime — if cached, swapping `Status` from Alive
  to Dead mid-round may not retroactively change voice routing. Will need to test
  the alive↔dead transition at the editor.

- **`[Rpc.Owner]` vs `Rpc.FilterInclude(owner)`:** both target a single specific
  client, but `[Rpc.Owner]` (engine `Rpc.Attributes.cs:55-60`) only works on
  *instance* RPCs where the GameObject has an owner. Our `RoleVisibility` is a
  static class with no owner; `FilterInclude` is the only option there. For
  targeted RPCs from a player component to its own owner,
  `[Rpc.Owner]` is the cleaner spelling.
