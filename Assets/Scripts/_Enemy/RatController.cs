using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[DisallowMultipleComponent]
public class RatController : MonoBehaviour
{
    private enum RatState
    {
        MoveToC,
        IdleAtC,
        MoveToA,
        IdleAtA,
        FleeToBurrow,
        HiddenInBurrow
    }

    private struct BoxTriggerCache
    {
        public BoxCollider2D collider;
        public Vector2 size;
        public Vector2 offset;
    }

    private struct CircleTriggerCache
    {
        public CircleCollider2D collider;
        public float radius;
        public Vector2 offset;
    }

    private struct CapsuleTriggerCache
    {
        public CapsuleCollider2D collider;
        public Vector2 size;
        public Vector2 offset;
    }

    [Header("Точки маршрута")]
    [SerializeField, Tooltip("Точка A. Обычно стартовая/левая точка патруля.")]
    private Transform pointA;

    [SerializeField, Tooltip("Точка C. Правая точка патруля.")]
    private Transform pointC;

    [SerializeField, Tooltip("Точка B / точка норки. Сюда крыса убегает при тревоге.")]
    private Transform burrowPoint;

    [Header("Основные ссылки")]
    [SerializeField, Tooltip("Rigidbody2D крысы. Если не задан, возьмется автоматически с этого же объекта.")]
    private Rigidbody2D rb;

    [SerializeField, Tooltip("Animator крысы. Если не задан, возьмется автоматически с этого же объекта.")]
    private Animator ratAnimator;

    [SerializeField, Tooltip("SpriteRenderer крысы. Если не задан, будет найден автоматически.")]
    private SpriteRenderer spriteRenderer;

    [SerializeField, Tooltip("Animator объекта норки. Назначь сюда Animator именно объекта норки.")]
    private Animator burrowAnimator;

    [Header("Визуал / разворот")]
    [SerializeField, Tooltip("Визуальный корень крысы. Лучше назначить сюда child-объект, внутри которого лежат спрайт, свет и прочий визуал. Тогда разворот и scale не будут трогать физический root.")]
    private Transform visualRoot;

    [SerializeField, Tooltip("Если включено — крыса будет разворачиваться через scale по X у visualRoot. Это зеркалит не только спрайт, но и свет/дочерние визуальные объекты.")]
    private bool flipVisualRootByMoveDirection = true;

    [Header("Свет крысы")]
    [SerializeField, Tooltip("Свет, который висит на крысе. Например твой _Rat_Light. Если не задан — будет найден в детях автоматически.")]
    private Light2D ratLight;

    [SerializeField, Tooltip("Если включено — свет крысы будет отключаться, пока крыса сидит в норке.")]
    private bool disableRatLightInBurrow = true;

    [Header("Поиск игрока")]
    [SerializeField, Tooltip("Тег игрока. Если у тебя объект игрока имеет именно tag = player, оставляй как есть.")]
    private string playerTag = "player";

    [SerializeField, Tooltip("Если включено — пока игрок находится в trigger-зоне обнаружения крысы, крыса вообще не вылезет из норки.")]
    private bool keepRatHiddenWhilePlayerInDetectionZone = true;

    [SerializeField, Tooltip("Если включено — trigger-зоны обнаружения будут дополнительно компенсироваться, когда крыса уменьшается у норки.")]
    private bool compensateDetectionTriggersWhileScaled = true;

    [SerializeField, Tooltip("Насколько сильно расширять trigger-зоны поверх обычной компенсации scale. 1 = просто вернуть исходный размер, 1.5-2.5 = сделать зону заметно больше.")]
    [Min(1f)]
    private float detectionTriggerCompensationMultiplier = 1.9f;

    [SerializeField, Tooltip("Если включено — когда крыса сидит в норке, Rigidbody переводится в Kinematic вместо полного simulated=false, чтобы trigger-зона продолжала видеть игрока.")]
    private bool keepBurrowDetectionWorkingWhenHidden = true;

    [Header("Движение")]
    [SerializeField, Tooltip("Скорость обычного патруля между A и C.")]
    private float patrolSpeed = 2f;

    [SerializeField, Tooltip("Скорость побега в норку.")]
    private float fleeSpeed = 4.5f;

    [SerializeField, Tooltip("На каком расстоянии по X считать, что точка достигнута.")]
    private float reachDistanceX = 0.08f;

    [SerializeField, Tooltip("Если включено — при старте крыса будет поставлена в точку A.")]
    private bool snapToAOnStart = false;

    [SerializeField, Tooltip("Если включено — в точках патруля A/C крыса будет подравниваться еще и по Y.")]
    private bool snapYToPatrolPoint = false;

    [SerializeField, Tooltip("Если включено — при входе в норку крыса будет точно поставлена в burrowPoint по X и Y.")]
    private bool snapToBurrowPointFully = true;

