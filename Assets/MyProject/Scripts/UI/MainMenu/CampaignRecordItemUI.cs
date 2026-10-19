using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColmeiaStudios.SoMaisUmPedido
{
    /// <summary>Reusable three-field item in the completed-campaign records list.</summary>
    public sealed class CampaignRecordItemUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text dateText;
        [SerializeField] private TMP_Text playTimeText;
        [SerializeField] private TMP_Text revenueText;
        [SerializeField] private Button button;
        private Action selected;

        private void Awake()
        {
            button?.onClick.AddListener(Select);
            AudioManager.Instance?.RegisterButton(button);
        }
        private void OnDestroy() => button?.onClick.RemoveListener(Select);

        public void Bind(CampaignHistoryRecord record, Action onSelected)
        {
            if (record == null) return;
            selected = onSelected;
            if (DateTime.TryParse(record.completedAtUtc, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out DateTime completed))
                SetText(dateText, completed.ToLocalTime().ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("pt-BR")));
            else SetText(dateText, "—");
            SetText(playTimeText, CampaignSummaryUI.FormatDuration(record.playSeconds));
            SetText(revenueText, record.revenue.ToString("C2", CultureInfo.GetCultureInfo("pt-BR")));
        }

        private void Select() => selected?.Invoke();
        private static void SetText(TMP_Text label, string value) { if (label != null) label.text = value; }
    }
}
