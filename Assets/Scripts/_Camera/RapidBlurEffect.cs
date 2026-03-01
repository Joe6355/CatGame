using UnityEngine;

// Выполнять скрипт и в редакторе, и во время игры
[ExecuteInEditMode]
// Скрипт должен висеть на объекте с камерой
[RequireComponent(typeof(Camera))]
// Пункт в меню Add Component
[AddComponentMenu("Learning Unity Shader/Lecture 15/RapidBlurEffect")]
public class RapidBlurEffect : MonoBehaviour
{
    [Header("Шейдер")]
    [SerializeField, Tooltip("Имя шейдера размытия. Если CurShader пустой или не совпадает по имени, шейдер будет найден автоматически.")]
    private string ShaderName = "Learning Unity Shader/Lecture 15/RapidBlurEffect";

    [Tooltip("Ссылка на шейдер размытия. Обычно подставляется автоматически по имени ShaderName.")]
    public Shader CurShader;

    // Внутренний материал, созданный на основе шейдера
    private Material CurMaterial;

    [Header("Параметры размытия")]
    [Range(0, 6)]
    [Tooltip("Степень уменьшения разрешения перед размытием.\n" +
             "Чем больше значение, тем быстрее работает эффект,\n" +
             "но тем грубее может выглядеть изображение.")]
    public int DownSampleNum = 2;

    [Range(0.0f, 20.0f)]
    [Tooltip("Сила размытия.\n" +
             "Чем больше значение, тем сильнее размытие,\n" +
             "но слишком большие значения могут давать искажения.")]
    public float BlurSpreadSize = 3.0f;

    [Range(0, 8)]
    [Tooltip("Количество итераций размытия.\n" +
             "Чем больше значение, тем мягче и качественнее эффект,\n" +
             "но тем выше нагрузка на производительность.")]
    public int BlurIterations = 3;

    [Header("Фикс размера для blur-камеры")]
    [SerializeField, Tooltip(
        "Камера-эталон, с которой будет синхронизироваться эта blur-камера.\n" +
        "Если не задана, будет использована Camera.main.")]
    private Camera referenceCamera;

    [SerializeField, Tooltip(
        "Если включено, blur-камера будет брать ТОЧНО такую же view/projection матрицу,\n" +
        "как referenceCamera. Это главный фикс, когда слой blur в Game выглядит больше/меньше.")]
    private bool forceExactCameraMatch = true;

    [SerializeField, Tooltip(
        "Дополнительно копировать позицию и поворот referenceCamera.\n" +
        "Полезно, если blur-камера должна смотреть абсолютно так же.")]
    private bool syncTransformWithReference = true;

    [SerializeField, Tooltip(
        "Копировать viewport rect referenceCamera.\n" +
        "Полезно, если у main camera нестандартный rect.")]
    private bool syncViewportRect = true;

    [SerializeField, Tooltip(
        "Копировать near/far clip plane и режим проекции referenceCamera.\n" +
        "Обычно это лучше оставить включённым.")]
    private bool syncLensSettings = true;

    private Camera ownCamera;

    // Флаги, чтобы не спамить одинаковыми предупреждениями
    private bool hasWarnedAboutMissingShader = false;
    private bool hasWarnedAboutUnsupportedShader = false;
    private bool hasWarnedAboutMissingReferenceCamera = false;

    // Вызывается при добавлении компонента
    private void Reset()
    {
        ownCamera = GetComponent<Camera>();
        ClampSettings();
        UpdateShaderReference();
        RebuildMaterial();
        ResolveReferenceCamera();
        SyncCameraIfNeeded();
    }

    // Вызывается при включении компонента
    private void OnEnable()
    {
        ownCamera = GetComponent<Camera>();
        ClampSettings();
        UpdateShaderReference();
        RebuildMaterial();
        ResolveReferenceCamera();
        SyncCameraIfNeeded();
    }

    // Вызывается перед первым кадром
    private void Start()
    {
        ownCamera = GetComponent<Camera>();
        ClampSettings();
        UpdateShaderReference();
        RebuildMaterial();
        ResolveReferenceCamera();
        SyncCameraIfNeeded();
    }

    // Вызывается при изменении значений в инспекторе
    private void OnValidate()
    {
        ownCamera = GetComponent<Camera>();
        ClampSettings();
        UpdateShaderReference();
        RebuildMaterial();
        ResolveReferenceCamera();
        SyncCameraIfNeeded();
    }

    // LateUpdate — на случай, если main camera двигается/меняется в течение кадра
    private void LateUpdate()
    {
        ResolveReferenceCamera();
        SyncCameraIfNeeded();
    }

