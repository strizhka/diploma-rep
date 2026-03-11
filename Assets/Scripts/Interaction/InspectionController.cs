using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Управляет режимом осмотра предметов. Полностью локальный — не синхронизируется по сети.
///
/// Два режима:
/// 1. Осмотр предмета со сцены (StartWorldInspection)
///    — объект перемещается перед камерой, можно вращать мышью
///    — Q = выход (объект возвращается на место)
///    — G = забрать (если CanCollect, передаёт в PlayerInventory)
///
/// 2. Осмотр предмета из инвентаря (StartInventoryInspection)
///    — создаётся временный превью-объект
///    — Q = выход (превью уничтожается)
///    — G не работает (предмет уже в инвентаре)
///
/// НАСТРОЙКА:
/// 1. Добавь компонент на Player-префаб
/// 2. Создай Canvas с Image (чёрный, alpha=0) → назначь в _fadeImage
/// 3. _fadeImage Canvas должен быть ScreenSpace-Overlay, sortingOrder > 0
/// 4. Добавь Input Actions: InspectExit (Q), Grab (G), Look (Mouse Delta)
///
/// ЗАВИСИМОСТИ на Player-префабе:
/// - PlayerInventory (для G — забрать предмет)
/// - InteractionRaycaster (отключается во время осмотра)
/// </summary>
public class InspectionController : MonoBehaviour
{
    [Header("Позиционирование")]
    [Tooltip("Расстояние от камеры до осматриваемого объекта")]
    [SerializeField] private float _inspectionDistance = 0.6f;

    [Tooltip("Скорость вращения объекта мышью")]
    [SerializeField] private float _rotateSpeed = 0.4f;

    [Header("Затемнение")]
    [Tooltip("UI Image для затемнения фона. Canvas: ScreenSpace-Overlay.")]
    [SerializeField] private Image _fadeImage;

    [Tooltip("Целевая прозрачность затемнения (0-1)")]
    [SerializeField] private float _fadeAlpha = 0.75f;

    [Tooltip("Скорость затемнения/просветления")]
    [SerializeField] private float _fadeSpeed = 5f;

    // ──── Состояние ────
    private bool _isActive;
    private bool _isWorldMode; // true = со сцены, false = из инвентаря
    private InspectableObject _worldObject; // объект со сцены (только в world mode)

    // Трансформ осматриваемого объекта (сценный или превью)
    private Transform _inspectedTransform;
    private GameObject _previewInstance; // только для inventory mode

    // Сохранённая позиция/поворот сценного объекта
    private Vector3 _originalPosition;
    private Quaternion _originalRotation;
    private Transform _originalParent;

    // Ссылки
    private Transform _cameraTransform;
    private CinemachineCamera _cinemachineCamera;
    private CinemachineInputAxisController _inputAxisController;

    // Целевое значение fade alpha (плавная анимация)
    private float _targetFadeAlpha;

    // Вращение мышью
    private Vector2 _rotateInput;

    public bool IsActive => _isActive;

    /// <summary>
    /// Текущий осматриваемый объект со сцены (null если не в world mode).
    /// Используется PlayerController для передачи в PlayerInventory при G.
    /// </summary>
    public InspectableObject CurrentWorldObject => _isWorldMode ? _worldObject : null;

