using Mirror;
using OdinNative.Odin.Room;
using OdinNative.Unity;
using OdinNative.Unity.Audio;
using UnityEngine;

/// <summary>
/// Телефон. Самодостаточный IFocusable, без InteractableObject.
///
/// РЕШЕНИЕ ПРОБЛЕМЫ «НЕ СЛЫШУ СОБЕСЕДНИКА»:
/// На каждом клиенте два PhoneController (Phone_A и Phone_B).
/// Оба получают Connected → оба пытаются JoinRoom → конфликт.
/// Исправлено: ODIN подключение вынесено в отдельный статический метод
/// с жёстким guard'ом. PlaybackComponent на _voiceSource (2D, spatialBlend=0 →
/// позиция не важна, голос слышен одинаково).
///
/// РЕШЕНИЕ ПРОБЛЕМЫ «НЕЛЬЗЯ ЗВОНИТЬ ДО ВКЛЮЧЕНИЯ СВЕТА»:
/// Телефоны начинают как SetActive(false) через SceneInitializer.
/// PuzzleDirector → T_Reveal → телефоны появляются.
/// После появления — обычная логика Use().
///
/// НАСТРОЙКА:
/// 1. Phone_A, Phone_B — каждый: PhoneController + NetworkIdentity + Collider + OutlineEffect
/// 2. Два AudioSource на каждом: SFX (3D, spatialBlend=1) + Voice (2D, spatialBlend=0)
/// 3. Phone_A._partnerPhone = Phone_B, Phone_B._partnerPhone = Phone_A
/// 4. SceneInitializer: оба телефона в _disableOnStart
/// 5. PuzzleDirector: T_Reveal при нужном условии → Targets = [Phone_A, Phone_B]
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

    // ──────────────────────── ИСПОЛЬЗОВАНИЕ ────────────────────────

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
                // Звоним партнёру
                _state = PhoneState.Calling;
                if (_partnerPhone != null)
                    _partnerPhone._state = PhoneState.Ringing;
                PuzzleDebugOverlay.Log($"[Phone] {name}: звоним");
                break;

            case PhoneState.Calling:
                // Отмена звонка
                _state = PhoneState.Idle;
                if (_partnerPhone != null)
                    _partnerPhone._state = PhoneState.Idle;
                PuzzleDebugOverlay.Log($"[Phone] {name}: отмена");
                break;

            case PhoneState.Ringing:
                // Снять трубку → оба connected
                _state = PhoneState.Connected;
                if (_partnerPhone != null)
                    _partnerPhone._state = PhoneState.Connected;
                PuzzleDebugOverlay.Log($"[Phone] {name}: ответили");
                break;

            case PhoneState.Connected:
                // Положить трубку
                _state = PhoneState.Idle;
                if (_partnerPhone != null)
                    _partnerPhone._state = PhoneState.Idle;
                PuzzleDebugOverlay.Log($"[Phone] {name}: трубка положена");
                break;
        }
    }

    // ──────────────────────── SYNCVAR HOOK ────────────────────────

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
        // Один JoinRoom на весь клиент.
        // На клиенте два PhoneController (A и B) — оба получают Connected.
        // Первый входит, второй пропускается.
        if (_clientInRoom) return;
        if (OdinHandler.Instance == null)
        {
            Debug.LogError("[Phone] OdinHandler не найден!");
            return;
        }

        _clientInRoom = true;

        OdinHandler.Instance.OnMediaAdded.AddListener(OnMediaAdded);
        OdinHandler.Instance.OnMediaRemoved.AddListener(OnMediaRemoved);
        OdinHandler.Instance.JoinRoom(_odinRoomName);

        PuzzleDebugOverlay.Log($"[Phone] ODIN: вошли в '{_odinRoomName}'",
            PuzzleDebugOverlay.DebugLevel.Ok);
    }

    private void LeaveOdin()
    {
        if (!_clientInRoom) return;

        if (OdinHandler.Instance != null)
        {
            OdinHandler.Instance.OnMediaAdded.RemoveListener(OnMediaAdded);
            OdinHandler.Instance.OnMediaRemoved.RemoveListener(OnMediaRemoved);

            try { OdinHandler.Instance.LeaveRoom(_odinRoomName); }
            catch (System.Exception e) { Debug.LogWarning($"[Phone] LeaveRoom: {e.Message}"); }
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

        // Голос собеседника — 2D (spatialBlend=0), позиция не важна.
        // Привязываем к _voiceSource этого телефона.
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