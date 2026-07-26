namespace Ryx.Core.Save
{
    /// <summary>
    /// Upgrades a serialized payload one schema version forward. Migrations work on the
    /// serialized string rather than on typed objects so that old shapes never have to be
    /// kept alive as C# types.
    /// </summary>
    public interface ISaveMigration
    {
        /// <summary>Version the payload must already be at for this migration to apply.</summary>
        int FromVersion { get; }

        /// <summary>Version the payload is at once this migration has run. Must be <c>FromVersion + 1</c>.</summary>
        int ToVersion { get; }

        string Migrate(string payload);
    }
}
