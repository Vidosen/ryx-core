using System;
using NUnit.Framework;

namespace Ryx.Core.Save.Tests
{
    public sealed class SaveMigrationPipelineTests
    {
        private sealed class AppendMigration : ISaveMigration
        {
            private readonly string _suffix;

            public AppendMigration(int fromVersion, string suffix)
            {
                FromVersion = fromVersion;
                ToVersion = fromVersion + 1;
                _suffix = suffix;
            }

            public int FromVersion { get; }
            public int ToVersion { get; }
            public string Migrate(string payload) => payload + _suffix;
        }

        [Test]
        public void None_IsVersionOne_AndPassesPayloadThrough()
        {
            Assert.AreEqual(1, SaveMigrationPipeline.None.TargetVersion);
            Assert.AreEqual("{}", SaveMigrationPipeline.None.Upgrade("{}", 1));
        }

        [Test]
        public void Upgrade_RunsEveryStepInOrder()
        {
            var pipeline = new SaveMigrationPipeline(
                3,
                new AppendMigration(1, "-a"),
                new AppendMigration(2, "-b"));

            Assert.AreEqual("base-a-b", pipeline.Upgrade("base", 1));
            Assert.AreEqual("base-b", pipeline.Upgrade("base", 2));
            Assert.AreEqual("base", pipeline.Upgrade("base", 3));
        }

        [Test]
        public void Upgrade_FromNewerVersion_Throws()
        {
            var pipeline = new SaveMigrationPipeline(2, new AppendMigration(1, "-a"));
            Assert.Throws<SaveMigrationException>(() => pipeline.Upgrade("base", 5));
        }

        [Test]
        public void Constructor_RejectsTargetVersionWithNoChain()
        {
            // Declaring version 4 without migrations from 1, 2 and 3 must fail at construction
            // (i.e. in the installer), not later on a player's device.
            Assert.Throws<ArgumentException>(() => new SaveMigrationPipeline(4));
        }

        [Test]
        public void Constructor_RejectsMultiVersionJump()
        {
            var bad = new StubMigration(1, 3);
            Assert.Throws<ArgumentException>(() => new SaveMigrationPipeline(3, bad));
        }

        [Test]
        public void Constructor_RejectsGapInChain()
        {
            Assert.Throws<ArgumentException>(() =>
                new SaveMigrationPipeline(3, new AppendMigration(2, "-b")));
        }

        [Test]
        public void CanUpgradeFrom_ReportsChainCompleteness()
        {
            var pipeline = new SaveMigrationPipeline(
                3,
                new AppendMigration(1, "-a"),
                new AppendMigration(2, "-b"));

            Assert.IsTrue(pipeline.CanUpgradeFrom(1));
            Assert.IsTrue(pipeline.CanUpgradeFrom(3));
            Assert.IsFalse(pipeline.CanUpgradeFrom(4));
            Assert.IsFalse(pipeline.CanUpgradeFrom(0));
        }

        private sealed class StubMigration : ISaveMigration
        {
            public StubMigration(int from, int to)
            {
                FromVersion = from;
                ToVersion = to;
            }

            public int FromVersion { get; }
            public int ToVersion { get; }
            public string Migrate(string payload) => payload;
        }
    }
}
