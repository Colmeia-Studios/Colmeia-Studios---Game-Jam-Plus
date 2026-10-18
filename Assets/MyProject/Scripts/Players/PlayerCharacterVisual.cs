using System;
using UnityEngine;

namespace ColmeiaStudios.SoMaisUmPedido
{
    /// <summary>Instantiates the role-specific third-person model and drives its locomotion Animator.</summary>
    public sealed class PlayerCharacterVisual : MonoBehaviour
    {
        [Serializable]
        private sealed class ModelSetup
        { 
            [SerializeField] private GameObject prefab;
            [SerializeField] private Vector3 localPosition;
            [SerializeField] private Vector3 localEulerAngles;
            [SerializeField] private Vector3 localScale = Vector3.one;

            public GameObject Prefab => prefab;
            public Vector3 LocalPosition => localPosition;
            public Quaternion LocalRotation => Quaternion.Euler(localEulerAngles);
            public Vector3 LocalScale => localScale;
        }

        [Header("Role models")]
        [SerializeField] private ModelSetup cookModel = new ModelSetup();
        [SerializeField] private ModelSetup deliveryModel = new ModelSetup();

        private GameObject modelInstance;
        private Animator animator;
        private PlayerRole currentRole;
        private bool hasCurrentRole;
        private bool runParameterAvailable;
        private bool runWithMealParameterAvailable;
        private bool missingModelReported;
        private int visualLayer = -1;

        public int VisualLayer => visualLayer;

        public void SetRole(PlayerRole role)
        {
            if (role != PlayerRole.Cook && role != PlayerRole.Delivery)
            {
                Debug.LogError($"PlayerCharacterVisual on '{name}' received invalid role '{role}'.", this);
                return;
            }

            if (hasCurrentRole && currentRole == role && modelInstance != null)
                return;

            GameObject previousModel = modelInstance;
            modelInstance = null;
            animator = null;
            if (previousModel != null)
            {
                previousModel.SetActive(false);
                Destroy(previousModel);
            }

            currentRole = role;
            hasCurrentRole = true;
            ModelSetup setup = role == PlayerRole.Cook ? cookModel : deliveryModel;
            if (setup.Prefab == null)
            {
                if (!missingModelReported)
                {
                    Debug.LogError($"PlayerCharacterVisual on '{name}' has no prefab assigned for role '{role}'.", this);
                    missingModelReported = true;
                }

                return;
            }

            modelInstance = Instantiate(setup.Prefab, transform, false);
            modelInstance.name = $"{setup.Prefab.name}_Visual";
            Transform modelTransform = modelInstance.transform;
            modelTransform.localPosition = setup.LocalPosition;
            modelTransform.localRotation = setup.LocalRotation;
            modelTransform.localScale = setup.LocalScale;
            missingModelReported = false;

            if (visualLayer >= 0)
                ApplyLayerRecursively(modelInstance.transform, visualLayer);

            animator = modelInstance.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                Debug.LogError($"The '{role}' visual prefab '{setup.Prefab.name}' has no Animator in its hierarchy.", setup.Prefab);
                return;
            }

            runParameterAvailable = HasBoolParameter(animator, "Run", setup.Prefab);
            runWithMealParameterAvailable = role == PlayerRole.Cook &&
                                            HasBoolParameter(animator, "RunWithMeal", setup.Prefab);
            SetLocomotion(false, false);
        }

        public void SetVisualLayer(int layer)
        {
            if (layer < 0 || layer > 31)
            {
                Debug.LogError($"PlayerCharacterVisual on '{name}' received invalid rendering layer index {layer}.", this);
                return;
            }

            visualLayer = layer;
            if (modelInstance != null)
                ApplyLayerRecursively(modelInstance.transform, visualLayer);
        }

        public void SetLocomotion(bool isMoving, bool isCarryingPreparedMealbox)
        {
            if (animator == null)
                return;

            if (runParameterAvailable)
                animator.SetBool("Run", isMoving);
            if (currentRole == PlayerRole.Cook && runWithMealParameterAvailable)
                animator.SetBool("RunWithMeal", isCarryingPreparedMealbox);
        }

        private static bool HasBoolParameter(Animator target, string parameterName, UnityEngine.Object context)
        {
            foreach (AnimatorControllerParameter parameter in target.parameters)
            {
                if (parameter.name != parameterName)
                    continue;

                if (parameter.type == AnimatorControllerParameterType.Bool)
                    return true;

                Debug.LogError($"Animator on '{context.name}' has '{parameterName}', but it is not a Bool parameter.", context);
                return false;
            }

            Debug.LogError($"Animator on '{context.name}' is missing the Bool parameter '{parameterName}'.", context);
            return false;
        }

        private static void ApplyLayerRecursively(Transform target, int layer)
        {
            target.gameObject.layer = layer;
            for (int child = 0; child < target.childCount; child++)
                ApplyLayerRecursively(target.GetChild(child), layer);
        }
    }
}
