using Mirror;
using DG.Tweening;
using UnityEngine;

public class DigitButton : NetworkBehaviour, IFocusable
{
    [Header("Настройка")]
    [Tooltip("Цифра этой кнопки (0-9)")]
    [SerializeField] private int _digit;

    [Tooltip("Кодовый замок")]
    [SerializeField] private CodeLock _codeLock;

    [Header("Анимация нажатия")]
    [Tooltip("Смещение при нажатии (локальные координаты). Например (0, 0, -0.005) — утопить на 5мм")]
    [SerializeField] private Vector3 _pressOffset = new Vector3(0, 0, -0.005f);

    [Tooltip("Длительность анимации нажатия")]
    [SerializeField] private float _pressDuration = 0.1f;

    private OutlineEffect _outline;
    private Vector3 _originalPosition;

    public int Digit => _digit;

    private void Awake()
    {
        _outline = GetComponentInChildren<OutlineEffect>(true);
        _originalPosition = transform.localPosition;
    }

    public void SetHighlight(bool enabled)
    {
        _outline?.SetHighlight(enabled);
    }

    public void Press()
    {
        if (_codeLock == null) return;
        AnimatePress();
        _codeLock.EnterDigit(_digit);
    }

    private void AnimatePress()
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