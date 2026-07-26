# Ryx Core Save

Serializer-agnostic persistence for Ryx games. No `MonoBehaviour`, no DI container, no
singletons — construct it in the application composition root and inject the interfaces.

## Layers

| Type | Role |
| --- | --- |
| `ISaveStore` | Raw medium port. Reads and writes strings by `SaveKey`. Knows nothing about versions or types. |
| `ISaveSerializer` | Payload format port. `JsonUtilitySaveSerializer` is the dependency-free default. |
| `ISaveMigration` / `SaveMigrationPipeline` | Pure domain. Moves a serialized payload one version forward at a time. |
| `ISaveService` / `SaveService` | Application facade. Owns the `SaveEnvelope`, resolves the schema, migrates, serializes. |

Every stored document is a `SaveEnvelope` — `{ "version": N, "payload": "<serialized T>" }` —
so the schema version is always readable without knowing the payload type.

## Shipped implementations

- `FileSaveStore` — `Application.persistentDataPath/Saves/<key>.sav`, atomic writes
  (temp file + replace), thread-pool I/O with a synchronous WebGL fallback.
- `InMemorySaveStore` — volatile; use in edit-mode tests and `Seed(...)` raw documents.
- `JsonUtilitySaveSerializer` — zero extra dependencies, but inherits `JsonUtility`'s
  limits: no dictionaries, no polymorphism, no top-level collections, and payload types
  must be `[Serializable]`. Swap in a Newtonsoft-backed `ISaveSerializer` when that bites.

## Composition

```csharp
var schemas = new SaveSchemaRegistry()
    .Register(new SaveKey("profile"), new SaveMigrationPipeline(2, new RenamePlayerNameMigration()));

ISaveService saves = new SaveService(
    store: new FileSaveStore(),
    serializer: new JsonUtilitySaveSerializer(prettyPrint: Debug.isDebugBuild),
    schemas: schemas,
    diagnostics: myLogAdapter);
```

## Usage

```csharp
var profile = await saves.LoadAsync(ProfileKey, fallback: Profile.NewGame());
profile.litresPumped += 10;
await saves.SaveAsync(ProfileKey, profile);
```

`LoadAsync` returns the fallback when the document is absent. When the document exists but
is corrupt or has no migration path, the default `SaveServiceOptions.RecoverFromCorruption`
also returns the fallback and reports through `ISaveDiagnostics`; set it to `false` to have
`SaveCorruptedException` / `SaveMigrationException` surface instead.

## Migrations

A migration rewrites the *serialized* payload, never a typed object — old shapes never have
to be kept alive as C# classes.

```csharp
public sealed class RenamePlayerNameMigration : ISaveMigration
{
    public int FromVersion => 1;
    public int ToVersion => 2;
    public string Migrate(string payload) => payload.Replace("\"name\"", "\"playerName\"");
}
```

`SaveMigrationPipeline` validates its chain at construction time: every step must advance
exactly one version, no duplicates, no gaps. A malformed chain fails fast in the installer
rather than silently at load time on a player's device.

## Adapting other packages

`com.ryx.core.tutorials` declares `ITutorialProgressStore` with only a Null implementation.
Bridge it in your game's infrastructure layer rather than coupling the two packages:

```csharp
internal sealed class SaveBackedTutorialProgressStore : ITutorialProgressStore { /* ... */ }
```
