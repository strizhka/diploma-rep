using DG.Tweening;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Управляет режимом осмотра предметов. Полностью локальный — не синхронизируется по сети.
///
/// МЕХАНИЗМ ЗАТЕМНЕНИЯ (почему объект не темнеет):
///
///   ScreenSpace-Overlay рендерится ПОСЛЕ ВСЕХ камер — InspectionCamera не может
///   нарисовать объект поверх Overlay. Поэтому при Initialize() Canvas автоматически
///   переключается на ScreenSpace-Camera, привязанный к Main Camera.
///
///   Итоговый порядок рендера:
///   1. Main Camera рисует сцену (Inspection layer ИСКЛЮЧЁН)
///   2. FadeCanvas (ScreenSpace-Camera на Main Camera) — затемняет как часть Main Camera
///   3. InspectionCamera (depth +10, Depth Only) — рисует объект ПОВЕРХ затемнения
///
/// ТРЕБОВАНИЯ:
/// 1. Создай Layer "Inspection" (Project Settings → Tags and Layers)
/// 2. Назначь номер слоя в _inspectionLayer (по умолчанию 31)
/// 3. FadeImage — на любом Canvas (Overlay или Camera — будет перенастроен автоматически)
/// 4. Canvas должен быть родителем или предком _fadeImage
///
/// InspectionCamera и переключение Canvas создаются автоматически при Initialize().
/// </summary>
public class InspectionController : MonoBehaviour
{
    [Header("Позиционирование")]
    [SerializeField] private float _inspectionDistance = 0.6f;
    [SerializeField] private float _rotateSpeed = 0.4f;

    [Header("Затемнение")]
    [Tooltip("UI Image для затемнения фона. Canvas будет автоматически переключён на ScreenSpace-Camera.")]
    [SerializeField] private Image _fadeImage;
    [SerializeField] private float _fadeAlpha = 0.75f;
    [SerializeField] private float _fadeDuration = 0.3f;

    [Header("Inspection Layer")]
    [Tooltip("Номер слоя 'Inspection'. Создай его в Tags and Layers.")]
    [SerializeField] private int _inspectionLayer = 31;

    // ──── Состояние ────
    private bool _isActive;
    private bool _isWorldMode;
    private InspectableObject _worldObject;

    private Transform _inspectedTransform;
    private GameObject _previewInstance;
    private int _originalLayer;
    private int[] _originalChildLayers;

    // Сохранённое положение сценного объекта
    private Vector3 _originalPosition;
    private Quaternion _originalRotation;
    private Transform _originalParent;

    // Ссылки
    private Transform _cameraTransform;
    private Camera _mainCamera;
    private Camera _inspectionCamera;
    private CinemachineInputAxisController _inputAxisController;

    // Вращение
    private Vector2 _rotateInput;

    // DOTween ID для отмены
    private const string TweenFadeId = "InspectionFade";

    public bool IsActive => _isActive;
    public InspectableObject CurrentWorldObject => _isWorldMode ? _worldObject : null;

    // Callback: PlayerController подписывается для скрытия/показа предмета в руках
    public event System.Action OnInspectionStarted;
    public event System.Action OnInspectionEnded;

