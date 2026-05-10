using UnityEngine;
using UnityEngine.Audio;

[RequireComponent(typeof(AudioSource))]
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("Button Sound Clips")]
    [SerializeField] private AudioClip hoverSound;
    [Range(0f, 1f)] public float HoverVolume = 0.7f;

    [SerializeField] private AudioClip clickSound;
    [Range(0f, 1f)] public float ClickVolume = 0.8f;

    [Header("Building Phase Sounds")]
    [SerializeField] private AudioClip shipPlacedSound;
    [Range(0f, 1f)] public float ShipPlacedVolume = 1.0f;

    [SerializeField] private AudioClip minePlacedSound;
    [Range(0f, 1f)] public float MinePlacedVolume = 1.0f;

    [Header("Fighting Phase Sounds")]
    [SerializeField] private AudioClip explosionSound;
    [Range(0f, 1f)] public float ExplosionVolume = 1.0f;
    [SerializeField] private AudioClip underwaterExplosionSound;
    [Range(0f, 1f)] public float UnderwaterExplosionVolume = 1.0f;

    [Header("Victory/Loss Sounds")]
    [SerializeField] private AudioClip victorySound;
    [Range(0f, 1f)] public float VictoryVolume = 1.0f;

    [SerializeField] private AudioClip lossSound;
    [Range(0f, 1f)] public float LossVolume = 1.0f;

    [Header("Background Music")]
    [SerializeField] private AudioClip backgroundMusic;
    [Range(0f, 1f)] public float MusicVolume = 0.5f;

    private AudioSource sfxSource;
    private AudioSource musicSource;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // SFX source on this object
        sfxSource = GetComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.spatialBlend = 0f;

        // Separate music source so it can loop independently
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
    }

    private void Start()
    {
        PlayBackgroundMusic();
    }

    public void PlayHoverSound()
    {
        PlaySfx(hoverSound, HoverVolume);
    }

    public void PlayClickSound()
    {
        PlaySfx(clickSound, ClickVolume);
    }

    public void PlayShipPlacedSound()
    {
        PlaySfx(shipPlacedSound, ShipPlacedVolume);
    }

    public void PlayMinePlacedSound()
    {
        PlaySfx(minePlacedSound, MinePlacedVolume);
    }

    public void PlayExplosionSound()
    {
        PlaySfx(explosionSound, ExplosionVolume);
    }

    public void PlayUnderwaterExplosionSound()
    {
        PlaySfx(underwaterExplosionSound, UnderwaterExplosionVolume);
    }

    public void PlayVictorySound()
    {
        Debug.Log("Playing victory sound");
        PlaySfx(victorySound, VictoryVolume);
    }

    public void PlayLossSound()
    {
        Debug.Log("Playing loss sound");
        PlaySfx(lossSound, LossVolume);
    }

    public void PlayBackgroundMusic()
    {
        if (backgroundMusic == null)
            return;

        if (musicSource.clip == backgroundMusic && musicSource.isPlaying)
            return;

        musicSource.clip = backgroundMusic;
        musicSource.volume = MusicVolume;
        musicSource.Play();
    }

    public void StopBackgroundMusic()
    {
        if (musicSource != null && musicSource.isPlaying)
            musicSource.Stop();
    }

    public void SetMusicVolume(float volume)
    {
        MusicVolume = Mathf.Clamp01(volume);

        if (musicSource != null)
            musicSource.volume = MusicVolume;
    }

    private void PlaySfx(AudioClip clip, float volume)
    {
        if (clip == null || sfxSource == null)
            return;

        sfxSource.PlayOneShot(clip, volume);
    }
}