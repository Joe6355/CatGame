using System.Collections;
using System.Reflection;
using CatGame.SaveSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider2D))]
public class DeadZone : MonoBehaviour
{
    [Header("Фильтр")]
    [SerializeField, Tooltip("Если включено — смерть сработает только для объекта с нужным тегом.")]
    private bool requirePlayerTag = true;

    [SerializeField, Tooltip("Тег игрока.")]
    private string playerTag = "Player";

    [SerializeField, Tooltip("Если включено — дополнительно проверять, что у игрока есть PlayerSaveAdapter.")]
    private bool requirePlayerSaveAdapter = false;

    [Header("Срабатывание")]
    [SerializeField, Tooltip("Срабатывать через OnTriggerEnter2D. Для шипов/ям обычно включено.")]
    private bool useTriggerEnter = true;

    [SerializeField, Tooltip("Срабатывать через OnCollisionEnter2D. Включай только если шипы НЕ trigger.")]
    private bool useCollisionEnter = false;

    [SerializeField, Tooltip("Не давать зоне смерти сработать повторно несколько раз подряд.")]
    private bool restartOnlyOnce = true;

    [SerializeField, Tooltip("Через сколько секунд реального времени выполнить респавн.")]
    private float respawnDelaySeconds = 0f;

    [SerializeField, Tooltip("Через сколько секунд зона снова сможет сработать.")]
    private float cooldownSeconds = 0.75f;

    [SerializeField, Tooltip("Отключать Collider2D зоны смерти на время респавна, чтобы не было двойного срабатывания.")]
    private bool disableColliderDuringRespawn = true;

    [Header("После респавна")]
    [SerializeField, Tooltip("Коротко отключать управление после респавна, чтобы зажатые кнопки не запускали движение/прыжок сразу.")]
    private bool lockInputAfterRespawn = true;

    [SerializeField, Tooltip("Сколько секунд держать управление выключенным после респавна.")]
    private float inputLockAfterRespawnSeconds = 0.08f;

    [SerializeField, Tooltip("Сбрасывать скорость Rigidbody2D после респавна.")]
    private bool resetVelocityAfterRespawn = true;

    [SerializeField, Tooltip("Сбрасывать Animator в дефолтное состояние после респавна.")]
    private bool resetAnimatorAfterRespawn = true;

    [SerializeField, Tooltip("Принудительно выставлять базовые параметры Animator после респавна.")]
    private bool forceIdleAnimatorParameters = true;

    [SerializeField, Tooltip("Подавлять эффект сильного приземления/паунса после респавна.")]
    private bool suppressLandingEffectsAfterRespawn = true;

    [SerializeField, Min(0f), Tooltip("Сколько секунд после респавна запрещать эффекты сильного падения.")]
    private float landingEffectsSuppressSeconds = 0.3f;

    [Header("Камера")]
    [SerializeField, Tooltip("Сообщать Cinemachine-камере, что игрок телепортировался. Чинит улёт камеры после респавна.")]
    private bool snapCinemachineCameraAfterRespawn = true;

    [SerializeField, Tooltip("Если Cinemachine не сработал — дополнительно сдвинуть Main Camera на дельту телепорта. Обычно выключено.")]
    private bool moveMainCameraByRespawnDeltaFallback = false;

    [Header("Fallback")]
    [SerializeField, Tooltip("Если SaveManager не найден — перезапустить сцену старым способом.")]
    private bool restartSceneIfSaveManagerMissing = true;

    [Header("Debug")]
    [SerializeField, Tooltip("Писать сообщения в Console.")]
    private bool verboseLogs = false;

    private Collider2D ownCollider;
    private bool isRestarting;
    private float lastRespawnUnscaledTime = -999f;

    private void Awake()
    {
        ownCollider = GetComponent<Collider2D>();

        if (ownCollider != null && useTriggerEnter)
            ownCollider.isTrigger = true;
    }

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();

