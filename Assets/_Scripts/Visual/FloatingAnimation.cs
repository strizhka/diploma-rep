using DG.Tweening;
using UnityEngine;

public class FloatingAnimation : MonoBehaviour
{
    [Header("Перемещение")]
    [SerializeField] private Vector3 _moveOffset = new Vector3(0, 0.1f, 0);
    [SerializeField] private float _moveDuration = 2f;

    [Header("Вращение")]
    [SerializeField] private Vector3 _rotateAmount = new Vector3(0, 0, 5f);
    [SerializeField] private float _rotateDuration = 3f;

    [Header("Настройки")]
    [SerializeField] private Ease _ease = Ease.InOutSine;
    [SerializeField] private float _randomDelay = 0.5f;

    private void Start()
    {
        float delay = Random.Range(0f, _randomDelay);

        if (_moveOffset != Vector3.zero)
        {
            transform.DOLocalMove(transform.localPosition + _moveOffset, _moveDuration)
                .SetEase(_ease)
                .SetLoops(-1, LoopType.Yoyo)
                .SetDelay(delay);
        }

        if (_rotateAmount != Vector3.zero)
        {
            transform.DOLocalRotate(transform.localEulerAngles + _rotateAmount, _rotateDuration)
                .SetEase(_ease)
                .SetLoops(-1, LoopType.Yoyo)
                .SetDelay(delay);
        }
    }

    private void OnDestroy()
    {
        DOTween.Kill(transform);
    }
}
