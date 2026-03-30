using Mirror;
using OdinNative.Odin.Room;
using OdinNative.Unity;
using OdinNative.Unity.Audio;
using UnityEngine;

/// <summary>
/// Самодостаточный телефон. IFocusable, без InteractableObject.
///
/// Новое: _canUse — пока false, телефон не реагирует на E и не подсвечивается.
/// Включается извне через StartRinging() (вызывается из PhoneActivator).
///
/// После StartRinging() телефон звонит + разблокирован для Use().
/// Дальше работает как раньше: E = ответить / положить трубку.
/// </summary>
public class PhoneController : NetworkBehaviour, IFocusable
{
    [Header("Партнёр")]
    [SerializeField] private PhoneController _partnerPhone;

    [Header("ODIN")]
    [SerializeField] private string _odinRoomName = "phone_call";

    [Header("Аудио")]
    [SerializeField] private AudioSource _sfxSource;
    [SerializeField] private AudioSource _voiceSource;

    [Header("Звуки")]
    [SerializeField] private AudioClip _ringSound;
    [SerializeField] private AudioClip _dialToneSound;
    [SerializeField] private AudioClip _hangUpSound;

    public enum PhoneState { Idle, Calling, Ringing, Connected }

    [SyncVar(hook = nameof(OnStateChanged))]
    private PhoneState _state = PhoneState.Idle;

    [SyncVar]
    private bool _canUse = false;

    private OutlineEffect _outline;
    private PlaybackComponent _playback;

    private static bool _clientInRoom;
    private static PhoneController _odinOwner;

    public PhoneState State => _state;
    public bool CanUse => _canUse;

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
        if (!_canUse)
        {
            _outline?.SetHighlight(false);
            return;
        }
        _outline?.SetHighlight(enabled);
    }

    // ──────────────────────── ВНЕШНЕЕ УПРАВЛЕНИЕ ────────────────────────

    /// <summary>
    /// Запускает звонок и разблокирует телефон. Вызывается из PhoneActivator.
    /// </summary>
    [Server]
    public void StartRinging()
    {
        _canUse = true;
        _state = PhoneState.Ringing;
        PuzzleDebugOverlay.Log($"[Phone] {gameObject.name}: активирован, звонит",
            PuzzleDebugOverlay.DebugLevel.Ok);
    }

    // ──────────────────────── ИСПОЛЬЗОВАНИЕ ИГРОКОМ ────────────────────────

    public void Use()
    {
        if (!_canUse) return;
        CmdUsePhone();
    }

    [Command(requiresAuthority = false)]
    private void CmdUsePhone()
    {
        if (!_canUse) return;

        switch (_state)
        {
            case PhoneState.Idle:
                // Самостоятельный звонок (после первой активации)
                _state = PhoneState.Calling;
                if (_partnerPhone != null)
                {
                    _partnerPhone._canUse = true;
                    _partnerPhone._state = PhoneState.Ringing;
                }
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

    // ──────────────────────── SYNCVAR HOOK ────────────────────────

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
                TryJoinOdin();
                break;
            case PhoneState.Idle:
                PlaySfxOnce(_hangUpSound);
                TryLeaveOdin();
                break;
        }
    }

    // ──────────────────────── ODIN ────────────────────────

    private void TryJoinOdin()
    {
        if (_clientInRoom) return;
        if (OdinHandler.Instance == null) return;

        try
        {
            OdinHandler.Instance.JoinRoom(_odinRoomName);
            _clientInRoom = true;
            _odinOwner = this;
            OdinHandler.Instance.OnMediaAdded.AddListener(HandleMediaAdded);
            OdinHandler.Instance.OnMediaRemoved.AddListener(HandleMediaRemoved);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Phone] ODIN JoinRoom: {e.Message}");
        }
    }

    private void TryLeaveOdin()
    {
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
    }

    private void HandleMediaAdded(object sender, MediaAddedEventArgs args)
    {
        try
        {
            _playback = OdinHandler.Instance.AddPlaybackComponent(
                gameObject, _odinRoomName, args.PeerId, args.Media.Id);
            if (_playback != null && _playback.PlaybackSource != null)
                _playback.PlaybackSource.spatialBlend = 0f;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Phone] AddPlayback: {e.Message}");
        }
    }

    private void HandleMediaRemoved(object sender, MediaRemovedEventArgs args) { }

    // ──────────────────────── SFX ────────────────────────

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