        if (col != null)
            col.isTrigger = true;
    }

    private void OnValidate()
    {
        respawnDelaySeconds = Mathf.Max(0f, respawnDelaySeconds);
        cooldownSeconds = Mathf.Max(0f, cooldownSeconds);
        inputLockAfterRespawnSeconds = Mathf.Max(0f, inputLockAfterRespawnSeconds);
        landingEffectsSuppressSeconds = Mathf.Max(0f, landingEffectsSuppressSeconds);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!useTriggerEnter)
            return;

        TryRespawn(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!useCollisionEnter)
            return;

        if (collision == null || collision.collider == null)
            return;

        TryRespawn(collision.collider);
    }

    private void TryRespawn(Collider2D other)
    {
        if (other == null)
            return;

        if (restartOnlyOnce && isRestarting)
            return;

        if (Time.unscaledTime - lastRespawnUnscaledTime < cooldownSeconds)
            return;

        if (!IsPlayer(other))
            return;

        PlayerSaveAdapter playerSaveAdapter = other.GetComponentInParent<PlayerSaveAdapter>();

        if (requirePlayerSaveAdapter && playerSaveAdapter == null)
            return;

        StartCoroutine(RespawnRoutine(playerSaveAdapter));
    }

    private IEnumerator RespawnRoutine(PlayerSaveAdapter playerSaveAdapter)
    {
        isRestarting = true;
        lastRespawnUnscaledTime = Time.unscaledTime;

        if (disableColliderDuringRespawn && ownCollider != null)
            ownCollider.enabled = false;

        Time.timeScale = 1f;

        if (playerSaveAdapter == null)
            playerSaveAdapter = FindObjectOfType<PlayerSaveAdapter>();

        Transform playerTransform = playerSaveAdapter != null ? playerSaveAdapter.transform : null;
        Vector3 positionBeforeRespawn = playerTransform != null ? playerTransform.position : Vector3.zero;

        PlayerController playerController = playerTransform != null
            ? playerTransform.GetComponentInParent<PlayerController>()
            : null;

        if (lockInputAfterRespawn && playerController != null)
            playerController.SetInputEnabled(false);

        if (respawnDelaySeconds > 0f)
            yield return new WaitForSecondsRealtime(respawnDelaySeconds);

        SaveManager saveManager = SaveManager.Instance;

        if (saveManager != null)
        {
            saveManager.RegisterDeathAndRespawn();
        }
        else
        {
            if (verboseLogs)
                Debug.LogWarning("DeadZone: SaveManager не найден.", this);

            if (restartSceneIfSaveManagerMissing)
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                yield break;
            }
        }

        yield return null;

        if (playerSaveAdapter == null)
            playerSaveAdapter = FindObjectOfType<PlayerSaveAdapter>();

        playerTransform = playerSaveAdapter != null ? playerSaveAdapter.transform : playerTransform;

        Vector3 positionAfterRespawn = playerTransform != null ? playerTransform.position : positionBeforeRespawn;
        Vector3 respawnDelta = positionAfterRespawn - positionBeforeRespawn;

        ResetPlayerPhysics(playerTransform);
        ResetPlayerAnimator(playerTransform);
        SuppressLandingEffectsAfterRespawn(playerTransform);
        SnapCameraAfterRespawn(playerTransform, respawnDelta);

        if (inputLockAfterRespawnSeconds > 0f)
            yield return new WaitForSecondsRealtime(inputLockAfterRespawnSeconds);

        if (lockInputAfterRespawn && playerController != null)
            playerController.SetInputEnabled(true);

        if (disableColliderDuringRespawn && ownCollider != null)
            ownCollider.enabled = true;

        isRestarting = false;

        if (verboseLogs)
            Debug.Log("DeadZone: игрок отправлен на последний чекпоинт.", this);
    }

    private void ResetPlayerPhysics(Transform playerTransform)
    {
        if (!resetVelocityAfterRespawn || playerTransform == null)
            return;

        Rigidbody2D rb = playerTransform.GetComponentInParent<Rigidbody2D>();

        if (rb == null)
            rb = playerTransform.GetComponent<Rigidbody2D>();

        if (rb == null)
            return;

        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.Sleep();
        rb.WakeUp();
    }

    private void ResetPlayerAnimator(Transform playerTransform)
    {
        if (!resetAnimatorAfterRespawn || playerTransform == null)
            return;

        Animator animator = playerTransform.GetComponentInChildren<Animator>();

        if (animator == null)
            return;

        animator.Rebind();

        if (forceIdleAnimatorParameters)
        {
            SetAnimatorBoolIfExists(animator, "Grounded", true);
            SetAnimatorBoolIfExists(animator, "Running", false);
            SetAnimatorBoolIfExists(animator, "Jumping", false);
            SetAnimatorBoolIfExists(animator, "Stopping", false);
            SetAnimatorBoolIfExists(animator, "Hooking", false);
            SetAnimatorBoolIfExists(animator, "FenceActive", false);
            SetAnimatorBoolIfExists(animator, "FenceBackFacing", false);

            SetAnimatorFloatIfExists(animator, "SpeedX", 0f);
            SetAnimatorFloatIfExists(animator, "SpeedY", 0f);
            SetAnimatorFloatIfExists(animator, "InputX", 0f);
            SetAnimatorFloatIfExists(animator, "FenceMoveX", 0f);
            SetAnimatorFloatIfExists(animator, "FenceMoveY", 0f);
        }

        animator.Update(0f);
    }

    private void SuppressLandingEffectsAfterRespawn(Transform playerTransform)
    {
        if (!suppressLandingEffectsAfterRespawn || playerTransform == null)
            return;

        float seconds = Mathf.Max(0f, landingEffectsSuppressSeconds);

        PlayerHardLandingImpactFX[] hardLandingFx =
            playerTransform.GetComponentsInChildren<PlayerHardLandingImpactFX>(true);

        for (int i = 0; i < hardLandingFx.Length; i++)
        {
            if (hardLandingFx[i] != null)
                hardLandingFx[i].SuppressAfterRespawn(seconds);
        }

        PlayerController controller = playerTransform.GetComponentInParent<PlayerController>();

        if (controller != null)
            controller.SuppressLandingFeedbackAfterRespawn(seconds);
    }

    private void SnapCameraAfterRespawn(Transform playerTransform, Vector3 respawnDelta)
    {
        if (playerTransform == null)
            return;

        if (snapCinemachineCameraAfterRespawn)
            NotifyCinemachineAboutWarp(playerTransform, respawnDelta);

        if (moveMainCameraByRespawnDeltaFallback && Camera.main != null)
            Camera.main.transform.position += respawnDelta;
    }

    private void NotifyCinemachineAboutWarp(Transform playerTransform, Vector3 respawnDelta)
    {
        if (respawnDelta.sqrMagnitude <= 0.0001f)
            return;

        MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>();

        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];

            if (behaviour == null)
                continue;

            System.Type type = behaviour.GetType();
            string fullName = type.FullName;

            if (string.IsNullOrEmpty(fullName) || !fullName.Contains("Cinemachine"))
                continue;

            Transform followTarget = GetTransformProperty(behaviour, type, "Follow");
            Transform lookAtTarget = GetTransformProperty(behaviour, type, "LookAt");

            bool relatedToPlayer =
                IsSameOrParentChild(followTarget, playerTransform) ||
                IsSameOrParentChild(lookAtTarget, playerTransform) ||
                followTarget == null && lookAtTarget == null;

            if (!relatedToPlayer)
                continue;

            MethodInfo warpMethod = type.GetMethod(
                "OnTargetObjectWarped",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(Transform), typeof(Vector3) },
                null);

            if (warpMethod != null)
                warpMethod.Invoke(behaviour, new object[] { playerTransform, respawnDelta });

            PropertyInfo previousStateProperty = type.GetProperty(
                "PreviousStateIsValid",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (previousStateProperty != null && previousStateProperty.CanWrite)
                previousStateProperty.SetValue(behaviour, false, null);

            FieldInfo previousStateField = type.GetField(
                "PreviousStateIsValid",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (previousStateField != null)
                previousStateField.SetValue(behaviour, false);
        }
    }

    private static Transform GetTransformProperty(MonoBehaviour behaviour, System.Type type, string propertyName)
    {
        PropertyInfo property = type.GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (property == null)
            return null;

        object value = property.GetValue(behaviour, null);
        return value as Transform;
    }

    private static bool IsSameOrParentChild(Transform a, Transform b)
    {
        if (a == null || b == null)
            return false;

        if (a == b)
            return true;

        if (a.IsChildOf(b))
            return true;

        if (b.IsChildOf(a))
            return true;

        return false;
    }

    private static void SetAnimatorBoolIfExists(Animator animator, string parameterName, bool value)
    {
        if (animator == null)
            return;

        AnimatorControllerParameter[] parameters = animator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == parameterName && parameters[i].type == AnimatorControllerParameterType.Bool)
            {
                animator.SetBool(parameterName, value);
                return;
            }
        }
    }

    private static void SetAnimatorFloatIfExists(Animator animator, string parameterName, float value)
    {
        if (animator == null)
            return;

        AnimatorControllerParameter[] parameters = animator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == parameterName && parameters[i].type == AnimatorControllerParameterType.Float)
            {
                animator.SetFloat(parameterName, value);
                return;
            }
        }
    }

    private bool IsPlayer(Collider2D other)
    {
        if (requirePlayerTag)
        {
            bool tagMatched = false;

            if (other.CompareTag(playerTag))
                tagMatched = true;

            if (!tagMatched && other.attachedRigidbody != null && other.attachedRigidbody.CompareTag(playerTag))
                tagMatched = true;

            if (!tagMatched)
            {
                Transform parent = other.transform.parent;

                while (parent != null)
                {
                    if (parent.CompareTag(playerTag))
                    {
                        tagMatched = true;
                        break;
                    }

                    parent = parent.parent;
                }
            }

            if (!tagMatched)
                return false;
        }

        if (requirePlayerSaveAdapter)
        {
            PlayerSaveAdapter adapter = other.GetComponentInParent<PlayerSaveAdapter>();

            if (adapter == null)
                return false;
        }

        return true;
    }
}
