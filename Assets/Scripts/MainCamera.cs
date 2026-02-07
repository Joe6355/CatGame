using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class MainCamera : MonoBehaviour
{
    private GameObject m_Player = null;
    private Transform m_PlayerTr = null;
    private Vector3 m_Pos = Vector3.zero;
    Vector3 mCenterPos;
    Vector3 mMousePos;
    Vector3 mMovePos;

    [SerializeField]
    private string m_TargetName = "";
    private bool mbIsTarget = false;

    [SerializeField]
    private float m_TargetSize = 11f; // Целевой размер камеры
    [SerializeField]
    private float m_InitialSize = 20f; // Начальный размер камеры
    [SerializeField]
    private float m_SizeTransitionDuration = 2f; // Длительность перехода в секундах

    private Camera m_Camera; // Ссылка на камеру, на которой висит этот скрипт

    void Start()
    {
        // Получаем компонент Camera с текущего объекта
        m_Camera = GetComponent<Camera>();

        // Проверяем, есть ли камера
        if (m_Camera == null)
        {
            Debug.LogError("No Camera component found on this GameObject!");
            return;
        }

        // Начинаем с большого размера камеры
        m_Camera.orthographicSize = m_InitialSize;

        // Запускаем корутину для плавного уменьшения размера
        StartCoroutine(SmoothCameraSizeTransition());

        m_Player = GameObject.Find(m_TargetName);
        if (m_Player != null)
        {
            m_PlayerTr = m_Player.GetComponent<Transform>();
            mbIsTarget = true;
        }
    }

    void Update()
    {
        if (null == m_Player)
        {
            TargetDetection();
        }
    }

    private void LateUpdate()
    {
        if (mbIsTarget && m_Camera != null)
        {
            CameraMove();
            m_Pos = m_PlayerTr.position;
            m_Pos.z = -10;
            transform.position = m_Pos + mMovePos;
        }
    }

    IEnumerator SmoothCameraSizeTransition()
    {
        if (m_Camera == null) yield break;

        float elapsedTime = 0f;

        while (elapsedTime < m_SizeTransitionDuration)
        {
            // Плавно интерполируем размер камеры
            m_Camera.orthographicSize = Mathf.Lerp(m_InitialSize, m_TargetSize, elapsedTime / m_SizeTransitionDuration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Убедимся, что в конце получили точное целевое значение
        m_Camera.orthographicSize = m_TargetSize;
    }

    public void TargetDetection()
    {
        m_Player = GameObject.Find(m_TargetName);
        if (m_Player != null)
            m_PlayerTr = m_Player.GetComponent<Transform>();
    }

    public void TargetChange(string _targetName)
    {
        m_TargetName = _targetName;
        TargetDetection();
    }

    public void CameraMove()
    {
        mCenterPos = m_PlayerTr.position;
        mMousePos = m_Camera.ScreenToWorldPoint(Input.mousePosition);

        mMovePos = mMousePos - mCenterPos;
        mMovePos = mMovePos / 4;
        mMovePos.z = 0;
    }
}