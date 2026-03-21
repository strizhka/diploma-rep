using Mirror;
using OdinNative.Odin.Room;
using OdinNative.Unity;
using OdinNative.Unity.Audio;
using UnityEngine;

/// <summary>
/// Контроллер телефона.
///
/// ИСПРАВЛЕНО:
/// - Статический флаг _clientInRoom — один JoinRoom/LeaveRoom на КЛИЕНТ,
///   а не на каждый PhoneController. Без этого: оба телефона на одном клиенте
///   вызывали JoinRoom/LeaveRoom → двойное подключение → "room is invalid".
/// - try/catch вокруг ODIN-вызовов — защита от runtime ошибок ODIN SDK.
/// - PlaybackComponent создаётся только на "своём" телефоне (том, у которого _isLocal).
/// </summary>
public class PhoneController : NetworkBehaviour, IFocusable
{
    [Header("Партнёр")]
    [SerializeField] private PhoneController _partnerPhone;

    [Header("ODIN")]
    [SerializeField] private string _odinRoomName = "phone_call";

    [Header("Аудио")]
    [Tooltip("3D AudioSource для SFX (звонок, гудки). spatialBlend = 1.")]
    [SerializeField] private AudioSource _sfxSource;

    [Tooltip("2D AudioSource для голоса (ODIN). spatialBlend = 0.")]
    [SerializeField] private AudioSource _voiceSource;

    [Header("Звуки")]
    [SerializeField] private AudioClip _ringSound;
    [SerializeField] private AudioClip _dialToneSound;
    [SerializeField] private AudioClip _hangUpSound;

    public enum PhoneState { Idle, Calling, Ringing, Connected }

    [SyncVar(hook = nameof(OnStateChanged))]
    private PhoneState _state = PhoneState.Idle;

    private OutlineEffect _outline;
    private PlaybackComponent _playback;

    // ─── Статический флаг: один ODIN join/leave на весь клиент ───
    // Оба PhoneController на одном клиенте разделяют это.
    private static bool _clientInRoom;
    // Какой PhoneController на этом клиенте "владеет" ODIN-сессией
    private static PhoneController _odinOwner;

    public PhoneState State => _state;

    private void Awake()
    {
        _outline = GetComponentInChildren<OutlineEffect>(true);

        if (_sfxSource == null || _voiceSource == null)
        {
            var sources = GetComponents<AudioSource>();
            if (sources.Length >= 2)
            {
                _sfxSource ??= sources[0];
                _voiceSource ??= sources[1];
            }
            else
            {
                if (_sfxSource == null)
                {
                    _sfxSource = gameObject.AddComponent<AudioSource>();
                    _sfxSource.spatialBlend = 1f;
                    _sfxSource.playOnAwake = false;
                }
                if (_voiceSource == null)
                {
                    _voiceSource = gameObject.AddComponent<AudioSource>();
                    _voiceSource.spatialBlend = 0f;
                    _voiceSource.playOnAwake = false;
                }
            }
        }
    }

    // ──── IFocusable ────

    public void SetHighlight(bool enabled)
    {
        _outline?.SetHighlight(enabled);
    }

    // ──── Использование ────

    public void Use()
    {
        CmdUsePhone();
    }

    [Command(requiresAuthority = false)]
    private void CmdUsePhone()
    {
        switch (_state)
        {
            case PhoneState.Idle:
                _state = PhoneState.Calling;
                if (_partnerPhone != null)
                    _partnerPhone._state = PhoneState.Ringing;
                break;

            case PhoneState.Calling:
                _state = PhoneState.Idle;
                if (_partnerPhone != null)
                    _partnerPhone._state = PhoneState.Idle;
                break;

            case PhoneState.Ringing:
                _state = PhoneState.Connected;
                if (_partnerPhone != null)
                    _partnerPhone._state = PhoneState.Connected;
                break;

            case PhoneState.Connected:
                _state = PhoneState.Idle;
                if (_partnerPhone != null)
                    _partnerPhone._state = PhoneState.Idle;
                break;
        }
    }

    // ──── SyncVar Hook (вызывается на ВСЕХ клиентах) ────

