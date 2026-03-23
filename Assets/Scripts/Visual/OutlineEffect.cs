using System;
using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class OutlineEffect : MonoBehaviour
{
    [SerializeField] private Material _outlineMaterial;

    [Header("Толщина")]
    [Tooltip("Базовая толщина обводки. Значение зависит от шейдера (обычно 0.01–0.1).")]
    [SerializeField] private float _outlineWidth = 0.003f;

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
                Vector3 boundsSize = meshFilter.sharedMesh.bounds.size;
                Vector3 scale = transform.lossyScale;
                float meshSize = Mathf.Max(
                    boundsSize.x * scale.x,
                    boundsSize.y * scale.y,
                    boundsSize.z * scale.z
                );
                
                if (meshSize > 0f)
                    width = MathF.Min(_outlineWidth * Mathf.Clamp(meshSize / _referenceSize, 0.2f, 3f), _outlineWidth);
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
    private void OnValidate()
    {
        if (_runtimeOutlineMaterial != null)
            ApplyWidth();
    }
#endif
}