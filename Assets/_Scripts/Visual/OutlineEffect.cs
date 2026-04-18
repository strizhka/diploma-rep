using UnityEngine;

/// <summary>
/// Обводка объекта через дополнительный material pass.
///
/// ИЗМЕНЕНИЯ:
/// 1. _outlineWidth — настраиваемая толщина обводки в инспекторе
/// 2. _autoScaleByMesh — если true, толщина масштабируется по размеру меша
///    (маленький объект → тоньше, большой → толще, визуально одинаково)
/// 3. Создаёт runtime-копию материала → каждый объект может иметь свою толщину
///    без изменения shared material
///
/// ТРЕБОВАНИЕ К ШЕЙДЕРУ:
/// Outline-шейдер должен иметь свойство для толщины.
/// Стандартные имена: "_OutlineWidth", "_Outline", "_Width".
/// Задай имя в _widthPropertyName (по умолчанию "_OutlineWidth").
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
public class OutlineEffect : MonoBehaviour
{
    [SerializeField] private Material _outlineMaterial;

    [Header("Толщина")]
    [Tooltip("Базовая толщина обводки. Значение зависит от шейдера (обычно 0.01–0.1).")]
    [SerializeField] private float _outlineWidth = 0.03f;

    [Tooltip("Имя свойства толщины в outline-шейдере")]
    [SerializeField] private string _widthPropertyName = "_OutlineWidth";

    [Tooltip("Автоматически уменьшать толщину для маленьких объектов. " +
             "Цель: визуально одинаковая обводка на объектах разного размера.")]
    [SerializeField] private bool _autoScaleByMesh = true;

    [Tooltip("Целевой визуальный размер обводки. При autoScale объект размером " +
             "referenceSize получит _outlineWidth, меньше → пропорционально тоньше.")]
    [SerializeField] private float _referenceSize = 1f;

    private MeshRenderer _renderer;
    private Material[] _originalMaterials;
    private Material[] _outlineMaterials;
    private Material _runtimeOutlineMaterial;
    private bool _isHighlighted;

    private void Awake()
    {
        _renderer = GetComponent<MeshRenderer>();
        _originalMaterials = _renderer.materials;

        // Создаём runtime-копию outline материала для этого объекта
        _runtimeOutlineMaterial = new Material(_outlineMaterial);
        ApplyWidth();

        _outlineMaterials = new Material[_originalMaterials.Length + 1];
        for (int i = 0; i < _originalMaterials.Length; i++)
            _outlineMaterials[i] = _originalMaterials[i];
        _outlineMaterials[^1] = _runtimeOutlineMaterial;
    }

    public void SetHighlight(bool enabled)
    {
        if (_isHighlighted == enabled) return;
        _isHighlighted = enabled;
        _renderer.materials = enabled ? _outlineMaterials : _originalMaterials;
    }

    /// <summary>
    /// Обновляет кэш материалов. Вызывается из MaterialSwap после смены материала.
    /// Без этого SetHighlight(false) вернёт старый материал.
    /// </summary>
    public void RefreshMaterials()
    {
        // Читаем текущие материалы (уже с новым от MaterialSwap)
        _originalMaterials = _renderer.materials;

        // Пересобираем массив с обводкой
        _outlineMaterials = new Material[_originalMaterials.Length + 1];
        for (int i = 0; i < _originalMaterials.Length; i++)
            _outlineMaterials[i] = _originalMaterials[i];
        _outlineMaterials[^1] = _runtimeOutlineMaterial;

        // Если сейчас подсвечен — обновляем отображение
        if (_isHighlighted)
            _renderer.materials = _outlineMaterials;
    }

    private void ApplyWidth()
    {
        if (_runtimeOutlineMaterial == null) return;
        if (!_runtimeOutlineMaterial.HasProperty(_widthPropertyName)) return;

        float width = _outlineWidth;

        if (_autoScaleByMesh)
        {
            var meshFilter = GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                // Размер меша = максимальная ось bounding box × масштаб объекта
                Vector3 boundsSize = meshFilter.sharedMesh.bounds.size;
                Vector3 scale = transform.lossyScale;
                float meshSize = Mathf.Max(
                    boundsSize.x * scale.x,
                    boundsSize.y * scale.y,
                    boundsSize.z * scale.z
                );

                // Масштабируем: маленький объект → тоньше линия
                if (meshSize > 0f)
                    width = _outlineWidth * Mathf.Clamp(meshSize / _referenceSize, 0.2f, 3f);
            }
        }

        _runtimeOutlineMaterial.SetFloat(_widthPropertyName, width);
    }

    private void OnDisable() => SetHighlight(false);

    private void OnDestroy()
    {
        if (_runtimeOutlineMaterial != null)
            Destroy(_runtimeOutlineMaterial);
    }

#if UNITY_EDITOR
    /// <summary>
    /// Обновляет толщину при изменении в инспекторе (только в редакторе).
    /// </summary>
    private void OnValidate()
    {
        if (_runtimeOutlineMaterial != null)
            ApplyWidth();
    }
#endif
}