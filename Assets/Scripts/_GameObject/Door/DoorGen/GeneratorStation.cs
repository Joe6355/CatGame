using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class GeneratorStation : MonoBehaviour
{
    [Header("��������")]
    [Range(0, 100)] public float progress = 0f;
    public float completeAt = 100f;

    [Header("��������������")]
    public KeyCode interactKey = KeyCode.F;
    public float interactDistance = 2.0f;
    public Transform interactPoint;
    public LayerMask playerMask = ~0;
    public bool lockPlayerMovementWhileFixing = true;

    [Header("UI")]
    public GeneratorUIPanel uiPrefab;
    public Canvas uiCanvas;
    public bool closeOnComplete = true;

    [Header("��������� � ����")]
    [Tooltip("GO � Canvas World Space ��� �������� �Press F�. ���� ��������/���������.")]
    public GameObject worldHint;
    [Tooltip("����-������� ��������� � ������ (���� ���� BillboardToCamera).")]
    public bool autoBillboard = true;

    [Header("�������� ����� ��� 100%")]
    [Tooltip("���� ������ DoorToggle � ������� SetOpen(true). ����� ������ �������� Active � doorRoot.")]
    public DoorToggle doorToggle;              // ����������� (���� ����������� ��� DoorToggle)
    public GameObject doorRoot;                // ����������� (����� ������ �����)
    public bool doorActiveStateWhenOpen = true;// �� ��� ��������� SetActive ��� ��������

    [Header("FX/���� (���.)")]
    public GameObject explosionFX;
    public AudioSource sfxSource;
    public AudioClip sfxOpen;
    public AudioClip sfxSuccess;
    public AudioClip sfxFail;
    public AudioClip sfxComplete;

    [Header("�������")]
    public UnityEvent onOpened;
    public UnityEvent onClosed;
    public UnityEvent onExploded;
    public UnityEvent onCompleted;

    // runtime
    private GeneratorUIPanel spawnedUI;
    private Transform currentPlayer;
    private bool sessionActive = false;

    void Update()
    {
        // 1) ����/��������� ������ �����
        if (currentPlayer == null)
        {
            Collider2D hit = Physics2D.OverlapCircle(
                interactPoint ? (Vector2)interactPoint.position : (Vector2)transform.position,
                interactDistance, playerMask);
            if (hit) currentPlayer = hit.transform;
        }
        else
        {
            float d = Vector2.Distance(
                interactPoint ? interactPoint.position : transform.position,
                currentPlayer.position);
            if (d > interactDistance * 1.5f) currentPlayer = null;
        }

        // 2) ���������� ���������
        bool showHint = !sessionActive && currentPlayer != null;
        if (worldHint && worldHint.activeSelf != showHint) worldHint.SetActive(showHint);

        // 3) �������� UI
        if (!sessionActive && currentPlayer != null && Input.GetKeyDown(interactKey))
            OpenUI();
    }

    private void OpenUI()
    {
        if (!uiPrefab || !uiCanvas)
        {
            Debug.LogWarning("[Generator] uiPrefab/uiCanvas �� ���������.");
            return;
        }

        spawnedUI = Instantiate(uiPrefab, uiCanvas.transform);
        spawnedUI.name = $"GeneratorUI_{name}";
        spawnedUI.BindStation(this);
        spawnedUI.SetProgress(progress, completeAt);

        if (lockPlayerMovementWhileFixing && currentPlayer)
        {
            currentPlayer.SendMessage("SetInputEnabled", false, SendMessageOptions.DontRequireReceiver);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        if (sfxSource && sfxOpen) sfxSource.PlayOneShot(sfxOpen);
        onOpened?.Invoke();
        sessionActive = true;

        // ��������
        spawnedUI.onCloseRequested += HandleCloseRequested;
        spawnedUI.onSkillCheckSuccess += HandleSkillSuccess;
        spawnedUI.onSkillCheckFail += HandleSkillFail;
        spawnedUI.onProgressTick += HandleProgressTick;
        spawnedUI.onCompleted += HandleCompleted;

        // ��������� ������
        if (worldHint) worldHint.SetActive(false);
    }

    private void HandleCloseRequested() => CloseUI();

    private void HandleSkillSuccess(float addValue)
    {
        progress = Mathf.Min(completeAt, progress + addValue);
        if (sfxSource && sfxSuccess) sfxSource.PlayOneShot(sfxSuccess);
        if (progress >= completeAt) HandleCompleted();
    }

    private void HandleSkillFail(float penaltyValue)
    {
        progress = Mathf.Max(0f, progress - penaltyValue);
        if (explosionFX) Instantiate(explosionFX, transform.position, Quaternion.identity);
        if (sfxSource && sfxFail) sfxSource.PlayOneShot(sfxFail);
        onExploded?.Invoke();
    }

    private void HandleProgressTick(float delta)
    {
        progress = Mathf.Clamp(progress + delta, 0f, completeAt);
        if (progress >= completeAt) HandleCompleted();
    }

    private void HandleCompleted()
    {
        // ������� �����
        TryOpenDoor();

        if (sfxSource && sfxComplete) sfxSource.PlayOneShot(sfxComplete);
        onCompleted?.Invoke();

        if (closeOnComplete) CloseUI();
    }

    private void TryOpenDoor()
    {
        // 1) DoorToggle (���� ����)
        if (doorToggle != null)
        {
            // ��� GameObject ������ ���� �������, ����� �������� �� ��������
            if (!doorToggle.gameObject.activeSelf) doorToggle.gameObject.SetActive(true);
            doorToggle.SetOpen(true);
            return;
        }

        // 2) ������ ��������/��������� ������ �����
        if (doorRoot != null)
            doorRoot.SetActive(doorActiveStateWhenOpen);
    }

    public void CloseUI()
    {
        if (!sessionActive) return;

        if (spawnedUI)
        {
            spawnedUI.onCloseRequested -= HandleCloseRequested;
            spawnedUI.onSkillCheckSuccess -= HandleSkillSuccess;
            spawnedUI.onSkillCheckFail -= HandleSkillFail;
            spawnedUI.onProgressTick -= HandleProgressTick;
            spawnedUI.onCompleted -= HandleCompleted;

            Destroy(spawnedUI.gameObject);
            spawnedUI = null;
        }

        if (lockPlayerMovementWhileFixing && currentPlayer)
            currentPlayer.SendMessage("SetInputEnabled", true, SendMessageOptions.DontRequireReceiver);

        onClosed?.Invoke();
        sessionActive = false;

        // ����� ���������, ���� ����� ��� �����
        if (worldHint && currentPlayer != null) worldHint.SetActive(true);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 p = interactPoint ? interactPoint.position : transform.position;
        Gizmos.DrawWireSphere(p, interactDistance);
    }
}
