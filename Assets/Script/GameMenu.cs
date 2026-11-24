using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Màn hình Start Menu (Cách 1) hoàn toàn bằng code:
/// - Hình nền riêng: Tự động tải 3.png từ Assets/Settings/background_new
/// - Nhạc nền riêng: Tự động phát sound_background.wav từ Assets/Settings/background_new
/// - Hai nút chính: NEW GAME và EXIT
/// - Khi bấm NEW GAME: Tắt menu, dừng nhạc menu, bật BGM_Manager phát sound_1.wav và bắt đầu chơi!
/// - Khi bấm EXIT: Thoát game (hoặc dừng Play mode trong Unity Editor)
/// - Hỗ trợ bấm phím ESC trong game để tạm dừng (Pause) hoặc Thoát bất cứ lúc nào
/// </summary>
public class GameMenu : MonoBehaviour
{
    private static GameMenu instance;
    public static GameMenu Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Object.FindFirstObjectByType<GameMenu>();
                if (instance == null)
                {
                    GameObject go = new GameObject("GameMenu_Controller");
                    instance = go.AddComponent<GameMenu>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }

    [Header("Menu Configuration")]
    [Tooltip("Tiêu đề game hiển thị trên Menu")]
    [SerializeField] private string gameTitle = "ADVENTURE";

    [Tooltip("Ảnh nền Start Menu (để trống script sẽ tự tìm 3.png trong Assets/Settings/background_new)")]
    [SerializeField] private Texture2D menuBackgroundTexture;

    [Tooltip("Nhạc nền Start Menu (để trống script sẽ tự tìm sound_background.wav)")]
    [SerializeField] private AudioClip menuMusicClip;

    [Tooltip("Âm lượng nhạc nền Start Menu")]
    [Range(0f, 1f)]
    [SerializeField] private float menuMusicVolume = 1.0f;

    [Tooltip("Mở Menu ngay khi bấm Play")]
    [SerializeField] private bool openOnStart = true;

    private bool isMenuActive = true;
    private bool isGameOver = false;
    private bool isVictory = false;
    private AudioSource menuAudioSource;

    [Header("End Game (Victory) Settings")]
    [Tooltip("Ảnh hiển thị khi Victory / End Game (mặc định tải 73.png từ Assets/Settings/endgame)")]
    [SerializeField] private Texture2D victoryTexture;
    [Tooltip("Nhạc nền khi Victory / End Game (mặc định tải bgm_special_research_battle.wav từ Assets/Settings/endgame)")]
    [SerializeField] private AudioClip victoryMusicClip;

    [Header("Enemy Kill Tracker")]
    private int killedEnemies = 0;
    private int totalEnemiesInLevel = 0;
    public int KilledEnemies => killedEnemies;
    public int TotalEnemiesInLevel => totalEnemiesInLevel;

    public void RegisterEnemyKill()
    {
        killedEnemies++;
        Debug.Log($"<color=red><b>[GameMenu] Quái bị tiêu diệt! Tổng quái đã diệt: {killedEnemies}</b></color>");
    }

    public void ResetEnemyStats()
    {
        killedEnemies = 0;
        CountEnemiesInScene();
    }

    public void CountEnemiesInScene()
    {
        enemy_controller[] enemies = Object.FindObjectsByType<enemy_controller>(FindObjectsSortMode.None);
        totalEnemiesInLevel = (enemies != null) ? enemies.Length : 0;
    }

    private Texture2D btnNormalTexture;
    private Texture2D btnHoverTexture;
    private Texture2D centerBoxTexture;
    private Texture2D gameOverBoxTexture;
    private Texture2D victoryBoxTexture;
    private AudioClip btnClickClip;
    private AudioClip gameOverSfx;

    /// <summary>
    /// Tự động khởi tạo GameMenu ngay khi bấm Play
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
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

        // Khởi tạo AudioSource cho Menu
        menuAudioSource = gameObject.AddComponent<AudioSource>();
        menuAudioSource.loop = true;
        menuAudioSource.playOnAwake = false;
        menuAudioSource.spatialBlend = 0f; // 2D Stereo
        menuAudioSource.ignoreListenerPause = true; // Phát được cả khi Time.timeScale = 0
        menuAudioSource.volume = menuMusicVolume;

        btnClickClip = CreateClickChime();

