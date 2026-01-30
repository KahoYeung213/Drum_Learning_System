using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class SidebarToggle : MonoBehaviour
{
    public RectTransform sidebar;
    public float openX = 0f;
    public float closedX = -270f; // width - tab size
    public float animationTime = 0.25f;

    private bool isOpen = true;
    private Coroutine currentRoutine;

    public void Toggle()
    {
        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        isOpen = !isOpen;
        float targetX = isOpen ? openX : closedX;
        currentRoutine = StartCoroutine(Slide(targetX));
    }

    IEnumerator Slide(float targetX)
    {
        Vector2 startPos = sidebar.anchoredPosition;
        Vector2 targetPos = new Vector2(targetX, startPos.y);

        float elapsed = 0f;

        while (elapsed < animationTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / animationTime;
            sidebar.anchoredPosition = Vector2.Lerp(startPos, targetPos, Mathf.SmoothStep(0, 1, t));
            yield return null;
        }

        sidebar.anchoredPosition = targetPos;
    }
}