    [SerializeField, Tooltip("Если включено — после выхода из норки крыса сначала всегда идет к A.")]
    private bool returnToAAfterBurrow = true;

    [Header("Паузы")]
    [SerializeField, Tooltip("Сколько секунд крыса стоит в idle в точке A.")]
    private float idleAtASeconds = 1.5f;

    [SerializeField, Tooltip("Сколько секунд крыса стоит в idle в точке C.")]
    private float idleAtCSeconds = 1.5f;

    [SerializeField, Tooltip("Сколько секунд крыса сидит в норке после того, как добежала до нее.")]
    private float burrowHideSeconds = 2f;

    [Header("Препятствия / перепрыгивание")]
    [SerializeField, Tooltip("Включить автоматический перепрыг препятствий.")]
    private bool enableObstacleJump = true;

    [SerializeField, Tooltip("Тег препятствия. По твоему описанию это wall.")]
    private string obstacleTag = "wall";

    [SerializeField, Tooltip("Слои, по которым вообще делать BoxCast вперед. Так как земля и wall могут быть на одном слое, финальная фильтрация дальше все равно идет по тегу.")]
    private LayerMask obstacleDetectMask = Physics2D.AllLayers;

    [SerializeField, Tooltip("Игнорировать trigger-коллайдеры при поиске препятствия.")]
    private bool ignoreTriggerCollidersAsObstacles = true;

    [SerializeField, Tooltip("Сила прыжка вверх при перепрыгивании препятствия.")]
    private float obstacleJumpForce = 5.5f;

    [SerializeField, Tooltip("Минимальная пауза между автопрыжками.")]
    private float jumpCooldown = 0.25f;

    [SerializeField, Tooltip("Точка, откуда проверяется препятствие впереди. Если не задана — используется позиция крысы.")]
    private Transform obstacleCheckOrigin;

    [SerializeField, Tooltip("Размер бокса проверки препятствия впереди.")]
    private Vector2 obstacleCheckBoxSize = new Vector2(0.3f, 0.6f);

    [SerializeField, Tooltip("Дистанция проверки препятствия впереди.")]
    private float obstacleCheckDistance = 0.2f;

    [SerializeField, Tooltip("Точка проверки земли. Если не задана — используется позиция крысы.")]
    private Transform groundCheckPoint;

    [SerializeField, Tooltip("Длина луча проверки земли вниз.")]
    private float groundCheckDistance = 0.15f;

    [SerializeField, Tooltip("Слои земли.")]
    private LayerMask groundMask;

    [Header("Физика и коллайдеры в норке")]
    [SerializeField, Tooltip("Если включено — пока крыса в норке, у Rigidbody2D отключается simulated.")]
    private bool disableRbSimulationInBurrow = true;

    [SerializeField, Tooltip("Если включено — пока крыса в норке, ее коллайдеры будут отключаться.")]
    private bool disableCollidersInBurrow = true;

    [SerializeField, Tooltip("Если включено — в норке отключаются только НЕ trigger-коллайдеры. Это безопаснее, потому что зона обнаружения игрока может остаться рабочей.")]
    private bool disableOnlySolidCollidersInBurrow = true;

    [Header("Визуал крысы в норке")]
    [SerializeField, Tooltip("Если включено — пока крыса сидит в норке, ее Renderer'ы будут скрыты.")]
    private bool hideRatRenderersInBurrow = true;

    [SerializeField, Tooltip("Если включено — вместе с рендерами в норке будет отключаться и Animator крысы.")]
    private bool disableRatAnimatorInBurrow = false;

    [Header("Уменьшение крысы при нырке в норку")]
    [SerializeField, Tooltip("Если включено — крыса будет визуально уменьшаться, когда подбегает к норке.")]
    private bool enableBurrowScaleEffect = true;

    [SerializeField, Tooltip("Во сколько раз уменьшать крысу в норке. 1 = без изменений, 0.2 = сильно уменьшить.")]
    [Range(0.05f, 1f)]
    private float burrowTargetScale = 0.2f;

    [SerializeField, Tooltip("За какое расстояние до burrowPoint начинать плавно уменьшать крысу.")]
    private float burrowScaleStartDistance = 0.7f;

    [SerializeField, Tooltip("Скорость уменьшения крысы при нырке.")]
    private float burrowScaleLerpSpeed = 8f;

    [SerializeField, Tooltip("Скорость возврата крысы к обычному размеру после выхода из норки.")]
    private float normalScaleLerpSpeed = 8f;

    [Header("Игнор столкновения с игроком")]
    [SerializeField, Tooltip("Если включено — физические коллайдеры крысы будут игнорировать коллайдеры игрока. Trigger-зона обнаружения при этом НЕ отключается.")]
    private bool ignoreSolidCollisionWithPlayer = true;

