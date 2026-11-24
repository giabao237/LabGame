using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Quản lý nhạc nền (Background Music - BGM) của game:
/// - Tự động phát file sound_1.wav trong Assets/Settings làm nhạc nền
/// - Lặp vô tận (Loop), âm thanh Stereo 2D rõ nét không bị suy hao theo khoảng cách
/// - Tự động khởi chạy ngay khi bấm Play (không cần kéo thả thủ công)
/// - Hỗ trợ điều chỉnh âm lượng mượt mà và fade-in khi vào game
/// </summary>
public class BGM_Manager : MonoBehaviour
{
    private static BGM_Manager instance;
    public static BGM_Manager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Object.FindFirstObjectByType<BGM_Manager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("BGM_Manager");
                    instance = go.AddComponent<BGM_Manager>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }

    [Header("BGM Settings")]
    [Tooltip("Clip nhạc nền (nếu để trống script sẽ tự tìm file sound_1.wav trong Assets/Settings)")]
    [SerializeField] private AudioClip bgmClip;

    [Tooltip("Âm lượng nhạc nền")]
    [Range(0f, 1f)]
    [SerializeField] private float musicVolume = 1.0f;

    [Tooltip("Tự động phát khi game bắt đầu (để false vì đã có Start Menu phát nhạc riêng)")]
    [SerializeField] private bool playOnStart = false;

    [Tooltip("Hiệu ứng tăng âm lượng từ từ khi bắt đầu (Fade-in)")]
    [SerializeField] private bool fadeIn = true;
    [SerializeField] private float fadeInDuration = 1.5f;

    private AudioSource audioSource;

    /// <summary>
    /// Tự động kích hoạt BGM_Manager ngay khi bấm Play
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoStartBGM()
    {
        var _ = Instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Cấu hình chuẩn cho nhạc nền
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // 2D Sound - âm lượng đều khắp bản đồ
        audioSource.volume = musicVolume;

        // Tự động tìm và gán sound_1 nếu chưa có
        if (bgmClip == null)
        {
            LoadBgmClip();
        }
    }

    private void Start()
    {
        if (playOnStart)
        {
            PlayBGM();
        }
    }

    private void LoadBgmClip()
    {
#if UNITY_EDITOR
        // Tải trực tiếp sound_1.wav trong Editor
        bgmClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Settings/sound_1.wav");
        if (bgmClip != null)
        {
            Debug.Log("<color=green>[BGM_Manager] Đã tải thành công file nhạc: Assets/Settings/sound_1.wav</color>");
            return;
        }
#endif

        // Phương thức dự phòng tải qua Streaming / File WebRequest
        string path = System.IO.Path.Combine(Application.dataPath, "Settings", "sound_1.wav");
        if (System.IO.File.Exists(path))
        {
            StartCoroutine(LoadAudioFileCoroutine(path));
        }
        else
        {
            Debug.LogWarning("[BGM_Manager] Không tìm thấy file sound_1.wav tại: " + path);
        }
    }

    private IEnumerator LoadAudioFileCoroutine(string filePath)
    {
        string uri = "file://" + filePath;
        using (UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.WAV))
        {
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                bgmClip = DownloadHandlerAudioClip.GetContent(req);
                if (bgmClip != null)
                {
                    bgmClip.name = "sound_1";
                    Debug.Log("<color=green>[BGM_Manager] Đã tải nhạc sound_1.wav thành công qua File Stream!</color>");
                    if (playOnStart && (audioSource == null || !audioSource.isPlaying))
                    {
                        PlayBGM();
                    }
                }
            }
            else
            {
                Debug.LogError("[BGM_Manager] Lỗi tải file nhạc: " + req.error);
            }
        }
    }

    /// <summary>
    /// Bắt đầu phát nhạc nền
    /// </summary>
    public void PlayBGM()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        if (bgmClip != null)
        {
            audioSource.clip = bgmClip;

            if (fadeIn)
            {
                StartCoroutine(FadeInCoroutine(fadeInDuration));
            }
            else
            {
                audioSource.volume = musicVolume;
                audioSource.Play();
            }

            Debug.Log($"<color=cyan><b>[BGM_Manager] Đang phát nhạc nền: {bgmClip.name} (Loop: Bật, Âm lượng: {musicVolume})</b></color>");
        }
        else
        {
            // Thử tải lại lần nữa nếu chưa sẵn sàng
            LoadBgmClip();
            if (bgmClip != null)
            {
                audioSource.clip = bgmClip;
                audioSource.volume = musicVolume;
                audioSource.Play();
            }
        }
    }

    private IEnumerator FadeInCoroutine(float duration)
    {
        audioSource.volume = 0f;
        audioSource.Play();

        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(0f, musicVolume, timer / duration);
            yield return null;
        }

        audioSource.volume = musicVolume;
    }

    /// <summary>
    /// Thay đổi âm lượng nhạc nền theo thời gian thực (0.0 đến 1.0)
    /// </summary>
    public void SetVolume(float newVolume)
    {
        musicVolume = Mathf.Clamp01(newVolume);
        if (audioSource != null)
        {
            audioSource.volume = musicVolume;
        }
    }

    /// <summary>
    /// Tạm dừng nhạc
    /// </summary>
    public void PauseBGM()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Pause();
        }
    }

    /// <summary>
    /// Tiếp tục phát nhạc
    /// </summary>
    public void ResumeBGM()
    {
        if (audioSource != null && !audioSource.isPlaying)
        {
            audioSource.UnPause();
        }
    }

    /// <summary>
    /// Dừng phát nhạc
    /// </summary>
    public void StopBGM()
    {
        if (audioSource != null)
        {
            audioSource.Stop();
        }
    }
}
