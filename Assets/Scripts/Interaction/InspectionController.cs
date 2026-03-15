using DG.Tweening;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

public class InspectionController : MonoBehaviour
{
    [Header("Позиционирование")]
    [SerializeField] private float _inspectionDistance = 0.6f;
    [SerializeField] private float _rotateSpeed = 0.4f;

    [Header("Затемнение")]
    [SerializeField] private Image _fadeImage;
    [SerializeField] private float _fadeAlpha = 0.75f;
    [SerializeField] private float _fadeDuration = 0.3f;

    [Header("Inspection Layer")]
    [SerializeField] private int _inspectionLayer = 31;
    
    private bool _isActive;
    private bool _isWorldMode;
    private InspectableObject _worldObject;

    private Transform _inspectedTransform;
    private GameObject _previewInstance;
    private int _originalLayer;
    private int[] _originalChildLayers;
    
    private Vector3 _originalPosition;
    private Quaternion _originalRotation;
    private Transform _originalParent;
    
    private Transform _cameraTransform;
    private Camera _mainCamera;
    private Camera _inspectionCamera;
    private CinemachineInputAxisController _inputAxisController;
    
    private Vector2 _rotateInput;
    
    private const string TweenFadeId = "InspectionFade";

    public bool IsActive => _isActive;
    public InspectableObject CurrentWorldObject => _isWorldMode ? _worldObject : null;
    
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

    public void Initialize(Transform cameraTransform)
    {
        _cameraTransform = cameraTransform;
        _mainCamera = cameraTransform.GetComponent<Camera>();
        
        if (_mainCamera == null)
            _mainCamera = Camera.main;

        var cinCam = cameraTransform.GetComponent<CinemachineCamera>();
        if (cinCam != null)
            _inputAxisController = cinCam.GetComponent<CinemachineInputAxisController>();

        CreateInspectionCamera();
        ConfigureFadeCanvas();
        
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
        
        CleanupAndExit();
    }

    public void OnRotateInput(Vector2 delta)
    {
        if (!_isActive) return;
        _rotateInput = delta;
    }

    private void EnterInspectionMode()
    {
        if (_inputAxisController != null)
            _inputAxisController.enabled = false;

        if (_inspectionCamera != null)
            _inspectionCamera.enabled = true;
        
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

        PuzzleDebugOverlay.Log(
            $"[Inspection] FadeCanvas '{canvas.name}' переключён на ScreenSpace-Camera");
    }
    
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

        PuzzleDebugOverlay.Log($"[Inspection] InspectionCamera создана, layer={_inspectionLayer}");
    }
    
    private void SaveAndSetLayer(Transform target, int layer)
    {
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