    [SerializeField, Tooltip("Если включено — игнор столкновений будет применяться автоматически ко всем найденным коллайдерам объекта игрока и его детей.")]
    private bool autoIgnorePlayerCollisionsOnStart = true;

    [Header("Анимации крысы")]
    [SerializeField, Tooltip("Имя состояния Animator для бега.")]
    private string runStateName = "run";

    [SerializeField, Tooltip("Имя состояния Animator для idle.")]
    private string idleStateName = "ide";

    [SerializeField, Tooltip("Время плавного перехода между анимациями.")]
    private float animationCrossFade = 0.05f;

    [Header("Анимация норки")]
    [SerializeField, Tooltip("Триггер Animator норки на вход крысы.")]
    private string burrowEnterTrigger = "Enter";

    [SerializeField, Tooltip("Триггер Animator норки на выход крысы.")]
    private string burrowExitTrigger = "Exit";

    [SerializeField, Tooltip("Если включено — перед установкой одного триггера будет сбрасываться противоположный.")]
    private bool resetOppositeBurrowTrigger = true;

    [Header("Отладка")]
    [SerializeField, Tooltip("Показывать gizmo-проверки в Scene view.")]
    private bool drawGizmos = true;

    [SerializeField, Tooltip("Показывать в Scene View примерный прямоугольник, по которому сейчас проверяется наличие игрока рядом с норкой.")]
    private bool drawDetectionGizmos = true;

    private RatState currentState;
    private bool stateInitialized = false;

    private float stateTimer = 0f;
    private float nextAllowedJumpTime = 0f;
    private int currentMoveDir = 1;

    private Collider2D[] cachedAllColliders;
    private Collider2D[] cachedSolidColliders;
    private Collider2D[] cachedTriggerColliders;
    private Renderer[] cachedRenderers;

    private BoxTriggerCache[] cachedBoxTriggers;
    private CircleTriggerCache[] cachedCircleTriggers;
    private CapsuleTriggerCache[] cachedCapsuleTriggers;

    private Vector3 visualBaseLocalScale;
    private float currentScaleMultiplier = 1f;
    private int currentVisualFacingSign = 1;
    private RigidbodyType2D originalBodyType;

    private Transform VisualTarget => visualRoot != null ? visualRoot : transform;

    private void Reset()
    {
        rb = GetComponent<Rigidbody2D>();
        ratAnimator = GetComponent<Animator>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        ratLight = GetComponentInChildren<Light2D>(true);
        visualRoot = transform;
    }

    private void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (ratAnimator == null) ratAnimator = GetComponent<Animator>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (ratLight == null) ratLight = GetComponentInChildren<Light2D>(true);

        CacheComponents();

        if (rb != null)
        {
            rb.freezeRotation = true;
            originalBodyType = rb.bodyType;
        }

        visualBaseLocalScale = VisualTarget.localScale;
        currentVisualFacingSign = visualBaseLocalScale.x < 0f ? -1 : 1;
        currentScaleMultiplier = 1f;

