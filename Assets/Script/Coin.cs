using UnityEngine;

/// <summary>
/// Quản lý từng đồng xu (Coin) rơi ra trong game:
/// - Tự động tạo hình ảnh đồng xu vàng kim loại bằng code (không cần Sprite có sẵn)
/// - Hiệu ứng nảy và xoay tròn 3D đẹp mắt
/// - Cơ chế nam châm: Khi Player lại gần trong tầm hút, coin sẽ tự động bay mượt vào người Player
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class Coin : MonoBehaviour
{
    [Header("Coin Stats")]
    [Tooltip("Giá trị của đồng xu")]
    [SerializeField] private int coinValue = 1;

    [Header("Magnet Settings (Hút vào Player)")]
    [Tooltip("Khoảng cách bắt đầu hút vào người Player")]
    [SerializeField] private float magnetRange = 3.2f;
    [Tooltip("Tốc độ bay ban đầu khi bắt đầu hút")]
    [SerializeField] private float initialFlySpeed = 2f;
    [Tooltip("Tốc độ bay tối đa")]
    [SerializeField] private float maxFlySpeed = 16f;
    [Tooltip("Gia tốc tăng tốc độ bay khi đang hút")]
    [SerializeField] private float acceleration = 25f;
    [Tooltip("Thời gian chờ nảy ra tự nhiên trước khi bắt đầu cho phép hút (giúp thấy rõ hiệu ứng quái rơi coin)")]
    [SerializeField] private float delayBeforeMagnet = 0.35f;
    [Tooltip("Khoảng cách tiếp xúc tối thiểu để tính là đã thu thập")]
    [SerializeField] private float collectDistance = 0.4f;

    [Header("Visual Effects")]
    [Tooltip("Kích thước cơ bản của đồng xu")]
    [SerializeField] private float baseScale = 0.5f;
    [Tooltip("Tốc độ xoay đồng xu")]
    [SerializeField] private float spinSpeed = 7f;

    private SpriteRenderer sr;
    private Rigidbody2D rb;
    private CircleCollider2D col;
    private Transform playerTransform;

    private bool isAttracted = false;
    private float currentFlySpeed;
    private float spawnTimer;
    private float spinPhase;
    private bool isCollected = false;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<CircleCollider2D>();

        // Thiết lập Sorting Order cao để nổi rõ trên sàn và cảnh nền
        sr.sortingOrder = 95;

        // Thiết lập Collider & Rigidbody nảy tự nhiên
        col.radius = 0.22f;
        rb.mass = 0.5f;
        rb.gravityScale = 2.2f;
        rb.freezeRotation = true;

        // Tạo vật liệu nảy (Bouncy) bằng code
        PhysicsMaterial2D bouncyMat = new PhysicsMaterial2D("CoinBounce")
        {
            bounciness = 0.45f,
            friction = 0.4f
        };
        col.sharedMaterial = bouncyMat;

        // Pha ngẫu nhiên để các đồng xu không xoay đồng loạt
        spinPhase = Random.Range(0f, Mathf.PI * 2f);
        currentFlySpeed = initialFlySpeed;
    }

    private void Start()
    {
        // Gán Sprite xu vàng nếu chưa có
        if (sr.sprite == null)
        {
            sr.sprite = CoinManager.GetOrCreateCoinSprite();
        }

        FindPlayer();
    }

    /// <summary>
    /// Khởi tạo lực bắn tung ra khi quái chết
    /// </summary>
    public void Launch(Vector2 popForce, int value = 1)
    {
        coinValue = value;
        if (rb == null) rb = GetComponent<Rigidbody2D>();

#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = popForce;
#else
        rb.velocity = popForce;
#endif
    }

    private void Update()
    {
        if (isCollected) return;

        spawnTimer += Time.deltaTime;

        // 1. Hiệu ứng xoay tròn 2.5D (thu hẹp trục X theo hình sin)
        float spinX = Mathf.Cos(Time.time * spinSpeed + spinPhase) * baseScale;
        transform.localScale = new Vector3(spinX, baseScale, 1f);

        // 2. Tìm Player nếu chưa có
        if (playerTransform == null)
        {
            FindPlayer();
        }

        if (playerTransform == null) return;

        // Lấy tâm của Player (ngang thắt lưng/ngực)
        Vector3 targetPos = playerTransform.position + Vector3.up * 0.4f;
        float distToPlayer = Vector2.Distance(transform.position, targetPos);

        // 3. Cơ chế tự động hút vào Player
        if (!isAttracted)
        {
            if (spawnTimer >= delayBeforeMagnet && distToPlayer <= magnetRange)
            {
                isAttracted = true;
                // Chuyển sang bay tự do không ma sát
                rb.gravityScale = 0f;
#if UNITY_6000_0_OR_NEWER
                rb.linearVelocity = Vector2.zero;
#else
                rb.velocity = Vector2.zero;
#endif
                col.isTrigger = true; // Không bị vướng sàn khi đang bay về Player
            }
        }
        else
        {
            // Tăng tốc dần khi bay về phía Player
            currentFlySpeed = Mathf.Min(currentFlySpeed + acceleration * Time.deltaTime, maxFlySpeed);
            transform.position = Vector3.MoveTowards(transform.position, targetPos, currentFlySpeed * Time.deltaTime);

            // Khi chạm vào người Player
            if (distToPlayer <= collectDistance)
            {
                Collect();
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isCollected) return;

        if (spawnTimer >= delayBeforeMagnet && (other.CompareTag("Player") || other.GetComponent<Player_Controller>() != null))
        {
            Collect();
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isCollected) return;

        if (spawnTimer >= delayBeforeMagnet && (collision.gameObject.CompareTag("Player") || collision.gameObject.GetComponent<Player_Controller>() != null))
        {
            Collect();
        }
    }

    private void Collect()
    {
        if (isCollected) return;
        isCollected = true;

        // Cộng xu vào hệ thống quản lý Coin
        CoinManager.Instance.AddCoins(coinValue);

        // Hủy Coin khỏi scene
        Destroy(gameObject);
    }

    private void FindPlayer()
    {
        Player_Controller player = Object.FindFirstObjectByType<Player_Controller>();
        if (player != null)
        {
            playerTransform = player.transform;
            return;
        }

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }
    }
}
