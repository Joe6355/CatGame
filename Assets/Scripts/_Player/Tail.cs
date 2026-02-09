using UnityEngine;

public class Tail : MonoBehaviour
{
    // =========================
    // MAIN
    // =========================
    [Header("Main")]
    [Tooltip("Сколько сегментов (точек) у хвоста. Больше = плавнее, но дороже.\nРекоменд: 8–16 (для пиксель-арта часто 10–14).")]
    public int length = 12;

    [Tooltip("LineRenderer, который рисует хвост.\nРекоменд: назначить в инспекторе или оставить пустым, тогда возьмёт GetComponent<LineRenderer>().")]
    public LineRenderer lineRend;

    [Tooltip("Точка крепления хвоста (якорь). Обычно пустышка (child) у кота в месте хвоста.\nРекоменд: обязательно назначить и поставить её у 'попы'.")]
    public Transform targetDir;

    // =========================
    // MOVEMENT
    // =========================
    [Header("Movement")]
    [Tooltip("Базовое расстояние между сегментами.\nРекоменд: 0.12–0.30 (подбирай под размер спрайта).")]
    public float targetDist = 0.25f;

    [Tooltip("Плавность следования (SmoothDamp smoothTime).\nМеньше = быстрее догоняет, но может дёргаться.\nРекоменд: 0.03–0.08 (обычно 0.05).")]
    public float smoothSpeed = 0.05f;

    // =========================
    // DIRECTION / FLIP / CONVEYOR
    // =========================
    [Header("Direction (Flip / Conveyor)")]
    [Tooltip("Если ВКЛ: хвост строится 'сзади' относительно выбранного направления.\nЕсли ВЫКЛ: хвост будет строиться 'вперёд'.\nРекоменд: ВКЛ (true).")]
    public bool tailBehindDirection = true;

    [Tooltip("Если ВКЛ: направление хвоста берётся по Rigidbody2D.velocity.x (хвост становится 'против движения' — идеально для конвейера/платформ/ветра).\nЕсли ВЫКЛ: направление берётся только по Flip (scale.x).\nРекоменд: ВКЛ (true), если есть конвейеры/платформы.")]
    public bool useVelocityForDirection = true;

    [Tooltip("Откуда брать скорость для определения направления. Обычно Rigidbody2D игрока.\nЕсли пусто — попробует найти в родителях.\nРекоменд: перетащить Rigidbody2D кота сюда.")]
    public Rigidbody2D velocitySource;

    [Tooltip("Мёртвая зона скорости: если |vx| меньше — считаем, что персонаж стоит, чтобы хвост не дёргался.\nРекоменд: 0.03–0.12 (обычно 0.05).")]
    public float velocityDeadZone = 0.05f;

    [Tooltip("Задержка переключения направления (чтобы в момент разворота/малой скорости хвост не мигал и не пропадал).\nРекоменд: 0.05–0.12 (обычно 0.06–0.08).")]
    public float signHoldTime = 0.06f;

    [Header("Direction Fix (anti-stuck)")]
    [Tooltip("Если ВКЛ — при смене направления (знака) хвост мгновенно пересобирается от якоря на новую сторону.\nУбирает 'заедание' хвоста при резком развороте/флипе.\nРекоменд: ВКЛ (true).")]
    public bool resetTailOnSignChange = true;

    [Tooltip("Если ВКЛ — при старте/включении объекта хвост инициализируется прямой линией.\nПолезно, если объект появляется/телепортируется.\nРекоменд: ВКЛ (true).")]
    public bool resetOnEnable = true;

    // =========================
    // STRETCH
    // =========================
    [Header("Stretch")]
    [Tooltip("Насколько можно растянуть сегмент дальше targetDist.\n0 = жёсткая длина, 0.2 = +20%.\nРекоменд: 0.0–0.25 (обычно 0.10–0.20).")]
    [Range(0f, 1f)]
    public float maxStretch = 0.2f;

    // =========================
    // WIGGLE
    // =========================
    [Header("Wiggle")]
    [Tooltip("Скорость волны покачивания.\nРекоменд: 4–10 (обычно 6).")]
    public float wiggleSpeed = 6f;

    [Tooltip("Амплитуда волны (насколько сильно хвост гуляет вверх/вниз).\nРекоменд: 0.05–0.20 (обычно 0.10–0.16).")]
    public float wiggleMagnitude = 0.15f;

    [Tooltip("Фазовый сдвиг между сегментами: больше = волна 'чаще' по длине хвоста.\nРекоменд: 0.25–0.80 (обычно 0.45–0.60).")]
    public float wigglePhaseOffset = 0.5f;