    private void Awake()
    {
        if (_fadeImage != null)
        {
            var c = _fadeImage.color;
            c.a = 0f;
            _fadeImage.color = c;
            _fadeImage.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (!_isActive) return;

        HandleRotation();
        KeepPositionInFrontOfCamera();
    }

    private void OnDestroy()
    {
        DOTween.Kill(TweenFadeId);

        if (_inspectionCamera != null)
            Destroy(_inspectionCamera.gameObject);
    }

    // ──────────────────────── PUBLIC API ────────────────────────

    /// <summary>
    /// Инициализация. Вызывается из PlayerController.OnStartLocalPlayer().
    /// Создаёт InspectionCamera и переключает FadeCanvas на ScreenSpace-Camera.
    /// </summary>
    public void Initialize(Transform cameraTransform)
    {
        _cameraTransform = cameraTransform;
        _mainCamera = cameraTransform.GetComponent<Camera>();

        // Если Cinemachine управляет камерой — на ней нет Camera компонента напрямую.
        // Camera обычно на том же объекте или на родительском Main Camera.
        if (_mainCamera == null)
            _mainCamera = Camera.main;

        var cinCam = cameraTransform.GetComponent<CinemachineCamera>();
        if (cinCam != null)
            _inputAxisController = cinCam.GetComponent<CinemachineInputAxisController>();

        CreateInspectionCamera();
        ConfigureFadeCanvas();

        // Убираем Inspection layer из Main Camera
        if (_mainCamera != null)
            _mainCamera.cullingMask &= ~(1 << _inspectionLayer);
    }

    public void StartWorldInspection(InspectableObject obj)
    {
        if (_isActive || obj == null || obj.IsCollected) return;

        _isActive = true;
        _isWorldMode = true;
        _worldObject = obj;
        _inspectedTransform = obj.transform;

        _originalPosition = obj.transform.position;
        _originalRotation = obj.transform.rotation;
        _originalParent = obj.transform.parent;

        obj.SetHighlight(false);

        // Сохраняем и меняем слой
        SaveAndSetLayer(_inspectedTransform, _inspectionLayer);

        PositionInFrontOfCamera();
        EnterInspectionMode();

        PuzzleDebugOverlay.Log($"[Inspection] Осмотр: {obj.ObjectId} (со сцены)");
    }

    public void StartInventoryInspection(ItemDefinition item)
    {
        if (_isActive || item == null || item.PreviewPrefab == null) return;

        _isActive = true;
        _isWorldMode = false;
        _worldObject = null;

        _previewInstance = Instantiate(item.PreviewPrefab);
        _inspectedTransform = _previewInstance.transform;

        foreach (var netId in _previewInstance.GetComponentsInChildren<Mirror.NetworkIdentity>())
            Destroy(netId);

        // Превью сразу на inspection layer
        SetLayerRecursive(_previewInstance, _inspectionLayer);

        PositionInFrontOfCamera();
        EnterInspectionMode();

        PuzzleDebugOverlay.Log($"[Inspection] Осмотр: {item.ItemId} (из инвентаря)");
    }

    public void StopInspection()
    {
        if (!_isActive) return;

        if (_isWorldMode && _inspectedTransform != null)
        {
            RestoreLayer(_inspectedTransform);
            _inspectedTransform.SetParent(_originalParent);
            _inspectedTransform.position = _originalPosition;
            _inspectedTransform.rotation = _originalRotation;
        }

        CleanupAndExit();
    }

    public void StopInspectionCollected()
    {
        if (!_isActive) return;

        // Объект будет скрыт через SyncVar — не восстанавливаем слой/позицию
        CleanupAndExit();
    }

    public void OnRotateInput(Vector2 delta)
    {
        if (!_isActive) return;
        _rotateInput = delta;
    }

    // ──────────────────────── PRIVATE ────────────────────────

    private void EnterInspectionMode()
    {
        if (_inputAxisController != null)
            _inputAxisController.enabled = false;

        if (_inspectionCamera != null)
            _inspectionCamera.enabled = true;

        // DOTween: затемнение
        if (_fadeImage != null)
        {
            DOTween.Kill(TweenFadeId);
            _fadeImage.gameObject.SetActive(true);
            _fadeImage.DOFade(_fadeAlpha, _fadeDuration).SetId(TweenFadeId).SetUpdate(true);
        }

        OnInspectionStarted?.Invoke();
    }

    private void CleanupAndExit()
    {
        CleanupPreview();

        if (_inputAxisController != null)
            _inputAxisController.enabled = true;

        if (_inspectionCamera != null)
            _inspectionCamera.enabled = false;

        // DOTween: просветление — работает независимо от _isActive
        if (_fadeImage != null)
        {
            DOTween.Kill(TweenFadeId);
            _fadeImage.DOFade(0f, _fadeDuration)
                .SetId(TweenFadeId)
                .SetUpdate(true)
                .OnComplete(() => _fadeImage.gameObject.SetActive(false));
        }

        _isActive = false;
        _isWorldMode = false;
        _worldObject = null;
        _inspectedTransform = null;

        OnInspectionEnded?.Invoke();

        PuzzleDebugOverlay.Log("[Inspection] Осмотр завершён");
    }

    private void PositionInFrontOfCamera()
    {
        if (_inspectedTransform == null || _cameraTransform == null) return;

        _inspectedTransform.SetParent(null);
        Vector3 pos = _cameraTransform.position + _cameraTransform.forward * _inspectionDistance;
        _inspectedTransform.position = pos;
        _inspectedTransform.rotation = Quaternion.identity;
    }

    private void KeepPositionInFrontOfCamera()
    {
        if (_inspectedTransform == null || _cameraTransform == null) return;

        Vector3 targetPos = _cameraTransform.position + _cameraTransform.forward * _inspectionDistance;
        _inspectedTransform.position = Vector3.Lerp(
            _inspectedTransform.position, targetPos, Time.deltaTime * 10f);
    }

    private void HandleRotation()
    {
        if (_inspectedTransform == null) return;

        _inspectedTransform.Rotate(Vector3.up, -_rotateInput.x * _rotateSpeed, Space.World);
        _inspectedTransform.Rotate(Vector3.right, _rotateInput.y * _rotateSpeed, Space.World);
        _rotateInput = Vector2.zero;
    }

    private void CleanupPreview()
    {
        if (_previewInstance != null)
        {
            Destroy(_previewInstance);
            _previewInstance = null;
        }
    }

    // ──────────────────────── FADE CANVAS SETUP ────────────────────────

    /// <summary>
    /// Переключает Canvas, содержащий _fadeImage, на ScreenSpace-Camera режим.
    ///
    /// ScreenSpace-Overlay рендерится ПОСЛЕ всех камер → InspectionCamera
    /// не может нарисовать объект поверх Overlay. ScreenSpace-Camera привязывает
    /// Canvas к конкретной камере, и камеры с более высоким depth рисуют поверх.
    /// </summary>
    private void ConfigureFadeCanvas()
    {
        if (_fadeImage == null || _mainCamera == null) return;

        var canvas = _fadeImage.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[Inspection] Canvas не найден у _fadeImage!");
            return;
        }

        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = _mainCamera;
        canvas.planeDistance = _mainCamera.nearClipPlane + 0.1f;
        // sortingOrder не нужен для ScreenSpace-Camera — порядок определяется depth камеры

        PuzzleDebugOverlay.Log(
            $"[Inspection] FadeCanvas '{canvas.name}' переключён на ScreenSpace-Camera");
    }