    // Ещё раз синхронизируем перед самим рендером камеры
    private void OnPreCull()
    {
        ResolveReferenceCamera();
        SyncCameraIfNeeded();
    }

    // Главная функция постобработки
    private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
    {
        ClampSettings();
        UpdateShaderReference();

        // Если эффект сейчас применить нельзя — просто выводим исходное изображение
        if (!EnsureMaterialIsReady())
        {
            Graphics.Blit(sourceTexture, destTexture);
            return;
        }

        // Если размытие фактически выключено — тоже просто отдаём исходную картинку
        if (BlurIterations <= 0 || BlurSpreadSize <= 0.0f)
        {
            Graphics.Blit(sourceTexture, destTexture);
            return;
        }

        // Коэффициент, зависящий от степени уменьшения разрешения
        float widthMod = 1.0f / (1 << DownSampleNum);

        // Защита от нулевой ширины/высоты после уменьшения
        int renderWidth = Mathf.Max(1, sourceTexture.width >> DownSampleNum);
        int renderHeight = Mathf.Max(1, sourceTexture.height >> DownSampleNum);

        // Билинейная фильтрация делает масштабирование мягче
        sourceTexture.filterMode = FilterMode.Bilinear;
        sourceTexture.wrapMode = TextureWrapMode.Clamp;

        RenderTexture renderBuffer = null;
        RenderTexture tempBuffer = null;

        try
        {
            // Передаём стартовое значение в шейдер
            CurMaterial.SetFloat("_DownSampleValue", BlurSpreadSize * widthMod);

            // Первый проход: уменьшение изображения
            renderBuffer = RenderTexture.GetTemporary(renderWidth, renderHeight, 0, sourceTexture.format);
            renderBuffer.filterMode = FilterMode.Bilinear;
            renderBuffer.wrapMode = TextureWrapMode.Clamp;

            // Pass 0: подготовка уменьшенной версии изображения
            Graphics.Blit(sourceTexture, renderBuffer, CurMaterial, 0);

            // Несколько итераций вертикального и горизонтального размытия
            for (int i = 0; i < BlurIterations; i++)
            {
                // Небольшое увеличение смещения на каждой итерации
                float iterationOffset = i;
                CurMaterial.SetFloat("_DownSampleValue", BlurSpreadSize * widthMod + iterationOffset);

                // Pass 1: вертикальное размытие
                tempBuffer = RenderTexture.GetTemporary(renderWidth, renderHeight, 0, sourceTexture.format);
                tempBuffer.filterMode = FilterMode.Bilinear;
                tempBuffer.wrapMode = TextureWrapMode.Clamp;

                Graphics.Blit(renderBuffer, tempBuffer, CurMaterial, 1);

                RenderTexture.ReleaseTemporary(renderBuffer);
                renderBuffer = tempBuffer;
                tempBuffer = null;

                // Pass 2: горизонтальное размытие
                tempBuffer = RenderTexture.GetTemporary(renderWidth, renderHeight, 0, sourceTexture.format);
                tempBuffer.filterMode = FilterMode.Bilinear;
                tempBuffer.wrapMode = TextureWrapMode.Clamp;

                Graphics.Blit(renderBuffer, tempBuffer, CurMaterial, 2);

                RenderTexture.ReleaseTemporary(renderBuffer);
                renderBuffer = tempBuffer;
                tempBuffer = null;
            }

            // Выводим финальный результат на экран
            Graphics.Blit(renderBuffer, destTexture);
        }
        finally
        {
            // Гарантированно освобождаем временные текстуры
            if (tempBuffer != null)
            {
                RenderTexture.ReleaseTemporary(tempBuffer);
                tempBuffer = null;
            }

            if (renderBuffer != null)
            {
                RenderTexture.ReleaseTemporary(renderBuffer);
                renderBuffer = null;
            }
        }
    }

    // Находим reference camera
    private void ResolveReferenceCamera()
    {
        if (referenceCamera == null && Camera.main != null && Camera.main != ownCamera)
        {
            referenceCamera = Camera.main;
        }
    }

