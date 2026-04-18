using Mirror;
using OdinNative.Odin.Room;
using OdinNative.Unity;
using OdinNative.Unity.Audio;
using UnityEngine;

/// <summary>
/// ИЗМЕНЕНИЯ:
/// 1. Если odin.dll не подгрузился (DllNotFoundException) — больше не пытаемся
///    делать JoinRoom (раньше это приводило к OdinException: room is invalid).
///    Состояние Connected всё равно работает корректно: SFX, синхронизация, UI —
///    просто без голосового канала. Один раз пишем в файл предупреждение и не
///    спамим.
///
/// 2. _clientInRoom / _odinAvailable — статические, на весь клиент.
///    Если ODIN не работает на старте сессии — он не работает до перезапуска
///    приложения, поэтому не имеет смысла повторно дёргать Initialize.
///
/// 3. Если PhoneController пересоздаётся при смене сцены, OnEnable восстанавливает
///    подписки на ODIN-события если мы уже в комнате.
///
/// КАК ЛЕЧИТЬ ODIN В БИЛДЕ — см. README в папке с патчем.
/// </summary>
public class PhoneController : NetworkBehaviour, IFocusable
{
    [Header("Партнёр")]
    [SerializeField] private PhoneController _partnerPhone;

    [Header("ODIN")]
    [SerializeField] private string _odinRoomName = "phone_call";

    [Header("Аудио")]
    [Tooltip("3D — звонок, гудки (spatialBlend = 1)")]
    [SerializeField] private AudioSource _sfxSource;

    [Tooltip("2D — голос собеседника (spatialBlend = 0)")]
    [SerializeField] private AudioSource _voiceSource;

    [Header("Звуки")]
    [SerializeField] private AudioClip _ringSound;
    [SerializeField] private AudioClip _dialToneSound;
    [SerializeField] private AudioClip _hangUpSound;

    public enum PhoneState { Idle, Calling, Ringing, Connected }

    [SyncVar(hook = nameof(OnStateChanged))]
    private PhoneState _state = PhoneState.Idle;

    private OutlineEffect _outline;

    // ─── ODIN: один вход на весь клиент ───
    private static bool _clientInRoom;
    private static PlaybackComponent _playback;

    // Если odin.dll нет — больше не дёргаем ODIN до перезапуска приложения
    private static bool _odinUnavailable;
    private static bool _odinUnavailableLogged;

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

    public void SetHighlight(bool enabled)
    {
        _outline?.SetHighlight(enabled);
    }

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
                PuzzleDebugOverlay.Log($"[Phone] {name}: звоним");
                break;

            case PhoneState.Calling:
                _state = PhoneState.Idle;
                if (_partnerPhone != null)
                    _partnerPhone._state = PhoneState.Idle;
                PuzzleDebugOverlay.Log($"[Phone] {name}: отмена");
                break;

            case PhoneState.Ringing:
                _state = PhoneState.Connected;
                if (_partnerPhone != null)
                    _partnerPhone._state = PhoneState.Connected;
                PuzzleDebugOverlay.Log($"[Phone] {name}: ответили");
                break;

            case PhoneState.Connected:
                _state = PhoneState.Idle;
                if (_partnerPhone != null)
                    _partnerPhone._state = PhoneState.Idle;
                PuzzleDebugOverlay.Log($"[Phone] {name}: трубка положена");
                break;
        }
    }

    private void OnStateChanged(PhoneState oldState, PhoneState newState)
    {
        PuzzleDebugOverlay.Log($"[Phone] {name}: {oldState} → {newState}",
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
                JoinOdin();
                break;

            case PhoneState.Idle:
                PlaySfxOnce(_hangUpSound);
                LeaveOdin();
                break;
        }
    }

    // ──────────────────────── ODIN ────────────────────────

    private void JoinOdin()
    {
        if (_clientInRoom) return;
        if (_odinUnavailable) return;

        if (OdinHandler.Instance == null)
        {
            _odinUnavailable = true;
            if (!_odinUnavailableLogged)
            {
                _odinUnavailableLogged = true;
                Debug.LogError(
                    "[Phone] OdinHandler не найден или не инициализирован. " +
                    "Звонок будет работать без голосового канала. " +
                    "Скорее всего odin.dll не попал в билд — проверь Player → Plugins.");
            }
            return;
        }

        try
        {
            OdinHandler.Instance.OnMediaAdded.AddListener(OnMediaAdded);
            OdinHandler.Instance.OnMediaRemoved.AddListener(OnMediaRemoved);
            OdinHandler.Instance.JoinRoom(_odinRoomName);

            _clientInRoom = true;

            PuzzleDebugOverlay.Log($"[Phone] ODIN: вошли в '{_odinRoomName}'",
                PuzzleDebugOverlay.DebugLevel.Ok);
        }
        catch (System.Exception e)
        {
            // OdinException: room is invalid — это симптом неживого ODIN
            _odinUnavailable = true;
            _clientInRoom = false;
            Debug.LogError($"[Phone] JoinRoom упал: {e.Message}. Дальше работаем без голоса.");
        }
    }

    private void LeaveOdin()
    {
        if (!_clientInRoom) return;
        if (OdinHandler.Instance == null) { _clientInRoom = false; return; }

        try
        {
            OdinHandler.Instance.OnMediaAdded.RemoveListener(OnMediaAdded);
            OdinHandler.Instance.OnMediaRemoved.RemoveListener(OnMediaRemoved);
            OdinHandler.Instance.LeaveRoom(_odinRoomName);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Phone] LeaveRoom: {e.Message}");
        }

        if (_playback != null)
        {
            Destroy(_playback);
            _playback = null;
        }

        _clientInRoom = false;
        PuzzleDebugOverlay.Log("[Phone] ODIN: покинули комнату");
    }

    private void OnMediaAdded(object sender, MediaAddedEventArgs args)
    {
        if (OdinHandler.Instance == null) return;

        try
        {
            _playback = OdinHandler.Instance.AddPlaybackComponent(
                gameObject, _odinRoomName, args.PeerId, args.Media.Id);

            if (_playback != null && _playback.PlaybackSource != null)
                _playback.PlaybackSource.spatialBlend = 0f;

            PuzzleDebugOverlay.Log("[Phone] ODIN: голос собеседника подключён",
                PuzzleDebugOverlay.DebugLevel.Ok);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Phone] AddPlayback: {e.Message}");
        }
    }

    private void OnMediaRemoved(object sender, MediaRemovedEventArgs args)
    {
        PuzzleDebugOverlay.Log("[Phone] ODIN: голос отключён");
    }

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

    private void OnDisable() => LeaveOdin();
    private void OnDestroy() => LeaveOdin();
}