        ApplyVisualScaleAndDirection();
        UpdateDetectionTriggerCompensation();
    }

    private void Start()
    {
        if (snapToAOnStart && pointA != null)
        {
            Vector2 pos = rb != null ? rb.position : (Vector2)transform.position;
            pos.x = pointA.position.x;

            if (snapYToPatrolPoint)
                pos.y = pointA.position.y;

            if (rb != null) rb.position = pos;
            else transform.position = new Vector3(pos.x, pos.y, transform.position.z);
        }

        ResetBurrowTriggers();

        SetBurrowPhysicsDisabled(false);
        SetRatVisualHidden(false);
        SetRatLightEnabled(true);

        if (autoIgnorePlayerCollisionsOnStart && ignoreSolidCollisionWithPlayer)
            TryIgnoreCollisionWithPlayerByTag();

        SetState(RatState.MoveToC, true);
    }

    private void Update()
    {
        HandleStateLogic();
        UpdateBurrowScaleEffect();
    }

    private void FixedUpdate()
    {
        HandleMovement();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!enabled) return;
        if (!IsPlayerCollider(other)) return;

        if (ignoreSolidCollisionWithPlayer)
            IgnoreCollisionWithPlayerRoot(other);

        if (currentState != RatState.FleeToBurrow && currentState != RatState.HiddenInBurrow)
            SetState(RatState.FleeToBurrow);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!ignoreSolidCollisionWithPlayer) return;
        if (collision == null || collision.collider == null) return;
        if (!IsPlayerCollider(collision.collider)) return;

        IgnoreCollisionWithPlayerRoot(collision.collider);
    }

    private void CacheComponents()
    {
        cachedAllColliders = GetComponentsInChildren<Collider2D>(true);
        cachedRenderers = GetComponentsInChildren<Renderer>(true);

        List<Collider2D> solidCols = new List<Collider2D>();
        List<Collider2D> triggerCols = new List<Collider2D>();
        List<BoxTriggerCache> boxTriggers = new List<BoxTriggerCache>();
        List<CircleTriggerCache> circleTriggers = new List<CircleTriggerCache>();
        List<CapsuleTriggerCache> capsuleTriggers = new List<CapsuleTriggerCache>();

        for (int i = 0; i < cachedAllColliders.Length; i++)
        {
            Collider2D col = cachedAllColliders[i];
            if (col == null) continue;

            if (col.isTrigger)
            {
                triggerCols.Add(col);

                BoxCollider2D box = col as BoxCollider2D;
                if (box != null)
                {
                    boxTriggers.Add(new BoxTriggerCache
                    {
                        collider = box,
                        size = box.size,
                        offset = box.offset
                    });
                }

                CircleCollider2D circle = col as CircleCollider2D;
                if (circle != null)
                {
                    circleTriggers.Add(new CircleTriggerCache
                    {
                        collider = circle,
                        radius = circle.radius,
                        offset = circle.offset
                    });
                }

                CapsuleCollider2D capsule = col as CapsuleCollider2D;
                if (capsule != null)
                {
                    capsuleTriggers.Add(new CapsuleTriggerCache
                    {
                        collider = capsule,
                        size = capsule.size,
                        offset = capsule.offset
                    });
                }
            }
            else
            {
                solidCols.Add(col);
            }
        }

        cachedSolidColliders = solidCols.ToArray();
        cachedTriggerColliders = triggerCols.ToArray();
        cachedBoxTriggers = boxTriggers.ToArray();
        cachedCircleTriggers = circleTriggers.ToArray();
        cachedCapsuleTriggers = capsuleTriggers.ToArray();
    }

    private void HandleStateLogic()
    {
        switch (currentState)
        {
            case RatState.MoveToC:
                if (HasReachedPoint(pointC))
                {
                    SnapToPatrolPoint(pointC);
                    SetState(RatState.IdleAtC);
                }
                break;

            case RatState.IdleAtC:
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                    SetState(RatState.MoveToA);
                break;

            case RatState.MoveToA:
                if (HasReachedPoint(pointA))
                {
                    SnapToPatrolPoint(pointA);
                    SetState(RatState.IdleAtA);
                }
                break;

            case RatState.IdleAtA:
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                    SetState(RatState.MoveToC);
                break;

            case RatState.FleeToBurrow:
                if (HasReachedPoint(burrowPoint))
                    SetState(RatState.HiddenInBurrow);
                break;

            case RatState.HiddenInBurrow:
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                {
                    if (keepRatHiddenWhilePlayerInDetectionZone && IsPlayerInsideDetectionZone())
                        return;

                    ExitBurrowAndResumePatrol();
                }
                break;
        }
    }

    private void HandleMovement()
    {
        switch (currentState)
        {
            case RatState.MoveToC:
                MoveTowards(pointC, patrolSpeed);
                break;

            case RatState.MoveToA:
                MoveTowards(pointA, patrolSpeed);
                break;

            case RatState.FleeToBurrow:
                MoveTowards(burrowPoint, fleeSpeed);
                break;

            case RatState.IdleAtA:
            case RatState.IdleAtC:
            case RatState.HiddenInBurrow:
                StopHorizontal();
                break;
        }
    }

    private void MoveTowards(Transform targetPoint, float speed)
    {
        if (targetPoint == null || rb == null) return;
        if (!rb.simulated && disableRbSimulationInBurrow) return;

        float deltaX = targetPoint.position.x - rb.position.x;

        if (Mathf.Abs(deltaX) <= reachDistanceX)
        {
            StopHorizontal();
            return;
        }

        int dir = deltaX > 0f ? 1 : -1;
        currentMoveDir = dir;

        if (flipVisualRootByMoveDirection)
            UpdateVisualDirection(dir);

        if (enableObstacleJump)
            TryJumpOverObstacle(dir);

        Vector2 velocity = rb.velocity;
        velocity.x = dir * speed;
        rb.velocity = velocity;
    }

    private void StopHorizontal()
    {
        if (rb == null) return;
        if (!rb.simulated && disableRbSimulationInBurrow) return;

        Vector2 velocity = rb.velocity;
        velocity.x = 0f;
        rb.velocity = velocity;
    }

    private void TryJumpOverObstacle(int dir)
    {
        if (!enableObstacleJump) return;
        if (rb == null) return;
        if (Time.time < nextAllowedJumpTime) return;
        if (!IsGrounded()) return;
        if (!HasObstacleAheadByTag(dir)) return;

        Vector2 velocity = rb.velocity;
        velocity.y = Mathf.Max(velocity.y, obstacleJumpForce);
        rb.velocity = velocity;

        nextAllowedJumpTime = Time.time + jumpCooldown;
    }

    private bool IsGrounded()
    {
        Vector2 origin = groundCheckPoint != null
            ? (Vector2)groundCheckPoint.position
            : rb != null
                ? rb.position + Vector2.down * 0.5f
                : (Vector2)transform.position + Vector2.down * 0.5f;

        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, groundCheckDistance, groundMask);
        return hit.collider != null;
    }

    private bool HasObstacleAheadByTag(int dir)
    {
        Vector2 origin = obstacleCheckOrigin != null
            ? (Vector2)obstacleCheckOrigin.position
            : rb != null
                ? rb.position + new Vector2(dir * 0.35f, 0f)
                : (Vector2)transform.position + new Vector2(dir * 0.35f, 0f);

        RaycastHit2D[] hits = Physics2D.BoxCastAll(
            origin,
            obstacleCheckBoxSize,
            0f,
            Vector2.right * dir,
            obstacleCheckDistance,
            obstacleDetectMask
        );

        if (hits == null || hits.Length == 0)
            return false;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i].collider;
            if (col == null) continue;

            if (col.transform == transform || col.transform.IsChildOf(transform))
                continue;

            if (ignoreTriggerCollidersAsObstacles && col.isTrigger)
                continue;

            if (col.CompareTag(obstacleTag))
                return true;
        }

        return false;
    }

    private bool HasReachedPoint(Transform point)
    {
        if (point == null) return false;

        float currentX = rb != null ? rb.position.x : transform.position.x;
        return Mathf.Abs(currentX - point.position.x) <= reachDistanceX;
    }

    private void SnapToPatrolPoint(Transform point)
    {
        if (point == null) return;

        if (rb != null)
        {
            Vector2 pos = rb.position;
            pos.x = point.position.x;

            if (snapYToPatrolPoint)
                pos.y = point.position.y;

            rb.position = pos;
        }
        else
        {
            Vector3 pos = transform.position;
            pos.x = point.position.x;

            if (snapYToPatrolPoint)
                pos.y = point.position.y;

            transform.position = pos;
        }
    }

    private void SnapToBurrowPoint()
    {
        if (burrowPoint == null) return;

        if (rb != null)
        {
            Vector2 pos = rb.position;
            pos.x = burrowPoint.position.x;

            if (snapToBurrowPointFully)
                pos.y = burrowPoint.position.y;

            rb.position = pos;
        }
        else
        {
            Vector3 pos = transform.position;
            pos.x = burrowPoint.position.x;

            if (snapToBurrowPointFully)
                pos.y = burrowPoint.position.y;

            transform.position = pos;
        }
    }

    private RatState GetStateAfterBurrow()
    {
        if (returnToAAfterBurrow)
            return RatState.MoveToA;

        if (pointA == null) return RatState.MoveToC;
        if (pointC == null) return RatState.MoveToA;

        float currentX = rb != null ? rb.position.x : transform.position.x;
        float distToA = Mathf.Abs(currentX - pointA.position.x);
        float distToC = Mathf.Abs(currentX - pointC.position.x);

        return distToA <= distToC ? RatState.MoveToA : RatState.MoveToC;
    }

    private void ExitBurrowAndResumePatrol()
    {
        SetBurrowPhysicsDisabled(false);
        SetRatVisualHidden(false);
        SetRatLightEnabled(true);
        PlayBurrowExitAnimation();

        RatState next = GetStateAfterBurrow();
        SetState(next);
    }

    private void SetState(RatState newState, bool force = false)
    {
        if (stateInitialized && !force && currentState == newState)
            return;

        stateInitialized = true;
        currentState = newState;

        switch (newState)
        {
            case RatState.MoveToC:
                SetBurrowPhysicsDisabled(false);
                SetRatVisualHidden(false);
                SetRatLightEnabled(true);
                PlayRatAnimation(runStateName);
                break;

            case RatState.MoveToA:
                SetBurrowPhysicsDisabled(false);
                SetRatVisualHidden(false);
                SetRatLightEnabled(true);
                PlayRatAnimation(runStateName);
                break;

            case RatState.FleeToBurrow:
                SetBurrowPhysicsDisabled(false);
                SetRatVisualHidden(false);
                SetRatLightEnabled(true);
                PlayRatAnimation(runStateName);
                break;

            case RatState.IdleAtC:
                SetBurrowPhysicsDisabled(false);
                SetRatVisualHidden(false);
                SetRatLightEnabled(true);
                StopHorizontal();
                stateTimer = idleAtCSeconds;
                PlayRatAnimation(idleStateName);
                break;

            case RatState.IdleAtA:
                SetBurrowPhysicsDisabled(false);
                SetRatVisualHidden(false);
                SetRatLightEnabled(true);
                StopHorizontal();
                stateTimer = idleAtASeconds;
                PlayRatAnimation(idleStateName);
                break;

            case RatState.HiddenInBurrow:
                StopHorizontal();
                SnapToBurrowPoint();
                stateTimer = burrowHideSeconds;

                PlayBurrowEnterAnimation();

                SetBurrowPhysicsDisabled(true);
                SetRatVisualHidden(hideRatRenderersInBurrow);
                SetRatLightEnabled(false);
                break;
        }
    }

    private void SetBurrowPhysicsDisabled(bool disabled)
    {
        if (rb != null && disableRbSimulationInBurrow)
        {
            if (disabled)
            {
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;

                bool canKeepDetectionAlive = keepBurrowDetectionWorkingWhenHidden && HasActiveTriggerDetectionSetup();
                if (canKeepDetectionAlive)
                {
                    rb.simulated = true;
                    rb.bodyType = RigidbodyType2D.Kinematic;
                }
                else
                {
                    rb.simulated = false;
                }
            }
            else
            {
                rb.simulated = true;
                rb.bodyType = originalBodyType;
            }
        }

        if (!disableCollidersInBurrow || cachedAllColliders == null)
            return;

        for (int i = 0; i < cachedAllColliders.Length; i++)
        {
            Collider2D col = cachedAllColliders[i];
            if (col == null) continue;

            bool keepThisTriggerEnabled = disabled && keepBurrowDetectionWorkingWhenHidden && col.isTrigger;
            if (keepThisTriggerEnabled)
            {
                col.enabled = true;
                continue;
            }

            if (disableOnlySolidCollidersInBurrow && col.isTrigger)
                continue;

            col.enabled = !disabled;
        }
    }

    private bool HasActiveTriggerDetectionSetup()
    {
        return cachedTriggerColliders != null && cachedTriggerColliders.Length > 0;
    }

    private void SetRatVisualHidden(bool hidden)
    {
        if (cachedRenderers != null)
        {
            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                if (cachedRenderers[i] == null) continue;
                cachedRenderers[i].enabled = !hidden;
            }
        }

        if (ratAnimator != null && disableRatAnimatorInBurrow)
            ratAnimator.enabled = !hidden;
    }

    private void SetRatLightEnabled(bool enabledState)
    {
        if (!disableRatLightInBurrow) return;
        if (ratLight == null) return;

        ratLight.enabled = enabledState;
    }

    private void PlayRatAnimation(string stateName)
    {
        if (ratAnimator == null) return;
        if (!ratAnimator.enabled) return;
        if (string.IsNullOrWhiteSpace(stateName)) return;

        ratAnimator.CrossFade(stateName, animationCrossFade, 0);
    }

    private void PlayBurrowEnterAnimation()
    {
        if (burrowAnimator == null) return;
        if (string.IsNullOrWhiteSpace(burrowEnterTrigger)) return;

        if (resetOppositeBurrowTrigger && !string.IsNullOrWhiteSpace(burrowExitTrigger))
            burrowAnimator.ResetTrigger(burrowExitTrigger);

        burrowAnimator.SetTrigger(burrowEnterTrigger);
    }

    private void PlayBurrowExitAnimation()
    {
        if (burrowAnimator == null) return;
        if (string.IsNullOrWhiteSpace(burrowExitTrigger)) return;

        if (resetOppositeBurrowTrigger && !string.IsNullOrWhiteSpace(burrowEnterTrigger))
            burrowAnimator.ResetTrigger(burrowEnterTrigger);

        burrowAnimator.SetTrigger(burrowExitTrigger);
    }

    private void ResetBurrowTriggers()
    {
        if (burrowAnimator == null) return;

        if (!string.IsNullOrWhiteSpace(burrowEnterTrigger))
            burrowAnimator.ResetTrigger(burrowEnterTrigger);

        if (!string.IsNullOrWhiteSpace(burrowExitTrigger))
            burrowAnimator.ResetTrigger(burrowExitTrigger);
    }

    private void UpdateBurrowScaleEffect()
    {
        if (!enableBurrowScaleEffect)
        {
            currentScaleMultiplier = 1f;
            ApplyVisualScaleAndDirection();
            UpdateDetectionTriggerCompensation();
            return;
        }

        float targetMultiplier = 1f;
        float lerpSpeed = normalScaleLerpSpeed;

        if (currentState == RatState.FleeToBurrow && burrowPoint != null)
        {
            float currentX = rb != null ? rb.position.x : transform.position.x;
            float distToBurrowX = Mathf.Abs(currentX - burrowPoint.position.x);

            if (burrowScaleStartDistance <= 0.0001f)
            {
                targetMultiplier = burrowTargetScale;
            }
            else
            {
                float t = Mathf.Clamp01(1f - (distToBurrowX / burrowScaleStartDistance));
                targetMultiplier = Mathf.Lerp(1f, burrowTargetScale, t);
            }

            lerpSpeed = burrowScaleLerpSpeed;
        }
        else if (currentState == RatState.HiddenInBurrow)
        {
            targetMultiplier = burrowTargetScale;
            lerpSpeed = burrowScaleLerpSpeed;
        }

        currentScaleMultiplier = Mathf.MoveTowards(currentScaleMultiplier, targetMultiplier, lerpSpeed * Time.deltaTime);
        ApplyVisualScaleAndDirection();
        UpdateDetectionTriggerCompensation();
    }

    private void ApplyVisualScaleAndDirection()
    {
        Transform target = VisualTarget;
        Vector3 baseScale = visualBaseLocalScale;

        float baseX = Mathf.Abs(baseScale.x);
        float baseY = Mathf.Abs(baseScale.y);

        target.localScale = new Vector3(
            baseX * currentScaleMultiplier * currentVisualFacingSign,
            baseY * currentScaleMultiplier,
            baseScale.z
        );
    }

    private void UpdateVisualDirection(int dir)
    {
        if (dir == 0) return;

        currentVisualFacingSign = dir < 0 ? -1 : 1;
        ApplyVisualScaleAndDirection();
        UpdateDetectionTriggerCompensation();
    }

    private void UpdateDetectionTriggerCompensation()
    {
        RestoreOriginalTriggerShapes();

        if (!compensateDetectionTriggersWhileScaled)
            return;

        if (currentScaleMultiplier >= 0.9999f)
            return;

        float compensation = (1f / Mathf.Max(currentScaleMultiplier, 0.0001f)) * detectionTriggerCompensationMultiplier;

        ApplyBoxTriggerCompensation(compensation);
        ApplyCircleTriggerCompensation(compensation);
        ApplyCapsuleTriggerCompensation(compensation);
    }

    private void RestoreOriginalTriggerShapes()
    {
        for (int i = 0; i < cachedBoxTriggers.Length; i++)
        {
            if (cachedBoxTriggers[i].collider == null) continue;

            cachedBoxTriggers[i].collider.size = cachedBoxTriggers[i].size;
            cachedBoxTriggers[i].collider.offset = cachedBoxTriggers[i].offset;
        }

        for (int i = 0; i < cachedCircleTriggers.Length; i++)
        {
            if (cachedCircleTriggers[i].collider == null) continue;

            cachedCircleTriggers[i].collider.radius = cachedCircleTriggers[i].radius;
            cachedCircleTriggers[i].collider.offset = cachedCircleTriggers[i].offset;
        }

        for (int i = 0; i < cachedCapsuleTriggers.Length; i++)
        {
            if (cachedCapsuleTriggers[i].collider == null) continue;

            cachedCapsuleTriggers[i].collider.size = cachedCapsuleTriggers[i].size;
            cachedCapsuleTriggers[i].collider.offset = cachedCapsuleTriggers[i].offset;
        }
    }

    private void ApplyBoxTriggerCompensation(float compensation)
    {
        for (int i = 0; i < cachedBoxTriggers.Length; i++)
        {
            BoxCollider2D col = cachedBoxTriggers[i].collider;
            if (col == null) continue;
            if (!DoesVisualScaleAffectCollider(col)) continue;

            col.size = cachedBoxTriggers[i].size * compensation;
            col.offset = cachedBoxTriggers[i].offset * compensation;
        }
    }

    private void ApplyCircleTriggerCompensation(float compensation)
    {
        for (int i = 0; i < cachedCircleTriggers.Length; i++)
        {
            CircleCollider2D col = cachedCircleTriggers[i].collider;
            if (col == null) continue;
            if (!DoesVisualScaleAffectCollider(col)) continue;

            col.radius = cachedCircleTriggers[i].radius * compensation;
            col.offset = cachedCircleTriggers[i].offset * compensation;
        }
    }

    private void ApplyCapsuleTriggerCompensation(float compensation)
    {
        for (int i = 0; i < cachedCapsuleTriggers.Length; i++)
        {
            CapsuleCollider2D col = cachedCapsuleTriggers[i].collider;
            if (col == null) continue;
            if (!DoesVisualScaleAffectCollider(col)) continue;

            col.size = cachedCapsuleTriggers[i].size * compensation;
            col.offset = cachedCapsuleTriggers[i].offset * compensation;
        }
    }

    private bool DoesVisualScaleAffectCollider(Collider2D col)
    {
        if (col == null) return false;

        Transform target = VisualTarget;
        return col.transform == target || col.transform.IsChildOf(target);
    }

    private bool IsPlayerInsideDetectionZone()
    {
        if (cachedTriggerColliders == null || cachedTriggerColliders.Length == 0)
            return false;

        for (int i = 0; i < cachedTriggerColliders.Length; i++)
        {
            Collider2D trigger = cachedTriggerColliders[i];
            if (trigger == null || !trigger.enabled)
                continue;

            Bounds bounds = trigger.bounds;
            if (bounds.size.x <= 0.0001f || bounds.size.y <= 0.0001f)
                continue;

            Collider2D[] hits = Physics2D.OverlapBoxAll(bounds.center, bounds.size, 0f);
            if (hits == null || hits.Length == 0)
                continue;

            for (int j = 0; j < hits.Length; j++)
            {
                Collider2D hit = hits[j];
                if (hit == null) continue;

                if (hit.transform == transform || hit.transform.IsChildOf(transform))
                    continue;

                if (IsPlayerCollider(hit))
                    return true;
            }
        }

        return false;
    }

    private bool IsPlayerCollider(Collider2D other)
    {
        if (other == null) return false;

        if (other.CompareTag(playerTag))
            return true;

        if (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag(playerTag))
            return true;

        Transform root = other.transform.root;
        if (root != null && root.CompareTag(playerTag))
            return true;

        return false;
    }

    private Transform GetPlayerRoot(Collider2D other)
    {
        if (other == null) return null;

        if (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag(playerTag))
            return other.attachedRigidbody.transform;

        if (other.transform.root != null && other.transform.root.CompareTag(playerTag))
            return other.transform.root;

        if (other.CompareTag(playerTag))
            return other.transform;

        return null;
    }

    private void IgnoreCollisionWithPlayerRoot(Collider2D other)
    {
        if (!ignoreSolidCollisionWithPlayer) return;

        Transform playerRoot = GetPlayerRoot(other);
        if (playerRoot == null) return;

        Collider2D[] playerColliders = playerRoot.GetComponentsInChildren<Collider2D>(true);
        if (playerColliders == null || playerColliders.Length == 0) return;
        if (cachedSolidColliders == null || cachedSolidColliders.Length == 0) return;

        for (int i = 0; i < cachedSolidColliders.Length; i++)
        {
            Collider2D ratCol = cachedSolidColliders[i];
            if (ratCol == null) continue;

            for (int j = 0; j < playerColliders.Length; j++)
            {
                Collider2D playerCol = playerColliders[j];
                if (playerCol == null) continue;

                Physics2D.IgnoreCollision(ratCol, playerCol, true);
            }
        }
    }

    private void TryIgnoreCollisionWithPlayerByTag()
    {
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player == null) return;

        Collider2D[] playerColliders = player.GetComponentsInChildren<Collider2D>(true);
        if (playerColliders == null || playerColliders.Length == 0) return;
        if (cachedSolidColliders == null || cachedSolidColliders.Length == 0) return;

        for (int i = 0; i < cachedSolidColliders.Length; i++)
        {
            Collider2D ratCol = cachedSolidColliders[i];
            if (ratCol == null) continue;

            for (int j = 0; j < playerColliders.Length; j++)
            {
                Collider2D playerCol = playerColliders[j];
                if (playerCol == null) continue;

                Physics2D.IgnoreCollision(ratCol, playerCol, true);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        if (pointA != null && pointC != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(pointA.position, pointC.position);
        }

        if (burrowPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(burrowPoint.position, 0.08f);

            if (enableBurrowScaleEffect)
            {
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
                Vector3 left = burrowPoint.position + Vector3.left * burrowScaleStartDistance;
                Vector3 right = burrowPoint.position + Vector3.right * burrowScaleStartDistance;
                Gizmos.DrawLine(left, right);
            }
        }

        Vector2 obstacleOrigin = obstacleCheckOrigin != null
            ? obstacleCheckOrigin.position
            : transform.position + Vector3.right * 0.35f;

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(obstacleOrigin, obstacleCheckBoxSize);
        Gizmos.DrawWireCube(obstacleOrigin + Vector2.right * obstacleCheckDistance, obstacleCheckBoxSize);

        Vector2 groundOrigin = groundCheckPoint != null
            ? groundCheckPoint.position
            : transform.position + Vector3.down * 0.5f;

        Gizmos.color = Color.green;
        Gizmos.DrawLine(groundOrigin, groundOrigin + Vector2.down * groundCheckDistance);
        Gizmos.DrawSphere(groundOrigin + Vector2.down * groundCheckDistance, 0.03f);

        if (drawDetectionGizmos && cachedTriggerColliders != null)
        {
            Gizmos.color = new Color(1f, 0f, 1f, 0.85f);

            for (int i = 0; i < cachedTriggerColliders.Length; i++)
            {
                Collider2D trigger = cachedTriggerColliders[i];
                if (trigger == null) continue;

                Bounds b = trigger.bounds;
                Gizmos.DrawWireCube(b.center, b.size);
            }
        }
    }
}