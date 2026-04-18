using DG.Tweening;
using UnityEngine;

public static class CanvasGroupExtensions
{
    private const float DefaultDuration = 0.4f;

    public static void FadeIn(this CanvasGroup cg, float duration = DefaultDuration, System.Action onComplete = null)
    {
        if (cg == null) return;
        cg.gameObject.SetActive(true);
        cg.interactable = false;
        cg.blocksRaycasts = false;
        DOTween.Kill(cg);
        cg.DOFade(1f, duration)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                cg.interactable = true;
                cg.blocksRaycasts = true;
                onComplete?.Invoke();
            });
    }

    public static void FadeOut(this CanvasGroup cg, float duration = DefaultDuration, System.Action onComplete = null)
    {
        if (cg == null) { onComplete?.Invoke(); return; }
        cg.interactable = false;
        cg.blocksRaycasts = false;
        DOTween.Kill(cg);
        cg.DOFade(0f, duration)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                cg.gameObject.SetActive(false);
                onComplete?.Invoke();
            });
    }

    public static void SwitchTo(this CanvasGroup from, CanvasGroup to, float duration = DefaultDuration)
    {
        from.FadeOut(duration, () => to.FadeIn(duration));
    }

    public static void SetAlpha(this CanvasGroup cg, float alpha)
    {
        if (cg == null) return;
        cg.alpha = alpha;
        cg.interactable = alpha > 0.5f;
        cg.blocksRaycasts = alpha > 0.5f;
    }

    public static void ShowInstant(this CanvasGroup cg)
    {
        if (cg == null) return;
        cg.gameObject.SetActive(true);
        cg.SetAlpha(1f);
    }

    public static void HideInstant(this CanvasGroup cg)
    {
        if (cg == null) return;
        cg.SetAlpha(0f);
        cg.gameObject.SetActive(false);
    }
}
