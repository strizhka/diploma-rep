
using DG.Tweening;
using Mirror;
using UnityEngine;

public class ButtonAnimation : NetworkBehaviour
{
    [Header("Анимация нажатия")]
    [Tooltip("Смещение при нажатии (локальные координаты). Например (0, 0, -0.005) — утопить на 5мм")]
    [SerializeField] private Vector3 _pressOffset = new Vector3(0, 0, -0.005f);

    [Tooltip("Длительность анимации нажатия")]
    [SerializeField] private float _pressDuration = 0.1f;

    private OutlineEffect _outline;
    private Vector3 _originalPosition;

    private void Awake()
    {
        _originalPosition = transform.localPosition;
    }

    public void SetHighlight(bool enabled)
    {
        _outline?.SetHighlight(enabled);
    }

    public void Press()
    {
        DOTween.Kill(transform);

        transform.DOLocalMove(_originalPosition + _pressOffset, _pressDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                transform.DOLocalMove(_originalPosition, _pressDuration)
                    .SetEase(Ease.InQuad);
            });
    }
}
