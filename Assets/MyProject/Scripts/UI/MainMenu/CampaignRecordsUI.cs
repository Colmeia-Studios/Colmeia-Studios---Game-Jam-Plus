using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace ColmeiaStudios.SoMaisUmPedido
{
    /// <summary>Lists completed campaigns and opens a summary from the selected historical snapshot.</summary>
    public sealed class CampaignRecordsUI : MonoBehaviour
    {
        [SerializeField] private GameObject recordsPanel;
        [SerializeField] private GameObject listPanel;
        [SerializeField] private Transform content;
        [SerializeField] private CampaignRecordItemUI itemPrefab;
        [SerializeField] private TMP_Text emptyMessage;
        [SerializeField] private CampaignSummaryUI summaryUI;

        private readonly List<GameObject> spawnedItems = new List<GameObject>();

        private void Awake()
        {
            if (recordsPanel != null) recordsPanel.SetActive(false);
        }

        private void OnDestroy() => ClearItems();

        public void OpenRecords()
        {
            if (recordsPanel == null || content == null || itemPrefab == null || summaryUI == null)
            {
                Debug.LogError("CampaignRecordsUI requires Records Panel, Content, Item Prefab and CampaignSummaryUI references.", this);
                return;
            }
            recordsPanel.SetActive(true);
            if (listPanel != null) listPanel.SetActive(true);
            summaryUI.Hide();
            Rebuild();
        }

        public void CloseRecords() => recordsPanel?.SetActive(false);

        private void Rebuild()
        {
            ClearItems();
            bool loaded = CampaignSaveSystem.TryLoadHistory(out IReadOnlyList<CampaignHistoryRecord> records);
            if (!loaded)
            {
                if (emptyMessage != null)
                {
                    emptyMessage.text = "Could not read campaign history. The file was preserved.";
                    emptyMessage.gameObject.SetActive(true);
                }
                return;
            }
            bool empty = records.Count == 0;
            if (emptyMessage != null)
            {
                emptyMessage.text = "No completed campaigns yet.";
                emptyMessage.gameObject.SetActive(empty);
            }
            foreach (CampaignHistoryRecord record in records)
            {
                CampaignRecordItemUI item = Instantiate(itemPrefab, content);
                item.Bind(record, () => ShowRecord(record));
                spawnedItems.Add(item.gameObject);
            }
        }

        private void ShowRecord(CampaignHistoryRecord record)
        {
            if (listPanel != null) listPanel.SetActive(false);
            summaryUI.Show(record, ReturnToList, false);
        }

        private void ReturnToList()
        {
            summaryUI.Hide();
            if (listPanel != null) listPanel.SetActive(true);
        }

        private void ClearItems()
        {
            for (int i = spawnedItems.Count - 1; i >= 0; i--)
                if (spawnedItems[i] != null) Destroy(spawnedItems[i]);
            spawnedItems.Clear();
        }
    }
}
