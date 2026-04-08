using UnityEngine;

/// <summary>
/// Меняет материал объекта по состоянию. Подключается к InteractableObject.OnStateChanged.
/// Примеры: экран монитора (off → on), лампочка (тусклая → яркая).
///
/// НАСТРОЙКА:
/// 1. Добавь на объект с MeshRenderer (или укажи _renderer вручную)
/// 2. _onMaterial = материал для состояния _onState (например screen_on)
/// 3. InteractableObject.OnStateChanged → MaterialSwap.OnStateChanged (Dynamic string)
///    Или используй через PuzzleDirector: T_SetState → InteractableObject → OnStateChanged
/// </summary>
public class MaterialSwap : MonoBehaviour
{
    [Tooltip("MeshRenderer, на котором меняется материал. Если пусто — берётся с этого GO.")]
    [SerializeField] private MeshRenderer _renderer;

    [Tooltip("Индекс материала в массиве Materials (0 = первый)")]
    [SerializeField] private int _materialIndex = 0;

    [Tooltip("Материал для включённого состояния")]
    [SerializeField] private Material _onMaterial;

    [Tooltip("Состояние при котором включается _onMaterial")]
    [SerializeField] private string _onState = "on";

    private Material _offMaterial;
    private OutlineEffect _outline;

    private void Awake()
    {
        if (_renderer == null)
            _renderer = GetComponent<MeshRenderer>();

        if (_renderer != null && _materialIndex < _renderer.materials.Length)
            _offMaterial = _renderer.materials[_materialIndex];

        _outline = GetComponent<OutlineEffect>();
    }

    public void OnStateChanged(string newState)
    {
        if (_renderer == null || _onMaterial == null || _offMaterial == null) return;

        var mats = _renderer.materials;
        if (_materialIndex >= mats.Length) return;

        mats[_materialIndex] = (newState == _onState) ? _onMaterial : _offMaterial;
        _renderer.materials = mats;

        // Обновляем кэш OutlineEffect — иначе SetHighlight(false) вернёт старый материал
        _outline?.RefreshMaterials();
    }
}