    private void Awake()
    {
        if (_fadeImage != null)
        {
            var color = _fadeImage.color;
            color.a = 0f;
            _fadeImage.color = color;
            _fadeImage.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (!_isActive) return;

        HandleRotation();
        UpdateFade();
        KeepPositionInFrontOfCamera();
    }

    // ──────────────────────── PUBLIC API ────────────────────────

    /// <summary>
    /// Инициализация. Вызывается из PlayerController.OnStartLocalPlayer().
    /// </summary>
    public void Initialize(Transform cameraTransform)
    {
        _cameraTransform = cameraTransform;

        // Находим Cinemachine компоненты для отключения ввода камеры
        var cinCam = _cameraTransform.GetComponent<CinemachineCamera>();
        if (cinCam != null)
        {
            _cinemachineCamera = cinCam;
            _inputAxisController = cinCam.GetComponent<CinemachineInputAxisController>();
        }
    }

    /// <summary>
    /// Начать осмотр объекта со сцены.
    /// </summary>
    public void StartWorldInspection(InspectableObject obj)
    {
        if (_isActive || obj == null || obj.IsCollected) return;

        _isActive = true;
        _isWorldMode = true;
        _worldObject = obj;
        _inspectedTransform = obj.transform;

        // Запоминаем оригинальное положение
        _originalPosition = obj.transform.position;
        _originalRotation = obj.transform.rotation;
        _originalParent = obj.transform.parent;

        // Убираем подсветку
        obj.SetHighlight(false);

        // Перемещаем перед камерой
        PositionInFrontOfCamera();

        EnterInspectionMode();

        PuzzleDebugOverlay.Log($"[Inspection] Осмотр: {obj.ObjectId} (со сцены)");
    }

    /// <summary>
    /// Начать осмотр предмета из инвентаря (создаёт превью).
    /// </summary>
    public void StartInventoryInspection(ItemDefinition item)
    {
        if (_isActive || item == null || item.PreviewPrefab == null) return;

        _isActive = true;
        _isWorldMode = false;
        _worldObject = null;

        // Создаём превью
        _previewInstance = Instantiate(item.PreviewPrefab);
        _inspectedTransform = _previewInstance.transform;

        // Убираем сетевые компоненты с превью (если случайно есть)
        foreach (var netId in _previewInstance.GetComponentsInChildren<Mirror.NetworkIdentity>())
            Destroy(netId);

        PositionInFrontOfCamera();

        EnterInspectionMode();

        PuzzleDebugOverlay.Log($"[Inspection] Осмотр: {item.ItemId} (из инвентаря)");
    }

    /// <summary>
    /// Завершить осмотр. Возвращает объект на место (world) или уничтожает превью (inventory).
    /// </summary>
    public void StopInspection()
    {
        if (!_isActive) return;

        if (_isWorldMode && _inspectedTransform != null)
        {
            // Возвращаем сценный объект на место
            _inspectedTransform.SetParent(_originalParent);
            _inspectedTransform.position = _originalPosition;
            _inspectedTransform.rotation = _originalRotation;
        }

        CleanupPreview();
        ExitInspectionMode();

        _isActive = false;
        _isWorldMode = false;
        _worldObject = null;
        _inspectedTransform = null;

        PuzzleDebugOverlay.Log("[Inspection] Осмотр завершён");
    }

    /// <summary>
    /// Завершить осмотр БЕЗ возврата объекта на место (объект забран в инвентарь).
    /// Вызывается из PlayerController при успешном сборе.
    /// </summary>
    public void StopInspectionCollected()
    {
        if (!_isActive) return;

        // Не возвращаем объект — он будет скрыт через SyncVar
        CleanupPreview();
        ExitInspectionMode();

        _isActive = false;
        _isWorldMode = false;
        _worldObject = null;
        _inspectedTransform = null;

        PuzzleDebugOverlay.Log("[Inspection] Осмотр завершён (предмет собран)");
    }

    // ──────────────────────── INPUT ────────────────────────

    /// <summary>
    /// Ввод мыши для вращения. Подключается к Input Action "Look" или "InspectRotate".
    /// </summary>
    public void OnRotateInput(Vector2 delta)
    {
        if (!_isActive) return;
        _rotateInput = delta;
    }

    // ──────────────────────── PRIVATE ────────────────────────

    private void EnterInspectionMode()
    {
        // Отключаем ввод камеры (Cinemachine продолжает рендерить, но не вращается)
        if (_inputAxisController != null)
            _inputAxisController.enabled = false;

        // Курсор остаётся заблокированным — мышь вращает объект напрямую

        // Затемнение
        if (_fadeImage != null)
        {
            _fadeImage.gameObject.SetActive(true);
            _targetFadeAlpha = _fadeAlpha;
        }
    }

    private void ExitInspectionMode()
    {
        // Включаем ввод камеры
        if (_inputAxisController != null)
            _inputAxisController.enabled = true;

        // Просветление
        _targetFadeAlpha = 0f;
    }

    private void PositionInFrontOfCamera()
    {
        if (_inspectedTransform == null || _cameraTransform == null) return;

        // Открепляем от родителя чтобы свободно позиционировать
        _inspectedTransform.SetParent(null);

        Vector3 pos = _cameraTransform.position + _cameraTransform.forward * _inspectionDistance;
        _inspectedTransform.position = pos;
        _inspectedTransform.rotation = Quaternion.identity;
    }

    private void KeepPositionInFrontOfCamera()
    {
        // Объект следует за камерой (на случай если камера немного двигается)
        if (_inspectedTransform == null || _cameraTransform == null) return;

        Vector3 targetPos = _cameraTransform.position + _cameraTransform.forward * _inspectionDistance;
        _inspectedTransform.position = Vector3.Lerp(
            _inspectedTransform.position, targetPos, Time.deltaTime * 10f);
    }

    private void HandleRotation()
    {
        if (_inspectedTransform == null) return;

        float rotX = _rotateInput.x * _rotateSpeed;
        float rotY = _rotateInput.y * _rotateSpeed;

        // Вращаем в мировых координатах для интуитивного управления
        _inspectedTransform.Rotate(Vector3.up, -rotX, Space.World);
        _inspectedTransform.Rotate(Vector3.right, rotY, Space.World);

        // Сброс — без этого объект будет крутиться после отпускания мыши
        _rotateInput = Vector2.zero;
    }

    private void UpdateFade()
    {
        if (_fadeImage == null) return;

        var color = _fadeImage.color;
        color.a = Mathf.Lerp(color.a, _targetFadeAlpha, Time.deltaTime * _fadeSpeed);
        _fadeImage.color = color;

        // Скрываем Image когда полностью прозрачен
        if (!_isActive && color.a < 0.01f)
            _fadeImage.gameObject.SetActive(false);
    }

    private void CleanupPreview()
    {
        if (_previewInstance != null)
        {
            Destroy(_previewInstance);
            _previewInstance = null;
        }
    }
}
