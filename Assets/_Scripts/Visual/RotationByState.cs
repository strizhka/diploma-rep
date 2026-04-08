using System;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Вращение объекта по состоянию. Каждому состоянию соответствует свой угол.
/// Подключается к InteractableObject.OnStateChanged.
///
/// Вместо 5 компонентов DoorAnimation — один RotationByState с таблицей углов.
///
/// НАСТРОЙКА:
/// 1. Добавь на объект (или на _pivot)
/// 2. Заполни _rotations: состояние → угол
/// 3. InteractableObject.OnStateChanged → RotationByState.OnStateChanged
/// </summary>
public class RotationByState : MonoBehaviour
{
    [Tooltip("Transform вращения. Если пусто — вращается сам объект.")]
    [SerializeField] private Transform _pivot;

    public enum RotationAxis { X, Y, Z }

    [SerializeField] private RotationAxis _axis = RotationAxis.Z;
    [SerializeField] private float _duration = 0.5f;
    [SerializeField] private Ease _ease = Ease.InOutQuad;

    [Tooltip("Таблица: состояние → угол поворота")]
    [SerializeField] private StateRotation[] _rotations;

    [Serializable]
    public class StateRotation
    {
        public string State;
        public float Angle;
    }

    private Transform Target => _pivot != null ? _pivot : transform;

    public void OnStateChanged(string newState)
    {
        float? angle = FindAngle(newState);
        if (angle == null) return;

        DOTween.Kill(Target);

        Vector3 targetRotation = _axis switch
        {
            RotationAxis.X => new Vector3(angle.Value, 0, 0),
            RotationAxis.Y => new Vector3(0, angle.Value, 0),
            RotationAxis.Z => new Vector3(0, 0, angle.Value),
            _ => Vector3.zero
        };

        Target.DOLocalRotate(targetRotation, _duration).SetEase(_ease);
    }

    private float? FindAngle(string state)
    {
        if (_rotations == null) return null;

        foreach (var r in _rotations)
        {
            if (r.State == state)
                return r.Angle;
        }

        return null;
    }
}
