using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class OutlineEffect : MonoBehaviour
{
    [SerializeField] private Material _outlineMaterial;

    private MeshRenderer _renderer;
    private Material[] _originalMaterials;
    private Material[] _outlineMaterials;
    private bool _isHighlighted;

    private void Awake()
    {
        _renderer = GetComponent<MeshRenderer>();
        _originalMaterials = _renderer.materials;

        _outlineMaterials = new Material[_originalMaterials.Length + 1];
        for (int i = 0; i < _originalMaterials.Length; i++)
            _outlineMaterials[i] = _originalMaterials[i];
        _outlineMaterials[^1] = _outlineMaterial;
    }

    public void SetHighlight(bool enabled)
    {
        if (_isHighlighted == enabled) return;
        _isHighlighted = enabled;
        _renderer.materials = enabled ? _outlineMaterials : _originalMaterials;
    }

    private void OnDisable() => SetHighlight(false);
}