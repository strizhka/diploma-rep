using DG.Tweening;
using UnityEngine;

/// <summary>
/// Плавное выдвижение/задвижение ящика. Подключается к InteractableObject.OnStateChanged.
///
/// НАСТРОЙКА:
/// 1. Добавь на тот же GameObject что InteractableObject
/// 2. _openOffset = направление и расстояние выдвижения (локальные координаты)
///    Например: (0, 0, -0.35) = выдвинуть на 35см по локальному -Z
/// 3. InteractableObject → OnStateChanged → DrawerAnimation.OnStateChanged (Dynamic string)
/// </summary>
public class DrawerAnimation : MonoBehaviour
{
    [Tooltip("Смещение в ЛОКАЛЬНЫХ координатах при открытии. " +
             "Подбери экспериментально: (0, 0, -0.35) = вперёд на 35см.")]
    [SerializeField] private Vector3 _openOffset = new Vector3(0, 0, -0.35f);

    [SerializeField] private float _duration = 0.4f;
    [SerializeField] private Ease _ease = Ease.InOutQuad;

    [Tooltip("Состояние при котором ящик открыт")]
    [SerializeField] private string _openState = "open";

    private Vector3 _closedPosition;
    private Vector3 _openPosition;

    private void Awake()
    {
        _closedPosition = transform.localPosition;
        _openPosition = _closedPosition + _openOffset;
    }

    /// <summary>
    /// Привязывается к InteractableObject.OnStateChanged (Dynamic string).
    /// </summary>
    public void OnStateChanged(string newState)
    {
        DOTween.Kill(transform);

        Vector3 target = (newState == _openState) ? _openPosition : _closedPosition;

        transform.DOLocalMove(target, _duration).SetEase(_ease);
    }
}