    // ──────────────────────── INSPECTION CAMERA ────────────────────────

    /// <summary>
    /// Создаёт дочернюю камеру, рендерящую ТОЛЬКО Inspection layer.
    /// SolidColor + чёрный фон → объект на чистом чёрном фоне, поверх затемнённой сцены.
    /// </summary>
    private void CreateInspectionCamera()
    {
        if (_mainCamera == null) return;

        var go = new GameObject("InspectionCamera");
        go.transform.SetParent(_cameraTransform, worldPositionStays: false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        _inspectionCamera = go.AddComponent<Camera>();
        _inspectionCamera.clearFlags = CameraClearFlags.SolidColor;
        _inspectionCamera.backgroundColor = Color.black;
        _inspectionCamera.cullingMask = 1 << _inspectionLayer;
        _inspectionCamera.depth = _mainCamera.depth + 10;
        _inspectionCamera.fieldOfView = _mainCamera.fieldOfView;
        _inspectionCamera.nearClipPlane = 0.01f;
        _inspectionCamera.farClipPlane = 10f;

        _inspectionCamera.enabled = false;

        // Свет для осмотра — освещает предмет даже в тёмной комнате.
        // Привязан к камере, светит вперёд. Culling Mask = только Inspection layer.
        var lightGo = new GameObject("InspectionLight");
        lightGo.transform.SetParent(go.transform, worldPositionStays: false);
        lightGo.transform.localPosition = new Vector3(0, 0.3f, -0.5f);
        lightGo.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);

        var inspLight = lightGo.AddComponent<Light>();
        inspLight.type = LightType.Point;
        inspLight.range = 3f;
        inspLight.intensity = 2f;
        inspLight.color = Color.white;
        inspLight.cullingMask = 1 << _inspectionLayer;
        inspLight.shadows = LightShadows.None;

        PuzzleDebugOverlay.Log($"[Inspection] InspectionCamera + Light создана, layer={_inspectionLayer}");
    }

    // ──────────────────────── LAYER MANAGEMENT ────────────────────────

    private void SaveAndSetLayer(Transform target, int layer)
    {
        // Сохраняем оригинальные слои
        _originalLayer = target.gameObject.layer;
        var children = target.GetComponentsInChildren<Transform>(true);
        _originalChildLayers = new int[children.Length];
        for (int i = 0; i < children.Length; i++)
        {
            _originalChildLayers[i] = children[i].gameObject.layer;
            children[i].gameObject.layer = layer;
        }
    }

    private void RestoreLayer(Transform target)
    {
        if (_originalChildLayers == null) return;

        var children = target.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length && i < _originalChildLayers.Length; i++)
            children[i].gameObject.layer = _originalChildLayers[i];

        _originalChildLayers = null;
    }

    private static void SetLayerRecursive(GameObject obj, int layer)
    {
        foreach (var t in obj.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;
    }
}