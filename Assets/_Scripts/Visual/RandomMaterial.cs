using Mirror;
using UnityEngine;

public class RandomMaterial : NetworkBehaviour
{
    [Header("Лица")]
    [Tooltip("Список материалов лиц. На спавне игрока выбирается случайный.")]
    [SerializeField] private Material[] _faceMaterials;

    [Header("Renderer")]
    [Tooltip("MeshRenderer на quad с лицом игрока.")]
    [SerializeField] private MeshRenderer _faceRenderer;

    [SyncVar(hook = nameof(OnFaceIndexChanged))]
    private int _faceIndex = -1;

    public override void OnStartServer()
    {
        base.OnStartServer();

        if (_faceMaterials == null || _faceMaterials.Length == 0)
        {
            Debug.LogWarning($"[PlayerFace] Список материалов пуст на {name}");
            return;
        }

        _faceIndex = Random.Range(0, _faceMaterials.Length);
        Debug.Log($"[PlayerFace] {name}: выбрано лицо #{_faceIndex} ({_faceMaterials[_faceIndex].name})");
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        ApplyFace(_faceIndex);
    }

    private void OnFaceIndexChanged(int oldIndex, int newIndex)
    {
        ApplyFace(newIndex);
    }

    private void ApplyFace(int index)
    {
        if (_faceRenderer == null)
        {
            Debug.LogWarning($"[PlayerFace] {name}: _faceRenderer не назначен");
            return;
        }
        if (_faceMaterials == null || _faceMaterials.Length == 0) return;
        if (index < 0 || index >= _faceMaterials.Length) return;

        _faceRenderer.material = _faceMaterials[index];
    }
}