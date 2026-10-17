using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ColmeiaStudios.SoMaisUmPedido
{
    public class KifomePhoneUI : MonoBehaviour
    {
        private static readonly HashSet<KifomePhoneUI> CursorOwners = new HashSet<KifomePhoneUI>();
        private static readonly HashSet<KifomePhoneUI> OpenPhones = new HashSet<KifomePhoneUI>();
        private static EventSystem navigationEventSystem;
        private static bool restoreNavigationEvents;
        private static CursorLockMode restoreCursorLockState;
        private static bool restoreCursorVisibility;
        private static bool cursorStateCaptured;

        [Header("Owner")] [SerializeField] private PlayerController owner;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private OrderManager orderManager;

        [Header("Canvas and views")] [SerializeField]
        private Canvas phoneCanvas;

        [SerializeField] private GraphicRaycaster phoneRaycaster;
        [SerializeField] private GameObject phonePanel;

        [Header("Shared match status")] [SerializeField]
        private TMP_Text clockText;

        [SerializeField] private TMP_Text balanceText;
        [SerializeField] private TMP_Text reputationText;
        [SerializeField] private TMP_Text dailyGoalText;

        [Header("Controls")] [SerializeField] private bool mousePointerAllowed = true;

        protected readonly List<Button> NavigableButtons = new List<Button>();
        private bool initialized;
        private bool restoreRaycasterEnabled = true;
        private int focusIndex;
        private float navigationReadyAt;
        private Button focusedButton;

        protected PlayerController Owner => owner;
        protected GameManager Match => gameManager;
        protected OrderManager Orders => orderManager;
        protected virtual PlayerRole RequiredRole => PlayerRole.Cook;
        protected virtual bool HasRequiredRoleReferences => true;

        protected virtual void LogMissingRoleReferences()
        {
            Debug.LogError($"{GetType().Name} is missing required references for its primary view.", this);
        }

        public bool IsOpen => phonePanel != null && phonePanel.activeSelf;
        public PlayerController BoundPlayer => owner;
        public PlayerRole PhoneRole => RequiredRole;

        protected virtual void Awake()
        {
            if (phonePanel == null)
            {
                return;
            }

            if (phonePanel == gameObject || transform.IsChildOf(phonePanel.transform))
            {
                Debug.LogError(
                    $"{GetType().Name} must be on an active object outside its phone panel hierarchy.",
                    this);
                enabled = false;
                return;
            }

            phonePanel.SetActive(false);
        }

        protected virtual IEnumerator Start()
        {
            yield return null;

            gameManager ??= GameManager.Instance;
            orderManager ??= gameManager != null ? gameManager.Orders : null;
            if (owner == null || owner.AssignedRole != RequiredRole)
            {
                Debug.LogError(
                    $"{GetType().Name} requires a {RequiredRole} PlayerController assigned to Owner.",
                    this);
                enabled = false;
                yield break;
            }

            List<string> missingSharedReferences = new List<string>();
            if (gameManager == null) missingSharedReferences.Add(nameof(gameManager));
            if (orderManager == null) missingSharedReferences.Add(nameof(orderManager));
            if (phoneCanvas == null) missingSharedReferences.Add(nameof(phoneCanvas));
            if (phonePanel == null) missingSharedReferences.Add(nameof(phonePanel));
            if (clockText == null) missingSharedReferences.Add(nameof(clockText));
            if (balanceText == null) missingSharedReferences.Add(nameof(balanceText));
            if (reputationText == null) missingSharedReferences.Add(nameof(reputationText));
            if (dailyGoalText == null) missingSharedReferences.Add(nameof(dailyGoalText));
            if (owner.GetCamera() == null) missingSharedReferences.Add("owner camera");
            if (missingSharedReferences.Count > 0)
            {
                Debug.LogError(
                    $"{GetType().Name} is missing required shared references: {string.Join(", ", missingSharedReferences)}.",
                    this);
                enabled = false;
                yield break;
            }

            if (!HasRequiredRoleReferences)
            {
                LogMissingRoleReferences();
                enabled = false;
                yield break;
            }

            phoneRaycaster ??= phoneCanvas.GetComponent<GraphicRaycaster>();
            if (phoneRaycaster == null)
            {
                Debug.LogError($"{GetType().Name} requires a GraphicRaycaster on its Canvas.", this);
                enabled = false;
                yield break;
            }

            restoreRaycasterEnabled = phoneRaycaster.enabled;
            phoneCanvas.worldCamera = owner.GetCamera();
            initialized = true;
            BindRoleListeners();
            ShowPrimaryView();
            Subscribe();
            RefreshAll();
            SetOpen(false);
        }

        protected virtual void OnEnable()
        {
            if (!initialized)
            {
                return;
            }

            BindRoleListeners();
            Subscribe();
            RefreshAll();
        }

        protected virtual void OnDisable()
        {
            Unsubscribe();
            UnbindRoleListeners();
            SetOpen(false);
        }

        protected virtual void OnDestroy()
        {
            CursorOwners.Remove(this);
            OpenPhones.Remove(this);
            ApplyCursorState();
            ApplyNavigationState();
        }

        public void BindGameplay(PlayerController player, GameManager match)
        {
            Unsubscribe();
            owner = player;
            gameManager = match;
            orderManager = gameManager != null ? gameManager.Orders : null;

            if (initialized)
            {
                if (owner == null || owner.AssignedRole != RequiredRole)
                {
                    Debug.LogError(
                        $"{GetType().Name} cannot bind a player whose role is not {RequiredRole}.",
                        this);
                    SetOpen(false);
                    return;
                }

                phoneCanvas.worldCamera = owner.GetCamera();
                Subscribe();
                RefreshAll();
            }
        }

        public void Toggle()
        {
            SetOpen(!IsOpen);
        }

        public void ClosePhone()
        {
            SetOpen(false);
        }

        public void SetMousePointerEnabled(bool pointerAllowed)
        {
            mousePointerAllowed = pointerAllowed;
            UpdateCursorOwnership(IsOpen);
            UpdateRaycaster(IsOpen);
            ApplyCursorState();
        }

        public void HandlePlayerInput(Vector2 move, bool confirm, bool cancel)
        {
            if (!IsOpen)
            {
                return;
            }

            if (cancel)
            {
                ClosePhone();
                return;
            }

            float axis = Mathf.Abs(move.y) > Mathf.Abs(move.x) ? move.y : move.x;
            if (Mathf.Abs(axis) > 0.65f && Time.unscaledTime >= navigationReadyAt)
            {
                navigationReadyAt = Time.unscaledTime + 0.22f;
                RebuildNavigation();
                if (NavigableButtons.Count > 0)
                {
                    int direction = axis > 0f ? -1 : 1;
                    focusIndex = (focusIndex + direction + NavigableButtons.Count) % NavigableButtons.Count;
                    SetFocusedButton(NavigableButtons[focusIndex]);
                }
            }

            if (confirm)
            {
                RebuildNavigation();
                if (NavigableButtons.Count > 0)
                {
                    int index = Mathf.Clamp(focusIndex, 0, NavigableButtons.Count - 1);
                    Button button = NavigableButtons[index];
                    if (button != null && button.interactable)
                    {
                        button.onClick.Invoke();
                    }
                }
            }
        }

        public void ShowPrimaryView()
        {
            ShowPrimaryViewContent();
            RefreshRoleOrders();
            RefreshRoleDetails();
            focusIndex = 0;
            RebuildNavigation();
        }

        protected virtual void ShowPrimaryViewContent()
        {
        }

        protected virtual void BindRoleListeners()
        {
        }

        protected virtual void UnbindRoleListeners()
        {
        }

        protected virtual void RefreshRoleOrders()
        {
        }

        protected virtual void RefreshRoleDetails()
        {
        }

        protected virtual void AddRoleButtonsToNavigation()
        {
        }

        protected void RebuildNavigation()
        {
            NavigableButtons.Clear();
            AddRoleButtonsToNavigation();

            if (focusedButton != null && !NavigableButtons.Contains(focusedButton))
            {
                SetFocusedButton(null);
            }

            if (focusIndex >= NavigableButtons.Count)
            {
                focusIndex = Mathf.Max(0, NavigableButtons.Count - 1);
            }

            if (IsOpen && focusedButton == null && NavigableButtons.Count > 0)
            {
                focusIndex = Mathf.Clamp(focusIndex, 0, NavigableButtons.Count - 1);
                SetFocusedButton(NavigableButtons[focusIndex]);
            }
        }

        protected void AddNavigableButton(Button button)
        {
            if (button != null && button.isActiveAndEnabled && button.interactable)
            {
                NavigableButtons.Add(button);
            }
        }

        protected void RefreshNavigationIfOpen()
        {
            if (IsOpen)
            {
                RebuildNavigation();
            }
        }

        private void SetOpen(bool open)
        {
            if (phonePanel == null || IsOpen == open)
            {
                return;
            }

            if (open)
            {
                ShowPrimaryView();
                if (CursorOwners.Count == 0)
                {
                    restoreCursorLockState = Cursor.lockState;
                    restoreCursorVisibility = Cursor.visible;
                    cursorStateCaptured = true;
                }

                OpenPhones.Add(this);
            }
            else
            {
                OpenPhones.Remove(this);
            }

            phonePanel.SetActive(open);
            UpdateCursorOwnership(open);
            UpdateRaycaster(open);
            if (open)
            {
                RebuildNavigation();
            }
            else
            {
                SetFocusedButton(null);
            }

            ApplyNavigationState();
            ApplyCursorState();
        }

        private void Subscribe()
        {
            if (!initialized)
            {
                return;
            }

            if (orderManager != null)
            {
                orderManager.OrdersChanged -= OnOrdersChanged;
                orderManager.OrdersChanged += OnOrdersChanged;
            }

            if (gameManager != null)
            {
                gameManager.MatchStateChanged -= OnMatchStateChanged;
                gameManager.MatchStateChanged += OnMatchStateChanged;
                gameManager.ClockChanged -= OnClockChanged;
                gameManager.ClockChanged += OnClockChanged;
            }

            if (owner != null)
            {
                owner.PreparationChanged -= OnPreparationChanged;
                owner.PreparationChanged += OnPreparationChanged;
            }
        }

        private void Unsubscribe()
        {
            if (orderManager != null) orderManager.OrdersChanged -= OnOrdersChanged;
            if (gameManager != null)
            {
                gameManager.MatchStateChanged -= OnMatchStateChanged;
                gameManager.ClockChanged -= OnClockChanged;
            }

            if (owner != null)
            {
                owner.PreparationChanged -= OnPreparationChanged;
            }
        }

        private void OnOrdersChanged()
        {
            RefreshRoleOrders();
            RefreshRoleDetails();
            RefreshNavigationIfOpen();
        }

        private void OnMatchStateChanged()
        {
            RefreshSharedHeader();
        }

        private void OnClockChanged()
        {
            RefreshSharedHeader();
            RefreshRoleOrders();
            RefreshRoleDetails();
        }

        private void OnPreparationChanged()
        {
            RefreshRoleOrders();
            RefreshRoleDetails();
            RefreshNavigationIfOpen();
        }

        private void RefreshAll()
        {
            RefreshSharedHeader();
            RefreshRoleOrders();
            RefreshRoleDetails();
        }

        private void RefreshSharedHeader()
        {
            if (gameManager == null)
            {
                return;
            }

            clockText.text = gameManager.ClockText;
            balanceText.text = $"R$ {gameManager.Balance:0.00}";
            int reputation = Mathf.Clamp(gameManager.Reputation, 0, 5);
            reputationText.text = $"{new string('★', reputation)}{new string('☆', 5 - reputation)}";
            dailyGoalText.text =
                $"{gameManager.CompletedDailyDeliveries}/{gameManager.DailyDeliveryGoal}";
        }

        private void UpdateCursorOwnership(bool open)
        {
            bool requestCursor = open && mousePointerAllowed && owner != null && owner.UsesMouseAndKeyboard;
            if (requestCursor)
            {
                CursorOwners.Add(this);
            }
            else
            {
                CursorOwners.Remove(this);
            }
        }

        private void UpdateRaycaster(bool open)
        {
            if (phoneRaycaster != null)
            {
                bool pointerAllowed = mousePointerAllowed && owner != null && owner.UsesMouseAndKeyboard;
                phoneRaycaster.enabled = restoreRaycasterEnabled && (!open || pointerAllowed);
            }
        }

        private void SetFocusedButton(Button button)
        {
            if (focusedButton == button)
            {
                return;
            }

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem != null)
            {
                BaseEventData eventData = new BaseEventData(eventSystem);
                if (focusedButton != null)
                {
                    focusedButton.OnDeselect(eventData);
                }

                focusedButton = button;
                if (focusedButton != null)
                {
                    focusedButton.OnSelect(eventData);
                }
            }
            else
            {
                focusedButton = button;
            }
        }

        private static void ApplyCursorState()
        {
            if (CursorOwners.Count > 0)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (cursorStateCaptured)
            {
                Cursor.lockState = restoreCursorLockState;
                Cursor.visible = restoreCursorVisibility;
                cursorStateCaptured = false;
            }
        }

        private static void ApplyNavigationState()
        {
            if (OpenPhones.Count > 0)
            {
                if (navigationEventSystem == null)
                {
                    navigationEventSystem = EventSystem.current;
                    if (navigationEventSystem != null)
                    {
                        restoreNavigationEvents = navigationEventSystem.sendNavigationEvents;
                    }
                }

                if (navigationEventSystem != null)
                {
                    navigationEventSystem.sendNavigationEvents = false;
                }

                return;
            }

            if (navigationEventSystem != null)
            {
                navigationEventSystem.sendNavigationEvents = restoreNavigationEvents;
            }

            navigationEventSystem = null;
        }
    }
}