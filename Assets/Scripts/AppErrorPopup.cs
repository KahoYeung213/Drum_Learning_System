using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Global app-level error popup with auto-hide behavior.
/// Add this to one UI object in your scene and wire popupRoot + messageText.
/// </summary>
public class AppErrorPopup : MonoBehaviour
{
    public static AppErrorPopup Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private TMP_Text messageText;

    [Header("Display Settings")]
    [SerializeField] private float defaultDurationSeconds = 2.5f;
    [SerializeField] private Color messageColor = new Color(1f, 0.3f, 0.3f, 1f);

    private Coroutine hideCoroutine;

    void Awake()
    {
        Instance = this;
        HideNow();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }
    }

    public static void Show(string message)
    {
        if (Instance == null)
        {
            Debug.LogWarning($"[AppErrorPopup] No instance in scene. Error: {message}");
            return;
        }

        Instance.ShowInternal(message, Instance.defaultDurationSeconds);
    }

    public static void Show(string message, float durationSeconds)
    {
        if (Instance == null)
        {
            Debug.LogWarning($"[AppErrorPopup] No instance in scene. Error: {message}");
            return;
        }

        Instance.ShowInternal(message, durationSeconds);
    }

    public void ShowInternal(string message, float durationSeconds)
    {
        if (popupRoot == null || messageText == null)
        {
            Debug.LogWarning($"[AppErrorPopup] Missing popupRoot/messageText. Error: {message}");
            return;
        }

        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }

        popupRoot.SetActive(true);
        messageText.text = message;
        messageText.color = messageColor;

        hideCoroutine = StartCoroutine(HideAfterDelay(Mathf.Max(0.1f, durationSeconds)));
    }

    public void HideNow()
    {
        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }

        if (popupRoot != null)
        {
            popupRoot.SetActive(false);
        }

        if (messageText != null)
        {
            messageText.text = string.Empty;
        }
    }

    IEnumerator HideAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        HideNow();
    }
}
