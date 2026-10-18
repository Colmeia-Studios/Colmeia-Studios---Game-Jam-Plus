using UnityEngine;
using System.Collections.Generic;

namespace ColmeiaStudios.SoMaisUmPedido
{
    public sealed class MealboxPhysical : MonoBehaviour
    {
        [System.Serializable]
        private sealed class CompartmentSlot
        {
            [SerializeField] private Transform point;
            [SerializeField] private Vector3 usableSize = new Vector3(0.12f, 0.08f, 0.12f);
            public Transform Point => point;
            public Vector3 UsableSize => usableSize;
        }

        [SerializeField] private string orderId;
        [SerializeField] private string orderDisplayName;
        [SerializeField] private LunchboxType lunchboxType = LunchboxType.TwoCompartments;
        [SerializeField] private bool isClosed;
        [Header("Optional preparation presentation")]
        [SerializeField] private GameObject preparationVisual;
        [SerializeField] private GameObject closedVisual;
        [SerializeField] private CompartmentSlot[] compartments = new CompartmentSlot[5];
        private readonly List<GameObject> ingredientVisuals = new List<GameObject>();

        public string OrderId => orderId;
        public string OrderDisplayName => orderDisplayName;
        public LunchboxType Type => lunchboxType;
        public bool IsClosed => isClosed;

        public void Configure(string id, string displayName)
        {
            orderId = string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
            orderDisplayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Trim();
            gameObject.name = string.IsNullOrEmpty(orderId) ? "Mealbox" : $"Mealbox_{orderId}";
        }

        public void Configure(string id, string displayName, LunchboxType type)
        {
            Configure(id, displayName);
            lunchboxType = type;
            RefreshCompartmentSlots();
        }

        public void ConfigureEmptyForOrder(string id, string displayName, LunchboxType type)
        {
            Configure(id, displayName, type);
            isClosed = false;
            SetPreparationMode(true);
        }

        public void ConfigureClosedForOrder(string id, string displayName, LunchboxType type)
        {
            Configure(id, displayName, type);
            isClosed = true;
            CloseForTransport();
        }

        public void SetAttachedToPlayer(bool attached)
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            foreach (Collider item in colliders)
                item.enabled = !attached;

            Rigidbody[] bodies = GetComponentsInChildren<Rigidbody>(true);
            foreach (Rigidbody body in bodies)
            {
                body.isKinematic = attached;
                if (attached) body.linearVelocity = Vector3.zero;
            }
        }

        private void RefreshCompartmentSlots()
        {
            int activeSlots = GetCompartmentCount(lunchboxType);
            if (compartments == null) return;
            for (int i = 0; i < compartments.Length; i++)
            {
                Transform point = compartments[i] != null ? compartments[i].Point : null;
                if (point != null && point.gameObject.activeSelf != (i < activeSlots))
                    point.gameObject.SetActive(i < activeSlots);
            }
        }

        /// <summary>
        /// Returns whether this mealbox has a configured placement point for every
        /// compartment required by the requested type.
        /// </summary>
        public bool HasCompartmentPoints(LunchboxType type)
        {
            int requiredSlots = GetCompartmentCount(type);
            if (requiredSlots <= 0 || compartments == null || compartments.Length < requiredSlots)
                return false;

            for (int i = 0; i < requiredSlots; i++)
            {
                if (compartments[i] == null || compartments[i].Point == null)
                    return false;
            }

            return true;
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

        public bool TryAddIngredientVisual(IngredientData ingredient, int slotIndex)
        {
            return ingredient != null && TryAddIngredientVisual(ingredient.VisualPrefab, slotIndex);
        }

        public bool TryAddIngredientVisual(GameObject visualPrefab, int slotIndex)
        {
            if (visualPrefab == null || compartments == null ||
                slotIndex < 0 || slotIndex >= compartments.Length || compartments[slotIndex] == null ||
                compartments[slotIndex].Point == null)
                return false;

            Transform point = compartments[slotIndex].Point;
            GameObject visual = Instantiate(visualPrefab, point, false);
            if (visual == null) return false;
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            FitVisualToSlot(visual, compartments[slotIndex].UsableSize);
            ingredientVisuals.Add(visual);
            if (preparationVisual != null) preparationVisual.SetActive(true);
            if (closedVisual != null) closedVisual.SetActive(false);
            return true;
        }

        public void ClearPreparationVisuals()
        {
            for (int i = ingredientVisuals.Count - 1; i >= 0; i--)
                if (ingredientVisuals[i] != null) Destroy(ingredientVisuals[i]);
            ingredientVisuals.Clear();
        }

        public void SetPreparationMode(bool preparing)
        {
            if (preparationVisual != null) preparationVisual.SetActive(preparing);
            if (closedVisual != null) closedVisual.SetActive(!preparing);
            if (!preparing) ClearPreparationVisuals();
        }

        public void CloseForTransport()
        {
            isClosed = true;
            ClearPreparationVisuals();
            if (preparationVisual != null) preparationVisual.SetActive(false);
            if (closedVisual != null) closedVisual.SetActive(true);
        }

        private static void FitVisualToSlot(GameObject visual, Vector3 targetSize)
        {
            Transform slot = visual.transform.parent;
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            if (slot == null) return;

            Bounds bounds = GetBoundsInLocalSpace(renderers, slot);
            Vector3 size = bounds.size;
            float factor = float.PositiveInfinity;
            if (size.x > 0.0001f) factor = Mathf.Min(factor, targetSize.x / size.x);
            if (size.y > 0.0001f) factor = Mathf.Min(factor, targetSize.y / size.y);
            if (size.z > 0.0001f) factor = Mathf.Min(factor, targetSize.z / size.z);
            if (float.IsInfinity(factor) || float.IsNaN(factor) || factor <= 0f) return;
            visual.transform.localScale *= factor;
            Physics.SyncTransforms();
            Bounds fittedBounds = GetBoundsInLocalSpace(renderers, slot);
            visual.transform.localPosition -= fittedBounds.center;
        }

        private static Bounds GetBoundsInLocalSpace(Renderer[] renderers, Transform space)
        {
            bool hasBounds = false;
            Bounds result = default;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null) continue;
                Bounds worldBounds = renderer.bounds;
                Vector3 min = worldBounds.min;
                Vector3 max = worldBounds.max;
                for (int x = 0; x <= 1; x++)
                for (int y = 0; y <= 1; y++)
                for (int z = 0; z <= 1; z++)
                {
                    Vector3 corner = new Vector3(
                        x == 0 ? min.x : max.x,
                        y == 0 ? min.y : max.y,
                        z == 0 ? min.z : max.z);
                    Vector3 localCorner = space.InverseTransformPoint(corner);
                    if (!hasBounds)
                    {
                        result = new Bounds(localCorner, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        result.Encapsulate(localCorner);
                    }
                }
            }
            return result;
        }
    }
}
