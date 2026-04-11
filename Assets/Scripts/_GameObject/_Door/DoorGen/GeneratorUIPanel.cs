using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GeneratorUIPanel : MonoBehaviour
{
    [Header("UI ссылки")]
    public Slider progressSlider;
    public Text progressText;
    public Button closeButton;               // выйти (или Esc)
    public RectTransform skillCheckArea;     // область для спавна кнопок
    public Button goodButtonPrefab;          // зелёная кнопка (успех)
    public Button badButtonPrefab;           // красная кнопка (взрыв)
    public Image timerRing;                  // круговой таймер над GOOD (опц.)

    [Header("Тики прогресса")]
    public float baseProgressPerSecond = 5f; // базовый автопрогресс (можно 0)
    public bool pauseProgressDuringSkill = true;

    [Header("Скилл-чеки")]
    public Vector2 timeBetweenSkillChecks = new Vector2(2.5f, 4.0f); // случайный интервал
    public float goodButtonLifetime = 1.0f;   // время на реакцию (сек)
    public float goodReward = 10f;            // +к прогрессу за успех
    public float badPenalty = 12f;            // -к прогрессу за провал/клик по красной
    public float badButtonChance = 0.35f;     // вероятность, что вместе появится красная
    public Vector2 buttonSize = new Vector2(90f, 90f);

    [Header("Клавиши")]
    public KeyCode exitKey = KeyCode.Escape;  // альтернатива кнопке Close

    // События наружу (подписывает GeneratorStation)
    public event Action onCloseRequested;
    public event Action<float> onProgressTick;
    public event Action<float> onSkillCheckSuccess;
    public event Action<float> onSkillCheckFail;
    public event Action onCompleted;

    private GeneratorStation station;
    private float curProgress = 0f;
    private float completeAt = 100f;

    private bool skillActive = false;
    private Button spawnedGood;
    private Button spawnedBad;
    private float nextSkillTime = 0f;

    void Start()
    {
        if (closeButton) closeButton.onClick.AddListener(RequestClose);

        // первый запуск таймера чеков
        ScheduleNextSkillCheck();
    }

    void Update()
    {
        // выход
        if (Input.GetKeyDown(exitKey)) RequestClose();

        // тики прогресса
        bool allowTick = !skillActive || !pauseProgressDuringSkill;
        if (allowTick && baseProgressPerSecond > 0f && curProgress < completeAt)
        {
            float delta = baseProgressPerSecond * Time.deltaTime;
            curProgress = Mathf.Min(completeAt, curProgress + delta);
            UpdateProgressUI();
            onProgressTick?.Invoke(delta);

            if (curProgress >= completeAt)
            {
                onCompleted?.Invoke();
            }
        }

        // появление скилл-чека по расписанию
        if (!skillActive && Time.time >= nextSkillTime && curProgress < completeAt)
        {
            StartCoroutine(SpawnSkillCheckRoutine());
        }
    }

    public void BindStation(GeneratorStation s) => station = s;

    public void SetProgress(float current, float completeTarget)
    {
        curProgress = current;
        completeAt = Mathf.Max(1f, completeTarget);
        UpdateProgressUI();
    }

    private void UpdateProgressUI()
    {
        if (progressSlider)
        {
            progressSlider.minValue = 0f;
            progressSlider.maxValue = completeAt;
            progressSlider.value = curProgress;
        }
        if (progressText)
        {
            float pct = (curProgress / completeAt) * 100f;
            progressText.text = $"{pct:0}%";
        }
    }

    private void RequestClose()
    {
        onCloseRequested?.Invoke();
    }

    private void ScheduleNextSkillCheck()
    {
        float t = UnityEngine.Random.Range(timeBetweenSkillChecks.x, timeBetweenSkillChecks.y);
        nextSkillTime = Time.time + Mathf.Max(0.1f, t);
    }

    private IEnumerator SpawnSkillCheckRoutine()
    {
        skillActive = true;

        // заспавним GOOD
        spawnedGood = Instantiate(goodButtonPrefab, skillCheckArea);
        var goodRect = spawnedGood.transform as RectTransform;
        goodRect.sizeDelta = buttonSize;
        PlaceInsideArea(goodRect);

        // таймер (кольцо) сидит у GOOD
        Image ring = timerRing;
        if (ring)
        {
            ring.gameObject.SetActive(true);
            ring.transform.SetParent(goodRect, worldPositionStays: false);
            ring.rectTransform.anchorMin = ring.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            ring.rectTransform.anchoredPosition = Vector2.zero;
        }

        // иногда спавним BAD
        spawnedBad = null;
        if (UnityEngine.Random.value < badButtonChance && badButtonPrefab)
        {
            spawnedBad = Instantiate(badButtonPrefab, skillCheckArea);
            var badRect = spawnedBad.transform as RectTransform;
            badRect.sizeDelta = buttonSize;
            PlaceInsideArea(badRect);
        }

        bool resolved = false;
        float deadline = Time.time + goodButtonLifetime;

        // подписки кликов
        spawnedGood.onClick.AddListener(() => {
            if (resolved) return;
            resolved = true;
            curProgress = Mathf.Min(completeAt, curProgress + goodReward);
            UpdateProgressUI();
            onSkillCheckSuccess?.Invoke(goodReward);
        });

        if (spawnedBad)
        {
            spawnedBad.onClick.AddListener(() => {
                if (resolved) return;
                resolved = true;
                curProgress = Mathf.Max(0f, curProgress - badPenalty);
                UpdateProgressUI();
                onSkillCheckFail?.Invoke(badPenalty);
            });
        }

        // обратный отсчёт
        while (!resolved && Time.time < deadline)
        {
            if (ring)
            {
                float t = Mathf.InverseLerp(deadline, deadline - goodButtonLifetime, Time.time);
                ring.fillAmount = 1f - t;
            }
            yield return null;
        }

        // таймаут = провал
        if (!resolved)
        {
            curProgress = Mathf.Max(0f, curProgress - badPenalty);
            UpdateProgressUI();
            onSkillCheckFail?.Invoke(badPenalty);
        }

        // зачистка
        if (ring)
        {
            ring.transform.SetParent(transform, worldPositionStays: false);
            ring.gameObject.SetActive(false);
        }
        if (spawnedGood) Destroy(spawnedGood.gameObject);
        if (spawnedBad) Destroy(spawnedBad.gameObject);

        spawnedGood = null;
        spawnedBad = null;
        skillActive = false;

        if (curProgress >= completeAt)
        {
            onCompleted?.Invoke();
        }
        else
        {
            ScheduleNextSkillCheck();
        }
    }

    private void PlaceInsideArea(RectTransform rect)
    {
        // Случайная позиция внутри skillCheckArea
        RectTransform area = skillCheckArea;
        Vector2 areaSize = area.rect.size;

        float x = UnityEngine.Random.Range(-areaSize.x * 0.5f + rect.sizeDelta.x * 0.5f,
                                           +areaSize.x * 0.5f - rect.sizeDelta.x * 0.5f);
        float y = UnityEngine.Random.Range(-areaSize.y * 0.5f + rect.sizeDelta.y * 0.5f,
                                           +areaSize.y * 0.5f - rect.sizeDelta.y * 0.5f);

        rect.anchoredPosition = new Vector2(x, y);
    }
}
