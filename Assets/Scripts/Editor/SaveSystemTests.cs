using System;
using System.IO;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using InTheArena.Save;
using InTheArena.MainGame;

namespace InTheArena.Tests.Editor
{
    public class FakeClock : IClock
    {
        public DateTime UtcNow { get; set; } = DateTime.UtcNow;
    }

    public class FakeSaveRepository : IPlayerSaveRepository
    {
        public bool FailNextSave { get; set; }
        public PlayerProgressState SavedState { get; private set; }

        public SaveLoadResult LoadResult { get; set; } = new SaveLoadResult { Status = SaveLoadStatus.Success, State = new PlayerProgressState() };
        public int SaveCallCount { get; private set; }

        public SaveLoadResult LoadOrCreate(PlayerProgressState defaultCandidate)
        {
            if (LoadResult.Status == SaveLoadStatus.CreatedDefaults)
            {
                SavedState = defaultCandidate;
                LoadResult.State = defaultCandidate;
            }
            return LoadResult;
        }

        public bool TrySave(PlayerProgressState state, out string error)
        {
            SaveCallCount++;
            if (FailNextSave)
            {
                FailNextSave = false;
                error = "Simulated Save Failure";
                return false;
            }
            error = null;
            SavedState = state.DeepClone();
            return true;
        }
    }

    public class FakeFileSystem : IFileSystem
    {
        public bool FailOnCreateOldMain { get; set; }
        public bool FailOnCopyNewMain { get; set; }
        public bool FailOnRestoreOldMain { get; set; }
        public bool FailOnBackupRotation { get; set; }
        public bool FailOnDeleteOldMain { get; set; }
        public bool FailOnBackupVerificationRead { get; set; }
        public bool CorruptMainAfterMove { get; set; }
        public bool CorruptMainAfterReplace { get; set; }
        public bool CorruptRestoredMainOnRead { get; set; }
        public bool CorruptMainAfterRecoveryCopy { get; set; }

        private SystemFileSystem sys = new SystemFileSystem();
        private bool isRestoringOldMain = false;

        public bool FileExists(string path) => sys.FileExists(path);
        public void CreateDirectory(string path) => sys.CreateDirectory(path);

        public string ReadAllText(string path)
        {
            if (FailOnBackupVerificationRead && path.EndsWith(".bak"))
            {
                return "{ invalid }";
            }
            if (CorruptRestoredMainOnRead && isRestoringOldMain && !path.EndsWith(".bak") && !path.EndsWith(".tmp") && !path.EndsWith(".old") && !path.EndsWith(".recovery.tmp") && !path.Contains(".invalid_"))
            {
                isRestoringOldMain = false;
                return "{ invalid }";
            }
            return sys.ReadAllText(path);
        }
        public void WriteAllText(string path, string contents) => sys.WriteAllText(path, contents);

        public void Copy(string sourceFileName, string destFileName, bool overwrite)
        {
            if (FailOnCreateOldMain && destFileName.EndsWith(".old")) throw new IOException("Fake fail on create oldMain");
            if (FailOnCopyNewMain && sourceFileName.EndsWith(".tmp") && !destFileName.EndsWith(".old") && !destFileName.EndsWith(".bak") && !destFileName.EndsWith(".recovery.tmp")) throw new IOException("Fake fail on copy new main");
            if (FailOnRestoreOldMain && sourceFileName.EndsWith(".old") && !destFileName.EndsWith(".bak")) throw new IOException("Fake fail on restore oldMain");
            if (FailOnBackupRotation && destFileName.EndsWith(".bak") && sourceFileName.EndsWith(".old")) throw new IOException("Fake fail on backup rotation");

            if (sourceFileName.EndsWith(".old") && !destFileName.EndsWith(".bak"))
            {
                isRestoringOldMain = true;
            }

            sys.Copy(sourceFileName, destFileName, overwrite);

            if (CorruptMainAfterReplace && sourceFileName.EndsWith(".tmp") && !destFileName.EndsWith(".old") && !destFileName.EndsWith(".bak"))
            {
                sys.WriteAllText(destFileName, "{ corrupted main after copy }");
            }
            if (CorruptMainAfterRecoveryCopy && sourceFileName.EndsWith(".recovery.tmp") && !destFileName.EndsWith(".bak") && !destFileName.EndsWith(".old") && !destFileName.EndsWith(".tmp") && !destFileName.EndsWith(".corrupt") && !destFileName.EndsWith(".invalid_"))
            {
                sys.WriteAllText(destFileName, "{ corrupted main after recovery copy }");
            }
        }

        public void Replace(string sourceFileName, string destinationFileName, string destinationBackupFileName, bool ignoreMetadataErrors)
        {
            throw new PlatformNotSupportedException("Force fallback replace in test");
        }

        public void Delete(string path)
        {
            if (FailOnDeleteOldMain && path.EndsWith(".old"))
            {
                throw new IOException("Fake fail on delete oldMain");
            }
            sys.Delete(path);
        }

        public void Move(string sourceFileName, string destFileName)
        {
            sys.Move(sourceFileName, destFileName);
            if (CorruptMainAfterMove && destFileName.EndsWith(".json") && !destFileName.EndsWith(".old") && !destFileName.EndsWith(".tmp") && !destFileName.EndsWith(".bak"))
            {
                sys.WriteAllText(destFileName, "{ corrupted main after move }");
            }
        }
        public Stream OpenWrite(string path) => sys.OpenWrite(path);
    }


    [TestFixture]
    public class SaveSystemTests
    {
        private string m_TempDir;
        private string m_FileName = "test-save.json";

        private const string LegacyPlayerDataKey = "InTheArena.PlayerData.v1";

        private bool m_HadLegacyPlayerData;
        private string m_OriginalLegacyPlayerData;