        // Tải Background và Nhạc riêng của Menu
        LoadMenuAssets();
        // Tải Ảnh và Nhạc của End Game (Victory)
        LoadEndGameAssets();

        if (openOnStart)
        {
            ShowMenu();
        }
    }

    private void LoadEndGameAssets()
    {
        // 1. Tải ảnh 73.png từ Assets/Settings/endgame
        if (victoryTexture == null)
        {
#if UNITY_EDITOR
            victoryTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Settings/endgame/73.png");
#endif
            if (victoryTexture == null)
            {
                string imgPath = Path.Combine(Application.dataPath, "Settings", "endgame", "73.png");
                if (File.Exists(imgPath))
                {
                    byte[] bytes = File.ReadAllBytes(imgPath);
                    victoryTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                    {
                        filterMode = FilterMode.Point,
                        wrapMode = TextureWrapMode.Clamp
                    };
                    victoryTexture.LoadImage(bytes);
                }
            }
        }

        // 2. Tải nhạc nền bgm_special_research_battle.wav từ Assets/Settings/endgame
        if (victoryMusicClip == null)
        {
#if UNITY_EDITOR
            victoryMusicClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Settings/endgame/bgm_special_research_battle.wav");
#endif
            if (victoryMusicClip == null)
            {
                string musicPath = Path.Combine(Application.dataPath, "Settings", "endgame", "bgm_special_research_battle.wav");
                if (File.Exists(musicPath))
                {
                    StartCoroutine(LoadEndGameSoundCoroutine(musicPath));
                }
            }
        }
    }

    private IEnumerator LoadEndGameSoundCoroutine(string filePath)
    {
        string uri = "file://" + filePath.Replace("\\", "/");
        using (UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.WAV))
        {
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                victoryMusicClip = DownloadHandlerAudioClip.GetContent(req);
                if (isVictory && menuAudioSource != null && !menuAudioSource.isPlaying)
                {
                    PlayVictoryMusic();
                }
            }
        }
    }

    private void LoadMenuAssets()
    {
        // 1. Tải ảnh nền 3.png
        if (menuBackgroundTexture == null)
        {
#if UNITY_EDITOR
            menuBackgroundTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Settings/background_new/3.png");
#endif
            if (menuBackgroundTexture == null)
            {
                string imgPath = Path.Combine(Application.dataPath, "Settings", "background_new", "3.png");
                if (File.Exists(imgPath))
                {
                    byte[] bytes = File.ReadAllBytes(imgPath);
                    menuBackgroundTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                    {
                        filterMode = FilterMode.Point,
                        wrapMode = TextureWrapMode.Clamp
                    };
                    menuBackgroundTexture.LoadImage(bytes);
                }
            }
        }

        // 2. Tải nhạc nền sound_background.wav
        if (menuMusicClip == null)
        {
#if UNITY_EDITOR
            menuMusicClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Settings/background_new/sound_background.wav");
#endif
            if (menuMusicClip == null)
            {
                string soundPath = Path.Combine(Application.dataPath, "Settings", "background_new", "sound_background.wav");
                if (File.Exists(soundPath))
                {
                    StartCoroutine(LoadMenuSoundCoroutine(soundPath));
                }
            }
        }
    }

    private IEnumerator LoadMenuSoundCoroutine(string filePath)
    {
        string uri = "file://" + filePath;
        using (UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.WAV))
        {
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                menuMusicClip = DownloadHandlerAudioClip.GetContent(req);
                if (isMenuActive && menuMusicClip != null)
                {
                    PlayMenuMusic();
                }
            }
        }
    }

    private void Start()
    {
        if (isMenuActive)
        {
            PlayMenuMusic();
        }
    }

    private void Update()
    {
        // Khi đang ở màn hình Victory / End Game
        if (isVictory)
        {
            if (IsEnterPressed() || IsRPressed())
            {
                RetryGame();
            }
            else if (IsEscapePressed())
            {
                ExitGame();
            }
            return;
        }

        // Khi đang ở trạng thái Game Over
        if (isGameOver)
        {
            if (IsEnterPressed() || IsRPressed())
            {
                RestartGame();
            }
            else if (IsEscapePressed())
            {
                ExitGame();
            }
            return;
        }

        // Phím ESC để bật/tắt Menu trong lúc chơi game
        if (IsEscapePressed())
        {
            if (isMenuActive)
            {
                StartNewGame();
            }
            else
            {
                ShowMenu();
            }
        }
    }

    private bool IsEnterPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            return Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#else
        return false;
