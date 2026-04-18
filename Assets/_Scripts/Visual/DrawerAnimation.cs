using DG.Tweening;
using UnityEngine;

/// <summary>
/// Плавное перемещение объекта (ящик, шторы, крышка).
/// Подключается к InteractableObject.OnStateChanged.
/// </summary>
public class DrawerAnimation : MonoBehaviour
{
    [Tooltip("Смещение в ЛОКАЛЬНЫХ координатах при открытии")]
    [SerializeField] private Vector3 _openOffset = new Vector3(0, 0, -0.35f);

    [SerializeField] private float _duration = 0.4f;
    [SerializeField] private float _delay = 0f;
    [SerializeField] private Ease _ease = Ease.InOutQuad;

    [Tooltip("Состояние при котором объект в открытой позиции")]
    [SerializeField] private string _openState = "open";

    [Header("Звук (опционально)")]
    [SerializeField] private AudioClip _moveSound;
    [Tooltip("3D звук (1) или 2D (0)")]
    [SerializeField] private float _spatialBlend = 1f;

    private Vector3 _closedPosition;
    private Vector3 _openPosition;
    private AudioSource _audioSource;
    private bool _initialized;

    private void Start()
    {
        _closedPosition = transform.localPosition;
        _openPosition = _closedPosition + _openOffset;
        _initialized = true;

        if (_moveSound != null)
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.spatialBlend = _spatialBlend;
                _audioSource.playOnAwake = false;
            }
        }
    }

    public void OnStateChanged(string newState)
    {
        if (!_initialized) return;

        DOTween.Kill(transform);

        Vector3 target = (newState == _openState) ? _openPosition : _closedPosition;

        if (_moveSound != null && _audioSource != null)
            _audioSource.PlayOneShot(_moveSound);

        transform.DOLocalMove(target, _duration)
            .SetEase(_ease)
            .SetDelay(_delay);
    }
}