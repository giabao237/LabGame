using UnityEngine;

public class Health : MonoBehaviour
{
    [Header("Health Settings (Chỉ số máu)")]
    [Tooltip("Lượng máu tối đa - bạn tự điền ở Inspector")]
    [SerializeField] private float maxHealth = 100f;

    [Tooltip("Tự động xóa GameObject sau khi hết máu và chạy xong animation chết")]
    [SerializeField] private bool destroyOnDeath = true;

    [Tooltip("Thời gian chờ trước khi biến mất (nếu không tìm thấy độ dài clip)")]
    [SerializeField] private float defaultDeathDelay = 1.0f;

    [Header("Health Bar Visuals (Thanh máu trên đầu)")]
    [Tooltip("Độ lệch vị trí thanh máu trên đầu nhân vật")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 2.0f, 0f);

    [Tooltip("Chiều dài thanh máu (mặc định 1.4 để 2 thanh ngang bằng nhau)")]
    [SerializeField] private float barWidth = 1.4f;

    [Tooltip("Độ dày của thanh máu")]
    [SerializeField] private float barHeight = 0.18f;

    [Tooltip("Màu của thanh máu (Màu đỏ)")]
    [SerializeField] private Color healthColor = new Color(0.95f, 0.15f, 0.15f, 1f);

