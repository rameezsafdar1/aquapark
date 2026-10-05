using System;
using UnityEngine;
using UnityEngine.UI;

public enum Sfx
{
    Jump,
    Land,
    Splash,
    Finish,
    Hit,      // the player bumps into an AI racer
    GotHit,   // an AI racer bumps into the player
    Click
}

/// <summary>
/// Background music and sound effects. Lives on the "== Audio Manager" object, whose AudioSource plays the music.
/// Play a sound from anywhere with AudioManager.Play(Sfx.Jump). Every UI button plays the click sound.
/// The Settings popup's Sound switch mutes the effects and its Music switch mutes the music.
/// </summary>
public class AudioManager : MonoBehaviour
{
    [Serializable]
    public class Sound
    {
        public Sfx id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        [Range(0.5f, 2f)] public float pitch = 1f;
        [Tooltip("Random pitch change each time it plays, so repeats do not sound identical.")]
        [Range(0f, 0.3f)] public float pitchVariation = 0.05f;
    }

    public static AudioManager Instance { get; private set; }

    [SerializeField] private AudioSource music;
    [SerializeField] private Sound[] sounds;
    [Tooltip("The same effect is not repeated faster than this (seconds), e.g. when several triggers fire at once.")]
    [SerializeField] private float minRepeatInterval = 0.08f;

    private AudioSource sfx;   // one source plays every effect as a one-shot
    private float[] lastPlayed;

    private void Awake()
    {
        Instance = this;

        if (music == null)
        {
            music = GetComponent<AudioSource>();
        }

        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.spatialBlend = 0f;

        lastPlayed = new float[Enum.GetValues(typeof(Sfx)).Length];
        for (int i = 0; i < lastPlayed.Length; i++)
        {
            lastPlayed[i] = -1f;
        }

        ApplySettings();
        SaveData.Changed += ApplySettings;
    }

    private void Start()
    {
        // All UI is in the scene from the start, so hooking every button here covers them all.
        foreach (Button button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            button.onClick.AddListener(() => Play(Sfx.Click));
        }
    }

    private void OnDestroy()
    {
        SaveData.Changed -= ApplySettings;
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>Plays a sound effect. Safe to call when there is no AudioManager in the scene.</summary>
    public static void Play(Sfx id)
    {
        if (Instance != null)
        {
            Instance.PlaySound(id);
        }
    }

    private void PlaySound(Sfx id)
    {
        float now = Time.unscaledTime;
        if (lastPlayed[(int)id] >= 0f && now - lastPlayed[(int)id] < minRepeatInterval)
        {
            return;
        }

        Sound sound = Array.Find(sounds, s => s.id == id);
        if (sound == null || sound.clip == null)
        {
            return;
        }

        lastPlayed[(int)id] = now;
        // One-shots share the source's pitch, so an effect still ringing out briefly takes on the newest one's pitch.
        sfx.pitch = sound.pitch + UnityEngine.Random.Range(-sound.pitchVariation, sound.pitchVariation);
        sfx.PlayOneShot(sound.clip, sound.volume);
    }

    private void ApplySettings()
    {
        if (music != null)
        {
            music.mute = !SaveData.Music;
        }

        sfx.mute = !SaveData.Sound;
    }
}
