using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ColmeiaStudios.SoMaisUmPedido
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Role")] [SerializeField] private PlayerRole assignedRole = PlayerRole.Cook;
        [SerializeField] private bool useMouseLook = true;

        [Header("Input")] [SerializeField] private PlayerInput playerInput;
        [SerializeField] private string playerActionMap = "PlayerOne";
        [SerializeField] private string controlScheme = "KeyboardMouse";
        [SerializeField] private PlayerConfig playerConfig;
        [SerializeField] private KifomePhoneUI kifomePhone;

        [Header("Movement")] [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float lookSensitivity = 2.2f;
        [SerializeField] private float minPitch = -80f;
        [SerializeField] private float maxPitch = 80f;

        [Header("Camera")] [SerializeField] private Camera playerCamera;
        [SerializeField] private Transform cameraPivot;

        [Header("Character visual")]
        [SerializeField] private PlayerCharacterVisual characterVisual;

        [Header("Cook transport")] [SerializeField] private Transform mealboxCarryPoint;
        [SerializeField] private Transform mealboxPreparationPoint;

        private CharacterController characterController;
        private PlayerInteractor interactor;
        private Vector3 velocity;
        private float pitch;
        private bool isConfigured;
        private readonly List<string> currentIngredients = new List<string>();
        private readonly List<IngredientData> currentIngredientData = new List<IngredientData>();
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction interactAction;
        private InputAction confirmAction;
        private InputAction cancelAction;
        private InputAction togglePhoneAction;
        private string selectedOrderId;
        private string preparationMessage;
        private float preparationStartedAt;
        private LunchboxType selectedLunchboxType = LunchboxType.ThreeCompartments;
        private bool preparationPresentationVisible = true;
        private bool completingPreparation;
        private readonly List<string> carriedOrderIds = new List<string>(3);
        private readonly HashSet<string> purchasedUpgradeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private string selectedDeliveryOrderId;
        private string selectedPickupOrderId;
        private string gameplayMessage;
        private float baseMoveSpeed;
        private MealboxPhysical carriedPreparedMealbox;
        private MealboxPhysical preparationMealbox;
        private bool missingCharacterVisualReported;

        public event Action PreparationChanged;
        public event Action InventoryChanged;
        public event Action DeliverySelectionChanged;
        public event Action GameplayMessageChanged;
        public PlayerRole AssignedRole => assignedRole;
        public bool UsesMouseAndKeyboard => controlScheme == "KeyboardMouse";
        public string SelectedOrderId => selectedOrderId;
        public bool IsPreparationPresentationVisible => preparationPresentationVisible;
        public string CarriedOrderId => selectedDeliveryOrderId;
        public string SelectedDeliveryOrderId => selectedDeliveryOrderId;
        public string SelectedPickupOrderId => selectedPickupOrderId;
        public IReadOnlyList<string> CarriedOrderIds => carriedOrderIds;
        public int InventoryCapacity => 3;
        public bool IsPhoneOpen => kifomePhone != null && kifomePhone.IsOpen;
        public string GameplayMessage => gameplayMessage;
        public string PreparationMessage => preparationMessage;

        public bool HasActivePreparation =>
            assignedRole == PlayerRole.Cook &&
            !string.IsNullOrWhiteSpace(selectedOrderId) &&
            GameManager.Instance != null &&
            GameManager.Instance.TryGetOrder(selectedOrderId, out OrderRecord activeOrder) &&
            activeOrder.State == OrderState.InPreparation;

        public float PreparationElapsedSeconds =>
            GameManager.Instance != null ? GameManager.Instance.ElapsedSeconds - preparationStartedAt : 0f;

        public LunchboxType SelectedLunchboxType => selectedLunchboxType;
        public IReadOnlyList<string> CurrentIngredientIds => currentIngredients;
        public IReadOnlyList<IngredientData> CurrentIngredientData => currentIngredientData;
        public MealboxPhysical CarriedPreparedMealbox => carriedPreparedMealbox;
        public MealboxPhysical PreparationMealbox => preparationMealbox;

        public void SetRoleForRotation(PlayerRole role)
        {
            if (role != PlayerRole.Cook && role != PlayerRole.Delivery)
            {
                Debug.LogError($"Cannot assign invalid player role '{role}' to {name}.", this);
                return;
            }

            assignedRole = role;
            EnsureCharacterVisual();
            characterVisual?.SetRole(role);
            kifomePhone?.SetMousePointerEnabled(UsesMouseAndKeyboard);
        }

        public bool SetCharacterVisualLayer(int layer)
        {
            EnsureCharacterVisual();
            if (characterVisual == null)
                return false;

            characterVisual.SetVisualLayer(layer);
            return characterVisual.VisualLayer == layer;
        }

        public void ConfigureCameraVisualLayers(int ownVisualLayer, int otherVisualLayer)
        {
            if (playerCamera == null || ownVisualLayer < 0 || ownVisualLayer > 31 ||
                otherVisualLayer < 0 || otherVisualLayer > 31)
                return;

            int mask = playerCamera.cullingMask;
            mask &= ~(1 << ownVisualLayer);
            mask |= 1 << otherVisualLayer;
            playerCamera.cullingMask = mask;
        }

        private void EnsureCharacterVisual()
        {
            if (characterVisual == null)
                characterVisual = GetComponent<PlayerCharacterVisual>();

            if (characterVisual == null && !missingCharacterVisualReported)
            {
                Debug.LogError($"PlayerController '{name}' requires a PlayerCharacterVisual component for role models.", this);
                missingCharacterVisualReported = true;
            }
        }

        public bool WasRoleVoteTogglePressedThisFrame()
        {
            if (playerInput == null || !playerInput.user.valid)
                return false;

            if (controlScheme == "Gamepad")
                return cancelAction != null && cancelAction.WasPressedThisFrame();

            // Read only the devices paired to this PlayerInput. This keeps each slot's
            // vote independent even when the machine has other keyboards/mice connected.
            foreach (InputDevice pairedDevice in playerInput.user.pairedDevices)
            {
                if (pairedDevice is Keyboard keyboard && keyboard.bKey.wasPressedThisFrame)
                    return true;
                if (pairedDevice is Mouse mouse && mouse.rightButton.wasPressedThisFrame)
                    return true;
            }

            return false;
        }

        public bool WasRoleVoteYesPressedThisFrame()
        {
            return playerInput != null && playerInput.user.valid &&
                   confirmAction != null && confirmAction.WasPressedThisFrame();
        }

        public bool WasRoleVoteNoPressedThisFrame()
        {
            return playerInput != null && playerInput.user.valid &&
                   cancelAction != null && cancelAction.WasPressedThisFrame();
        }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            interactor = GetComponent<PlayerInteractor>() ?? GetComponentInChildren<PlayerInteractor>();
            EnsureCharacterVisual();

            if (cameraPivot == null)
            {
                cameraPivot = transform;
            }

            if (playerCamera == null)
            {
                playerCamera = GetComponentInChildren<Camera>();
            }

            if (playerInput == null)
            {
                playerInput = GetComponent<PlayerInput>();
            }

            baseMoveSpeed = moveSpeed;
        }

        private void Start()
        {
            // When a GameManager is present it owns the two-player setup.
            // Wait for its explicit device assignment instead of binding
            // this slot to whichever global device happens to be current.
            if (!isConfigured && GameManager.Instance == null)
            {
                Configure(assignedRole, playerConfig, useMouseLook, playerActionMap, controlScheme);
            }

            if (kifomePhone != null && GameManager.Instance == null)
            {
                kifomePhone.BindGameplay(this, GameManager.Instance);
            }

            if (GameManager.Instance != null && GameManager.Instance.Orders != null)
            {
                GameManager.Instance.Orders.OrdersChanged -= OnOrdersChanged;
                GameManager.Instance.Orders.OrdersChanged += OnOrdersChanged;
            }
        }

        public void Configure(PlayerRole newRole, bool mouseLook, string actionMapName, string inputControlScheme)
        {
            Configure(newRole, playerConfig, mouseLook, actionMapName, inputControlScheme);
        }

        public void Configure(PlayerRole newRole, PlayerConfig config)
        {
            Configure(newRole, config,
                config != null ? config.UseMouseLook : useMouseLook,
                config != null ? config.ActionMap : playerActionMap,
                config != null ? config.ControlScheme : controlScheme);
        }

        private void Configure(
            PlayerRole newRole,
            PlayerConfig config,
            bool mouseLook,
            string actionMapName,
            string inputControlScheme)
        {
            assignedRole = newRole;
            EnsureCharacterVisual();
            characterVisual?.SetRole(newRole);
            playerConfig = config;
            useMouseLook = mouseLook;
            playerActionMap = actionMapName;
            controlScheme = inputControlScheme;
            if (kifomePhone != null)
            {
                kifomePhone.SetMousePointerEnabled(UsesMouseAndKeyboard);
            }

            if (config != null)
            {
                moveSpeed = config.MoveSpeed;
                gravity = config.Gravity;
                lookSensitivity = config.LookSensitivity;
                minPitch = config.MinPitch;
                maxPitch = config.MaxPitch;
            }

            if (playerInput != null)
            {
                playerInput.defaultActionMap = playerActionMap;
                playerInput.defaultControlScheme = controlScheme;
                playerInput.neverAutoSwitchControlSchemes = true;
                if (playerInput.actions != null &&
                    playerInput.actions.FindActionMap(playerActionMap, false) != null)
                {
                    playerInput.SwitchCurrentActionMap(playerActionMap);
                }
                else
                {
                    Debug.LogError($"Action map '{playerActionMap}' was not found for {name}.", this);
                }

                if (!CacheInputActions())
                {
                    isConfigured = false;
                    return;
                }
            }
            else
            {
                Debug.LogError($"PlayerInput is missing on {name}; this player cannot receive controls.", this);
                isConfigured = false;
                return;
            }

            baseMoveSpeed = moveSpeed;

            if (playerCamera != null)
            {
                playerCamera.transform.localRotation = Quaternion.identity;
            }

            isConfigured = true;
        }

        public bool ConfigureMatchInput(PlayerRole role, PlayerConfig config, InputDevice device, Mouse assignedMouse, string scheme)
        {
            if (playerInput == null || playerInput.actions == null || device == null || !device.added)
            {
                Debug.LogError($"PlayerInput, Input Actions, or assigned input device is missing/invalid for {name}.", this);
                return false;
            }

            string actionMap;
            InputDevice[] assignedDevices;
            if (scheme == "Gamepad" && device is Gamepad && assignedMouse == null)
            {
                actionMap = "PlayerTwo";
                assignedDevices = new[] { device };
            }
            else if (scheme == "KeyboardMouse" && device is Keyboard &&
                     assignedMouse != null && assignedMouse.added)
            {
                actionMap = "PlayerOne";
                assignedDevices = new InputDevice[] { device, assignedMouse };
            }
            else
            {
                Debug.LogError($"Atribuição inválida de dispositivo/esquema para {name}: {scheme}.", this);
                return false;
            }

            if (playerInput.actions.FindActionMap(actionMap, false) == null)
            {
                Debug.LogError($"Action Map '{actionMap}' não existe no PlayerInput de {name}.", this);
                return false;
            }

            bool schemeExists = false;
            foreach (InputControlScheme availableScheme in playerInput.actions.controlSchemes)
            {
                if (availableScheme.name == scheme)
                {
                    schemeExists = true;
                    break;
                }
            }

            if (!schemeExists || !playerInput.user.valid)
            {
                Debug.LogError($"Control Scheme '{scheme}' ou InputUser inválido em {name}.", this);
                return false;
            }

            // This PlayerInput API performs the pairing through its InputUser,
            // unpairs the previous devices and binds only the explicit devices
            // passed here. Automatic global-device switching stays disabled.
            playerInput.neverAutoSwitchControlSchemes = true;
            try
            {
                playerInput.SwitchCurrentControlScheme(scheme, assignedDevices);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Falha ao parear dispositivos de {name}: {exception.Message}", this);
                return false;
            }

            if (playerInput.currentControlScheme != scheme ||
                playerInput.user.pairedDevices.Count != assignedDevices.Length)
            {
                Debug.LogError($"O PlayerInput de {name} não manteve o pareamento solicitado ({scheme}).", this);
                return false;
            }

            for (int i = 0; i < assignedDevices.Length; i++)
            {
                bool isPaired = false;
                for (int pairedIndex = 0; pairedIndex < playerInput.user.pairedDevices.Count; pairedIndex++)
                {
                    if (playerInput.user.pairedDevices[pairedIndex] == assignedDevices[i])
                    {
                        isPaired = true;
                        break;
                    }
                }

                if (!isPaired)
                {
                    Debug.LogError($"O dispositivo {assignedDevices[i].displayName} não foi pareado a {name}.", this);
                    return false;
                }
            }

            Configure(role, config, scheme == "KeyboardMouse", actionMap, scheme);
            return isConfigured;
        }

        private bool CacheInputActions()
        {
            if (playerInput == null || playerInput.actions == null)
            {
                Debug.LogError($"PlayerInput or Input Actions asset is missing on {name}.", this);
                return false;
            }

            InputActionMap actionMap = playerInput.actions.FindActionMap(playerActionMap, false);
            if (actionMap == null)
            {
                Debug.LogError($"Action Map '{playerActionMap}' was not found in PlayerInput on {name}.", this);
                return false;
            }

            moveAction = FindRequiredAction(actionMap, "Move");
            lookAction = FindRequiredAction(actionMap, "Look");
            interactAction = FindRequiredAction(actionMap, "Interact");
            confirmAction = FindRequiredAction(actionMap, "Confirm");
            cancelAction = FindRequiredAction(actionMap, "Cancel");
            togglePhoneAction = FindRequiredAction(actionMap, "TogglePhone");
            return moveAction != null && lookAction != null && interactAction != null &&
                   confirmAction != null && cancelAction != null && togglePhoneAction != null;
        }

        private InputAction FindRequiredAction(InputActionMap actionMap, string actionName)
        {
            InputAction action = actionMap.FindAction(actionName, false);
            if (action == null)
            {
                Debug.LogError($"A ação obrigatória '{actionName}' não existe no Action Map '{actionMap.name}' ({name}).", this);
            }

            return action;
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.IsGameOver)
            {
                velocity = Vector3.zero;
                UpdateCharacterLocomotion(false);
                return;
            }

            if (playerInput == null || playerInput.actions == null)
            {
                UpdateCharacterLocomotion(false);
                return;
            }

            if (togglePhoneAction != null && togglePhoneAction.triggered && kifomePhone != null)
            {
                kifomePhone.Toggle();
                return;
            }

            if (kifomePhone != null && kifomePhone.IsOpen)
            {
                UpdateCharacterLocomotion(false);
                kifomePhone.HandlePlayerInput(
                    moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero,
                    confirmAction != null && confirmAction.triggered,
                    cancelAction != null && cancelAction.triggered);
                return;
            }

            HandleMovement();
            HandleLook();
            HandleLunchboxSelection();

            if (!IsPhoneOpen && interactAction != null && interactAction.triggered)
            {
                if (interactor != null)
                {
                    interactor.TryInteract();
                }
            }
        }

        private void HandleMovement()
        {
            if (playerInput == null || playerInput.actions == null)
            {
                UpdateCharacterLocomotion(false);
                return;
            }

            Vector3 positionBeforeMovement = transform.position;

            Vector2 moveInput = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
            Vector3 movement = new Vector3(moveInput.x, 0f, moveInput.y);
            movement = Vector3.ClampMagnitude(movement, 1f);

            Vector3 moveDirection = transform.right * movement.x + transform.forward * movement.z;
            characterController.Move(moveDirection.normalized * moveSpeed * Time.deltaTime);

            if (characterController.isGrounded && velocity.y < 0f)
            {
                velocity.y = -2f;
            }

            velocity.y += gravity * Time.deltaTime;
            characterController.Move(velocity * Time.deltaTime);

            Vector3 actualDisplacement = transform.position - positionBeforeMovement;
            actualDisplacement.y = 0f;
            UpdateCharacterLocomotion(actualDisplacement.sqrMagnitude > 0.000001f);
        }

        private void UpdateCharacterLocomotion(bool isMoving)
        {
            if (characterVisual != null)
            {
                characterVisual.SetLocomotion(
                    isMoving,
                    assignedRole == PlayerRole.Cook && mealboxCarryPoint.childCount != 0);
            }
        }

        private void HandleLook()
        {
            if (playerInput == null || playerInput.actions == null)
            {
                return;
            }

            Vector2 lookInput = lookAction != null ? lookAction.ReadValue<Vector2>() : Vector2.zero;
            float yaw = lookInput.x * lookSensitivity;
            float pitchInput = lookInput.y * lookSensitivity;

            transform.Rotate(Vector3.up, yaw, Space.World);

            pitch -= pitchInput;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            if (playerCamera != null)
            {
                playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
        }

        public Camera GetCamera()
        {
            return playerCamera;
        }

        public bool TryAddIngredient(string ingredientId, string displayName, GameObject visualPrefab = null)
        {
            if (!HasActivePreparation || preparationMealbox == null ||
                preparationMealbox.IsClosed || preparationMealbox.Type != selectedLunchboxType ||
                string.IsNullOrWhiteSpace(ingredientId) || completingPreparation)
            {
                if (HasActivePreparation && preparationMealbox == null)
                    ShowGameplayMessage("Pegue a marmita correta na estação antes de adicionar ingredientes.");
                return false;
            }

            if (GameManager.Instance != null && !GameManager.Instance.IsIngredientIdUnlocked(ingredientId))
            {
                ShowGameplayMessage($"Ingrediente bloqueado: {displayName}.");
                return false;
            }

            string normalizedId = IngredientStation.NormalizeLegacyId(ingredientId);
            if (HasIngredient(normalizedId))
            {
                ShowGameplayMessage($"{displayName} já foi adicionado a esta receita.");
                return false;
            }

            if (!TryGetRequiredIngredientSlot(normalizedId, out int visualSlot))
            {
                ShowGameplayMessage($"{displayName} não faz parte da receita selecionada.");
                return false;
            }

            if (preparationMealbox != null && visualPrefab != null &&
                !preparationMealbox.TryAddIngredientVisual(visualPrefab, visualSlot))
            {
                ShowGameplayMessage("Não foi possível posicionar o modelo do ingrediente na marmita. Confira os pontos dos compartimentos.");
                return false;
            }

            currentIngredients.Add(normalizedId);
            currentIngredientData.Add(null);
            preparationMessage = $"Adicionado: {displayName}.";
            PreparationChanged?.Invoke();
            Debug.Log($"{name} adicionou o ingrediente '{displayName}' (total: {currentIngredients.Count}).");
            TryCompletePreparationIfExact();
            return true;
        }

        public bool TryAddIngredient(IngredientData ingredient)
        {
            if (ingredient == null || !HasActivePreparation || preparationMealbox == null ||
                preparationMealbox.IsClosed || preparationMealbox.Type != selectedLunchboxType || completingPreparation)
            {
                if (HasActivePreparation && preparationMealbox == null)
                    ShowGameplayMessage("Pegue a marmita correta na estação antes de adicionar ingredientes.");
                return false;
            }

            if (GameManager.Instance != null && !GameManager.Instance.IsIngredientUnlocked(ingredient))
            {
                ShowGameplayMessage($"Ingrediente bloqueado: {ingredient.DisplayName}.");
                return false;
            }

            string normalizedId = IngredientStation.NormalizeLegacyId(ingredient.Id);
            if (HasIngredient(normalizedId))
            {
                ShowGameplayMessage($"{ingredient.DisplayName} já foi adicionado a esta receita.");
                return false;
            }

            if (!TryGetRequiredIngredientSlot(normalizedId, out int visualSlot))
            {
                ShowGameplayMessage($"{ingredient.DisplayName} não faz parte da receita selecionada.");
                return false;
            }

            if (preparationMealbox != null && ingredient.VisualPrefab != null &&
                !preparationMealbox.TryAddIngredientVisual(ingredient, visualSlot))
            {
                ShowGameplayMessage("Não foi possível posicionar o modelo do ingrediente na marmita. Confira os pontos dos compartimentos.");
                return false;
            }

            currentIngredients.Add(normalizedId);
            currentIngredientData.Add(ingredient);
            preparationMessage = $"Adicionado: {ingredient.DisplayName}.";
            PreparationChanged?.Invoke();
            TryCompletePreparationIfExact();
            return true;
        }

        public void SetPreparationPresentationVisible(bool visible)
        {
            if (preparationPresentationVisible == visible)
            {
                return;
            }

            preparationPresentationVisible = visible;
            PreparationChanged?.Invoke();
        }

        public IReadOnlyList<string> GetCurrentIngredients()
        {
            return currentIngredients;
        }

        public void ClearIngredients()
        {
            currentIngredients.Clear();
            currentIngredientData.Clear();
            if (preparationMealbox != null)
                preparationMealbox.ClearPreparationVisuals();
            PreparationChanged?.Invoke();
        }

        public void ClearPreparationContents()
        {
            if (!HasActivePreparation)
            {
                return;
            }

            ClearIngredients();
            preparationMessage = "Marmita limpa. Continue a preparação deste pedido.";
            ShowGameplayMessage(preparationMessage);
            PreparationChanged?.Invoke();
        }

        public bool StartNextPreparation()
        {
            if (assignedRole != PlayerRole.Cook || !string.IsNullOrWhiteSpace(selectedOrderId) ||
                GameManager.Instance == null ||
                !GameManager.Instance.TryStartNextPreparation(out OrderRecord order))
            {
                preparationMessage = "Nenhum pedido disponível para preparar.";
                return false;
            }

            selectedOrderId = order.OrderId;
            preparationPresentationVisible = true;
            preparationStartedAt = GameManager.Instance.ElapsedSeconds;
            selectedLunchboxType = order.LunchboxType;
            ClearIngredients();
            preparationMessage = $"Pedido #{order.OrderId}: pegue a marmita de {GetCompartmentCount(order.LunchboxType)} compartimento(s) na estação.";
            selectedLunchboxType = order.LunchboxType;
            PreparationChanged?.Invoke();
            return true;
        }

        public bool StartPreparation(string orderId)
        {
            if (assignedRole != PlayerRole.Cook || string.IsNullOrWhiteSpace(orderId) ||
                !string.IsNullOrEmpty(selectedOrderId) || GameManager.Instance == null ||
                !GameManager.Instance.TryStartPreparation(orderId) ||
                !GameManager.Instance.TryGetOrder(orderId, out OrderRecord order))
            {
                preparationMessage = "Não foi possível iniciar a preparação deste pedido.";
                return false;
            }

            selectedOrderId = order.OrderId;
            preparationPresentationVisible = true;
            preparationStartedAt = GameManager.Instance.ElapsedSeconds;
            selectedLunchboxType = order.LunchboxType;
            ClearIngredients();
            preparationMessage = $"Pedido #{order.OrderId}: pegue a marmita de {GetCompartmentCount(order.LunchboxType)} compartimento(s) na estação.";
            PreparationChanged?.Invoke();
            return true;
        }

        public bool FinishPreparation()
        {
            if (assignedRole != PlayerRole.Cook || string.IsNullOrEmpty(selectedOrderId) ||
                GameManager.Instance == null || completingPreparation ||
                !GameManager.Instance.TryGetOrder(selectedOrderId, out OrderRecord order) ||
                order.State != OrderState.InPreparation ||
                !IngredientsMatch(order.RequiredIngredientIds, currentIngredients))
            {
                return false;
            }

            completingPreparation = true;
            bool completed = GameManager.Instance.TryCompletePreparation(
                selectedOrderId, currentIngredients, selectedLunchboxType, this, out string result);
            completingPreparation = false;
            if (!completed)
            {
                preparationMessage = result;
                if (string.IsNullOrEmpty(selectedOrderId))
                {
                    PreparationChanged?.Invoke();
                }

                return false;
            }

            preparationMessage = result;
            preparationMealbox = null;
            ShowGameplayMessage(result);
            selectedOrderId = null;
            preparationPresentationVisible = true;
            ClearIngredients();
            PreparationChanged?.Invoke();
            return true;
        }

        public Transform GetMealboxCarryPoint()
        {
            return mealboxCarryPoint != null ? mealboxCarryPoint : transform;
        }

        public bool CanCarryPreparedMealbox => assignedRole == PlayerRole.Cook && carriedPreparedMealbox == null;

        public bool TryCarryPreparedMealbox(MealboxPhysical mealbox)
        {
            if (!CanCarryPreparedMealbox || mealbox == null || !mealbox.IsClosed)
                return false;

            carriedPreparedMealbox = mealbox;
            Transform carryPoint = GetMealboxCarryPoint();
            mealbox.transform.SetParent(carryPoint, false);
            mealbox.transform.localPosition = mealboxCarryPoint != null ? Vector3.zero : new Vector3(0f, 1f, 0.55f);
            mealbox.transform.localRotation = Quaternion.identity;
            mealbox.SetAttachedToPlayer(true);
            PreparationChanged?.Invoke();
            return true;
        }

        public MealboxPhysical ReleasePreparedMealbox(bool tryCompletePreparation = true)
        {
            MealboxPhysical mealbox = carriedPreparedMealbox;
            carriedPreparedMealbox = null;
            if (mealbox != null) mealbox.SetAttachedToPlayer(false);
            preparationMessage = "Marmita depositada no balcão.";
            PreparationChanged?.Invoke();
            if (tryCompletePreparation) TryCompletePendingPreparation();
            return mealbox;
        }

        public bool TryTakeEmptyMealbox(MealboxPhysical mealbox, OrderRecord order, out string result)
        {
            result = "Não foi possível pegar esta marmita.";
            if (assignedRole != PlayerRole.Cook || mealbox == null || order == null || !HasActivePreparation ||
                preparationMealbox != null ||
                !string.Equals(order.OrderId, selectedOrderId, StringComparison.OrdinalIgnoreCase) ||
                order.State != OrderState.InPreparation || order.LunchboxType != selectedLunchboxType ||
                mealbox.IsClosed || mealbox.Type != order.LunchboxType ||
                !string.Equals(mealbox.OrderId, order.OrderId, StringComparison.OrdinalIgnoreCase) ||
                GameManager.Instance == null || GameManager.Instance.MatchConfiguration == null ||
                !GameManager.Instance.MatchConfiguration.IsLunchboxUnlocked(
                    mealbox.Type, Mathf.Max(1, GameManager.Instance.CurrentDay)))
                return false;

            preparationMealbox = mealbox;
            Transform point = mealboxPreparationPoint != null ? mealboxPreparationPoint : transform;
            mealbox.transform.SetParent(point, false);
            mealbox.transform.localPosition = mealboxPreparationPoint != null ? Vector3.zero : new Vector3(0f, 0.9f, 0.45f);
            mealbox.transform.localRotation = Quaternion.identity;
            mealbox.SetAttachedToPlayer(true);
            preparationMessage = $"Marmita correta selecionada para o pedido #{order.OrderId}.";
            PreparationChanged?.Invoke();
            result = preparationMessage;
            return true;
        }

        public bool ReplacePreparationWithClosedMealbox(MealboxPhysical closedMealbox, string orderId, LunchboxType type)
        {
            MealboxPhysical emptyMealbox = preparationMealbox;
            if (assignedRole != PlayerRole.Cook || !CanCarryPreparedMealbox || emptyMealbox == null ||
                closedMealbox == null || !closedMealbox.IsClosed || !HasActivePreparation ||
                !string.Equals(selectedOrderId, orderId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(closedMealbox.OrderId, orderId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(emptyMealbox.OrderId, orderId, StringComparison.OrdinalIgnoreCase) ||
                emptyMealbox.Type != type || closedMealbox.Type != type)
                return false;

            if (!TryCarryPreparedMealbox(closedMealbox)) return false;
            preparationMealbox = null;
            emptyMealbox.gameObject.SetActive(false);
            Destroy(emptyMealbox.gameObject);
            preparationMessage = $"Pedido #{orderId} pronto. Leve a marmita fechada até o balcão.";
            PreparationChanged?.Invoke();
            return true;
        }

        public void TryCompletePendingPreparation()
        {
            TryCompletePreparationIfExact();
        }

        public void RemovePreparedMealbox(string orderId)
        {
            if (carriedPreparedMealbox == null ||
                !string.Equals(carriedPreparedMealbox.OrderId, orderId, StringComparison.OrdinalIgnoreCase))
                return;

            MealboxPhysical mealbox = carriedPreparedMealbox;
            carriedPreparedMealbox = null;
            if (mealbox != null)
                Destroy(mealbox.gameObject);
            preparationMessage = "A marmita expirou antes de ser depositada.";
            PreparationChanged?.Invoke();
        }

        private void TryCompletePreparationIfExact()
        {
            if (string.IsNullOrWhiteSpace(selectedOrderId) || GameManager.Instance == null ||
                !GameManager.Instance.TryGetOrder(selectedOrderId, out OrderRecord order) ||
                order.State != OrderState.InPreparation ||
                !IngredientsMatch(order.RequiredIngredientIds, currentIngredients))
            {
                return;
            }

            if (!FinishPreparation())
            {
                PreparationChanged?.Invoke();
            }
        }

        private bool HasIngredient(string normalizedId)
        {
            for (int i = 0; i < currentIngredients.Count; i++)
            {
                if (string.Equals(currentIngredients[i], normalizedId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private bool TryGetRequiredIngredientSlot(string normalizedId, out int slot)
        {
            slot = -1;
            if (GameManager.Instance == null || string.IsNullOrWhiteSpace(selectedOrderId) ||
                !GameManager.Instance.TryGetOrder(selectedOrderId, out OrderRecord order))
                return false;

            for (int i = 0; i < order.RequiredIngredientIds.Count; i++)
            {
                if (string.Equals(IngredientStation.NormalizeLegacyId(order.RequiredIngredientIds[i]),
                    normalizedId, StringComparison.OrdinalIgnoreCase))
                {
                    slot = i;
                    return true;
                }
            }
            return false;
        }

        private static int GetCompartmentCount(LunchboxType type)
        {
            switch (type)
            {
                case LunchboxType.OneCompartment: return 1;
                case LunchboxType.TwoCompartments: return 2;
                case LunchboxType.ThreeCompartments: return 3;
                case LunchboxType.FourCompartments: return 4;
                case LunchboxType.FiveCompartments: return 5;
                default: return 0;
            }
        }

        private static bool IngredientsMatch(IReadOnlyList<string> required, IReadOnlyList<string> added)
        {
            if (required == null || added == null || required.Count != added.Count)
            {
                return false;
            }

            var remaining = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (string id in required)
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    return false;
                }

                string normalizedId = IngredientStation.NormalizeLegacyId(id);
                remaining.TryGetValue(normalizedId, out int count);
                remaining[normalizedId] = count + 1;
            }

            foreach (string id in added)
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    return false;
                }

                string normalizedId = IngredientStation.NormalizeLegacyId(id);
                if (!remaining.TryGetValue(normalizedId, out int count) || count == 0)
                {
                    return false;
                }

                remaining[normalizedId] = count - 1;
            }

            foreach (int count in remaining.Values)
            {
                if (count != 0)
                {
                    return false;
                }
            }

            return true;
        }

        public void CancelPreparation()
        {
            if (!HasActivePreparation || GameManager.Instance == null ||
                !GameManager.Instance.CancelPreparation(selectedOrderId))
            {
                return;
            }

            selectedOrderId = null;
            if (preparationMealbox != null)
                Destroy(preparationMealbox.gameObject);
            preparationMealbox = null;
            ClearIngredients();
            preparationMessage = "Preparação cancelada; o pedido voltou à lista.";
            PreparationChanged?.Invoke();
        }

        public void SelectLunchboxType(LunchboxType type)
        {
            if (HasActivePreparation && type != selectedLunchboxType)
            {
                ShowGameplayMessage("O tipo de marmita é definido pelo pedido selecionado e não pode ser trocado durante a preparação.");
                return;
            }

            if (GameManager.Instance != null && !GameManager.Instance.IsLunchboxUnlocked(type))
            {
                ShowGameplayMessage("Esse tipo de marmita ainda está bloqueado.");
                return;
            }

            selectedLunchboxType = type;
            if (!string.IsNullOrEmpty(selectedOrderId) && GameManager.Instance != null &&
                GameManager.Instance.TryGetOrder(selectedOrderId, out OrderRecord order) &&
                order.LunchboxType != type)
            {
                preparationMessage = "Tipo de marmita diferente do pedido.";
            }

            PreparationChanged?.Invoke();
            TryCompletePreparationIfExact();
        }

        public bool CanAddOrderToInventory(string orderId)
        {
            return assignedRole == PlayerRole.Delivery &&
                   !string.IsNullOrWhiteSpace(orderId) &&
                   carriedOrderIds.Count < InventoryCapacity &&
                   !ContainsOrderId(orderId) &&
                   GameManager.Instance != null &&
                   GameManager.Instance.TryGetOrder(orderId, out OrderRecord order) &&
                   order.State == OrderState.ReadyForPickup;
        }

        public bool TryAddOrderToInventory(string orderId)
        {
            if (!CanAddOrderToInventory(orderId))
            {
                if (carriedOrderIds.Count >= InventoryCapacity)
                {
                    ShowGameplayMessage("Mochila cheia: limite de três marmitas.");
                }

                return false;
            }

            carriedOrderIds.Add(orderId.Trim());
            if (string.IsNullOrWhiteSpace(selectedDeliveryOrderId))
            {
                selectedDeliveryOrderId = orderId.Trim();
                DeliverySelectionChanged?.Invoke();
            }

            InventoryChanged?.Invoke();
            return true;
        }

        public bool HasOrderInInventory(string orderId)
        {
            return !string.IsNullOrWhiteSpace(orderId) && ContainsOrderId(orderId);
        }

        public bool SelectPickupOrder(string orderId)
        {
            if (assignedRole != PlayerRole.Delivery ||
                GameManager.Instance == null ||
                !GameManager.Instance.TryGetOrder(orderId, out OrderRecord order) ||
                order.State != OrderState.ReadyForPickup)
            {
                return false;
            }

            bool alreadySelected = string.Equals(
                selectedPickupOrderId,
                order.OrderId,
                StringComparison.OrdinalIgnoreCase);
            if (!alreadySelected && carriedOrderIds.Count >= InventoryCapacity)
            {
                return false;
            }

            selectedPickupOrderId = alreadySelected ? null : order.OrderId;
            DeliverySelectionChanged?.Invoke();
            return true;
        }

        public bool SelectDeliveryOrder(string orderId)
        {
            if (assignedRole != PlayerRole.Delivery || !HasOrderInInventory(orderId) ||
                GameManager.Instance == null ||
                !GameManager.Instance.TryGetOrder(orderId, out OrderRecord order) ||
                order.State != OrderState.InDelivery)
            {
                return false;
            }

            if (string.Equals(selectedDeliveryOrderId, orderId, StringComparison.OrdinalIgnoreCase))
            {
                DeselectDeliveryOrder();
                return true;
            }

            selectedDeliveryOrderId = orderId.Trim();
            DeliverySelectionChanged?.Invoke();
            return true;
        }

        public void DeselectDeliveryOrder()
        {
            if (string.IsNullOrEmpty(selectedDeliveryOrderId))
            {
                return;
            }

            selectedDeliveryOrderId = null;
            DeliverySelectionChanged?.Invoke();
        }

        public bool RemoveOrderFromInventory(string orderId)
        {
            if (!HasOrderInInventory(orderId))
            {
                return false;
            }

            string normalizedId = orderId.Trim();
            carriedOrderIds.RemoveAll(id => string.Equals(id, normalizedId, StringComparison.OrdinalIgnoreCase));
            if (string.Equals(selectedDeliveryOrderId, normalizedId, StringComparison.OrdinalIgnoreCase))
            {
                selectedDeliveryOrderId = carriedOrderIds.Count > 0 ? carriedOrderIds[0] : null;
                DeliverySelectionChanged?.Invoke();
            }

            InventoryChanged?.Invoke();
            return true;
        }

        public void ShowGameplayMessage(string message)
        {
            gameplayMessage = message ?? string.Empty;
            GameplayMessageChanged?.Invoke();
        }

        private bool ContainsOrderId(string orderId)
        {
            string normalizedId = orderId.Trim();
            return carriedOrderIds.Exists(existingId =>
                string.Equals(existingId, normalizedId, StringComparison.OrdinalIgnoreCase));
        }

        private void OnOrdersChanged()
        {
            if (!string.IsNullOrWhiteSpace(selectedPickupOrderId) &&
                (GameManager.Instance == null ||
                 !GameManager.Instance.TryGetOrder(selectedPickupOrderId, out OrderRecord pickupOrder) ||
                 pickupOrder.State != OrderState.ReadyForPickup))
            {
                selectedPickupOrderId = null;
                DeliverySelectionChanged?.Invoke();
            }

            if (assignedRole == PlayerRole.Cook && !string.IsNullOrWhiteSpace(selectedOrderId) &&
                (GameManager.Instance == null ||
                 !GameManager.Instance.TryGetOrder(selectedOrderId, out OrderRecord preparationOrder) ||
                 preparationOrder.State != OrderState.InPreparation))
            {
                string expiredPreparationId = selectedOrderId;
                selectedOrderId = null;
                if (preparationMealbox != null)
                {
                    if (preparationMealbox == carriedPreparedMealbox)
                        preparationMealbox = null;
                    else
                        Destroy(preparationMealbox.gameObject);
                }
                preparationMealbox = null;
                ClearIngredients();
                preparationMessage = $"A preparação do pedido #{expiredPreparationId} não está mais ativa.";
                ShowGameplayMessage(preparationMessage);
            }

            bool inventoryChanged = false;
            for (int i = carriedOrderIds.Count - 1; i >= 0; i--)
            {
                if (GameManager.Instance == null ||
                    !GameManager.Instance.TryGetOrder(carriedOrderIds[i], out OrderRecord order) ||
                    order.State != OrderState.InDelivery)
                {
                    string removedId = carriedOrderIds[i];
                    carriedOrderIds.RemoveAt(i);
                    if (string.Equals(selectedDeliveryOrderId, removedId, StringComparison.OrdinalIgnoreCase))
                    {
                        selectedDeliveryOrderId = carriedOrderIds.Count > 0 ? carriedOrderIds[0] : null;
                        DeliverySelectionChanged?.Invoke();
                    }

                    inventoryChanged = true;
                }
            }

            if (inventoryChanged)
            {
                InventoryChanged?.Invoke();
            }
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null && GameManager.Instance.Orders != null)
            {
                GameManager.Instance.Orders.OrdersChanged -= OnOrdersChanged;
            }
        }

        public void AssignKifomePhone(KifomePhoneUI phone)
        {
            kifomePhone = phone;
            kifomePhone?.SetMousePointerEnabled(UsesMouseAndKeyboard);
            kifomePhone?.BindGameplay(this, GameManager.Instance);
        }

        public void ResetDailyState()
        {
            if (carriedPreparedMealbox != null)
            {
                Destroy(carriedPreparedMealbox.gameObject);
                carriedPreparedMealbox = null;
            }

            selectedOrderId = null;
            if (preparationMealbox != null)
                Destroy(preparationMealbox.gameObject);
            preparationMealbox = null;
            selectedPickupOrderId = null;
            selectedDeliveryOrderId = null;
            carriedOrderIds.Clear();
            completingPreparation = false;
            preparationMessage = string.Empty;
            ClearIngredients();
            InventoryChanged?.Invoke();
            DeliverySelectionChanged?.Invoke();
            PreparationChanged?.Invoke();
        }

        public KifomePhoneUI AssignedPhone => kifomePhone;

        private void HandleLunchboxSelection()
        {
            if (assignedRole != PlayerRole.Cook || string.IsNullOrEmpty(selectedOrderId) || playerInput == null)
            {
                return;
            }

            Keyboard pairedKeyboard = null;
            foreach (InputDevice pairedDevice in playerInput.devices)
            {
                if (pairedDevice is Keyboard keyboard)
                {
                    pairedKeyboard = keyboard;
                    break;
                }
            }

            if (pairedKeyboard == null)
                return;

            if (pairedKeyboard.digit1Key.wasPressedThisFrame)
            {
                SelectLunchboxType(LunchboxType.OneCompartment);
            }
            else if (pairedKeyboard.digit2Key.wasPressedThisFrame)
            {
                SelectLunchboxType(LunchboxType.TwoCompartments);
            }
            else if (pairedKeyboard.digit3Key.wasPressedThisFrame)
            {
                SelectLunchboxType(LunchboxType.ThreeCompartments);
            }
            else if (pairedKeyboard.digit4Key.wasPressedThisFrame)
            {
                SelectLunchboxType(LunchboxType.FourCompartments);
            }
            else if (pairedKeyboard.digit5Key.wasPressedThisFrame)
            {
                SelectLunchboxType(LunchboxType.FiveCompartments);
            }
        }
    }
}
