using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("BGM Clips")]
    [SerializeField] private AudioClip bgmMainMenu; // 메인 UI 배경음악
    [SerializeField] private AudioClip bgmInGame;   // 인게임 배경음악

    [Header("SFX Clips")]
    //[SerializeField] private AudioClip sfxSwap;     // 드롭 위치 변경 효과음
    //[SerializeField] private AudioClip sfxClear;    // 드롭 터질 때 효과음
    [SerializeField] private AudioClip sfxGameOver; // 게임 끝났을 때 효과음

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // BGM 재생
    public void PlayBGM(AudioClip clip)
    {
        if (clip == null) return;
        if (bgmSource.clip == clip && bgmSource.isPlaying) return;

        bgmSource.clip = clip;
        bgmSource.loop = true;
        bgmSource.Play();
    }

    public void PlayMainMenuBGM() => PlayBGM(bgmMainMenu);
    public void PlayInGameBGM() => PlayBGM(bgmInGame);
    public void StopBGM() => bgmSource.Stop();

    // SFX 재생
    public void PlaySFX(AudioClip clip)
    {
        if (clip != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(clip);
        }
    }

    public void PlaySwapSFX()
    {
        sfxSource.PlayOneShot(ProceduralAudio.CreateToneClip(800f, 0.05f));
    }

    public void PlayClearSFX()
    {
        sfxSource.PlayOneShot(ProceduralAudio.CreateToneClip(1200f, 0.1f));
    }

    //public void PlayGameOverSFX()
    //{

    //}

    //public void PlaySwapSFX() => PlaySFX(sfxSwap);
    //public void PlayClearSFX() => PlaySFX(sfxClear);
    public void PlayGameOverSFX() => PlaySFX(sfxGameOver);
}