using System.Collections;
using Mirror;
using UnityEngine;

public class CodeLock : NetworkBehaviour
{
    [Header("Код")]
    [Tooltip("Правильная последовательность цифр")]
    [SerializeField] private string _correctCode = "2402";

    [Header("Лампы")]
    [Tooltip("MeshRenderer зелёной лампочки")]
    [SerializeField] private MeshRenderer _greenLamp;
    [Tooltip("MeshRenderer красной лампочки")]
    [SerializeField] private MeshRenderer _redLamp;

    [Header("Материалы свечения")]
    [Tooltip("Материал зелёной лампы при успехе")]
    [SerializeField] private Material _greenOnMaterial;
    [Tooltip("Материал красной лампы при ошибке")]
    [SerializeField] private Material _redOnMaterial;

    [Header("Настройки")]
    [Tooltip("Длительность свечения лампы (секунды)")]
    [SerializeField] private float _flashDuration = 1f;

    [Tooltip("Звук нажатия кнопки")]
    [SerializeField] private AudioClip _buttonClickSound;
    [Tooltip("Звук успеха")]
    [SerializeField] private AudioClip _successSound;
    [Tooltip("Звук ошибки")]
    [SerializeField] private AudioClip _failSound;

    [Header("Результат (опционально)")]
    [Tooltip("Объект, который изменит состояние при правильном коде. " +
             "Например: сейф, дверь. Используется для цепочек через PuzzleDirector.")]
    [SerializeField] private InteractableObject _resultObject;
    [Tooltip("Состояние _resultObject при успехе")]
    [SerializeField] private string _resultState = "unlocked";

    [Header("Событийная шина (опционально)")]
    [Tooltip("Для уведомления PuzzleDirector при успехе")]
    [SerializeField] private GameEventInteraction _interactionEvent;

    private string _currentInput = "";
    private Material _greenOriginal;
    private Material _redOriginal;
    private AudioSource _audioSource;
    private bool _isSolved;

    private void Awake()
    {
        if (_greenLamp != null)
            _greenOriginal = _greenLamp.material;
        if (_redLamp != null)
            _redOriginal = _redLamp.material;

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 1f;
            _audioSource.playOnAwake = false;
        }
    }
    public void EnterDigit(int digit)
    {
        if (_isSolved) return;

        PuzzleDebugOverlay.Log($"[CodeLock] Нажата: {digit}");
        CmdEnterDigit(digit);
    }

    [Command(requiresAuthority = false)]
    private void CmdEnterDigit(int digit)
    {
        if (_isSolved) return;

        _currentInput += digit.ToString();

        PuzzleDebugOverlay.Log(
            $"[CodeLock:Server] Ввод: {_currentInput} ({_currentInput.Length}/{_correctCode.Length})");

        RpcPlaySound(true);

        if (_currentInput.Length < _correctCode.Length)
            return;

        if (_currentInput == _correctCode)
        {
            _isSolved = true;
            PuzzleDebugOverlay.Log("[CodeLock] ВЕРНЫЙ КОД!",
                PuzzleDebugOverlay.DebugLevel.Ok);

            RpcFlashLamp(true);

            if (_resultObject != null)
                _resultObject.ApplyState(_resultState);

            if (_interactionEvent != null && _resultObject != null)
                _interactionEvent.Raise(
                    new InteractionData(_resultObject.ObjectId, _resultState));
        }
        else
        {
            PuzzleDebugOverlay.Log(
                $"[CodeLock] Неверный код: {_currentInput}",
                PuzzleDebugOverlay.DebugLevel.Warning);

            _currentInput = "";
            RpcFlashLamp(false);
        }
    }

    [ClientRpc]
    private void RpcFlashLamp(bool success)
    {
        StartCoroutine(FlashLampCoroutine(success));

        if (success)
            PlayLocalSound(_successSound);
        else
            PlayLocalSound(_failSound);
    }

    [ClientRpc]
    private void RpcPlaySound(bool isClick)
    {
        if (isClick)
            PlayLocalSound(_buttonClickSound);
    }

    private IEnumerator FlashLampCoroutine(bool success)
    {
        if (success && _greenLamp != null && _greenOnMaterial != null)
        {
            _greenLamp.material = _greenOnMaterial;
            yield return new WaitForSeconds(_flashDuration);
            if (!_isSolved)
                _greenLamp.material = _greenOriginal;
        }
        else if (!success && _redLamp != null && _redOnMaterial != null)
        {
            _redLamp.material = _redOnMaterial;
            yield return new WaitForSeconds(_flashDuration);
            _redLamp.material = _redOriginal;
        }
    }

    private void PlayLocalSound(AudioClip clip)
    {
        if (clip != null && _audioSource != null)
            _audioSource.PlayOneShot(clip);
    }

    [Server]
    public void ResetLock()
    {
        _currentInput = "";
        _isSolved = false;
    }
}