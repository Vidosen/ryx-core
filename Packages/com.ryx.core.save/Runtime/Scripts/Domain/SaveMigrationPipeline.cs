using System;
using System.Collections.Generic;
using System.Linq;

namespace Ryx.Core.Save
{
    /// <summary>
    /// Ordered chain of <see cref="ISaveMigration"/> steps that brings a payload from any
    /// supported older version up to <see cref="TargetVersion"/>. Pure domain logic: no
    /// Unity types, no I/O, fully unit testable.
    /// </summary>
    public sealed class SaveMigrationPipeline
    {
        /// <summary>Version-1 schema with no migrations. Suitable for brand new documents.</summary>
        public static readonly SaveMigrationPipeline None = new(1);

        private readonly Dictionary<int, ISaveMigration> _byFromVersion;

        public SaveMigrationPipeline(int targetVersion, params ISaveMigration[] migrations)
        {
            if (targetVersion < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(targetVersion), targetVersion, "Save versions start at 1.");
            }

            TargetVersion = targetVersion;
            _byFromVersion = new Dictionary<int, ISaveMigration>();

            foreach (var migration in migrations ?? Array.Empty<ISaveMigration>())
            {
                if (migration == null)
                {
                    throw new ArgumentException("Migration list contains a null entry.", nameof(migrations));
                }

                if (migration.ToVersion != migration.FromVersion + 1)
                {
                    throw new ArgumentException(
                        $"Migration {migration.GetType().Name} must move exactly one version forward " +
                        $"(got {migration.FromVersion} -> {migration.ToVersion}).",
                        nameof(migrations));
                }

                if (!_byFromVersion.TryAdd(migration.FromVersion, migration))
                {
                    throw new ArgumentException(
                        $"Two migrations declare FromVersion {migration.FromVersion}.",
                        nameof(migrations));
                }
            }

            var gaps = Enumerable
                .Range(1, Math.Max(0, targetVersion - 1))
                .Where(from => !_byFromVersion.ContainsKey(from))
                .ToArray();

            // Fail here, not at load time. A pipeline declaring version N without a complete
            // 1..N chain would otherwise sail through construction and only blow up on a
            // player's device — where SaveService's recovery would quietly wipe their save.
            if (gaps.Length > 0)
            {
                throw new ArgumentException(
                    $"Migration chain to version {targetVersion} is missing steps from version(s): " +
                    string.Join(", ", gaps),
                    nameof(migrations));
            }
        }

        public int TargetVersion { get; }

        public bool CanUpgradeFrom(int fromVersion)
        {
            if (fromVersion == TargetVersion) return true;
            if (fromVersion < 1 || fromVersion > TargetVersion) return false;

            for (var version = fromVersion; version < TargetVersion; version++)
            {
                if (!_byFromVersion.ContainsKey(version)) return false;
            }

            return true;
        }

        /// <summary>
        /// Runs every migration between <paramref name="fromVersion"/> and <see cref="TargetVersion"/>.
        /// Returns the payload unchanged when it is already current.
        /// </summary>
        public string Upgrade(string payload, int fromVersion)
        {
            if (fromVersion == TargetVersion)
            {
                return payload;
            }

            if (fromVersion > TargetVersion)
            {
                throw new SaveMigrationException(
                    $"Save was written by a newer build (version {fromVersion}, this build understands {TargetVersion}). " +
                    "Downgrade is not supported.");
            }

            if (fromVersion < 1)
            {
                throw new SaveMigrationException($"Invalid save version {fromVersion}; versions start at 1.");
            }

            var current = payload;
            for (var version = fromVersion; version < TargetVersion; version++)
            {
                if (!_byFromVersion.TryGetValue(version, out var migration))
                {
                    throw new SaveMigrationException(
                        $"No migration registered from version {version} to {version + 1}.");
                }

                current = migration.Migrate(current);
            }

            return current;
        }
    }

    /// <summary>Resolves which schema applies to a given document.</summary>
    public interface ISaveSchemaRegistry
    {
        SaveMigrationPipeline Resolve(SaveKey key);
    }

    /// <summary>Dictionary-backed registry; unknown keys fall back to <see cref="SaveMigrationPipeline.None"/>.</summary>
    public sealed class SaveSchemaRegistry : ISaveSchemaRegistry
    {
        private readonly Dictionary<SaveKey, SaveMigrationPipeline> _pipelines = new();

        public SaveSchemaRegistry Register(SaveKey key, SaveMigrationPipeline pipeline)
        {
            _pipelines[key] = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            return this;
        }

        public SaveMigrationPipeline Resolve(SaveKey key) =>
            _pipelines.TryGetValue(key, out var pipeline) ? pipeline : SaveMigrationPipeline.None;
    }
}