        [SetUp]
        public void Setup()
        {
            m_HadLegacyPlayerData = PlayerPrefs.HasKey(LegacyPlayerDataKey);

            if (m_HadLegacyPlayerData)
            {
                m_OriginalLegacyPlayerData =
                    PlayerPrefs.GetString(LegacyPlayerDataKey);
            }

            PlayerPrefs.DeleteKey(LegacyPlayerDataKey);
            PlayerPrefs.Save();

            m_TempDir = Path.Combine(Application.temporaryCachePath, "TestSave_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(m_TempDir);
        }

        [TearDown]
        public void Teardown()
        {
            PlayerPrefs.DeleteKey(LegacyPlayerDataKey);

            if (m_HadLegacyPlayerData)
            {
                PlayerPrefs.SetString(
                    LegacyPlayerDataKey,
                    m_OriginalLegacyPlayerData);
            }

            PlayerPrefs.Save();

            if (Directory.Exists(m_TempDir))
            {
                Directory.Delete(m_TempDir, true);
            }
        }

        // ==========================================
        // 1. 미래 버전 보호 테스트
        // ==========================================
        [Test]
        public void Repository_FutureMain_DoesNotLoadBackup()
        {
            var clock = new FakeClock();
            var repo = new PlayerSaveRepository(m_TempDir, m_FileName, clock);

            var futureEnv = new PlayerSaveEnvelope { schemaVersion = PlayerSaveValidator.CurrentSchemaVersion + 1, payload = new PlayerSavePayload() };
            File.WriteAllText(Path.Combine(m_TempDir, m_FileName), JsonUtility.ToJson(futureEnv));

            var validEnv = new PlayerSaveEnvelope { schemaVersion = PlayerSaveValidator.CurrentSchemaVersion, payload = new PlayerSavePayload() };
            File.WriteAllText(Path.Combine(m_TempDir, m_FileName + ".bak"), JsonUtility.ToJson(validEnv));

            var result = repo.LoadOrCreate(new PlayerProgressState());

            Assert.IsNull(result.State);
            Assert.AreEqual(SaveLoadStatus.UnsupportedFutureVersion, result.Status);
        }

        [Test]
        public void Repository_FutureMain_DoesNotCreateDefaults()
        {
            var clock = new FakeClock();
            var repo = new PlayerSaveRepository(m_TempDir, m_FileName, clock);

            var futureEnv = new PlayerSaveEnvelope { schemaVersion = PlayerSaveValidator.CurrentSchemaVersion + 1, payload = new PlayerSavePayload() };
            File.WriteAllText(Path.Combine(m_TempDir, m_FileName), JsonUtility.ToJson(futureEnv));

            var result = repo.LoadOrCreate(new PlayerProgressState());

            Assert.IsNull(result.State);
            Assert.AreEqual(SaveLoadStatus.UnsupportedFutureVersion, result.Status);
        }

        [Test]
        public void Repository_FutureBackup_DisablesAllWrites()
        {
            var clock = new FakeClock();
            var repo = new PlayerSaveRepository(m_TempDir, m_FileName, clock);

            var validEnv = new PlayerSaveEnvelope { schemaVersion = PlayerSaveValidator.CurrentSchemaVersion, payload = new PlayerSavePayload() };
            File.WriteAllText(Path.Combine(m_TempDir, m_FileName), JsonUtility.ToJson(validEnv));

            var futureEnv = new PlayerSaveEnvelope { schemaVersion = PlayerSaveValidator.CurrentSchemaVersion + 1, payload = new PlayerSavePayload() };
            File.WriteAllText(Path.Combine(m_TempDir, m_FileName + ".bak"), JsonUtility.ToJson(futureEnv));

            var result = repo.LoadOrCreate(new PlayerProgressState());

            Assert.IsNull(result.State);
            Assert.AreEqual(SaveLoadStatus.UnsupportedFutureVersion, result.Status);
        }

        [Test]
        public void SaveManager_FutureVersion_DoesNotInitializeDefaultState()
        {
            var go = new GameObject("SaveManager");
            var sm = go.AddComponent<SaveManager>();

            var repo = new FakeSaveRepository();
            repo.LoadResult = new SaveLoadResult { Status = SaveLoadStatus.UnsupportedFutureVersion };

            sm.InitializeForTests(repo, new FakeClock(), null, isReadOnly: true);

            Assert.IsFalse(sm.TrySpendHeart(), "Should not allow spending heart in read-only mode");
            Assert.AreEqual(SaveAvailability.UnsupportedFutureVersion, sm.Availability);

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void TrySpendHeart_ReadOnly_ReturnsFalse()
        {
            var go = new GameObject("SaveManager");
            var sm = go.AddComponent<SaveManager>();

            var repo = new FakeSaveRepository();
            sm.InitializeForTests(repo, new FakeClock(), new PlayerProgressState(), isReadOnly: true);

            Assert.IsFalse(sm.TrySpendHeart());
            UnityEngine.Object.DestroyImmediate(go);
        }

        // ==========================================
        // 2. 신규 상태 판별 테스트
        // ==========================================
        [Test]
        public void Repository_MissingFiles_ReturnsCreatedDefaults()
        {
            var clock = new FakeClock();
            var repo = new PlayerSaveRepository(m_TempDir, m_FileName, clock);

            var defaultCandidate = new PlayerProgressState();
            defaultCandidate.SetHearts(3);
            var result = repo.LoadOrCreate(defaultCandidate);

            Assert.IsNotNull(result.State);
            Assert.AreEqual(SaveLoadStatus.CreatedDefaults, result.Status);
            Assert.AreEqual(3, result.State.Hearts);
        }

        [Test]
        public void Repository_ValidAllZeroSave_ReturnsSuccess()
        {
            var clock = new FakeClock();
            var repo = new PlayerSaveRepository(m_TempDir, m_FileName, clock);

            var env = new PlayerSaveEnvelope { schemaVersion = PlayerSaveValidator.CurrentSchemaVersion, payload = new PlayerSavePayload() };
            env.checksum = JsonFileSaveStorage.ComputeChecksum(env);
            File.WriteAllText(Path.Combine(m_TempDir, m_FileName), JsonUtility.ToJson(env));

            var result = repo.LoadOrCreate(new PlayerProgressState());

            Assert.IsNotNull(result.State);
            Assert.AreEqual(SaveLoadStatus.Success, result.Status);
        }

        // ==========================================
        // 3. 스테이지 커밋 테스트
        // ==========================================
        [Test]
        public void StageClear_MissingSaveManager_TransitionsToFailedThroughProductionMethod()
        {
            var go = new GameObject("StageManager");
            var stageManager = go.AddComponent<StageManager>();
            
            typeof(StageManager).GetMethod("SetStageClearCommitState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(stageManager, new object[] { StageClearCommitState.Pending });

            var processMethod = typeof(StageManager).GetMethod("ProcessPendingStageClearSave", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            processMethod.Invoke(stageManager, null);

            Assert.AreEqual(StageClearCommitState.Failed, stageManager.StageClearCommitState);
            Assert.AreEqual("SaveManager is unavailable.", stageManager.LastStageClearSaveError);
            
            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void StageClear_NullCandidate_TransitionsToFailed()
        {
            var smGo = new GameObject("SaveManager");
            var sm = smGo.AddComponent<SaveManager>();
            typeof(SaveManager).GetProperty("Instance")?.SetValue(null, sm);
            sm.InitializeForTests(new FakeSaveRepository(), new FakeClock(), new PlayerProgressState());

            var go = new GameObject("StageManager");
            var stageManager = go.AddComponent<StageManager>();

            typeof(StageManager).GetMethod("SetStageClearCommitState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(stageManager, new object[] { StageClearCommitState.Pending });

            var processMethod = typeof(StageManager).GetMethod("ProcessPendingStageClearSave", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            processMethod.Invoke(stageManager, null);

            Assert.AreEqual(StageClearCommitState.Failed, stageManager.StageClearCommitState);
            Assert.AreEqual("No pending candidate data.", stageManager.LastStageClearSaveError);

            UnityEngine.Object.DestroyImmediate(smGo);
            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void StageClear_SaveFailure_ExposesRetryState()
        {
            var smGo = new GameObject("SaveManager");
            var sm = smGo.AddComponent<SaveManager>();
            typeof(SaveManager).GetProperty("Instance")?.SetValue(null, sm);
            var repo = new FakeSaveRepository { FailNextSave = true };
            sm.InitializeForTests(repo, new FakeClock(), new PlayerProgressState());

            var stageGo = new GameObject("StageManager");
            var stageManager = stageGo.AddComponent<StageManager>();

            var candidate = sm.CreatePendingStageClearCandidate(new StagePlayerState(), 1, 10, 1);
            typeof(StageManager).GetField("m_PendingStageClearCandidate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(stageManager, candidate);
            
            typeof(StageManager).GetMethod("SetStageClearCommitState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(stageManager, new object[] { StageClearCommitState.Pending });

            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex(@"\[StageManager\] 스테이지 클리어 저장 실패: .*"));

            var processMethod = typeof(StageManager).GetMethod("ProcessPendingStageClearSave", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            processMethod.Invoke(stageManager, null);

            Assert.AreEqual(StageClearCommitState.Failed, stageManager.StageClearCommitState);
            
            UnityEngine.Object.DestroyImmediate(smGo);
            UnityEngine.Object.DestroyImmediate(stageGo);
        }

        [Test]
        public void StageClear_RetrySuccess_CommitsExactlyOnce()
        {
            var smGo = new GameObject("SaveManager");
            var sm = smGo.AddComponent<SaveManager>();
            typeof(SaveManager).GetProperty("Instance")?.SetValue(null, sm);
            var repo = new FakeSaveRepository();
            sm.InitializeForTests(repo, new FakeClock(), new PlayerProgressState());

            var stageGo = new GameObject("StageManager");
            var stageManager = stageGo.AddComponent<StageManager>();

            var candidate = sm.CreatePendingStageClearCandidate(new StagePlayerState(), 1, 10, 1);
            typeof(StageManager).GetField("m_PendingStageClearCandidate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(stageManager, candidate);
            
            typeof(StageManager).GetField("m_StageClearCommitState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(stageManager, StageClearCommitState.Failed);

            bool success = stageManager.RetryStageClearSave();

            Assert.IsTrue(success);
            Assert.AreEqual(StageClearCommitState.Committed, stageManager.StageClearCommitState);
            Assert.AreEqual(1, repo.SaveCallCount);

            UnityEngine.Object.DestroyImmediate(smGo);
            UnityEngine.Object.DestroyImmediate(stageGo);
        }

        [Test]
        public void StageClear_RetryDoubleClick_DoesNotDuplicateSave()
        {
            var smGo = new GameObject("SaveManager");
            var sm = smGo.AddComponent<SaveManager>();
            typeof(SaveManager).GetProperty("Instance")?.SetValue(null, sm);
            var repo = new FakeSaveRepository();
            sm.InitializeForTests(repo, new FakeClock(), new PlayerProgressState());

            var stageGo = new GameObject("StageManager");
            var stageManager = stageGo.AddComponent<StageManager>();

            var candidate = sm.CreatePendingStageClearCandidate(new StagePlayerState(), 1, 10, 1);
            typeof(StageManager).GetField("m_PendingStageClearCandidate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(stageManager, candidate);
            
            typeof(StageManager).GetField("m_StageClearCommitState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(stageManager, StageClearCommitState.Failed);

            bool retry1 = stageManager.RetryStageClearSave();
            bool retry2 = stageManager.RetryStageClearSave();

            Assert.IsTrue(retry1);
            Assert.IsFalse(retry2); // Second retry should be rejected
            Assert.AreEqual(1, repo.SaveCallCount); // Save should happen exactly once

            UnityEngine.Object.DestroyImmediate(smGo);
            UnityEngine.Object.DestroyImmediate(stageGo);
        }

        [Test]
        public void StageClear_GiveUpFromFailed_DiscardsCandidate()
        {
            var stageGo = new GameObject("StageManager");
            var stageManager = stageGo.AddComponent<StageManager>();

            var candidate = new PlayerProgressState();
            typeof(StageManager).GetField("m_PendingStageClearCandidate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(stageManager, candidate);
            
            typeof(StageManager).GetField("m_StageClearCommitState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(stageManager, StageClearCommitState.Failed);

            bool success = stageManager.GiveUpStageClearSave();

            Assert.IsTrue(success);
            Assert.AreEqual(StageClearCommitState.GivenUp, stageManager.StageClearCommitState);
            var resultingCandidate = typeof(StageManager).GetField("m_PendingStageClearCandidate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(stageManager);
            Assert.IsNull(resultingCandidate); // Candidate should be discarded

            UnityEngine.Object.DestroyImmediate(stageGo);
        }

        [Test]
        public void StageClear_GiveUpAfterCommitted_IsRejected()
        {
            var stageGo = new GameObject("StageManager");
            var stageManager = stageGo.AddComponent<StageManager>();

            var candidate = new PlayerProgressState();
            typeof(StageManager).GetField("m_PendingStageClearCandidate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(stageManager, candidate);
            
            typeof(StageManager).GetField("m_StageClearCommitState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(stageManager, StageClearCommitState.Committed);

            bool success = stageManager.GiveUpStageClearSave();

            Assert.IsFalse(success); // Cannot give up if already committed
            Assert.AreEqual(StageClearCommitState.Committed, stageManager.StageClearCommitState);

            UnityEngine.Object.DestroyImmediate(stageGo);
        }

        // ==========================================
        // 4. 하트 트랜잭션 테스트
        // ==========================================
        [Test]
        public void RefreshHearts_SaveFailure_DoesNotMutateState()
        {
            var smGo = new GameObject("SaveManager");
            var sm = smGo.AddComponent<SaveManager>();
            var repo = new FakeSaveRepository { FailNextSave = true };

            var state = new PlayerProgressState();
            state.SetHearts(0);
            state.SetLastHeartRecoveryUtcTicks(DateTime.UtcNow.AddHours(-1).Ticks);

            sm.InitializeForTests(repo, new FakeClock(), state);

            var result = sm.RefreshHearts();
            Assert.AreEqual(HeartRefreshResult.SaveFailed, result);
            Assert.AreEqual(0, sm.Hearts, "Heart count should not be mutated if save fails");

            UnityEngine.Object.DestroyImmediate(smGo);
        }

        [Test]
        public void TrySpendHeart_SaveFailure_DoesNotMutateState()
        {
            var smGo = new GameObject("SaveManager");
            var sm = smGo.AddComponent<SaveManager>();
            var repo = new FakeSaveRepository { FailNextSave = true };

            var state = new PlayerProgressState();
            state.SetHearts(5);

            sm.InitializeForTests(repo, new FakeClock(), state);

            bool success = sm.TrySpendHeart();
            Assert.IsFalse(success);
            Assert.AreEqual(5, sm.Hearts, "Hearts should not be spent if save fails");

            UnityEngine.Object.DestroyImmediate(smGo);
        }

        [Test]
        public void TryPurchaseItem_SavesGoldAndItemCountTogether()
        {
            var managerObject = new GameObject("SaveManager");
            var manager = managerObject.AddComponent<SaveManager>();
            var state = new PlayerProgressState();
            state.SetGold(100);
            manager.InitializeForTests(new FakeSaveRepository(), new FakeClock(), state);

            bool purchased = manager.TryPurchaseItem(ItemType.Meteor, 50, out string error);

            Assert.IsTrue(purchased, error);
            Assert.AreEqual(50, manager.Gold);
            Assert.AreEqual(1, manager.GetItemCount(ItemType.Meteor));
            UnityEngine.Object.DestroyImmediate(managerObject);
        }

        [Test]
        public void TryPurchaseItem_InsufficientGoldLeavesStateUntouched()
        {
            var managerObject = new GameObject("SaveManager");
            var manager = managerObject.AddComponent<SaveManager>();
            var state = new PlayerProgressState();
            state.SetGold(49);
            manager.InitializeForTests(new FakeSaveRepository(), new FakeClock(), state);

            Assert.IsFalse(manager.TryPurchaseItem(ItemType.Meteor, 50, out _));
            Assert.AreEqual(49, manager.Gold);
            Assert.AreEqual(0, manager.GetItemCount(ItemType.Meteor));
            UnityEngine.Object.DestroyImmediate(managerObject);
        }

        [Test]
        public void TryPurchaseItem_SaveFailureLeavesStateUntouched()
        {
            var managerObject = new GameObject("SaveManager");
            var manager = managerObject.AddComponent<SaveManager>();
            var state = new PlayerProgressState();
            state.SetGold(100);
            manager.InitializeForTests(
                new FakeSaveRepository { FailNextSave = true },
                new FakeClock(),
                state);

            Assert.IsFalse(manager.TryPurchaseItem(ItemType.Meteor, 50, out _));
            Assert.AreEqual(100, manager.Gold);
            Assert.AreEqual(0, manager.GetItemCount(ItemType.Meteor));
            UnityEngine.Object.DestroyImmediate(managerObject);
        }

        // ==========================================
        // 5. 저장소 테스트
        // ==========================================
        [Test]
        public void Storage_MainCorrupt_BackupValid_RecoversBackup()
        {
            var clock = new FakeClock();
            var repo = new PlayerSaveRepository(m_TempDir, m_FileName, clock);

            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex(".*Invalid JSON.*"));
            File.WriteAllText(Path.Combine(m_TempDir, m_FileName), "{ invalid json");

            var validEnv = new PlayerSaveEnvelope { schemaVersion = PlayerSaveValidator.CurrentSchemaVersion, payload = new PlayerSavePayload { gold = 999 } };
            validEnv.checksum = JsonFileSaveStorage.ComputeChecksum(validEnv);
            File.WriteAllText(Path.Combine(m_TempDir, m_FileName + ".bak"), JsonUtility.ToJson(validEnv));

            var result = repo.LoadOrCreate(new PlayerProgressState());

            Assert.IsNotNull(result.State);
            Assert.AreEqual(SaveLoadStatus.RecoveredFromBackup, result.Status);
            Assert.AreEqual(999, result.State.Gold);
        }

        [Test]
        public void Storage_EmptyChecksum_IsRejected()
        {
            var clock = new FakeClock();
            var repo = new PlayerSaveRepository(m_TempDir, m_FileName, clock);

            var env = new PlayerSaveEnvelope { schemaVersion = PlayerSaveValidator.CurrentSchemaVersion, payload = new PlayerSavePayload() };
            env.checksum = "";
            File.WriteAllText(Path.Combine(m_TempDir, m_FileName), JsonUtility.ToJson(env));

            var result = repo.LoadOrCreate(new PlayerProgressState());

            Assert.IsNull(result.State);
            Assert.AreEqual(SaveLoadStatus.Corrupted, result.Status);
        }

        [Test]
        public void Storage_InvalidJson_ReturnsInvalidJson()
        {
            var clock = new FakeClock();
            var repo = new PlayerSaveRepository(m_TempDir, m_FileName, clock);

            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex(".*Invalid JSON.*"));
            File.WriteAllText(Path.Combine(m_TempDir, m_FileName), "{ malformed json");

            var result = repo.LoadOrCreate(new PlayerProgressState());

            Assert.IsNull(result.State);
            Assert.AreEqual(SaveLoadStatus.Corrupted, result.Status); // Corrupted internally mapped from InvalidJson if no backup
        }

        [Test]
        public void Storage_ReplaceFailure_PreservesMainAndBackup()
        {
            var clock = new FakeClock();
            var fs = new FakeFileSystem { FailOnCopyNewMain = true };
            var storage = new JsonFileSaveStorage(m_TempDir, m_FileName, fs);
            var repo = new PlayerSaveRepository(m_TempDir, m_FileName, clock, fs);

            var validEnv = new PlayerSaveEnvelope { schemaVersion = PlayerSaveValidator.CurrentSchemaVersion, payload = new PlayerSavePayload() };
            string mainPath = Path.Combine(m_TempDir, m_FileName);
            string bakPath = Path.Combine(m_TempDir, m_FileName + ".bak");

            validEnv.checksum = JsonFileSaveStorage.ComputeChecksum(validEnv);
            string originalMainContent = JsonUtility.ToJson(validEnv);
            File.WriteAllText(mainPath, originalMainContent);
            File.WriteAllText(bakPath, "dummy_backup");

            bool success = repo.TrySave(new PlayerProgressState(), out string error);
            Debug.Log($"Storage_ReplaceFailure_PreservesMainAndBackup error: {error}");

            Assert.IsFalse(success);
            Assert.IsNotNull(error);
            Assert.IsTrue(error.Contains("Fallback replace failed during copy"));
            Assert.IsTrue(error.Contains("Original main restored successfully"));
            Assert.IsFalse(error.Contains("Critical Recovery Failure"));

            // Check preservation
            Assert.AreEqual(originalMainContent, File.ReadAllText(mainPath));
            Assert.AreEqual("dummy_backup", File.ReadAllText(bakPath));

            // Check tmp and old cleanup
            Assert.IsFalse(File.Exists(mainPath + ".old"), "oldMain should be deleted after successful restore");
            Assert.IsFalse(File.Exists(mainPath + ".tmp"), "tmp should be cleaned up after successful restore");
        }

        [Test]
        public void Storage_Fallback_RestoreFailure_PreservesRecoveryFiles()
        {
            var clock = new FakeClock();
            var fs = new FakeFileSystem { FailOnCopyNewMain = true, FailOnRestoreOldMain = true };
            var storage = new JsonFileSaveStorage(m_TempDir, m_FileName, fs);
            var repo = new PlayerSaveRepository(m_TempDir, m_FileName, clock, fs);

            var validEnv = new PlayerSaveEnvelope { schemaVersion = PlayerSaveValidator.CurrentSchemaVersion, payload = new PlayerSavePayload() };
            string mainPath = Path.Combine(m_TempDir, m_FileName);
            string bakPath = Path.Combine(m_TempDir, m_FileName + ".bak");

            validEnv.checksum = JsonFileSaveStorage.ComputeChecksum(validEnv);
            string originalMainContent = JsonUtility.ToJson(validEnv);
            File.WriteAllText(mainPath, originalMainContent);
            File.WriteAllText(bakPath, "dummy_backup");

            bool success = repo.TrySave(new PlayerProgressState(), out string error);

            Assert.IsFalse(success);
            Assert.IsNotNull(error);
            Assert.IsTrue(error.Contains("Critical Recovery Failure"), "Should report critical failure if restore fails");

            Assert.IsTrue(File.Exists(mainPath + ".old"), "oldMain must be preserved on critical failure");
            Assert.IsTrue(File.Exists(mainPath + ".tmp"), "tmp must be preserved on critical failure for debugging");
        }

        // ==========================================
        // 6. 상태 노출 테스트
        // ==========================================
        [Test]
        public void SaveManager_CorruptedLoad_SetsCorruptedAvailability()
        {
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex(".*세이브 데이터가 손상되었습니다.*"));
            var smGo = new GameObject("SaveManager");
            var sm = smGo.AddComponent<SaveManager>();
            var repo = new FakeSaveRepository();
            repo.LoadResult = new SaveLoadResult { Status = SaveLoadStatus.Corrupted };

            sm.InitializeForTests(repo, new FakeClock(), null);
            sm.Load();

            Assert.AreEqual(SaveAvailability.Corrupted, sm.Availability);
            Assert.IsFalse(sm.TrySpendHeart());

            UnityEngine.Object.DestroyImmediate(smGo);
        }

        [Test]
        public void SaveManager_IoFailureLoad_SetsIoFailureAvailability()
        {
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex(".*세이브 데이터 로드 중 IO 오류가 발생했습니다.*"));
            var smGo = new GameObject("SaveManager");
            var sm = smGo.AddComponent<SaveManager>();
            var repo = new FakeSaveRepository();
            repo.LoadResult = new SaveLoadResult { Status = SaveLoadStatus.IoFailure };

            sm.InitializeForTests(repo, new FakeClock(), null);
            sm.Load();

            Assert.AreEqual(SaveAvailability.IoFailure, sm.Availability);
            Assert.IsFalse(sm.TrySpendHeart());

            UnityEngine.Object.DestroyImmediate(smGo);
        }

        [Test]
        public void SaveManager_UnavailableState_RejectsAllMutations()
        {
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex(".*세이브 데이터가 손상되었습니다.*"));
            var smGo = new GameObject("SaveManager");
            var sm = smGo.AddComponent<SaveManager>();
            var repo = new FakeSaveRepository();
            repo.LoadResult = new SaveLoadResult { Status = SaveLoadStatus.Corrupted };

            sm.InitializeForTests(repo, new FakeClock(), null);
            sm.Load();

            Assert.IsFalse(sm.TrySave(new PlayerProgressState(), out _));
            Assert.AreEqual(HeartRefreshResult.Unavailable, sm.RefreshHearts());
            Assert.IsFalse(sm.TrySpendHeart());

            UnityEngine.Object.DestroyImmediate(smGo);
        }

        // ==========================================
        // 7. 마이그레이션 테스트
        // ==========================================
        [Test]
        public void Migrator_DropsLegacyInventory_Properly()
        {
            var legacy = new LegacyPlayerDataV1();
            legacy.gold = 123;
            legacy.hearts = 3;
            legacy.itemCounts = new int[] { 1, 2 };

            var payload = PlayerSaveMigrator.MigrateFromV1(legacy, new FakeClock());

            Assert.AreEqual(123, payload.gold);
            Assert.AreEqual(3, payload.hearts);
        }

        [Test]
        public void Migration_NormalizesLegacyValuesBeforeSave()
        {
            var legacy = new LegacyPlayerDataV1();
            legacy.gold = -50; // Invalid
            legacy.hearts = 99; // Invalid

            var payload = PlayerSaveMigrator.MigrateFromV1(legacy, new FakeClock());

            Assert.AreEqual(0, payload.gold);
            Assert.AreEqual(SaveManager.MaxHearts, payload.hearts);
        }

        [Test]
        public void Storage_ChecksumMismatch_IsRejected()
        {
            var clock = new FakeClock();
            var repo = new PlayerSaveRepository(m_TempDir, m_FileName, clock);

            var env = new PlayerSaveEnvelope { schemaVersion = PlayerSaveValidator.CurrentSchemaVersion, payload = new PlayerSavePayload { gold = 999 } };
            env.checksum = "wrong_checksum";
            File.WriteAllText(Path.Combine(m_TempDir, m_FileName), JsonUtility.ToJson(env));

            var result = repo.LoadOrCreate(new PlayerProgressState());

            Assert.IsNull(result.State, "State should be null");
            Assert.AreEqual(SaveLoadStatus.Corrupted, result.Status, "Status should be Corrupted for checksum mismatch when no backup exists");
        }

        [Test]
        public void Storage_AtomicSave_WritesValidMain()
        {
            var storage = new JsonFileSaveStorage(m_TempDir, m_FileName);
            var env = new PlayerSaveEnvelope { schemaVersion = PlayerSaveValidator.CurrentSchemaVersion, payload = new PlayerSavePayload { gold = 777 } };

            bool success = storage.AtomicSave(env, out string error);

            Assert.IsTrue(success, "AtomicSave should succeed");
            Assert.IsNull(error, "Error should be null");

            var verify = storage.LoadMain();
            Assert.AreEqual(SaveFileReadStatus.Success, verify.Status);
            Assert.AreEqual(777, verify.Envelope.payload.gold);

            string tmpPath = Path.Combine(m_TempDir, m_FileName + ".tmp");
            Assert.IsFalse(File.Exists(tmpPath), "Tmp file should be deleted");
        }

        [Test]
        public void Storage_Fallback_BackupRotationFailure_PreservesOldMain()
        {
            var clock = new FakeClock();
            var fs = new FakeFileSystem { FailOnBackupRotation = true };
            var storage = new JsonFileSaveStorage(m_TempDir, m_FileName, fs);
            var repo = new PlayerSaveRepository(m_TempDir, m_FileName, clock, fs);

            var validEnv = new PlayerSaveEnvelope { schemaVersion = PlayerSaveValidator.CurrentSchemaVersion, payload = new PlayerSavePayload() };
            string mainPath = Path.Combine(m_TempDir, m_FileName);
            string bakPath = Path.Combine(m_TempDir, m_FileName + ".bak");
            string oldPath = mainPath + ".old";

            validEnv.checksum = JsonFileSaveStorage.ComputeChecksum(validEnv);
            File.WriteAllText(mainPath, JsonUtility.ToJson(validEnv));
            File.WriteAllText(bakPath, "dummy_backup");

            var newState = new PlayerProgressState();
            newState.SetGold(555);
            bool success = repo.TrySave(newState, out string error);

            Assert.IsTrue(success, "Save should return true despite backup rotation failure");
            Assert.IsNotNull(error, "Warning error should be returned");
            Assert.IsTrue(error.Contains("[WARNING]"), "Error should contain [WARNING]");

            // Check files
            Assert.IsTrue(File.Exists(oldPath), "oldMain should be preserved because rotation failed");
            Assert.IsFalse(File.Exists(mainPath + ".tmp"), "tmp should be cleaned up");

            // Check main is new data
            var finalMain = storage.LoadMain();
            Assert.AreEqual(SaveFileReadStatus.Success, finalMain.Status);
            Assert.AreEqual(555, finalMain.Envelope.payload.gold);

            // Check backup remains original dummy (fake file system threw during copy)
            Assert.AreEqual("dummy_backup", File.ReadAllText(bakPath));
        }

        [Test]
        public void Storage_Fallback_RestoreCleanupFailure_IsWarningOnly()
        {
            var clock = new FakeClock();
            var fs = new FakeFileSystem { FailOnCopyNewMain = true, FailOnDeleteOldMain = true };
            var storage = new JsonFileSaveStorage(m_TempDir, m_FileName, fs);
            var repo = new PlayerSaveRepository(m_TempDir, m_FileName, clock, fs);

            var validEnv = new PlayerSaveEnvelope { schemaVersion = PlayerSaveValidator.CurrentSchemaVersion, payload = new PlayerSavePayload() };
            string mainPath = Path.Combine(m_TempDir, m_FileName);
            string oldPath = mainPath + ".old";
            string tmpPath = mainPath + ".tmp";

            validEnv.checksum = JsonFileSaveStorage.ComputeChecksum(validEnv);
            string originalMainContent = JsonUtility.ToJson(validEnv);
            File.WriteAllText(mainPath, originalMainContent);
            bool success = repo.TrySave(new PlayerProgressState(), out string error);

            Assert.IsFalse(success, "Save should fail because new main copy failed");
            Assert.IsNotNull(error);
            Assert.IsTrue(error.Contains("Original main restored successfully"), "Should report successful restore");
            Assert.IsFalse(error.Contains("Critical Recovery Failure"), "Should not be a critical failure since restore succeeded");

            // Check files
            Assert.AreEqual(originalMainContent, File.ReadAllText(mainPath), "Main should be fully restored");
            Assert.IsTrue(File.Exists(oldPath), "oldMain should remain due to cleanup failure");
            Assert.IsFalse(File.Exists(tmpPath), "tmp should be cleaned up as it is a failed candidate");

            // Final state check
            var loadMain = storage.LoadMain();
            Assert.AreEqual(SaveFileReadStatus.Success, loadMain.Status);
        }


        // ==========================================
        // 6. 상태 노출 테스트
        // ==========================================
        [Test]
        public void Storage_Fallback_VerifiesFinalMainAndRestoresOriginal()
        {
            var clock = new FakeClock();
            var fs = new FakeFileSystem { CorruptMainAfterReplace = true };
            var storage = new JsonFileSaveStorage(m_TempDir, m_FileName, fs);
            var repo = new PlayerSaveRepository(m_TempDir, m_FileName, clock, fs);

            var validEnv = new PlayerSaveEnvelope { schemaVersion = PlayerSaveValidator.CurrentSchemaVersion, payload = new PlayerSavePayload() };
            string mainPath = Path.Combine(m_TempDir, m_FileName);
            string bakPath = Path.Combine(m_TempDir, m_FileName + ".bak");

            validEnv.checksum = JsonFileSaveStorage.ComputeChecksum(validEnv);
            string originalMainContent = JsonUtility.ToJson(validEnv);
            File.WriteAllText(mainPath, originalMainContent);
            File.WriteAllText(bakPath, "dummy_backup");
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex(@"\[JsonFileSaveStorage\] Invalid JSON in .*test-save\.json: JSON parse error: Missing a name for object member\."));

 bool success = repo.TrySave(new PlayerProgressState(), out string error);

            Assert.IsFalse(success, "Save should fail if final verification fails");
            Assert.IsNotNull(error);
            Assert.IsTrue(error.Contains("Fallback replacement verification failed"), "Error should mention verification failure");

            // Check files
            Assert.AreEqual(originalMainContent, File.ReadAllText(mainPath), "Main should be restored from oldMain/backup");
            Assert.AreEqual("dummy_backup", File.ReadAllText(bakPath), "Backup should not be deleted");

            var verify = storage.LoadMain();
            Assert.AreEqual(SaveFileReadStatus.Success, verify.Status, "Main should be readable after recovery");
        }

        [Test]
        public void Storage_FirstSave_InvalidFinalMain_IsQuarantined()
        {
            var fs = new FakeFileSystem { CorruptMainAfterMove = true };
            var storage = new JsonFileSaveStorage(m_TempDir, m_FileName, fs);
            string mainPath = Path.Combine(m_TempDir, m_FileName);
            string tmpPath = mainPath + ".tmp";

            var env = new PlayerSaveEnvelope { schemaVersion = PlayerSaveValidator.CurrentSchemaVersion, payload = new PlayerSavePayload() };
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex(@"\[JsonFileSaveStorage\] Invalid JSON in .*test-save\.json: JSON parse error: Missing a name for object member\."));
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex(System.Text.RegularExpressions.Regex.Escape("[JsonFileSaveStorage] Final main verification failed")));


            bool success = storage.AtomicSave(env, out string error);

            Assert.IsFalse(success, "First save should fail if validation fails after move");
            Assert.IsFalse(File.Exists(mainPath), "Invalid main should not remain in main path");
            Assert.IsFalse(File.Exists(tmpPath), "Tmp should be cleaned up or moved");

            var invalidFiles = Directory.GetFiles(m_TempDir, "*.invalid_*");
            Assert.AreEqual(1, invalidFiles.Length, "Exactly one quarantined invalid file should exist");
            Assert.AreEqual("{ corrupted main after move }", File.ReadAllText(invalidFiles[0]));

            var verify = storage.LoadMain();
            Assert.AreEqual(SaveFileReadStatus.Missing, verify.Status, "Main file should be reported as missing");
        }

        [Test]
        public void Storage_Fallback_OldMainCopyFailure_PreservesOriginalMain()
        {
            var clock = new FakeClock();
            var fs = new FakeFileSystem { FailOnCreateOldMain = true };
            var storage = new JsonFileSaveStorage(m_TempDir, m_FileName, fs);
            var repo = new PlayerSaveRepository(m_TempDir, m_FileName, clock, fs);

            var validEnv = new PlayerSaveEnvelope { schemaVersion = PlayerSaveValidator.CurrentSchemaVersion, payload = new PlayerSavePayload() };
            string mainPath = Path.Combine(m_TempDir, m_FileName);
            string bakPath = mainPath + ".bak";
            string oldPath = mainPath + ".old";
            string tmpPath = mainPath + ".tmp";

            validEnv.checksum = JsonFileSaveStorage.ComputeChecksum(validEnv);
            string originalMainContent = JsonUtility.ToJson(validEnv);
            File.WriteAllText(mainPath, originalMainContent);
            File.WriteAllText(bakPath, "dummy_backup");
            bool success = repo.TrySave(new PlayerProgressState(), out string error);

            Assert.IsFalse(success, "Save should fail if oldMain creation fails");
            Assert.IsNotNull(error);
            Assert.IsTrue(error.Contains("Fallback failed to create oldMain"), "Error should indicate oldMain creation failure");
            Assert.IsFalse(error.Contains("Critical Recovery Failure"), "Should not be critical since main wasn't modified");

            Assert.AreEqual(originalMainContent, File.ReadAllText(mainPath), "Original main must remain unchanged");
            Assert.AreEqual("dummy_backup", File.ReadAllText(bakPath), "Backup must remain unchanged");
            Assert.IsFalse(File.Exists(oldPath), "oldMain should not exist");
            Assert.IsFalse(File.Exists(tmpPath), "tmp should be cleaned up");
        }

        [Test]
        public void Storage_Fallback_RestoredMainIsReverified()
        {
            var clock = new FakeClock();
            var fs = new FakeFileSystem { FailOnCopyNewMain = true, CorruptRestoredMainOnRead = true };
            var storage = new JsonFileSaveStorage(m_TempDir, m_FileName, fs);
            var repo = new PlayerSaveRepository(m_TempDir, m_FileName, clock, fs);

            var validEnv = new PlayerSaveEnvelope { schemaVersion = PlayerSaveValidator.CurrentSchemaVersion, payload = new PlayerSavePayload() };
            string mainPath = Path.Combine(m_TempDir, m_FileName);
            string oldPath = mainPath + ".old";
            string tmpPath = mainPath + ".tmp";

            validEnv.checksum = JsonFileSaveStorage.ComputeChecksum(validEnv);
            string originalMainContent = JsonUtility.ToJson(validEnv);
            File.WriteAllText(mainPath, originalMainContent);
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex(@"\[JsonFileSaveStorage\] Invalid JSON in .*test-save\.json: JSON parse error: Missing a name for object member\."));
 bool success = repo.TrySave(new PlayerProgressState(), out string error);

            Assert.IsFalse(success, "Save should fail");
            Assert.IsNotNull(error);
            Assert.IsTrue(error.Contains("Restored main verification failed"), "Error should indicate verification failed");
            Assert.IsTrue(error.Contains("Critical Recovery Failure"), "Should be a critical recovery failure");
            Assert.IsFalse(error.Contains("Original main restored successfully"), "Should not report successful restore");

            Assert.IsTrue(File.Exists(oldPath), "oldMain must be preserved");
            Assert.IsTrue(File.Exists(tmpPath), "tmp must be preserved");
        }

        [Test]
        public void Storage_RepairMainFromBackup_PreservesBackup()
        {
            var fs = new FakeFileSystem();
            var storage = new JsonFileSaveStorage(m_TempDir, m_FileName, fs);

            string mainPath = Path.Combine(m_TempDir, m_FileName);
            string bakPath = mainPath + ".bak";
            string corruptPath = mainPath + ".corrupt";
            string tmpPath = mainPath + ".recovery.tmp";

            string corruptMainContent = "{ completely invalid main }";
            File.WriteAllText(mainPath, corruptMainContent);

            var env = new PlayerSaveEnvelope { schemaVersion = PlayerSaveValidator.CurrentSchemaVersion, payload = new PlayerSavePayload() };
            env.payload.gold = 999;
            env.checksum = JsonFileSaveStorage.ComputeChecksum(env);
            string backupContent = JsonUtility.ToJson(env);
            File.WriteAllText(bakPath, backupContent);

            bool success = storage.TryRepairMainFromBackup(out string error);

            Assert.IsTrue(success);
            Assert.IsNull(error);

            var loadMainResult = storage.LoadMain();
            Assert.AreEqual(SaveFileReadStatus.Success, loadMainResult.Status);
            Assert.AreEqual(999, loadMainResult.Envelope.payload.gold);

            Assert.AreEqual(backupContent, File.ReadAllText(bakPath));

            var loadBakResult = storage.LoadBackup();
            Assert.AreEqual(SaveFileReadStatus.Success, loadBakResult.Status);

            Assert.IsTrue(File.Exists(corruptPath));
            Assert.AreEqual(corruptMainContent, File.ReadAllText(corruptPath));
            Assert.IsFalse(File.Exists(tmpPath));
        }

        [Test]
        public void Storage_RepairMainFromBackup_VerifiesRecoveredMain()
        {
            var fs = new FakeFileSystem { CorruptMainAfterRecoveryCopy = true };
            var storage = new JsonFileSaveStorage(m_TempDir, m_FileName, fs);

            string mainPath = Path.Combine(m_TempDir, m_FileName);
            string bakPath = mainPath + ".bak";
            string corruptPath = mainPath + ".corrupt";
            string tmpPath = mainPath + ".recovery.tmp";

            string corruptMainContent = "{ completely invalid main 2 }";
            File.WriteAllText(mainPath, corruptMainContent);

            var env = new PlayerSaveEnvelope { schemaVersion = PlayerSaveValidator.CurrentSchemaVersion, payload = new PlayerSavePayload() };
            env.payload.gold = 777;
            env.checksum = JsonFileSaveStorage.ComputeChecksum(env);
            string backupContent = JsonUtility.ToJson(env);
            File.WriteAllText(bakPath, backupContent);

            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex(@"\[JsonFileSaveStorage\] Invalid JSON in .*test-save\.json: JSON parse error: Missing a name for object member\."));

            bool success = storage.TryRepairMainFromBackup(out string error);

            Assert.IsFalse(success);
            Assert.IsNotNull(error);
            Assert.IsTrue(error.Contains("Main verification after recovery failed"));

            Assert.AreEqual(backupContent, File.ReadAllText(bakPath));

            var loadBakResult = storage.LoadBackup();
            Assert.AreEqual(SaveFileReadStatus.Success, loadBakResult.Status);

            Assert.IsTrue(File.Exists(corruptPath));
            Assert.AreEqual(corruptMainContent, File.ReadAllText(corruptPath));
            Assert.IsTrue(File.Exists(tmpPath));

            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex(@"\[JsonFileSaveStorage\] Invalid JSON in .*test-save\.json: JSON parse error: Missing a name for object member\."));
            var loadMainResult = storage.LoadMain();
            Assert.AreNotEqual(SaveFileReadStatus.Success, loadMainResult.Status);
        }
    }
}