    [Tooltip("Усиление покачивания к кончику хвоста.\n1 = одинаково везде, 1.3 = сильнее к концу.\nРекоменд: 1.0–1.6 (обычно 1.2–1.4).")]
    [Range(0f, 2f)]
    public float tailTipMultiplier = 1.3f;

    // =========================
    // BODY RADIUS (AVOID + CLUMP)
    // =========================
    [Header("Body Radius (avoid + clump)")]
    [Tooltip("Если ВКЛ: хвост не залезает в 'тело' (круг bodyRadius вокруг bodyCenter).\nРекоменд: ВКЛ (true) — убирает некрасивое залезание хвоста в спрайт.")]
    public bool useBodyAvoid = true;

    [Tooltip("Центр 'тела' для ограничения. Обычно transform кота.\nЕсли пусто — возьмёт targetDir.parent, иначе transform текущего объекта.\nРекоменд: указать transform кота.")]
    public Transform bodyCenter;

    [Tooltip("Радиус 'тела': внутрь этого круга хвост не должен заходить.\nРекоменд: 0.22–0.55 (под размер кота). Начни с 0.30–0.40 и подстрой.")]
    public float bodyRadius = 0.35f;

    [Tooltip("Запас к радиусу, чтобы хвост не дрожал на границе (анти-мерцание).\nРекоменд: 0.02–0.06 (обычно 0.03–0.04).")]
    public float bodyPadding = 0.03f;

    [Tooltip("С какого сегмента начинать избегание тела.\n1 = со 2-го сегмента (обычно правильно), 2 = с 3-го и т.д.\nРекоменд: 1.")]
    public int bodyAvoidStartIndex = 1;

    [Tooltip("Если ВКЛ: когда хвост упирается в тело, он может сжиматься и 'сбиваться в комок' у границы.\nРекоменд: ВКЛ (true) — выглядит лучше при упоре/конвейере.")]
    public bool clumpWhenBlocked = true;

    [Tooltip("Насколько сильно можно сжать хвост у упора (минимальная дистанция = targetDist * clumpMinDistMul).\n0.2 = до 20% длины сегмента.\nРекоменд: 0.10–0.35 (обычно 0.18–0.28).")]
    [Range(0f, 1f)]
    public float clumpMinDistMul = 0.2f;

    [Tooltip("Во сколько раз ослаблять волну, когда хвост уткнулся в тело.\n0 = совсем без волны у упора, 1 = как обычно.\nРекоменд: 0.20–0.60 (обычно 0.30–0.45).")]
    [Range(0f, 1f)]
    public float blockedWiggleMul = 0.35f;

    // =========================
    // GIZMOS
    // =========================
    [Header("Gizmos (visualize bodyRadius)")]
    [Tooltip("Рисовать круг bodyRadius (и padding) гизмосами.\nВидно в Scene, а в Game — если включить кнопку Gizmos.\nРекоменд: ВКЛ (true) на этапе настройки.")]
    public bool drawBodyRadiusGizmo = true;

    [Tooltip("Если ВКЛ: рисовать гизмос всегда.\nЕсли ВЫКЛ: рисовать только когда объект выбран.\nРекоменд: ВЫКЛ (false), чтобы не мешало.")]
    public bool drawAlways = false;

    // =========================
    // OPTIONAL BODY PARTS
    // =========================
    [Header("Optional Body Parts (2D, no rotation)")]
    [Tooltip("Доп. части (кости/спрайты) для сегментов хвоста. Если задано — i-й элемент ставится в позицию сегмента.\nРекоменд: можно оставить пустым, если хвост только LineRenderer.")]
    public Transform[] bodyParts;

    // =========================
    // INTERNAL
    // =========================
    private Vector3[] segmentPoses;
    private Vector3[] segmentV;

    private float currentSign = 1f;
    private float pendingSign = 1f;
    private float pendingSince = 0f;

    private float lastAppliedSign = 1f;
    private int lastLen = -1;

    private void Awake()
    {
        if (velocitySource == null)
            velocitySource = GetComponentInParent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        EnsureInitialized();

        if (resetOnEnable)
        {
            // Инициализация хвоста сразу в корректную сторону
            float desiredSign = GetDesiredSign();
            currentSign = desiredSign;
            pendingSign = desiredSign;
            pendingSince = Time.time;

            Vector3 forward = Vector3.right * currentSign;
            Vector3 dir = tailBehindDirection ? -forward : forward;
            ResetTail(dir);

            lastAppliedSign = currentSign;
        }
    }