#endif
    }

    private bool IsRPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            return Keyboard.current.rKey.wasPressedThisFrame;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.R);
#else
        return false;
#endif
    }

    private bool IsEscapePressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            return Keyboard.current.escapeKey.wasPressedThisFrame;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Escape);
#else
        return false;
#endif
    }

    public void ShowMenu()
    {
        isMenuActive = true;
        Time.timeScale = 0f; // Đóng băng thế giới game khi ở Menu

        // Tạm dừng nhạc game chính nếu đang phát
        if (BGM_Manager.Instance != null)
        {
            BGM_Manager.Instance.PauseBGM();
        }

        PlayMenuMusic();
    }

    private void PlayMenuMusic()
    {
        if (menuAudioSource == null) return;

        if (menuMusicClip != null)
        {
            menuAudioSource.clip = menuMusicClip;
            menuAudioSource.volume = menuMusicVolume;
            if (!menuAudioSource.isPlaying)
            {
                menuAudioSource.Play();
            }
        }
        else
        {
            LoadMenuAssets();
            if (menuMusicClip != null)
            {
                menuAudioSource.clip = menuMusicClip;
                menuAudioSource.volume = menuMusicVolume;
                menuAudioSource.Play();
            }
        }
    }

    /// <summary>
    /// Bắt đầu chơi game (New Game)
    /// </summary>
    public void StartNewGame()
    {
        PlayButtonSound();

        isMenuActive = false;
        Time.timeScale = 1f; // Tiếp tục thời gian game

        // Dừng nhạc Menu
        if (menuAudioSource != null && menuAudioSource.isPlaying)
        {
            menuAudioSource.Stop();
        }

        // Bật BGM chính của game (sound_1.wav)
        if (BGM_Manager.Instance != null)
        {
            BGM_Manager.Instance.PlayBGM();
        }

        ResetEnemyStats();

        Debug.Log("<color=green><b>[GameMenu] BẮT ĐẦU GAME! Chuyển sang nhạc nền sound_1.wav.</b></color>");
    }

    /// <summary>
    /// Thoát game
    /// </summary>
    public void ExitGame()
    {
        PlayButtonSound();
        Debug.Log("<color=red>[GameMenu] THOÁT GAME!</color>");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>
    /// Kích hoạt màn hình Game Over khi máu nhân vật về 0
    /// </summary>
    public void TriggerGameOver(float delay = 1.0f)
    {
        if (isGameOver) return;

        Debug.Log("<color=red><b>[GameMenu] PLAYER HP VỀ 0! DỪNG TOÀN BỘ NHẠC VÀ BẬT BẢNG GAME OVER...</b></color>");

        // 1. Tắt toàn bộ nhạc ngay lập tức
        if (BGM_Manager.Instance != null)
        {
            BGM_Manager.Instance.StopBGM();
        }

        if (menuAudioSource != null && menuAudioSource.isPlaying)
        {
            menuAudioSource.Stop();
        }

        // Tắt toàn bộ nguồn âm thanh lặp khác đang phát trong cảnh
        AudioSource[] allSources = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        foreach (var s in allSources)
        {
            if (s != null && s.isPlaying && s.loop)
            {
                s.Stop();
            }
        }

        StartCoroutine(ShowGameOverCoroutine(delay));
    }

    private IEnumerator ShowGameOverCoroutine(float delay)
    {
        // Chờ chạy xong animation chết của nhân vật
        yield return new WaitForSecondsRealtime(delay);

        isGameOver = true;
        isMenuActive = false;
        Time.timeScale = 0f; // Đóng băng trò chơi

        // Phát hiệu ứng âm thanh Game Over
        if (menuAudioSource != null)
        {
            if (gameOverSfx == null) gameOverSfx = CreateGameOverSound();
            menuAudioSource.PlayOneShot(gameOverSfx, 0.9f);
        }
    }

    /// <summary>
    /// Chơi lại từ đầu màn chơi (Restart)
    /// </summary>
    public void RestartGame()
    {
        PlayButtonSound();

        Time.timeScale = 1f; // Khôi phục thời gian
        isGameOver = false;
        isMenuActive = false;

        // Reset lại coin
        if (CoinManager.Instance != null)
        {
            CoinManager.Instance.ResetCoins();
        }

        // Đặt lại số quái
        ResetEnemyStats();

        // Tải lại Scene hiện tại
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        UnityEngine.SceneManagement.SceneManager.LoadScene(currentScene);

        // Khởi động lại BGM sau khi nạp scene
        StartCoroutine(RestartBGMDelay());

        Debug.Log("<color=green><b>[GameMenu] RESTART GAME! Tải lại màn chơi và bật lại nhạc nền.</b></color>");
    }

    private IEnumerator RestartBGMDelay()
    {
        yield return null; // Chờ 1 frame cho scene nạp xong
        if (BGM_Manager.Instance != null)
        {
            BGM_Manager.Instance.PlayBGM();
        }
    }

    /// <summary>
    /// Kích hoạt màn hình VICTORY khi Player chạm vào vùng kết thúc (telewin / endgame)
    /// </summary>
    public void TriggerVictory()
    {
        if (isVictory) return;

        Debug.Log("<color=green><b>[GameMenu] VICTORY! NGƯỜI CHƠI ĐÃ VỀ ĐÍCH! BẬT BẢNG CHIẾN THẮNG...</b></color>");

        // 1. Tắt toàn bộ nhạc nền game đang phát
        if (BGM_Manager.Instance != null)
        {
            BGM_Manager.Instance.StopBGM();
        }

        // Tắt toàn bộ AudioSource lặp khác trong cảnh
        AudioSource[] allSources = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        foreach (var s in allSources)
        {
            if (s != null && s.isPlaying && s.loop && s != menuAudioSource)
            {
                s.Stop();
            }
        }

        isVictory = true;
        isGameOver = false;
        isMenuActive = false;
        Time.timeScale = 0f; // Đóng băng trò chơi

        // 2. Phát nhạc Victory: bgm_special_research_battle.wav
        PlayVictoryMusic();
    }

    private void PlayVictoryMusic()
    {
        if (menuAudioSource == null) return;

        if (victoryMusicClip == null)
        {
            LoadEndGameAssets();
        }

        if (victoryMusicClip != null)
        {
            menuAudioSource.clip = victoryMusicClip;
            menuAudioSource.volume = menuMusicVolume;
            menuAudioSource.loop = true;
            menuAudioSource.Play();
        }
    }

    /// <summary>
    /// Chơi lại màn chơi từ màn hình Victory (Retry)
    /// </summary>
    public void RetryGame()
    {
        PlayButtonSound();

        Time.timeScale = 1f;
        isVictory = false;
        isGameOver = false;
        isMenuActive = false;

        // Dừng nhạc Victory
        if (menuAudioSource != null && menuAudioSource.isPlaying)
        {
            menuAudioSource.Stop();
        }

        // Đặt lại số coin
        if (CoinManager.Instance != null)
        {
            CoinManager.Instance.ResetCoins();
        }

        // Đặt lại số quái
        ResetEnemyStats();

        // Tải lại Scene hiện tại
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        UnityEngine.SceneManagement.SceneManager.LoadScene(currentScene);

        // Khởi động lại BGM bình thường sau khi nạp scene
        StartCoroutine(RestartBGMDelay());

        Debug.Log("<color=green><b>[GameMenu] RETRY GAME! Tải lại màn chơi và mở lại nhạc nền.</b></color>");
    }

    // =========================================================
    // VẼ GIAO DIỆN START MENU TRÊN TOÀN MÀN HÌNH (OnGUI)
    // =========================================================
    private void OnGUI()
    {
        // 1. Ưu tiên cao nhất: Bảng VICTORY khi về đích
        if (isVictory)
        {
            DrawVictoryUI();
            return;
        }

        // 2. Bảng GAME OVER khi người chơi hết máu
        if (isGameOver)
        {
            DrawGameOverUI();
            return;
        }

        if (!isMenuActive) return;

        // Ưu tiên hiển thị cao nhất đè lên tất cả HUD
        GUI.depth = -100;

        float screenW = Screen.width;
        float screenH = Screen.height;

        // 1. Vẽ Ảnh Nền riêng (3.png từ Assets/Settings/background_new)
        if (menuBackgroundTexture != null)
        {
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(0, 0, screenW, screenH), menuBackgroundTexture, ScaleMode.StretchToFill);
        }
        else
        {
            // Dự phòng nền màu tối nếu ảnh chưa tải xong
            GUI.color = new Color(0.08f, 0.08f, 0.12f, 1f);
            GUI.DrawTexture(new Rect(0, 0, screenW, screenH), Texture2D.whiteTexture);
        }

        // 2. Lớp phủ khung tối sang trọng ở giữa để chữ và nút nổi bật
        float boxW = Mathf.Min(480f, screenW * 0.85f);
        float boxH = Mathf.Min(300f, screenH * 0.8f);
        float boxX = (screenW - boxW) * 0.5f;
        float boxY = (screenH - boxH) * 0.5f;

        Texture2D boxTex = GetOrCreateCenterBoxTexture();
        GUI.color = new Color(1f, 1f, 1f, 0.88f);
        GUI.DrawTexture(new Rect(boxX, boxY, boxW, boxH), boxTex, ScaleMode.StretchToFill);

        // 3. Tiêu đề Game
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(Mathf.Clamp(screenH * 0.065f, 32f, 52f)),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        Rect titleShadowRect = new Rect(boxX, boxY + 36f, boxW, 60f);
        Rect titleRect = new Rect(boxX, boxY + 34f, boxW, 60f);

        // Đổ bóng chữ đen
        titleStyle.normal.textColor = new Color(0f, 0f, 0f, 0.9f);
        GUI.Label(titleShadowRect, gameTitle, titleStyle);

        // Chữ vàng ánh kim
        titleStyle.normal.textColor = new Color(1f, 0.88f, 0.28f, 1f);
        GUI.Label(titleRect, gameTitle, titleStyle);

        // Đường gạch chỉ trang trí dưới tiêu đề
        GUI.color = new Color(1f, 0.84f, 0.2f, 0.6f);
        GUI.DrawTexture(new Rect(boxX + boxW * 0.2f, boxY + 100f, boxW * 0.6f, 2f), Texture2D.whiteTexture);

        // 4. Các nút bấm (NEW GAME & EXIT)
        float btnW = Mathf.Min(260f, boxW * 0.75f);
        float btnH = 50f;
        float btnX = (screenW - btnW) * 0.5f;
        float startY = boxY + 125f;
        float spacing = 18f;

        GUIStyle btnStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        btnStyle.normal.background = GetOrCreateButtonNormalTexture();
        btnStyle.hover.background = GetOrCreateButtonHoverTexture();
        btnStyle.active.background = GetOrCreateButtonHoverTexture();
        btnStyle.normal.textColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        btnStyle.hover.textColor = new Color(1f, 0.95f, 0.4f, 1f);

        // --- Nút NEW GAME ---
        Rect newGameRect = new Rect(btnX, startY, btnW, btnH);
        GUI.color = Color.white;
        if (GUI.Button(newGameRect, "NEW GAME", btnStyle) || (isMenuActive && IsEnterPressed()))
        {
            StartNewGame();
        }

        // --- Nút EXIT ---
        Rect exitRect = new Rect(btnX, startY + btnH + spacing, btnW, btnH);
        GUI.color = Color.white;
        if (GUI.Button(exitRect, "EXIT", btnStyle))
        {
            ExitGame();
        }
    }

    private void PlayButtonSound()
    {
        if (menuAudioSource != null && btnClickClip != null)
        {
            menuAudioSource.PlayOneShot(btnClickClip, 0.8f);
        }
    }

    // =========================================================
    // TỰ ĐỘNG TẠO TEXTURE & AUDIO CHO GIAO DIỆN BẰNG CODE
    // =========================================================
    private Texture2D GetOrCreateButtonNormalTexture()
    {
        if (btnNormalTexture == null)
        {
            int w = 32;
            int h = 32;
            btnNormalTexture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color fill = new Color(0.12f, 0.14f, 0.18f, 0.95f);
            Color border = new Color(0.75f, 0.65f, 0.25f, 0.9f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool isBorder = (x <= 1 || x >= w - 2 || y <= 1 || y >= h - 2);
                    btnNormalTexture.SetPixel(x, y, isBorder ? border : fill);
                }
            }
            btnNormalTexture.Apply();
        }
        return btnNormalTexture;
    }

    private Texture2D GetOrCreateButtonHoverTexture()
    {
        if (btnHoverTexture == null)
        {
            int w = 32;
            int h = 32;
            btnHoverTexture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color fill = new Color(0.24f, 0.22f, 0.10f, 0.98f);
            Color border = new Color(1f, 0.9f, 0.35f, 1f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool isBorder = (x <= 2 || x >= w - 3 || y <= 2 || y >= h - 3);
                    btnHoverTexture.SetPixel(x, y, isBorder ? border : fill);
                }
            }
            btnHoverTexture.Apply();
        }
        return btnHoverTexture;
    }

    private Texture2D GetOrCreateCenterBoxTexture()
    {
        if (centerBoxTexture == null)
        {
            int w = 32;
            int h = 32;
            centerBoxTexture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color fill = new Color(0.06f, 0.07f, 0.10f, 0.92f);
            Color border = new Color(0.85f, 0.72f, 0.25f, 0.5f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool isBorder = (x <= 1 || x >= w - 2 || y <= 1 || y >= h - 2);
                    centerBoxTexture.SetPixel(x, y, isBorder ? border : fill);
                }
            }
            centerBoxTexture.Apply();
        }
        return centerBoxTexture;
    }

    private AudioClip CreateClickChime()
    {
        int sampleRate = 44100;
        float duration = 0.15f;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float decay = Mathf.Exp(-t * 22f);
            float wave = Mathf.Sin(2f * Mathf.PI * 1250f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 1650f * t) * 0.4f;
            samples[i] = wave * decay;
        }

        AudioClip clip = AudioClip.Create("BtnClick", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    // =========================================================
    // GIAO DIỆN GAME OVER (RESTART & EXIT)
    // =========================================================
    private void DrawGameOverUI()
    {
        GUI.depth = -200; // Ưu tiên hiển thị đè lên tất cả

        float screenW = Screen.width;
        float screenH = Screen.height;

        // 1. Lớp phủ đen mờ làm dịu nền game phía sau
        GUI.color = new Color(0f, 0f, 0f, 0.78f);
        GUI.DrawTexture(new Rect(0, 0, screenW, screenH), Texture2D.whiteTexture);

        // 2. Khung bảng Game Over ở chính giữa màn hình
        float boxW = Mathf.Min(460f, screenW * 0.88f);
        float boxH = Mathf.Min(270f, screenH * 0.82f);
        float boxX = (screenW - boxW) * 0.5f;
        float boxY = (screenH - boxH) * 0.5f;

        Texture2D boxTex = GetOrCreateGameOverBoxTexture();
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(boxX, boxY, boxW, boxH), boxTex, ScaleMode.StretchToFill);

        // 3. Tiêu đề GAME OVER
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(Mathf.Clamp(screenH * 0.07f, 34f, 52f)),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        Rect shadowRect = new Rect(boxX, boxY + 28f, boxW, 58f);
        Rect titleRect = new Rect(boxX, boxY + 26f, boxW, 58f);

        // Đổ bóng đen
        titleStyle.normal.textColor = new Color(0f, 0f, 0f, 0.95f);
        GUI.Label(shadowRect, "GAME OVER", titleStyle);

        // Màu đỏ rực bi tráng
        titleStyle.normal.textColor = new Color(0.95f, 0.22f, 0.22f, 1f);
        GUI.Label(titleRect, "GAME OVER", titleStyle);

        // Đường gạch chỉ trang trí màu đỏ thẫm
        GUI.color = new Color(0.95f, 0.25f, 0.25f, 0.7f);
        GUI.DrawTexture(new Rect(boxX + boxW * 0.18f, boxY + 90f, boxW * 0.64f, 2f), Texture2D.whiteTexture);

        // 4. Hai nút bấm: RESTART ở trên, EXIT ở dưới
        float btnW = Mathf.Min(260f, boxW * 0.75f);
        float btnH = 50f;
        float btnX = (screenW - btnW) * 0.5f;
        float startY = boxY + 115f;
        float spacing = 18f;

        GUIStyle btnStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        btnStyle.normal.background = GetOrCreateButtonNormalTexture();
        btnStyle.hover.background = GetOrCreateButtonHoverTexture();
        btnStyle.active.background = GetOrCreateButtonHoverTexture();
        btnStyle.normal.textColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        btnStyle.hover.textColor = new Color(1f, 0.95f, 0.4f, 1f);

        // --- Nút RESTART ---
        Rect restartRect = new Rect(btnX, startY, btnW, btnH);
        GUI.color = Color.white;
        if (GUI.Button(restartRect, "RESTART", btnStyle) || IsEnterPressed() || IsRPressed())
        {
            RestartGame();
        }

        // --- Nút EXIT (nằm bên dưới RESTART) ---
        Rect exitRect = new Rect(btnX, startY + btnH + spacing, btnW, btnH);
        GUI.color = Color.white;
        if (GUI.Button(exitRect, "EXIT", btnStyle))
        {
            ExitGame();
        }
    }

    private Texture2D GetOrCreateGameOverBoxTexture()
    {
        if (gameOverBoxTexture == null)
        {
            int w = 32;
            int h = 32;
            gameOverBoxTexture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color fill = new Color(0.08f, 0.05f, 0.06f, 0.95f);
            Color border = new Color(0.85f, 0.22f, 0.22f, 0.85f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool isBorder = (x <= 1 || x >= w - 2 || y <= 1 || y >= h - 2);
                    gameOverBoxTexture.SetPixel(x, y, isBorder ? border : fill);
                }
            }
            gameOverBoxTexture.Apply();
        }
        return gameOverBoxTexture;
    }

    private AudioClip CreateGameOverSound()
    {
        int sampleRate = 44100;
        float duration = 1.2f;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Clamp01(1f - t / duration);
            // Chuỗi 3 nốt nhạc đi xuống bi ai: G3 (196Hz) -> Eb3 (155Hz) -> C3 (130Hz)
            float freq = (t < 0.35f) ? 196f : (t < 0.7f) ? 155.5f : 130.8f;
            float wave = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * (freq * 0.5f) * t) * 0.3f;
            samples[i] = wave * env * 0.6f;
        }

        AudioClip clip = AudioClip.Create("GameOverSfx", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    // =========================================================
    // GIAO DIỆN END GAME (VICTORY, COINS, RETRY & EXIT)
    // =========================================================
    private void DrawVictoryUI()
    {
        GUI.depth = -200; // Ưu tiên hiển thị đè lên toàn bộ HUD

        float screenW = Screen.width;
        float screenH = Screen.height;

        // 1. Vẽ Ảnh Nền riêng của End Game (73.png từ Assets/Settings/endgame làm Fullscreen Background)
        if (victoryTexture != null)
        {
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(0, 0, screenW, screenH), victoryTexture, ScaleMode.StretchToFill);
        }
        else
        {
            LoadEndGameAssets();
            if (victoryTexture != null)
            {
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(0, 0, screenW, screenH), victoryTexture, ScaleMode.StretchToFill);
            }
            else
            {
                GUI.color = new Color(0.08f, 0.08f, 0.12f, 1f);
                GUI.DrawTexture(new Rect(0, 0, screenW, screenH), Texture2D.whiteTexture);
            }
        }

        // 2. Khung bảng Victory ở giữa màn hình
        float boxW = Mathf.Min(500f, screenW * 0.9f);
        float boxH = Mathf.Min(285f, screenH * 0.85f);
        float boxX = (screenW - boxW) * 0.5f;
        float boxY = (screenH - boxH) * 0.5f;

        Texture2D boxTex = GetOrCreateVictoryBoxTexture();
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(boxX, boxY, boxW, boxH), boxTex, ScaleMode.StretchToFill);

        // 3. Tiêu đề "VICTORY"
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(Mathf.Clamp(screenH * 0.07f, 34f, 50f)),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        Rect shadowRect = new Rect(boxX, boxY + 22f, boxW, 50f);
        Rect titleRect = new Rect(boxX, boxY + 20f, boxW, 50f);

        // Đổ bóng chữ đen
        titleStyle.normal.textColor = new Color(0f, 0f, 0f, 0.95f);
        GUI.Label(shadowRect, "VICTORY", titleStyle);

        // Chữ vàng rực ánh kim
        titleStyle.normal.textColor = new Color(1f, 0.88f, 0.28f, 1f);
        GUI.Label(titleRect, "VICTORY", titleStyle);

        // Đường gạch chỉ vàng ánh kim
        GUI.color = new Color(1f, 0.84f, 0.2f, 0.75f);
        GUI.DrawTexture(new Rect(boxX + boxW * 0.15f, boxY + 74f, boxW * 0.7f, 2f), Texture2D.whiteTexture);

        // 4. Hiển thị thông số kết thúc game (1 nửa trái: TOTAL COIN | 1 nửa phải: TOTAL ENEMY)
        int coins = (CoinManager.Instance != null) ? CoinManager.Instance.TotalCoins : 0;
        int killed = killedEnemies;
        string enemyCountStr = killed.ToString("N0");

        float statY = boxY + 86f;
        float halfW = boxW * 0.5f;

        // Vạch phân cách dọc ở giữa 2 nửa
        GUI.color = new Color(1f, 0.84f, 0.2f, 0.45f);
        GUI.DrawTexture(new Rect(boxX + halfW - 1f, statY + 3f, 2f, 24f), Texture2D.whiteTexture);

        GUIStyle statStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        // --- NỬA BÊN TRÁI: TOTAL COIN ---
        Rect leftShadow = new Rect(boxX, statY + 1f, halfW, 30f);
        Rect leftRect = new Rect(boxX, statY, halfW, 30f);

        statStyle.normal.textColor = new Color(0f, 0f, 0f, 0.9f);
        GUI.Label(leftShadow, "TOTAL COIN: " + coins.ToString("N0"), statStyle);

        statStyle.normal.textColor = new Color(1f, 0.92f, 0.35f, 1f);
        GUI.Label(leftRect, "TOTAL COIN: " + coins.ToString("N0"), statStyle);

        // --- NỬA BÊN PHẢI: TOTAL ENEMY ---
        Rect rightShadow = new Rect(boxX + halfW, statY + 1f, halfW, 30f);
        Rect rightRect = new Rect(boxX + halfW, statY, halfW, 30f);

        statStyle.normal.textColor = new Color(0f, 0f, 0f, 0.9f);
        GUI.Label(rightShadow, "TOTAL ENEMY: " + enemyCountStr, statStyle);

        statStyle.normal.textColor = new Color(1f, 0.88f, 0.42f, 1f);
        GUI.Label(rightRect, "TOTAL ENEMY: " + enemyCountStr, statStyle);

        // 5. Nút RETRY và Nút EXIT bên dưới
        float btnW = Mathf.Min(250f, boxW * 0.75f);
        float btnH = 48f;
        float btnX = (screenW - btnW) * 0.5f;
        float startY = statY + 40f;
        float spacing = 14f;

        GUIStyle btnStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        btnStyle.normal.background = GetOrCreateButtonNormalTexture();
        btnStyle.hover.background = GetOrCreateButtonHoverTexture();
        btnStyle.active.background = GetOrCreateButtonHoverTexture();
        btnStyle.normal.textColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        btnStyle.hover.textColor = new Color(1f, 0.95f, 0.4f, 1f);

        // --- Nút RETRY ---
        Rect retryRect = new Rect(btnX, startY, btnW, btnH);
        GUI.color = Color.white;
        if (GUI.Button(retryRect, "RETRY", btnStyle) || IsEnterPressed() || IsRPressed())
        {
            RetryGame();
        }

        // --- Nút EXIT (nằm bên dưới RETRY) ---
        Rect exitRect = new Rect(btnX, startY + btnH + spacing, btnW, btnH);
        GUI.color = Color.white;
        if (GUI.Button(exitRect, "EXIT", btnStyle))
        {
            ExitGame();
        }
    }

    private Texture2D GetOrCreateVictoryBoxTexture()
    {
        if (victoryBoxTexture == null)
        {
            int w = 32;
            int h = 32;
            victoryBoxTexture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color fill = new Color(0.06f, 0.08f, 0.12f, 0.95f);
            Color border = new Color(1f, 0.85f, 0.25f, 0.95f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool isBorder = (x <= 1 || x >= w - 2 || y <= 1 || y >= h - 2);
                    victoryBoxTexture.SetPixel(x, y, isBorder ? border : fill);
                }
            }
            victoryBoxTexture.Apply();
        }
        return victoryBoxTexture;
    }
}