    // Главный фикс: заставляем blur-камеру смотреть ровно так же, как reference camera
    private void SyncCameraIfNeeded()
    {
        if (!forceExactCameraMatch)
            return;

        if (ownCamera == null)
            ownCamera = GetComponent<Camera>();

        if (referenceCamera == null || ownCamera == null || referenceCamera == ownCamera)
        {
            if (!hasWarnedAboutMissingReferenceCamera && ownCamera != null)
            {
                Debug.LogWarning("RapidBlurEffect: не найдена reference camera для синхронизации размера blur-слоя.", this);
                hasWarnedAboutMissingReferenceCamera = true;
            }
            return;
        }

        hasWarnedAboutMissingReferenceCamera = false;

        // При необходимости копируем transform
        if (syncTransformWithReference)
        {
            transform.position = referenceCamera.transform.position;
            transform.rotation = referenceCamera.transform.rotation;
        }

        // При необходимости копируем базовые параметры линзы/проекции
        if (syncLensSettings)
        {
            ownCamera.orthographic = referenceCamera.orthographic;
            ownCamera.nearClipPlane = referenceCamera.nearClipPlane;
            ownCamera.farClipPlane = referenceCamera.farClipPlane;
            ownCamera.fieldOfView = referenceCamera.fieldOfView;
            ownCamera.orthographicSize = referenceCamera.orthographicSize;
            ownCamera.aspect = referenceCamera.aspect;
        }

        // Копируем viewport
        if (syncViewportRect)
        {
            ownCamera.rect = referenceCamera.rect;
        }

        // Самое важное:
        // 1) одинаковая матрица вида
        // 2) одинаковая матрица проекции
        // Тогда blur-слой будет рендериться в ТОЧНО том же масштабе.
        ownCamera.worldToCameraMatrix = referenceCamera.worldToCameraMatrix;
        ownCamera.projectionMatrix = referenceCamera.projectionMatrix;
    }

    // Вызывается при отключении компонента
    private void OnDisable()
    {
        ReleaseMaterial();

        if (ownCamera == null)
            ownCamera = GetComponent<Camera>();

        if (ownCamera != null)
        {
            ownCamera.ResetProjectionMatrix();
            ownCamera.ResetWorldToCameraMatrix();
        }
    }

    // Ограничиваем параметры допустимыми значениями
    private void ClampSettings()
    {
        DownSampleNum = Mathf.Clamp(DownSampleNum, 0, 6);
        BlurSpreadSize = Mathf.Clamp(BlurSpreadSize, 0.0f, 20.0f);
        BlurIterations = Mathf.Clamp(BlurIterations, 0, 8);
    }

    // Обновляем ссылку на шейдер по имени
    private void UpdateShaderReference()
    {
        if (string.IsNullOrEmpty(ShaderName))
            return;

        // Если шейдер не назначен или имя не совпадает — ищем заново
        if (CurShader == null || CurShader.name != ShaderName)
        {
            CurShader = Shader.Find(ShaderName);
        }
    }

    // Проверяем, можно ли использовать текущий шейдер и материал
    private bool EnsureMaterialIsReady()
    {
        if (CurShader == null)
        {
            if (!hasWarnedAboutMissingShader)
            {
                Debug.LogWarning("RapidBlurEffect: не найден шейдер \"" + ShaderName + "\". Эффект временно отключён.", this);
                hasWarnedAboutMissingShader = true;
            }

            hasWarnedAboutUnsupportedShader = false;
            ReleaseMaterial();
            return false;
        }

        hasWarnedAboutMissingShader = false;

        // Проверка поддержки шейдера текущим железом/платформой
        if (!CurShader.isSupported)
        {
            if (!hasWarnedAboutUnsupportedShader)
            {
                Debug.LogWarning("RapidBlurEffect: шейдер \"" + CurShader.name + "\" не поддерживается на текущем устройстве.", this);
                hasWarnedAboutUnsupportedShader = true;
            }

            ReleaseMaterial();
            return false;
        }

        hasWarnedAboutUnsupportedShader = false;

        // Если материал уже есть и использует нужный шейдер — всё готово
        if (CurMaterial != null && CurMaterial.shader == CurShader)
            return true;

        // Иначе пересоздаём материал
        RebuildMaterial();
        return CurMaterial != null;
    }

    // Пересоздаём материал на основе текущего шейдера
    private void RebuildMaterial()
    {
        ReleaseMaterial();

        if (CurShader == null)
            return;

        if (!CurShader.isSupported)
            return;

        CurMaterial = new Material(CurShader);
        CurMaterial.hideFlags = HideFlags.HideAndDontSave;
    }

    // Корректно удаляем материал
    private void ReleaseMaterial()
    {
        if (CurMaterial == null)
            return;

        if (Application.isPlaying)
            Destroy(CurMaterial);
        else
            DestroyImmediate(CurMaterial);

        CurMaterial = null;
    }
}