    private void Start()
    {
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        if (lineRend == null)
            lineRend = GetComponent<LineRenderer>();

        if (lineRend != null)
        {
            // Чтобы отрицательный scale (Flip) не ломал линию
            lineRend.useWorldSpace = true;
        }

        if (bodyCenter == null)
        {
            if (targetDir != null && targetDir.parent != null) bodyCenter = targetDir.parent;
            else bodyCenter = transform;
        }

        int len = Mathf.Max(2, length);
        if (segmentPoses == null || segmentPoses.Length != len)
        {
            segmentPoses = new Vector3[len];
            segmentV = new Vector3[len];
            lastLen = len;

            if (lineRend != null)
                lineRend.positionCount = len;

            Vector3 startPos = (targetDir != null) ? targetDir.position : transform.position;
            for (int i = 0; i < len; i++)
            {
                segmentPoses[i] = startPos;
                segmentV[i] = Vector3.zero;
            }

            currentSign = 1f;
            pendingSign = 1f;
            pendingSince = Time.time;
            lastAppliedSign = currentSign;
        }
        else
        {
            // если len не менялся, но length в инспекторе совпал — всё ок
            if (lineRend != null && lineRend.positionCount != len)
                lineRend.positionCount = len;
        }
    }

    private void Update()
    {
        if (targetDir == null || lineRend == null) return;

        // если length поменяли в инспекторе — пересоздадим массивы
        int len = Mathf.Max(2, length);
        if (segmentPoses == null || segmentPoses.Length != len || lastLen != len)
            EnsureInitialized();

        if (targetDir == null || lineRend == null || segmentPoses == null) return;

        segmentPoses[0] = targetDir.position;

        float desiredSign = GetDesiredSign();

        float oldSign = currentSign;
        UpdateSignWithHold(desiredSign);

        // ===== FIX: при реальной смене направления сбрасываем хвост =====
        if (resetTailOnSignChange && !Mathf.Approximately(currentSign, lastAppliedSign))
        {
            Vector3 forward = Vector3.right * currentSign;
            Vector3 dir = tailBehindDirection ? -forward : forward;
            ResetTail(dir);

            lastAppliedSign = currentSign;
        }
        else
        {
            // если sign не менялся, всё равно обновим lastAppliedSign (на случай старта)
            lastAppliedSign = currentSign;
        }

        Vector3 forwardDir = Vector3.right * currentSign;
        Vector3 direction = tailBehindDirection ? -forwardDir : forwardDir;
        Vector3 side = Vector3.up;

        Vector3 c = (bodyCenter != null) ? bodyCenter.position : transform.position;
        float r = Mathf.Max(0f, bodyRadius);
        float rPad = r + Mathf.Max(0f, bodyPadding);

        float smooth = Mathf.Max(0.0001f, smoothSpeed);
        float baseDist = Mathf.Max(0.0001f, targetDist);

        for (int i = 1; i < segmentPoses.Length; i++)
        {
            // Волна (и ослабление у упора)
            float wave = Mathf.Sin(Time.time * wiggleSpeed - i * wigglePhaseOffset);

            float tipT = (segmentPoses.Length <= 1) ? 1f : (i / (float)(segmentPoses.Length - 1));
            float tipMul = Mathf.Lerp(0.1f, tailTipMultiplier, tipT);

            float nearBodyMul = 1f;
            if (useBodyAvoid && i >= bodyAvoidStartIndex && r > 0.001f)
            {
                float dNow = (segmentPoses[i] - c).magnitude;
                if (dNow < rPad) nearBodyMul = blockedWiggleMul;
            }

            float strength = wiggleMagnitude * wave * tipMul * nearBodyMul;
            Vector3 wiggleOffset = side * strength;

            Vector3 desiredPos = segmentPoses[i - 1] + direction * baseDist + wiggleOffset;

            segmentPoses[i] = Vector3.SmoothDamp(
                segmentPoses[i],
                desiredPos,
                ref segmentV[i],
                smooth
            );

            // Сначала ограничим максимум (не рвёмся)
            EnforceMaxDistance(i, baseDist);

            bool blocked = false;

            // Избегание тела + "комок"
            if (useBodyAvoid && i >= bodyAvoidStartIndex && r > 0.001f)
            {
                Vector3 to = segmentPoses[i] - c;
                float d = to.magnitude;

                if (d < rPad)
                {
                    blocked = true;

                    // выталкиваем на границу радиуса
                    if (d < 0.00001f) to = direction;
                    segmentPoses[i] = c + to.normalized * r;

                    // снова максимум после выталкивания
                    EnforceMaxDistance(i, baseDist);

                    // "комок": разрешаем сильнее сжиматься
                    if (clumpWhenBlocked)
                    {
                        float minBlocked = baseDist * Mathf.Clamp01(clumpMinDistMul);
                        if (minBlocked > 0.0001f)
                            EnforceMinDistance(i, minBlocked, direction);
                    }
                }
            }

            // Обычная минимальная дистанция (если не упёрлись)
            if (!blocked)
            {
                EnforceMinDistance(i, baseDist, direction);
            }

            if (bodyParts != null && i - 1 < bodyParts.Length && bodyParts[i - 1] != null)
                bodyParts[i - 1].position = segmentPoses[i];
        }

        lineRend.SetPositions(segmentPoses);
    }

