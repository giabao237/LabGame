using UnityEngine;

/// <summary>
/// Quản lý toàn bộ hệ thống Coin trong Game:
/// - Tạo hình ảnh đồng xu vàng (Sprite) tự động 100% bằng code
/// - Phân phát / Rơi xu khi quái chết (DropCoins)
/// - Hiển thị giao diện tổng số Coin ở góc màn hình (HUD OnGUI mượt mà, tự co giãn theo mọi độ phân giải)
/// - Hiệu ứng nảy số (Punch scale) và âm thanh leng keng (Chime) khi nhận coin
/// </summary>
public class CoinManager : MonoBehaviour
{
    public enum ScreenCorner
    {
        TopLeft,
        TopRight
    }

    private static CoinManager instance;
    public static CoinManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Object.FindFirstObjectByType<CoinManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("CoinManager");
                    instance = go.AddComponent<CoinManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }

    /// <summary>
    /// Tự động khởi tạo CoinManager ngay khi bấm Play để hiển thị thanh Coin (0 coin) ngay từ đầu game
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        var _ = Instance;
    }

    [Header("Inventory Settings")]
    [Tooltip("Số coin hiện có")]
    [SerializeField] private int totalCoins = 0;

    [Header("UI Corner Settings")]
    [Tooltip("Góc hiển thị thanh Coin trên màn hình")]
    [SerializeField] private ScreenCorner cornerPosition = ScreenCorner.TopLeft;
    [Tooltip("Khoảng cách mép màn hình X")]
    [SerializeField] private float marginX = 24f;
    [Tooltip("Khoảng cách mép màn hình Y")]
    [SerializeField] private float marginY = 24f;
    [Tooltip("Hiển thị giao diện Coin trên góc màn hình")]
    [SerializeField] private bool showUI = true;

    [Header("Audio Settings")]
    [Tooltip("Phát âm thanh leng keng vui tai khi nhặt coin (tạo bằng code)")]
    [SerializeField] private bool playChimeSound = true;
    [Tooltip("Âm lượng âm thanh nhặt coin")]
    [Range(0f, 1f)]
    [SerializeField] private float chimeVolume = 0.85f;

    [Header("Player Health HUD (Trái tim & Máu cạnh Coin)")]
    [Tooltip("Hiển thị ô Trái tim & Máu của Player bên cạnh ô Coin")]
    [SerializeField] private bool showHealthUI = true;

    public int TotalCoins => totalCoins;

    private static Sprite cachedCoinSprite;
    private static Texture2D cachedCoinTexture;
    private static Texture2D cachedBadgeTexture;
    private static Texture2D cachedHeartTexture;

    private AudioSource audioSource;
    private AudioClip chimeClip;
    private float uiPunchScale = 1f;

    private Health playerHealth;
    private float lastPlayerHealth = -1f;
    private float heartPunchScale = 1f;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        // Tạo sẵn âm thanh leng keng tự động bằng code
        if (playChimeSound)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            chimeClip = CreateCoinChimeClip();
        }
    }

    private void Update()
    {
        // Hiệu ứng nảy số khi nhặt coin thu nhỏ dần về kích thước chuẩn
        if (uiPunchScale > 1f)
        {
            uiPunchScale = Mathf.MoveTowards(uiPunchScale, 1f, Time.deltaTime * 3.5f);
        }

        // Hiệu ứng đập tim khi mất máu thu nhỏ dần về kích thước chuẩn
        if (heartPunchScale > 1f)
        {
            heartPunchScale = Mathf.MoveTowards(heartPunchScale, 1f, Time.deltaTime * 3.5f);
        }

        // Kiểm tra biến động máu để kích hoạt nhịp tim
        float currentHp = GetPlayerHealth();
        if (lastPlayerHealth >= 0f && currentHp < lastPlayerHealth)
        {
            heartPunchScale = 1.35f; // Nảy to tim khi bị mất máu
        }
        lastPlayerHealth = currentHp;
    }

    /// <summary>
    /// Rơi ra số lượng Coin tại vị trí chỉ định
    /// </summary>
    public static void DropCoins(Vector3 position, int count = 3, int valuePerCoin = 1)
    {
        if (count <= 0) return;

        Sprite sprite = GetOrCreateCoinSprite();

        for (int i = 0; i < count; i++)
        {
            GameObject coinObj = new GameObject($"Coin_{i + 1}");
            // Rải vị trí ngẫu nhiên nhẹ quanh quái
            Vector3 spawnPos = position + new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0.2f, 0.6f), 0f);
            coinObj.transform.position = spawnPos;

            SpriteRenderer sr = coinObj.GetComponent<SpriteRenderer>();
            if (sr == null) sr = coinObj.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;

            Coin coin = coinObj.AddComponent<Coin>();

            // Lực bắn tung lên hình nón ngẫu nhiên
            float angleX = Random.Range(-2.8f, 2.8f);
            float popY = Random.Range(4.5f, 7.5f);
            coin.Launch(new Vector2(angleX, popY), valuePerCoin);
        }
    }

    /// <summary>
    /// Thêm coin vào túi đồ
    /// </summary>
    public void AddCoins(int amount)
    {
        if (amount <= 0) return;

        totalCoins += amount;
        uiPunchScale = 1.35f; // Nảy to giao diện khi nhặt

        Debug.Log($"<color=yellow><b>[CoinManager] +{amount} Coin! Tổng số coin hiện có: {totalCoins}</b></color>");

        // Phát tiếng leng keng
        if (playChimeSound && audioSource != null && chimeClip != null)
        {
            audioSource.PlayOneShot(chimeClip, chimeVolume);
        }
    }

    /// <summary>
    /// Đặt lại số coin về 0 khi chơi lại màn chơi (Restart Game)
    /// </summary>
    public void ResetCoins()
    {
        totalCoins = 0;
        uiPunchScale = 1f;
        Debug.Log("<color=yellow>[CoinManager] Đã đặt lại Coin về 0 khi Restart game.</color>");
    }

    /// <summary>
    /// Trừ coin (ví dụ mua đồ / nâng cấp sau này)
    /// </summary>
    public bool SpendCoins(int amount)
    {
        if (totalCoins >= amount)
        {
            totalCoins -= amount;
            return true;
        }
        return false;
    }

    // =========================================================
    // VẼ GIAO DIỆN COIN & MÁU (TRÁI TIM) TRÊN GÓC MÀN HÌNH (HUD OnGUI)
    // =========================================================
    private void OnGUI()
    {
        if (!showUI) return;

        Texture2D coinTex = GetOrCreateCoinTexture();
        Texture2D badgeTex = GetOrCreateBadgeTexture();
        Texture2D heartTex = GetOrCreateHeartTexture();

        // Kích thước thanh Badge
        float badgeWidth = 135f;
        float badgeHeight = 44f;
        float spacing = 12f;

        float hpPosX, coinPosX;
        float posY = marginY;

        if (cornerPosition == ScreenCorner.TopLeft)
        {
            hpPosX = marginX;
            coinPosX = marginX + badgeWidth + spacing;
        }
        else
        {
            coinPosX = Screen.width - badgeWidth - marginX;
            hpPosX = coinPosX - badgeWidth - spacing;
        }

        // =========================================================
        // 1. VẼ Ô MÁU: 1 TRÁI TIM + SỐ HP ĐANG CÓ BÊN CẠNH
        // =========================================================
        if (showHealthUI)
        {
            Rect hpBadgeRect = new Rect(hpPosX, posY, badgeWidth, badgeHeight);

            // Nền đen mờ
            GUI.color = new Color(1f, 1f, 1f, 0.9f);
            GUI.DrawTexture(hpBadgeRect, badgeTex, ScaleMode.StretchToFill);

            // Icon 1 Trái tim đỏ (nảy nhẹ khi trúng đòn)
            float heartSize = 28f * heartPunchScale;
            float heartOffset = (28f - heartSize) * 0.5f;
            Rect heartRect = new Rect(hpPosX + 9f + heartOffset, posY + (badgeHeight - 28f) * 0.5f + heartOffset, heartSize, heartSize);
            GUI.color = Color.white;
            GUI.DrawTexture(heartRect, heartTex, ScaleMode.ScaleToFit);

            // Chữ số lượng HP
            GUIStyle hpTextStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(20 * heartPunchScale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };

            float currentHp = GetPlayerHealth();
            string hpString = Mathf.Max(0, Mathf.CeilToInt(currentHp)).ToString();

            Rect hpShadowRect = new Rect(hpPosX + 46f, posY + 2f, badgeWidth - 50f, badgeHeight);
            Rect hpTextRect = new Rect(hpPosX + 45f, posY + 1f, badgeWidth - 50f, badgeHeight);

            // Đổ bóng chữ đen
            hpTextStyle.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            GUI.Label(hpShadowRect, hpString, hpTextStyle);

            // Chữ màu đỏ tươi rực rỡ
            hpTextStyle.normal.textColor = new Color(1f, 0.35f, 0.35f, 1f);
            GUI.Label(hpTextRect, hpString, hpTextStyle);
        }

        // =========================================================
        // 2. VẼ Ô COIN: 1 ĐỒNG XU + SỐ COIN HIỆN CÓ
        // =========================================================
        Rect coinBadgeRect = new Rect(coinPosX, posY, badgeWidth, badgeHeight);

        // Nền đen mờ
        GUI.color = new Color(1f, 1f, 1f, 0.9f);
        GUI.DrawTexture(coinBadgeRect, badgeTex, ScaleMode.StretchToFill);

        // Icon đồng xu vàng (nảy nhẹ khi nhặt)
        float iconSize = 30f * uiPunchScale;
        float iconOffset = (30f - iconSize) * 0.5f;
        Rect iconRect = new Rect(coinPosX + 8f + iconOffset, posY + (badgeHeight - 30f) * 0.5f + iconOffset, iconSize, iconSize);
        GUI.color = Color.white;
        GUI.DrawTexture(iconRect, coinTex, ScaleMode.ScaleToFit);

        // Chữ số lượng Coin
        GUIStyle textStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(20 * uiPunchScale),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };

        Rect textShadowRect = new Rect(coinPosX + 48f, posY + 2f, badgeWidth - 52f, badgeHeight);
        Rect textRect = new Rect(coinPosX + 47f, posY + 1f, badgeWidth - 52f, badgeHeight);

        // Đổ bóng chữ đen
        textStyle.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
        GUI.Label(textShadowRect, totalCoins.ToString("N0"), textStyle);

        // Chữ vàng ánh kim rực rỡ
        textStyle.normal.textColor = new Color(1f, 0.92f, 0.35f, 1f);
        GUI.Label(textRect, totalCoins.ToString("N0"), textStyle);

        GUI.color = Color.white;
    }

    // =========================================================
    // TỰ ĐỘNG TẠO HÌNH ẢNH ĐỒNG XU (SPRITE & TEXTURE) BẰNG CODE
    // =========================================================
    public static Sprite GetOrCreateCoinSprite()
    {
        if (cachedCoinSprite == null)
        {
            Texture2D tex = GetOrCreateCoinTexture();
            cachedCoinSprite = Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                32f // Pixels Per Unit
            );
            cachedCoinSprite.name = "Procedural_Coin_Sprite";
        }
        return cachedCoinSprite;
    }

    public static Texture2D GetOrCreateCoinTexture()
    {
        if (cachedCoinTexture == null)
        {
            int size = 32;
            cachedCoinTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            float center = size * 0.5f;
            float outerRadius = size * 0.44f;
            float innerRadius = size * 0.34f;

            Color darkBorder = new Color(0.65f, 0.42f, 0.05f, 1f); // Nâu đồng
            Color outerGold = new Color(1f, 0.82f, 0.12f, 1f);     // Vàng tươi
            Color highlight = new Color(1f, 0.98f, 0.75f, 1f);     // Vệt sáng ánh kim
            Color centerGold = new Color(0.95f, 0.72f, 0.08f, 1f);  // Lõi vàng

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, center));

                    if (dist > outerRadius + 0.5f)
                    {
                        cachedCoinTexture.SetPixel(x, y, Color.clear);
                    }
                    else if (dist >= outerRadius - 1.2f)
                    {
                        // Viền ngoài
                        float alpha = Mathf.Clamp01(outerRadius + 0.5f - dist);
                        Color c = darkBorder;
                        c.a = alpha;
                        cachedCoinTexture.SetPixel(x, y, c);
                    }
                    else if (dist >= innerRadius)
                    {
                        // Vành ngoài vàng bóng
                        float shine = (x + y) / (float)(size * 2);
                        Color c = Color.Lerp(outerGold, highlight, shine * 0.8f);
                        cachedCoinTexture.SetPixel(x, y, c);
                    }
                    else
                    {
                        // Lõi bên trong đồng xu
                        float shine = (x + y) / (float)(size * 2);
                        Color c = Color.Lerp(centerGold, highlight, shine);
                        // Vạch bóng nhỏ điểm nhấn ở góc trên bên trái
                        if (x >= center - 2 && x <= center + 2 && y >= center - 6 && y <= center + 6)
                        {
                            c = Color.Lerp(c, highlight, 0.7f);
                        }
                        cachedCoinTexture.SetPixel(x, y, c);
                    }
                }
            }
            cachedCoinTexture.Apply();
        }
        return cachedCoinTexture;
    }

    private static Texture2D GetOrCreateBadgeTexture()
    {
        if (cachedBadgeTexture == null)
        {
            int w = 64;
            int h = 32;
            cachedBadgeTexture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color bgColor = new Color(0.1f, 0.11f, 0.14f, 0.85f); // Đen mờ sang trọng
            Color borderColor = new Color(0.85f, 0.72f, 0.25f, 0.6f); // Viền vàng mờ

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool isBorder = (x <= 1 || x >= w - 2 || y <= 1 || y >= h - 2);
                    cachedBadgeTexture.SetPixel(x, y, isBorder ? borderColor : bgColor);
                }
            }
            cachedBadgeTexture.Apply();
        }
        return cachedBadgeTexture;
    }

    // =========================================================
    // TẠO ÂM THANH LENG KENG BẰNG CODE (Procedural Audio Chime)
    // =========================================================
    private AudioClip CreateCoinChimeClip()
    {
        int sampleRate = 44100;
        float duration = 0.25f;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        // 2 tần số nốt cao kết hợp (nốt Si 988 Hz và nốt Mi 1318 Hz)
        float freq1 = 988f;
        float freq2 = 1318f;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float decay = Mathf.Exp(-t * 14f); // Giảm dần âm lượng như chuông gõ
            float wave = (Mathf.Sin(2f * Mathf.PI * freq1 * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * freq2 * t) * 0.5f);
            samples[i] = wave * decay;
        }

        AudioClip clip = AudioClip.Create("CoinChime", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    // =========================================================
    // TỰ ĐỘNG TẠO HÌNH ẢNH TRÁI TIM (TEXTURE) BẰNG CODE
    // =========================================================
    public static Texture2D GetOrCreateHeartTexture()
    {
        if (cachedHeartTexture == null)
        {
            int size = 32;
            cachedHeartTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color darkBorder = new Color(0.48f, 0.04f, 0.08f, 1f); // Viền đỏ thẫm sắc nét
            Color heartRed = new Color(0.95f, 0.16f, 0.22f, 1f);   // Đỏ tươi rực rỡ
            Color deepRed = new Color(0.72f, 0.06f, 0.12f, 1f);    // Đỏ đậm phần bóng dưới
            Color highlight = new Color(1f, 0.72f, 0.76f, 1f);     // Vệt sáng ánh kim bóng bẩy

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Chuẩn hóa tọa độ tâm tim về khoảng quanh 0
                    float nx = (x - 15.5f) / 11.5f;
                    float ny = (y - 14.0f) / 11.5f;

                    // Đường cong toán học hình trái tim: (x^2 + y^2 - 1)^3 - x^2 * y^3 <= 0
                    float a = nx * nx + ny * ny - 1f;
                    float val = a * a * a - nx * nx * ny * ny * ny;

                    if (val > 0.06f)
                    {
                        cachedHeartTexture.SetPixel(x, y, Color.clear);
                    }
                    else if (val >= -0.05f)
                    {
                        // Viền ngoài sắc nét
                        float alpha = Mathf.Clamp01((0.06f - val) / 0.11f);
                        Color c = darkBorder;
                        c.a = alpha;
                        cachedHeartTexture.SetPixel(x, y, c);
                    }
                    else
                    {
                        // Ruột trái tim: chuyển sắc đỏ mượt mà từ trên xuống dưới
                        Color c = Color.Lerp(deepRed, heartRed, (y / (float)size) * 0.75f + 0.25f);

                        // Vệt bóng sáng ở góc trên bên trái của tim
                        float shineDist = Vector2.Distance(new Vector2(x, y), new Vector2(10.5f, 21.5f));
                        if (shineDist <= 4.5f)
                        {
                            float shineAlpha = Mathf.Clamp01(1f - shineDist / 4.5f);
                            c = Color.Lerp(c, highlight, shineAlpha * 0.85f);
                        }

                        cachedHeartTexture.SetPixel(x, y, c);
                    }
                }
            }
            cachedHeartTexture.Apply();
        }
        return cachedHeartTexture;
    }

    private float GetPlayerHealth()
    {
        if (playerHealth == null)
        {
            Player_Controller player = Object.FindFirstObjectByType<Player_Controller>();
            if (player != null)
            {
                playerHealth = player.GetComponent<Health>();
            }
        }
        return playerHealth != null ? playerHealth.CurrentHealth : 100f;
    }
}