    private void OnStateChanged(PhoneState oldState, PhoneState newState)
    {
        PuzzleDebugOverlay.Log(
            $"[Phone] {gameObject.name}: {oldState} → {newState}",
            PuzzleDebugOverlay.DebugLevel.Ok);

        StopSfx();

        switch (newState)
        {
            case PhoneState.Ringing:
                PlaySfx(_ringSound, loop: true);
                break;

            case PhoneState.Calling:
                PlaySfx(_dialToneSound, loop: true);
                break;

            case PhoneState.Connected:
                // Только ПЕРВЫЙ телефон, который стал Connected, подключается к ODIN.
                // Второй (партнёр на том же клиенте) пропускает — _clientInRoom уже true.
                TryJoinOdin();
                break;

            case PhoneState.Idle:
                PlaySfxOnce(_hangUpSound);
                // Только тот телефон, который "владеет" ODIN-сессией, покидает комнату.
                TryLeaveOdin();
                break;
        }
    }

    // ──── ODIN (со статической защитой) ────

    private void TryJoinOdin()
    {
        // Уже в комнате (другой PhoneController на этом клиенте подключился)
        if (_clientInRoom) return;

        if (OdinHandler.Instance == null)
        {
            Debug.LogError("[Phone] OdinHandler не найден!");
            return;
        }

        try
        {
            OdinHandler.Instance.JoinRoom(_odinRoomName);
            _clientInRoom = true;
            _odinOwner = this;

            OdinHandler.Instance.OnMediaAdded.AddListener(HandleMediaAdded);
            OdinHandler.Instance.OnMediaRemoved.AddListener(HandleMediaRemoved);

            PuzzleDebugOverlay.Log($"[Phone] ODIN: вошли в '{_odinRoomName}'",
                PuzzleDebugOverlay.DebugLevel.Ok);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Phone] ODIN JoinRoom ошибка: {e.Message}");
        }
    }

    private void TryLeaveOdin()
    {
        // Покидает только тот, кто подключался
        if (!_clientInRoom) return;
        if (_odinOwner != null && _odinOwner != this) return;

        try
        {
            if (OdinHandler.Instance != null)
            {
                OdinHandler.Instance.OnMediaAdded.RemoveListener(HandleMediaAdded);
                OdinHandler.Instance.OnMediaRemoved.RemoveListener(HandleMediaRemoved);
                OdinHandler.Instance.LeaveRoom(_odinRoomName);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Phone] ODIN LeaveRoom: {e.Message}");
        }

        if (_playback != null)
        {
            Destroy(_playback);
            _playback = null;
        }

        _clientInRoom = false;
        _odinOwner = null;

        PuzzleDebugOverlay.Log("[Phone] ODIN: покинули комнату");
    }

    private void HandleMediaAdded(object sender, MediaAddedEventArgs args)
    {
        try
        {
            // Голос собеседника — на телефоне, который владеет ODIN-сессией
            _playback = OdinHandler.Instance.AddPlaybackComponent(
                gameObject, _odinRoomName, args.PeerId, args.Media.Id);

            if (_playback != null && _playback.PlaybackSource != null)
                _playback.PlaybackSource.spatialBlend = 0f;

            PuzzleDebugOverlay.Log("[Phone] Голос подключён",
                PuzzleDebugOverlay.DebugLevel.Ok);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Phone] AddPlayback ошибка: {e.Message}");
        }
    }

    private void HandleMediaRemoved(object sender, MediaRemovedEventArgs args)
    {
        PuzzleDebugOverlay.Log("[Phone] Голос отключён");
    }

    // ──── SFX ────

    private void PlaySfx(AudioClip clip, bool loop = false)
    {
        if (clip == null || _sfxSource == null) return;
        _sfxSource.clip = clip;
        _sfxSource.loop = loop;
        _sfxSource.Play();
    }

    private void PlaySfxOnce(AudioClip clip)
    {
        if (clip == null || _sfxSource == null) return;
        _sfxSource.PlayOneShot(clip);
    }

    private void StopSfx()
    {
        if (_sfxSource != null)
        {
            _sfxSource.Stop();
            _sfxSource.loop = false;
        }
    }

    private void OnDisable() => TryLeaveOdin();
    private void OnDestroy() => TryLeaveOdin();
}