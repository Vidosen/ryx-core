using System;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Ryx.Core.Save.Infrastructure;

namespace Ryx.Core.Save.Tests
{
    public sealed class SaveServiceTests
    {
        [Serializable]
        private sealed class Profile
        {
            public string playerName;
            public int litresPumped;
        }

        private static readonly SaveKey ProfileKey = new("profile");

        private static (SaveService service, InMemorySaveStore store) CreateService(
            ISaveSchemaRegistry schemas = null,
            ISaveDiagnostics diagnostics = null,
            SaveServiceOptions options = null)
        {
            var store = new InMemorySaveStore();
            var service = new SaveService(store, new JsonUtilitySaveSerializer(), schemas, diagnostics, options);
            return (service, store);
        }

        [Test]
        public void LoadAsync_MissingDocument_ReturnsFallback()
        {
            var (service, _) = CreateService();
            var fallback = new Profile { playerName = "Ivan" };

            var loaded = service.LoadAsync(ProfileKey, fallback).GetAwaiter().GetResult();

            Assert.AreSame(fallback, loaded);
        }

        [Test]
        public void SaveThenLoad_RoundTripsPayload()
        {
            var (service, _) = CreateService();
            service.SaveAsync(ProfileKey, new Profile { playerName = "Ivan", litresPumped = 42 })
                .GetAwaiter().GetResult();

            var loaded = service.LoadAsync<Profile>(ProfileKey).GetAwaiter().GetResult();

            Assert.IsNotNull(loaded);
            Assert.AreEqual("Ivan", loaded.playerName);
            Assert.AreEqual(42, loaded.litresPumped);
        }

        [Test]
        public void SaveAsync_StampsCurrentSchemaVersion()
        {
            var schemas = new SaveSchemaRegistry()
                .Register(ProfileKey, new SaveMigrationPipeline(2, new RenameFieldMigration()));
            var (service, store) = CreateService(schemas);

            service.SaveAsync(ProfileKey, new Profile { playerName = "Ivan" }).GetAwaiter().GetResult();

            var raw = store.ReadAsync(ProfileKey).GetAwaiter().GetResult();
            StringAssert.Contains("\"version\":2", raw);
        }

        [Test]
        public void LoadAsync_MigratesOlderDocument()
        {
            var schemas = new SaveSchemaRegistry()
                .Register(ProfileKey, new SaveMigrationPipeline(2, new RenameFieldMigration()));
            var (service, store) = CreateService(schemas);

            // A v1 document used "name" where v2 uses "playerName".
            store.Seed(ProfileKey, "{\"version\":1,\"payload\":\"{\\\"name\\\":\\\"Ivan\\\"}\"}");

            var loaded = service.LoadAsync<Profile>(ProfileKey).GetAwaiter().GetResult();

            Assert.IsNotNull(loaded);
            Assert.AreEqual("Ivan", loaded.playerName);
        }

        [Test]
        public void LoadAsync_CorruptDocument_RecoversToFallbackAndReports()
        {
            var diagnostics = new RecordingDiagnostics();
            var (service, store) = CreateService(diagnostics: diagnostics);
            store.Seed(ProfileKey, "{\"version\":0,\"payload\":\"{}\"}");

            var fallback = new Profile { playerName = "default" };
            var loaded = service.LoadAsync(ProfileKey, fallback).GetAwaiter().GetResult();

            Assert.AreSame(fallback, loaded);
            Assert.AreEqual(1, diagnostics.RecoveredCount);
        }

        [Test]
        public void LoadAsync_CorruptDocument_ThrowsWhenRecoveryDisabled()
        {
            var options = new SaveServiceOptions { RecoverFromCorruption = false };
            var (service, store) = CreateService(options: options);
            store.Seed(ProfileKey, "{\"version\":0,\"payload\":\"{}\"}");

            Assert.Throws<SaveCorruptedException>(() =>
                service.LoadAsync<Profile>(ProfileKey).GetAwaiter().GetResult());
        }

        [Test]
        public void DeleteAsync_RemovesDocument()
        {
            var (service, store) = CreateService();
            service.SaveAsync(ProfileKey, new Profile()).GetAwaiter().GetResult();
            Assert.AreEqual(1, store.Count);

            service.DeleteAsync(ProfileKey).GetAwaiter().GetResult();

            Assert.AreEqual(0, store.Count);
            Assert.IsFalse(service.ExistsAsync(ProfileKey).GetAwaiter().GetResult());
        }

        private sealed class RenameFieldMigration : ISaveMigration
        {
            public int FromVersion => 1;
            public int ToVersion => 2;
            public string Migrate(string payload) => payload.Replace("\"name\"", "\"playerName\"");
        }

        private sealed class RecordingDiagnostics : ISaveDiagnostics
        {
            public int RecoveredCount { get; private set; }
            public void OnRecovered(SaveKey key, Exception error) => RecoveredCount++;
        }
    }
}