    private void ResetTail(Vector3 direction)
    {
        if (targetDir == null || segmentPoses == null || segmentV == null) return;

        Vector3 startPos = targetDir.position;
        segmentPoses[0] = startPos;

        Vector3 dir = direction.sqrMagnitude < 1e-6f ? Vector3.right : direction.normalized;
        float dist = Mathf.Max(0.0001f, targetDist);

        // Обнуляем инерцию SmoothDamp и ставим точки сразу в правильную сторону
        for (int i = 1; i < segmentPoses.Length; i++)
        {
            segmentV[i] = Vector3.zero;
            segmentPoses[i] = segmentPoses[i - 1] + dir * dist;
        }

        if (lineRend != null)
        {
            if (lineRend.positionCount != segmentPoses.Length)
                lineRend.positionCount = segmentPoses.Length;

            lineRend.SetPositions(segmentPoses);
        }
    }

    private float GetDesiredSign()
    {
        // По умолчанию — по flip (scale.x)
        float sign = 1f;
        if (targetDir != null)
        {
            float sx = targetDir.lossyScale.x;
            sign = Mathf.Sign(sx);
            if (Mathf.Approximately(sign, 0f)) sign = 1f;
        }

        // По скорости — если включено
        if (useVelocityForDirection && velocitySource != null)
        {
            float vx = velocitySource.velocity.x;
            if (Mathf.Abs(vx) > Mathf.Max(0f, velocityDeadZone))
            {
                float vs = Mathf.Sign(vx);
                if (!Mathf.Approximately(vs, 0f)) sign = vs;
            }
        }

        return sign;
    }

    private void UpdateSignWithHold(float desiredSign)
    {
        if (Mathf.Approximately(desiredSign, currentSign))
        {
            pendingSign = currentSign;
            pendingSince = Time.time;
            return;
        }

        if (!Mathf.Approximately(desiredSign, pendingSign))
        {
            pendingSign = desiredSign;
            pendingSince = Time.time;
            return;
        }

        float hold = Mathf.Max(0f, signHoldTime);
        if (hold <= 0f || (Time.time - pendingSince) >= hold)
            currentSign = pendingSign;
    }

    private void EnforceMaxDistance(int i, float baseDist)
    {
        Vector3 delta = segmentPoses[i] - segmentPoses[i - 1];
        float dist = delta.magnitude;

        float maxDist = Mathf.Max(0.0001f, baseDist) * (1f + Mathf.Clamp01(maxStretch));
        if (dist > maxDist && dist > 0.000001f)
            segmentPoses[i] = segmentPoses[i - 1] + delta.normalized * maxDist;
    }

    private void EnforceMinDistance(int i, float minDist, Vector3 fallbackDir)
    {
        Vector3 delta = segmentPoses[i] - segmentPoses[i - 1];
        float dist = delta.magnitude;

        if (dist < 0.000001f)
        {
            segmentPoses[i] = segmentPoses[i - 1] + fallbackDir.normalized * minDist;
            return;
        }

        if (dist < minDist)
            segmentPoses[i] = segmentPoses[i - 1] + delta.normalized * minDist;
    }

    // =========================
    // GIZMOS: visualize bodyRadius
    // =========================
    private void OnDrawGizmos()
    {
        if (!drawAlways) return;
        DrawBodyGizmo();
    }

    private void OnDrawGizmosSelected()
    {
        if (drawAlways) return;
        DrawBodyGizmo();
    }

    private void DrawBodyGizmo()
    {
        if (!drawBodyRadiusGizmo || !useBodyAvoid) return;

        Transform cT = bodyCenter;
        if (cT == null)
        {
            if (targetDir != null && targetDir.parent != null) cT = targetDir.parent;
            else cT = transform;
        }

        float r = Mathf.Max(0f, bodyRadius);
        if (r <= 0.0001f) return;

        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.9f);
        Gizmos.DrawWireSphere(cT.position, r);

        float pad = Mathf.Max(0f, bodyPadding);
        if (pad > 0.0001f)
        {
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(cT.position, r + pad);
        }
    }
}
