using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ColmeiaStudios.SoMaisUmPedido
{
    [Serializable]
    public sealed class CampaignSaveData
    {
        public int version = CampaignSaveSystem.CurrentVersion;
        public string campaignId;
        public float campaignPlaySeconds;
        public int currentDay = 1;
        public float elapsedSeconds;
        public float balance;
        public int reputation;
        public int dailyDeliveryGoal;
        public int completedDailyDeliveries;
        public float openingBalance;
        public float dailyMoneyEarned;
        public MatchCycleState cycleState;
        public PlayerRole playerOneRole;
        public PlayerRole playerTwoRole;
        public List<string> paidOrderIds = new List<string>();
        public List<string> purchasedUpgradeIds = new List<string>();
        public List<CampaignOrderData> orders = new List<CampaignOrderData>();
        public List<CampaignPlayerData> players = new List<CampaignPlayerData>();
        public int generatedOrderSequence;
        public float transitionElapsed;
        public float voteCountdownRemaining;
        public int lastRoleSwapVoteDay;
        public bool lastRoleSwapSucceeded;
        public RoleSwapVote playerOneVote;
        public RoleSwapVote playerTwoVote;
        public List<CampaignDayData> dailyResults = new List<CampaignDayData>();
    }

    [Serializable]
    public sealed class CampaignOrderData
    {
        public string id;
        public string displayName;
        public List<string> ingredientIds = new List<string>();
        public LunchboxType lunchboxType;
        public float baseValue;
        public float deadlineSeconds;
        public float remainingSeconds;
        public float activatedAt;
        public float pickedUpAt;
        public int dayNumber;
        public string destinationHouseId;
        public OrderState state;
        public int closedVisualVariant = -1;
    }

    [Serializable]
    public sealed class CampaignPlayerData
    {
        public PlayerRole role;
        public string selectedOrderId;
        public LunchboxType selectedLunchboxType;
        public List<string> ingredients = new List<string>();
        public List<string> carriedOrderIds = new List<string>();
        public string selectedDeliveryOrderId;
        public string selectedPickupOrderId;
        public string carriedPreparedOrderId;
        public int carriedClosedVisualVariant = -1;
        public string preparationMealboxOrderId;
        public float preparationElapsedSeconds;
    }

    [Serializable]
    public sealed class CampaignDayData
    {
        public int day;
        public int deliveries;
        public int goal;
        public int expired;
        public float openingBalance;
        public float closingBalance;
        public float moneyEarned;
        public int reputation;
    }

    [Serializable]
    public sealed class CampaignHistoryRecord
    {
        public string id;
        public string completedAtUtc;
        public float playSeconds;
        public float revenue;
        public int deliveries;
        public int reputation;
        public bool victory;
    }

    [Serializable]
    public sealed class CampaignHistoryFile
    {
        public int version = CampaignSaveSystem.HistoryVersion;
        public List<CampaignHistoryRecord> records = new List<CampaignHistoryRecord>();
    }

    /// <summary>Versioned local campaign storage, independent from audio preferences.</summary>
    public static class CampaignSaveSystem
    {
        private const string SaveFileName = "so-mais-um-pedido-campaign.json";
        private const string HistoryFileName = "so-mais-um-pedido-history.json";
        public const int CurrentVersion = 2;
        public const int HistoryVersion = 1;
        private static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);
        private static string HistoryPath => Path.Combine(Application.persistentDataPath, HistoryFileName);
        private static bool continueRequested;
        private static bool historyInvalidLogged;
        private static string activeCampaignId;

        public static bool HasValidSave => TryLoad(out CampaignSaveData save) && !IsCampaignCompleted(save.campaignId);
        public static void RequestContinue() => continueRequested = true;
        public static void CancelContinue() => continueRequested = false;
        public static void BeginNewCampaign() => activeCampaignId = Guid.NewGuid().ToString("N");
        public static string GetOrCreateActiveCampaignId()
        {
            if (string.IsNullOrWhiteSpace(activeCampaignId)) BeginNewCampaign();
            return activeCampaignId;
        }
        public static void EndActiveCampaign() => activeCampaignId = null;
        public static bool ConsumeContinueRequest(out CampaignSaveData data)
        {
            bool requested = continueRequested;
            continueRequested = false;
            data = null;
            if (!requested || !TryLoad(out data) || IsCampaignCompleted(data.campaignId)) return false;
            activeCampaignId = data.campaignId;
            return true;
        }

        public static bool Save(CampaignSaveData data)
        {
            if (data == null) return false;
            try
            {
                data.version = CurrentVersion;
                string path = SavePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temporaryPath = path + ".tmp";
                File.WriteAllText(temporaryPath, JsonUtility.ToJson(data));
                if (File.Exists(path)) File.Replace(temporaryPath, path, null);
                else File.Move(temporaryPath, path);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"Could not write campaign save: {exception.Message}");
                return false;
            }
        }

        public static bool TryLoad(out CampaignSaveData data)
        {
            data = null;
            if (!File.Exists(SavePath)) return false;
            try
            {
                CampaignSaveData loaded = JsonUtility.FromJson<CampaignSaveData>(File.ReadAllText(SavePath));
                if (loaded == null || (loaded.version != 1 && loaded.version != CurrentVersion) || loaded.currentDay < 1 ||
                    !Enum.IsDefined(typeof(PlayerRole), loaded.playerOneRole) ||
                    !Enum.IsDefined(typeof(PlayerRole), loaded.playerTwoRole) || loaded.playerOneRole == loaded.playerTwoRole ||
                    loaded.players == null || loaded.players.Count != 2 ||
                    loaded.players[0] == null || loaded.players[1] == null ||
                    loaded.players[0].carriedClosedVisualVariant < -1 || loaded.players[0].carriedClosedVisualVariant > 1 ||
                    loaded.players[1].carriedClosedVisualVariant < -1 || loaded.players[1].carriedClosedVisualVariant > 1 ||
                    loaded.players[0].role != loaded.playerOneRole || loaded.players[1].role != loaded.playerTwoRole ||
                    float.IsNaN(loaded.elapsedSeconds) || float.IsInfinity(loaded.elapsedSeconds) || loaded.elapsedSeconds < 0f ||
                    float.IsNaN(loaded.balance) || float.IsInfinity(loaded.balance) || loaded.balance < 0f ||
                    loaded.reputation < 0 || loaded.reputation > 5 || loaded.orders == null || loaded.dailyResults == null ||
                    float.IsNaN(loaded.campaignPlaySeconds) || float.IsInfinity(loaded.campaignPlaySeconds) || loaded.campaignPlaySeconds < 0f)
                    return false;
                bool hasCampaignId = Guid.TryParse(loaded.campaignId, out _);
                bool migrate = loaded.version == 1 || !hasCampaignId;
                if (!hasCampaignId) loaded.campaignId = Guid.NewGuid().ToString("N");
                if (loaded.version == 1) { loaded.campaignPlaySeconds = 0f; loaded.version = CurrentVersion; }
                var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (CampaignOrderData order in loaded.orders)
                {
                    if (order == null || string.IsNullOrWhiteSpace(order.id) || !ids.Add(order.id) ||
                        !Enum.IsDefined(typeof(LunchboxType), order.lunchboxType) ||
                        !Enum.IsDefined(typeof(OrderState), order.state) ||
                        float.IsNaN(order.deadlineSeconds) || float.IsInfinity(order.deadlineSeconds) || order.deadlineSeconds <= 0f ||
                        float.IsNaN(order.remainingSeconds) || float.IsInfinity(order.remainingSeconds) || order.remainingSeconds < 0f ||
                        order.closedVisualVariant < -1 || order.closedVisualVariant > 1)
                        return false;
                }
                if (migrate && !Save(loaded))
                    Debug.LogWarning("Campaign save migration could not be written; loaded data remains available for this session.");
                data = loaded;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Campaign save is invalid and cannot be continued: {exception.Message}");
                return false;
            }
        }

        public static void Delete()
        {
            try { if (File.Exists(SavePath)) File.Delete(SavePath); }
            catch (Exception exception) { Debug.LogError($"Could not delete campaign save: {exception.Message}"); }
            continueRequested = false;
        }

        public static IReadOnlyList<CampaignHistoryRecord> LoadHistory()
        {
            return TryLoadHistory(out IReadOnlyList<CampaignHistoryRecord> records)
                ? records : Array.Empty<CampaignHistoryRecord>();
        }

        public static bool TryLoadHistory(out IReadOnlyList<CampaignHistoryRecord> records)
        {
            records = Array.Empty<CampaignHistoryRecord>();
            if (!File.Exists(HistoryPath)) return true;
            if (!TryReadHistory(out CampaignHistoryFile file)) return false;
            records = file.records.OrderByDescending(item => item.completedAtUtc, StringComparer.Ordinal)
                .ThenBy(item => item.id, StringComparer.Ordinal).ToList();
            return true;
        }

        public static bool RecordCompletedCampaign(CampaignHistoryRecord record)
        {
            if (!IsValidHistoryRecord(record))
            {
                Debug.LogError("Campaign history record is invalid; it was not written.");
                return false;
            }
            if (!TryReadHistory(out CampaignHistoryFile file))
            {
                if (File.Exists(HistoryPath))
                {
                    Debug.LogError("Campaign history exists but is invalid; refusing to overwrite it.");
                    return false;
                }
                file = new CampaignHistoryFile();
            }
            if (file.records.Any(existing => existing != null && existing.id == record.id)) return true;
            file.records.Add(record);
            file.records = file.records.OrderByDescending(item => item.completedAtUtc, StringComparer.Ordinal).ToList();
            try
            {
                string temporaryPath = HistoryPath + ".tmp";
                Directory.CreateDirectory(Path.GetDirectoryName(HistoryPath));
                File.WriteAllText(temporaryPath, JsonUtility.ToJson(file));
                if (File.Exists(HistoryPath)) File.Replace(temporaryPath, HistoryPath, null);
                else File.Move(temporaryPath, HistoryPath);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"Could not write campaign history: {exception.Message}");
                return false;
            }
        }

        public static bool TryGetHistoryRecord(string id, out CampaignHistoryRecord record)
        {
            record = null;
            if (string.IsNullOrWhiteSpace(id)) return false;
            CampaignHistoryRecord found = LoadHistory().FirstOrDefault(item => item != null && item.id == id);
            if (found == null) return false;
            record = found;
            return true;
        }

        private static bool TryReadHistory(out CampaignHistoryFile file)
        {
            file = null;
            if (!File.Exists(HistoryPath)) return false;
            try
            {
                CampaignHistoryFile loaded = JsonUtility.FromJson<CampaignHistoryFile>(File.ReadAllText(HistoryPath));
                if (loaded == null || loaded.version != HistoryVersion || loaded.records == null ||
                    loaded.records.Any(record => !IsValidHistoryRecord(record)) ||
                    loaded.records.Select(record => record.id).Distinct(StringComparer.Ordinal).Count() != loaded.records.Count)
                {
                    if (!historyInvalidLogged)
                    {
                        Debug.LogWarning("Campaign history has an unsupported version or invalid records; it remains untouched.");
                        historyInvalidLogged = true;
                    }
                    return false;
                }
                historyInvalidLogged = false;
                file = loaded;
                return true;
            }
            catch (Exception exception)
            {
                if (!historyInvalidLogged)
                {
                    Debug.LogWarning($"Campaign history could not be loaded and remains untouched: {exception.Message}");
                    historyInvalidLogged = true;
                }
                return false;
            }
        }

        private static bool IsValidHistoryRecord(CampaignHistoryRecord record)
        {
            return record != null && Guid.TryParse(record.id, out _) &&
                   DateTime.TryParse(record.completedAtUtc, System.Globalization.CultureInfo.InvariantCulture,
                       System.Globalization.DateTimeStyles.RoundtripKind, out _) &&
                   !float.IsNaN(record.playSeconds) && !float.IsInfinity(record.playSeconds) && record.playSeconds >= 0f &&
                   !float.IsNaN(record.revenue) && !float.IsInfinity(record.revenue) && record.revenue >= 0f &&
                   record.deliveries >= 0 && record.reputation >= 0 && record.reputation <= 5;
        }

        private static bool IsCampaignCompleted(string campaignId)
        {
            return !string.IsNullOrWhiteSpace(campaignId) &&
                   LoadHistory().Any(record => record != null && record.id == campaignId);
        }
    }
}
