#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using InTheArena.MainGame;
using InTheArena.Save;
using InTheArena.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InTheArena.Tests.Editor
{
    public sealed class FreePassSystemTests
    {
        private GameObject m_SaveManagerObject;
        private GameObject m_HeaderObject;
        private ItemData m_FreePassData;
        private Texture2D m_IconTexture;
        private Sprite m_DefaultTicketSprite;
        private Sprite m_FreePassTicketSprite;

        [TearDown]
        public void TearDown()
        {
            ClearSaveManagerInstance();

            if (m_SaveManagerObject != null)
            {
                UnityEngine.Object.DestroyImmediate(m_SaveManagerObject);
            }

            if (m_HeaderObject != null)
            {
                UnityEngine.Object.DestroyImmediate(m_HeaderObject);
            }

            if (m_FreePassData != null)
            {
                UnityEngine.Object.DestroyImmediate(m_FreePassData);
            }

            if (m_DefaultTicketSprite != null)
            {
                UnityEngine.Object.DestroyImmediate(m_DefaultTicketSprite);
            }

            if (m_FreePassTicketSprite != null)
            {
                UnityEngine.Object.DestroyImmediate(m_FreePassTicketSprite);
            }

            if (m_IconTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(m_IconTexture);
            }
        }

        [Test]
        public void ItemModel_FreePassUsesLobbyCategoryAndTimedDuration()
        {
            m_FreePassData = CreateFreePassData(1800);

            Assert.That(m_FreePassData.ItemType, Is.EqualTo(ItemType.FreePass));
            Assert.That(m_FreePassData.Category, Is.EqualTo(ItemCategory.Lobby));
            Assert.That(m_FreePassData.EffectDurationSeconds, Is.EqualTo(1800));
        }

        [Test]
        public void TryUseLobbyItem_ConsumesItemAndPersistsExpiration()
        {
            DateTime now = new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc);
            var clock = new FakeClock { UtcNow = now };
            var repository = new FakeSaveRepository();
            var state = new PlayerProgressState();
            state.SetItemCount(ItemType.FreePass, 2);

            SaveManager manager = CreateSaveManager(repository, clock, state);
            m_FreePassData = CreateFreePassData(1800);

            bool success = manager.TryUseLobbyItem(m_FreePassData, out string error);

            Assert.That(success, Is.True, error);
            Assert.That(manager.GetItemCount(ItemType.FreePass), Is.EqualTo(1));
            Assert.That(manager.FreePassExpirationUtcTicks, Is.EqualTo(now.AddMinutes(30).Ticks));
            Assert.That(repository.SavedState.FreePassExpirationUtcTicks, Is.EqualTo(now.AddMinutes(30).Ticks));
        }

        [Test]
        public void RemainingTime_UsesSavedUtcExpirationAfterRestartGap()
        {
            DateTime activatedAt = new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc);
            var clock = new FakeClock { UtcNow = activatedAt };
            string saveDirectory = Path.Combine(
                Path.GetTempPath(),
                $"FreePassSave_{Guid.NewGuid():N}");

            try
            {
                var savedState = new PlayerProgressState();
                savedState.SetFreePassExpirationUtcTicks(activatedAt.AddMinutes(30).Ticks);

                var writeRepository = new PlayerSaveRepository(
                    saveDirectory,
                    "player-data.json",
                    clock);
                bool saved = writeRepository.TrySave(savedState, out string error);
                Assert.That(saved, Is.True, error);

                clock.UtcNow = activatedAt.AddMinutes(10);
                var readRepository = new PlayerSaveRepository(
                    saveDirectory,
                    "player-data.json",
                    clock);
                SaveLoadResult loadResult = readRepository.LoadOrCreate(new PlayerProgressState());
                SaveManager manager = CreateSaveManager(readRepository, clock, loadResult.State);

                Assert.That(loadResult.Status, Is.EqualTo(SaveLoadStatus.Success));
                Assert.That(manager.GetRemainingFreePassTime(), Is.EqualTo(TimeSpan.FromMinutes(20)));
            }
            finally
            {
                if (Directory.Exists(saveDirectory))
                {
                    Directory.Delete(saveDirectory, true);
                }
            }
        }

        [Test]
        public void TrySpendHeart_ActiveFreePassKeepsTicketAndContinuesRecovery()
        {
            DateTime now = new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc);
            var clock = new FakeClock { UtcNow = now };
            var repository = new FakeSaveRepository();
            var state = new PlayerProgressState();
            state.SetHearts(3);
            state.SetLastHeartRecoveryUtcTicks(now.AddSeconds(-SaveManager.HeartRecoverySeconds).Ticks);
            state.SetFreePassExpirationUtcTicks(now.AddMinutes(30).Ticks);

            SaveManager manager = CreateSaveManager(repository, clock, state);

            bool enteredStage = manager.TrySpendHeart();

            Assert.That(enteredStage, Is.True);
            Assert.That(manager.Hearts, Is.EqualTo(4));
            Assert.That(repository.SaveCallCount, Is.EqualTo(1));
        }

        [Test]
        public void TrySpendHeart_ExpiredFreePassConsumesTicket()
        {
            DateTime now = new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc);
            var clock = new FakeClock { UtcNow = now };
            var state = new PlayerProgressState();
            state.SetHearts(2);
            state.SetLastHeartRecoveryUtcTicks(now.Ticks);
            state.SetFreePassExpirationUtcTicks(now.AddSeconds(-1).Ticks);

            SaveManager manager = CreateSaveManager(new FakeSaveRepository(), clock, state);

            bool enteredStage = manager.TrySpendHeart();

            Assert.That(enteredStage, Is.True);
            Assert.That(manager.Hearts, Is.EqualTo(1));
        }

        [Test]
        public void TryUseLobbyItem_SaveFailureKeepsItemAndExpiration()
        {
            DateTime now = new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc);
            var clock = new FakeClock { UtcNow = now };
            var repository = new FakeSaveRepository { FailNextSave = true };
            var state = new PlayerProgressState();
            state.SetItemCount(ItemType.FreePass, 1);

            SaveManager manager = CreateSaveManager(repository, clock, state);
            m_FreePassData = CreateFreePassData(1800);

            bool success = manager.TryUseLobbyItem(m_FreePassData, out _);

            Assert.That(success, Is.False);
            Assert.That(manager.GetItemCount(ItemType.FreePass), Is.EqualTo(1));
            Assert.That(manager.FreePassExpirationUtcTicks, Is.Zero);
        }

        [Test]
        public void LobbyHeader_ActiveFreePassShowsPassTimerAndHidesRecoveryTimer()
        {
            DateTime now = new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc);
            var state = new PlayerProgressState();
            state.SetHearts(3);
            state.SetLastHeartRecoveryUtcTicks(now.Ticks);
            state.SetFreePassExpirationUtcTicks(now.AddMinutes(30).Ticks);
            CreateSaveManager(
                new FakeSaveRepository(),
                new FakeClock { UtcNow = now },
                state);

            UI_LobbyHeader header = CreateLobbyHeader(
                out TMP_Text ticketText,
                out TMP_Text recoveryTimerText,
                out RectTransform recoveryTimerBox,
                out Image ticketImage);

            header.Refresh();

            Assert.That(ticketText.text, Is.EqualTo("30:00"));
            Assert.That(recoveryTimerText.text, Is.Empty);
            Assert.That(recoveryTimerBox.anchoredPosition.y, Is.EqualTo(0f));
            Assert.That(ticketImage.sprite, Is.SameAs(m_FreePassTicketSprite));
        }

        [Test]
        public void LobbyHeader_InactiveFreePassShowsTicketCountAndRecoveryTimer()
        {
            DateTime now = new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc);
            var state = new PlayerProgressState();
            state.SetHearts(3);
            state.SetLastHeartRecoveryUtcTicks(now.Ticks);
            CreateSaveManager(
                new FakeSaveRepository(),
                new FakeClock { UtcNow = now },
                state);

            UI_LobbyHeader header = CreateLobbyHeader(
                out TMP_Text ticketText,
                out TMP_Text recoveryTimerText,
                out RectTransform recoveryTimerBox,
                out Image ticketImage);

            header.Refresh();

            Assert.That(ticketText.text, Is.EqualTo("3/5"));
            Assert.That(recoveryTimerText.text, Is.EqualTo("05:00"));
            Assert.That(recoveryTimerBox.anchoredPosition.y, Is.EqualTo(-50f));
            Assert.That(ticketImage.sprite, Is.SameAs(m_DefaultTicketSprite));
        }

        [Test]
        public void Version4Checksum_DoesNotIncludeFreePassExpiration()
        {
            var original = new PlayerSaveEnvelope
            {
                schemaVersion = 4,
                revision = 7,
                savedAtUtcTicks = 12345,
                payload = new PlayerSavePayload
                {
                    gold = 100,
                    itemCounts = new[]
                    {
                        new ItemCountPayload { itemType = (int)ItemType.Meteor, count = 2 }
                    }
                }
            };
            var withInjectedFreePass = new PlayerSaveEnvelope
            {
                schemaVersion = original.schemaVersion,
                revision = original.revision,
                savedAtUtcTicks = original.savedAtUtcTicks,
                payload = new PlayerSavePayload
                {
                    gold = 100,
                    freePassExpirationUtcTicks = DateTime.MaxValue.Ticks,
                    itemCounts = new[]
                    {
                        new ItemCountPayload { itemType = (int)ItemType.Meteor, count = 2 }
                    }
                }
            };

            Assert.That(
                JsonFileSaveStorage.ComputeChecksum(withInjectedFreePass),
                Is.EqualTo(JsonFileSaveStorage.ComputeChecksum(original)));
        }

        [Test]
        public void Validator_RemovesFreePassValueFromOlderSchema()
        {
            DateTime now = new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc);
            var envelope = new PlayerSaveEnvelope
            {
                schemaVersion = 4,
                payload = new PlayerSavePayload
                {
                    freePassExpirationUtcTicks = now.AddHours(1).Ticks
                }
            };

            bool valid = PlayerSaveValidator.ValidateAndNormalize(
                envelope,
                new FakeClock { UtcNow = now });

            Assert.That(valid, Is.True);
            Assert.That(envelope.payload.freePassExpirationUtcTicks, Is.Zero);
        }

        /// <summary>
        /// 테스트용 자유 이용권 ItemData를 지정한 지속시간으로 생성합니다.
        /// </summary>
        private static ItemData CreateFreePassData(int durationSeconds)
        {
            ItemData itemData = ScriptableObject.CreateInstance<ItemData>();
            SetPrivateField(itemData, "m_ItemType", ItemType.FreePass);
            SetPrivateField(itemData, "m_Category", ItemCategory.Lobby);
            SetPrivateField(itemData, "m_EffectDurationSeconds", durationSeconds);
            return itemData;
        }

        /// <summary>
        /// 테스트 저장소와 시계를 사용하는 SaveManager를 생성합니다.
        /// </summary>
        private SaveManager CreateSaveManager(
            IPlayerSaveRepository repository,
            IClock clock,
            PlayerProgressState state)
        {
            ClearSaveManagerInstance();
            m_SaveManagerObject = new GameObject("FreePassTestSaveManager");
            SaveManager manager = m_SaveManagerObject.AddComponent<SaveManager>();
            manager.InitializeForTests(repository, clock, state);

            PropertyInfo property = typeof(SaveManager).GetProperty(
                "Instance",
                BindingFlags.Static | BindingFlags.Public);
            property?.SetValue(null, manager);

            return manager;
        }

        /// <summary>
        /// 자유 이용권 표시 검증에 필요한 최소 로비 헤더 UI를 생성합니다.
        /// </summary>
        private UI_LobbyHeader CreateLobbyHeader(
            out TMP_Text ticketText,
            out TMP_Text recoveryTimerText,
            out RectTransform recoveryTimerBox,
            out Image ticketImage)
        {
            m_HeaderObject = new GameObject(
                "FreePassTestLobbyHeader",
                typeof(RectTransform),
                typeof(CanvasGroup));
            UI_LobbyHeader header = m_HeaderObject.AddComponent<UI_LobbyHeader>();

            ticketText = CreateText("TicketText", m_HeaderObject.transform);
            recoveryTimerText = CreateText("RecoveryTimerText", m_HeaderObject.transform);
            TMP_Text goldText = CreateText("GoldText", m_HeaderObject.transform);
            TMP_Text starText = CreateText("StarText", m_HeaderObject.transform);

            var timerBoxObject = new GameObject("TimerBox", typeof(RectTransform));
            timerBoxObject.transform.SetParent(m_HeaderObject.transform, false);
            recoveryTimerBox = timerBoxObject.GetComponent<RectTransform>();

            var ticketImageObject = new GameObject(
                "TicketImage",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            ticketImageObject.transform.SetParent(m_HeaderObject.transform, false);
            ticketImage = ticketImageObject.GetComponent<Image>();

            CreateTicketSprites();
            ticketImage.sprite = m_DefaultTicketSprite;

            SetPrivateField(header, "m_HeartText", ticketText);
            SetPrivateField(header, "m_TimerText", recoveryTimerText);
            SetPrivateField(header, "m_TimerBox", recoveryTimerBox);
            SetPrivateField(header, "m_GoldText", goldText);
            SetPrivateField(header, "m_StarText", starText);
            SetPrivateField(header, "m_TicketImage", ticketImage);
            SetPrivateField(header, "m_FreePassTicketSprite", m_FreePassTicketSprite);

            return header;
        }

        /// <summary>
        /// 로비 헤더 테스트에 사용할 TextMeshPro 텍스트를 생성합니다.
        /// </summary>
        private static TMP_Text CreateText(string objectName, Transform parent)
        {
            var textObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            return textObject.GetComponent<TMP_Text>();
        }

        /// <summary>
        /// 기본 티켓과 자유 이용권 상태를 구분할 테스트 스프라이트를 생성합니다.
        /// </summary>
        private void CreateTicketSprites()
        {
            m_IconTexture = new Texture2D(4, 2);
            m_DefaultTicketSprite = Sprite.Create(
                m_IconTexture,
                new Rect(0f, 0f, 2f, 2f),
                new Vector2(0.5f, 0.5f));
            m_FreePassTicketSprite = Sprite.Create(
                m_IconTexture,
                new Rect(2f, 0f, 2f, 2f),
                new Vector2(0.5f, 0.5f));
        }

        /// <summary>
        /// ScriptableObject의 직렬화 필드를 테스트 값으로 설정합니다.
        /// </summary>
        private static void SetPrivateField<T>(object target, string fieldName, T value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        /// <summary>
        /// 테스트 사이에 정적 SaveManager 참조가 남지 않도록 초기화합니다.
        /// </summary>
        private static void ClearSaveManagerInstance()
        {
            PropertyInfo property = typeof(SaveManager).GetProperty(
                "Instance",
                BindingFlags.Static | BindingFlags.Public);

            property?.SetValue(null, null);
        }
    }
}
#endif