    [Tooltip("Màu nền phía sau (Màu đen - khi mất máu sẽ lộ ra)")]
    [SerializeField] private Color backgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f);

    [Tooltip("Màu viền ngoài")]
    [SerializeField] private Color borderColor = Color.black;

    private float currentHealth;
    private GameObject healthBarRoot;
    private Transform fillTransform;
    private bool isDead = false;

    // Cache static sprites chuẩn kích thước 1x1 đơn vị
    private static Sprite centerPivotSprite;
    private static Sprite leftPivotSprite;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => isDead;

    private void Awake()
    {
        currentHealth = maxHealth;
        CreateHealthBar();
    }

    private void Start()
    {
        UpdateHealthBar();
    }

    private void LateUpdate()
    {
        if (healthBarRoot != null)
        {
            // Luôn đi theo vị trí nhân vật nhưng KHÔNG xoay hoặc lật theo nhân vật (khóa Flip)
            healthBarRoot.transform.position = transform.position + offset;
            healthBarRoot.transform.rotation = Quaternion.identity;
        }
    }

    private void OnDestroy()
    {
        if (healthBarRoot != null)
        {
            Destroy(healthBarRoot);
        }
    }

    private static Sprite GetCenterPivotSprite()
    {
        if (centerPivotSprite == null)
        {
            // Tạo Texture 1x1 pixel chuẩn xác tuyệt đối (1 pixel = 1 Unity unit)
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            centerPivotSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }
        return centerPivotSprite;
    }

    private static Sprite GetLeftPivotSprite()
    {
        if (leftPivotSprite == null)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            // Pivot ở (0, 0.5) = Cạnh trái ở chính giữa theo trục Y
            leftPivotSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f), 1f);
        }
        return leftPivotSprite;
    }

    private void CreateHealthBar()
    {
        // 1. Tạo Root của thanh máu độc lập (để không bị ảnh hưởng scale/flip của nhân vật)
        healthBarRoot = new GameObject($"{gameObject.name}_HealthBar");
        healthBarRoot.transform.position = transform.position + offset;

        Sprite centerSprite = GetCenterPivotSprite();
        Sprite leftSprite = GetLeftPivotSprite();

        // 2. Viền ngoài màu đen (Border) - Lớn hơn nền một chút
        GameObject borderObj = new GameObject("Border");
        borderObj.transform.SetParent(healthBarRoot.transform, false);
        SpriteRenderer borderSR = borderObj.AddComponent<SpriteRenderer>();
        borderSR.sprite = centerSprite;
        borderSR.color = borderColor;
        borderSR.sortingOrder = 100;
        borderObj.transform.localPosition = Vector3.zero;
        borderObj.transform.localScale = new Vector3(barWidth + 0.06f, barHeight + 0.06f, 1f);

        // 3. Nền thanh máu màu đen (Background) - Nằm chính giữa
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(healthBarRoot.transform, false);
        SpriteRenderer bgSR = bgObj.AddComponent<SpriteRenderer>();
        bgSR.sprite = centerSprite;
        bgSR.color = backgroundColor;
        bgSR.sortingOrder = 101;
        bgObj.transform.localPosition = Vector3.zero;
        bgObj.transform.localScale = new Vector3(barWidth, barHeight, 1f);

        // 4. Thanh máu màu đỏ (Fill) - Nằm đè lên trên nền đen
        // Bắt đầu từ mép trái (-barWidth / 2) và kéo dài sang mép phải (+barWidth / 2)
        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(healthBarRoot.transform, false);
        SpriteRenderer fillSR = fillObj.AddComponent<SpriteRenderer>();
        fillSR.sprite = leftSprite;
        fillSR.color = healthColor;
        fillSR.sortingOrder = 102; // Order cao hơn để nằm đè lên trên nền đen

        // Đặt vị trí chính xác ở mép trái của thanh đen
        fillObj.transform.localPosition = new Vector3(-barWidth * 0.5f, 0f, 0f);
        fillTransform = fillObj.transform;
    }

    /// <summary>
    /// Nhận sát thương và trừ máu
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (isDead) return;

        currentHealth = Mathf.Clamp(currentHealth - damage, 0f, maxHealth);
        UpdateHealthBar();

        Debug.Log($"<color=red>[Health] {gameObject.name} bị trừ {damage} HP! Còn lại: {currentHealth}/{maxHealth}</color>");

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    public void Heal(float amount)
    {
        if (isDead) return;

        currentHealth = Mathf.Clamp(currentHealth + amount, 0f, maxHealth);
        UpdateHealthBar();
    }

    public void SetMaxHealth(float newMaxHealth)
    {
        maxHealth = newMaxHealth;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        UpdateHealthBar();
    }

    public void SetDestroyOnDeath(bool destroy)
    {
        destroyOnDeath = destroy;
    }

    private void UpdateHealthBar()
    {
        if (fillTransform == null) return;

        // Tính tỉ lệ phần trăm máu còn lại (0.0 đến 1.0)
        float fillPercent = maxHealth > 0 ? (currentHealth / maxHealth) : 0f;

        // Thu hẹp chiều dài thanh đỏ từ phải sang trái, để lộ nền đen bên dưới
        fillTransform.localScale = new Vector3(barWidth * fillPercent, barHeight, 1f);
    }

    private void Die()
    {
        isDead = true;
        Debug.Log($"<color=black><b>[Health] {gameObject.name} ĐÃ HẾT MÁU (DIE)!</b></color>");

        // 1. Ẩn thanh máu ngay lập tức
        if (healthBarRoot != null)
        {
            healthBarRoot.SetActive(false);
        }

        // 2. Tắt các Collider để không cản đường người chơi nữa
        Collider2D[] cols = GetComponentsInChildren<Collider2D>();
        foreach (var c in cols)
        {
            c.enabled = false;
        }

        float deathDelay = defaultDeathDelay;

        // 3. Nếu là Enemy, báo cho enemy_controller dừng hành vi
        enemy_controller enemy = GetComponent<enemy_controller>();
        if (enemy != null)
        {
            enemy.Die();
        }

        // 4. Nếu là Player, báo cho Player_Controller dừng hành vi và kích hoạt Die
        Player_Controller player = GetComponent<Player_Controller>();
        if (player != null)
        {
            player.Die();
        }
        else if (CompareTag("Player") || name.ToLower().Contains("player"))
        {
            if (BGM_Manager.Instance != null) BGM_Manager.Instance.StopBGM();
            if (GameMenu.Instance != null) GameMenu.Instance.TriggerGameOver(1.2f);
        }

        // 5. Tìm và kích hoạt Animation Die
        Animator anim = GetComponent<Animator>();
        if (anim == null) anim = GetComponentInChildren<Animator>();
        if (anim != null)
        {
            // Bật parameter Die tương ứng (IsDie cho Player, EnemyDie cho Enemy)
            foreach (var p in anim.parameters)
            {
                if (p.name == "IsDie") anim.SetBool("IsDie", true);
                if (p.name == "EnemyDie") anim.SetBool("EnemyDie", true);
                if (p.name == "IsDead") anim.SetBool("IsDead", true);
            }

            // Chơi trực tiếp animation state để đảm bảo hoạt cảnh chạy 100%
            if (anim.HasState(0, Animator.StringToHash("Animation_Die_Player")))
            {
                anim.Play("Animation_Die_Player", 0, 0f);
            }
            else if (anim.HasState(0, Animator.StringToHash("Animation_Die_Enemy")))
            {
                anim.Play("Animation_Die_Enemy", 0, 0f);
            }

            // Tự động đo độ dài chính xác của clip Animation_Die để biến mất đúng lúc
            if (anim.runtimeAnimatorController != null)
            {
                foreach (var clip in anim.runtimeAnimatorController.animationClips)
                {
                    if (clip.name == "Animation_Die_Player" || clip.name == "Animation_Die_Enemy")
                    {
                        deathDelay = clip.length;
                        break;
                    }
                }
            }
        }

        // 5. Sau khi chạy hết animation thì xóa GameObject (biến mất khỏi game)
        if (destroyOnDeath)
        {
            Debug.Log($"<color=grey>[Health] {gameObject.name} sẽ biến mất sau {deathDelay}s (khi hoạt cảnh chết kết thúc).</color>");
            Destroy(gameObject, deathDelay);
        }
    }
}
