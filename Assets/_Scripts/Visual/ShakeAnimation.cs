using DG.Tweening;
using UnityEngine;

public class ShakeAnimation : MonoBehaviour
{
    [Header("Тряска")]
    [SerializeField] private float _duration = 3f;
    [SerializeField] private float _strength = 0.05f;
    [SerializeField] private int _vibrato = 20;

    [Header("Звук после остановки")]
    [SerializeField] private AudioClip _finishSound;
    [SerializeField] private float _spatialBlend = 1f;

    [Header("Состояние-триггер")]
    [SerializeField] private string _activeState = "on";

    private Vector3 _originalPosition;
    private AudioSource _audioSource;

    private void Awake()
    {
        _originalPosition = transform.localPosition;

        if (_finishSound != null)
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
        if (newState != _activeState) return;

        DOTween.Kill(transform);
        transform.localPosition = _originalPosition;

        transform.DOShakePosition(_duration, _strength, _vibrato, fadeOut: true)
            .OnComplete(() =>
            {
                transform.localPosition = _originalPosition;

                if (_finishSound != null && _audioSource != null)
                    _audioSource.PlayOneShot(_finishSound);
            });
    }
}
