using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class enemy_controller : MonoBehaviour
{
    [Header("Patrol Settings (Cài đặt tuần tra)")]
    [Tooltip("Tốc độ di chuyển của quái")]
    [SerializeField] private float moveSpeed = 2.5f;

    [Tooltip("Số bước / khoảng cách đi sang trái (tính từ vị trí bắt đầu)")]
    [SerializeField] private float leftDistance = 3f;

    [Tooltip("Số bước / khoảng cách đi sang phải (tính từ vị trí bắt đầu)")]
    [SerializeField] private float rightDistance = 3f;

    [Tooltip("Thời gian đứng yên sau khi đi hết quãng đường trước khi quay đầu (giây)")]
    [SerializeField] private float waitTime = 2f;

    [Header("Attack Settings (Tấn công Player)")]
    [Tooltip("Khoảng cách tối thiểu để Enemy phát hiện và chém Player")]
    [SerializeField] private float attackRange = 1.6f;

    [Tooltip("Sát thương đòn đánh của Enemy lên Player")]
    [SerializeField] private float attackDamage = 2f;

    [Tooltip("Thời gian hồi chiêu giữa các đòn đánh (giây) - Tự chỉnh theo ý bạn")]
    [SerializeField] private float attackCooldown = 2.0f;

    [Tooltip("Thời gian hoạt ảnh đòn đánh của Enemy kéo dài (giây)")]
    [SerializeField] private float attackDuration = 0.5f;

    [Tooltip("Điểm xuất phát đòn đánh (để trống script sẽ tự tính ở trước mặt Enemy)")]
    [SerializeField] private Transform attackPoint;

    [Header("Hurt Settings")]
    [Tooltip("Thời gian quái bị choáng / chạy animation Hurt (giây)")]
    [SerializeField] private float hurtDuration = 0.5f;

    [Header("Target Settings")]
    [Tooltip("Tag của Player")]
    [SerializeField] private string playerTag = "Player";

    [Header("Animation")]
    [Tooltip("Animator của Enemy (để trống script sẽ tự tìm trên GameObject hoặc con)")]
    [SerializeField] private Animator animator;

    // Animator Parameter Hashes khớp chính xác tên Parameters trong Animator của bạn
    private static readonly int EnemyWalkHash = Animator.StringToHash("EnemyWalk");
    private static readonly int EnemyHurtHash = Animator.StringToHash("EnemyHurt");
    private static readonly int EnemyAttackHash = Animator.StringToHash("EnemyAttack");

    [Header("Coin Drop Settings (Rơi xu khi bị tiêu diệt)")]
    [Tooltip("Số lượng coin tối thiểu rơi ra khi quái chết")]
    [SerializeField] private int minCoinsDropped = 2;
    [Tooltip("Số lượng coin tối đa rơi ra khi quái chết")]
    [SerializeField] private int maxCoinsDropped = 4;
    [Tooltip("Giá trị của mỗi đồng xu")]
    [SerializeField] private int valuePerCoin = 1;

    [Header("Audio Settings (Âm thanh quái tạo bằng Code)")]
    [Tooltip("Bật/tắt âm thanh tấn công và bị thương của quái")]
    [SerializeField] private bool enableAudio = true;
    [Tooltip("Âm lượng âm thanh của quái")]
    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 0.85f;
    [Tooltip("Clip âm thanh quái tấn công (để trống script sẽ tự tạo bằng code)")]
    [SerializeField] private AudioClip customAttackClip;
    [Tooltip("Clip âm thanh quái bị thương khi trúng đòn (để trống script sẽ tự tạo bằng code)")]
    [SerializeField] private AudioClip customHurtClip;

    private Rigidbody2D rb;
    private Health health;
    private Transform playerTarget;
    private float startPosX;
    private float leftBound;
    private float rightBound;

    private bool movingRight = true;
    private bool isWaiting = false;
    private float waitTimer = 0f;

    private bool isHurt = false;
    private float hurtTimer = 0f;
    private bool isFacingRight = true;

    // Quản lý trạng thái Attack
    private bool isAttacking = false;
    private float attackTimer = 0f;
    private float attackCooldownTimer = 0f;
    private bool hasDealtDamage = false;
    private bool hasDroppedCoins = false;

    // Quản lý âm thanh
    private AudioSource audioSource;
    private AudioClip attackClip;
    private AudioClip hurtClip;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
        }

        // Khóa trục xoay để quái không bị lăn ngã
        rb.freezeRotation = true;

        health = GetComponent<Health>();
        if (health == null)
        {
            health = gameObject.AddComponent<Health>();
            health.SetMaxHealth(30f);
        }

        // Khởi tạo hệ thống âm thanh cho quái
        InitAudio();
    }

    private void Start()
    {
        // Ghi nhận tọa độ ban đầu và tính toán 2 điểm mút tuần tra
        startPosX = transform.position.x;
        leftBound = startPosX - leftDistance;
        rightBound = startPosX + rightDistance;

        // Nhận diện hướng mặt ban đầu (theo scale.x)
        isFacingRight = transform.localScale.x > 0;

        FindPlayer();
    }

    private void Update()
    {
        if (health != null && health.IsDead) return;

        // Giảm thời gian hồi chiêu đòn đánh
        if (attackCooldownTimer > 0f)
        {
            attackCooldownTimer -= Time.deltaTime;
        }

        // Xử lý trạng thái Hurt
        if (isHurt)
        {
            hurtTimer -= Time.deltaTime;
            if (hurtTimer <= 0f)
            {
                isHurt = false;
            }
            UpdateAnimations();
            return;
        }

        // Xử lý khi đang thực hiện đòn đánh
        if (isAttacking)
        {
            attackTimer -= Time.deltaTime;

            // Gây sát thương cho Player ở giữa đòn đánh
            if (!hasDealtDamage && attackTimer <= attackDuration * 0.6f)
            {
                hasDealtDamage = true;
                CheckAndDamagePlayer();
            }

            if (attackTimer <= 0f)
            {
                isAttacking = false;
                if (animator != null)
                {
                    animator.SetBool(EnemyAttackHash, false);
                }
            }

            UpdateAnimations();
            return;
        }

        // Kiểm tra xem Player có đang trong tầm đánh không
        if (playerTarget == null)
        {
            FindPlayer();
        }

        if (playerTarget != null)
        {
            float distToPlayer = Vector2.Distance(transform.position, playerTarget.position);

            if (distToPlayer <= attackRange)
            {
                // Tự động quay mặt về phía Player
                FaceTarget(playerTarget.position.x);

                // Nếu đã hồi chiêu xong -> Tấn công!
                if (attackCooldownTimer <= 0f)
                {
                    ExecuteAttack();
                    UpdateAnimations();
                    return;
                }
                else
                {
                    // Đang chờ hồi chiêu -> Đứng yên thế thủ cận chiến
                    UpdateAnimations();
                    return;
                }
            }
        }

        // Xử lý trạng thái Đứng yên tuần tra (Wait)
        if (isWaiting)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
            {
                isWaiting = false;
                // Đổi hướng di chuyển
                movingRight = !movingRight;
                UpdateDirection();
            }
        }
        else
        {
            // Kiểm tra đến điểm quay đầu
            CheckPatrolBounds();
        }

        UpdateAnimations();
    }

    private void FixedUpdate()
    {
        // Khi bị thương, đang đánh, hoặc đang đợi thì đứng yên
        bool inCombatRange = false;
        if (playerTarget != null)
        {
            inCombatRange = Vector2.Distance(transform.position, playerTarget.position) <= attackRange;
        }

        if (isHurt || isAttacking || isWaiting || inCombatRange)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
#else
            rb.velocity = new Vector2(0f, rb.velocity.y);
#endif
            return;
        }

        float vx = movingRight ? moveSpeed : -moveSpeed;
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);
#else
        rb.velocity = new Vector2(vx, rb.velocity.y);
#endif
    }

    private void ExecuteAttack()
    {
        isAttacking = true;
        attackTimer = attackDuration;
        attackCooldownTimer = attackCooldown;
        hasDealtDamage = false;

        Debug.Log($"<color=red>[Enemy] {gameObject.name} vung đòn Attack vào Player (Hồi chiêu {attackCooldown}s)!</color>");

        // Phát âm thanh khi quái vung đòn tấn công
        PlayAttackSound();

        if (animator != null)
        {
            animator.SetBool(EnemyAttackHash, true);

            // Chơi trực tiếp animation state
            if (animator.HasState(0, Animator.StringToHash("Animation_Attack1_Enemy")))
            {
                animator.Play("Animation_Attack1_Enemy", 0, 0f);
            }
            else if (animator.HasState(0, Animator.StringToHash("Animation_Attack_Enemy")))
            {
                animator.Play("Animation_Attack_Enemy", 0, 0f);
            }
        }
    }

    private Vector2 GetAttackPosition()
    {
        if (attackPoint != null)
        {
            return attackPoint.position;
        }

        float direction = isFacingRight ? 1f : -1f;
        return (Vector2)transform.position + new Vector2(direction * (attackRange * 0.5f), 0f);
    }

    private void CheckAndDamagePlayer()
    {
        Vector2 checkPos = GetAttackPosition();
        Collider2D[] hits = Physics2D.OverlapCircleAll(checkPos, attackRange * 0.9f);

        foreach (Collider2D hit in hits)
        {
            if (hit == null || hit.gameObject == gameObject || hit.transform.IsChildOf(transform)) continue;

            if (hit.CompareTag(playerTag) || hit.GetComponent<Player_Controller>() != null || hit.GetComponentInParent<Player_Controller>() != null)
            {
                Debug.Log($"<color=magenta>[Enemy] Đòn đánh trúng Player: {hit.gameObject.name} (Gây {attackDamage} damage) -> Player bị Hurt!</color>");

                Player_Controller player = hit.GetComponent<Player_Controller>();
                if (player == null) player = hit.GetComponentInParent<Player_Controller>();
                if (player != null)
                {
                    player.TriggerHurt(attackDamage);
                }
                break;
            }
        }
    }

    private void FindPlayer()
    {
        GameObject p = GameObject.FindGameObjectWithTag(playerTag);
        if (p != null)
        {
            playerTarget = p.transform;
        }
    }

    private void FaceTarget(float targetX)
    {
        if (targetX > transform.position.x && !isFacingRight)
        {
            Flip();
        }
        else if (targetX < transform.position.x && isFacingRight)
        {
            Flip();
        }
    }

    private void CheckPatrolBounds()
    {
        if (movingRight)
        {
            if (transform.position.x >= rightBound)
            {
                transform.position = new Vector3(rightBound, transform.position.y, transform.position.z);
                StartWait();
            }
        }
        else
        {
            if (transform.position.x <= leftBound)
            {
                transform.position = new Vector3(leftBound, transform.position.y, transform.position.z);
                StartWait();
            }
        }
    }

    private void StartWait()
    {
        isWaiting = true;
        waitTimer = waitTime;
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
#else
        rb.velocity = new Vector2(0f, rb.velocity.y);
#endif
    }

    private void UpdateDirection()
    {
        if (movingRight && !isFacingRight)
        {
            Flip();
        }
        else if (!movingRight && isFacingRight)
        {
            Flip();
        }
    }

    private void Flip()
    {
        isFacingRight = !isFacingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1f;
        transform.localScale = scale;
    }

    /// <summary>
    /// Kích hoạt trạng thái chết khi hết máu
    /// </summary>
    public void Die()
    {
        isHurt = false;
        isAttacking = false;
        isWaiting = true;

        // Rơi Coin khi quái bị tiêu diệt
        if (!hasDroppedCoins)
        {
            hasDroppedCoins = true;

            // Báo cho GameMenu ghi nhận tiêu diệt 1 quái
            if (GameMenu.Instance != null)
            {
                GameMenu.Instance.RegisterEnemyKill();
            }

            int count = Random.Range(minCoinsDropped, maxCoinsDropped + 1);
            CoinManager.DropCoins(transform.position, count, valuePerCoin);
        }

        if (animator != null)
        {
            animator.SetBool(EnemyAttackHash, false);
            animator.SetBool(EnemyWalkHash, false);
            animator.SetBool(EnemyHurtHash, false);

            foreach (var p in animator.parameters)
            {
                if (p.name == "EnemyDie")
                {
                    animator.SetBool("EnemyDie", true);
                    break;
                }
            }

            if (animator.HasState(0, Animator.StringToHash("Animation_Die_Enemy")))
            {
                animator.Play("Animation_Die_Enemy", 0, 0f);
            }
        }

        if (rb != null)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = Vector2.zero;
#else
            rb.velocity = Vector2.zero;
#endif
            rb.simulated = false;
        }

        this.enabled = false;
    }

    /// <summary>
    /// Kích hoạt trạng thái bị thương khi Player tác động
    /// </summary>
    public void TriggerHurt()
    {
        if (health != null && health.IsDead) return;

        isHurt = true;
        isAttacking = false;
        attackTimer = 0f;
        hurtTimer = hurtDuration;

        // Phát âm thanh quái bị đánh trúng (Hurt)
        PlayHurtSound();

        if (animator != null)
        {
            animator.SetBool(EnemyAttackHash, false);
        }

#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = Vector2.zero;
#else
        rb.velocity = Vector2.zero;
#endif

        Debug.Log($"<color=yellow>[Enemy] {gameObject.name} bị trúng đòn -> Kích hoạt EnemyHurt!</color>");
    }

    private void UpdateAnimations()
    {
        if (animator == null) return;

        // Cập nhật trạng thái EnemyHurt
        animator.SetBool(EnemyHurtHash, isHurt);

        if (isHurt)
        {
            animator.SetBool(EnemyWalkHash, false);
            animator.SetBool(EnemyAttackHash, false);
            return;
        }

        // Cập nhật trạng thái EnemyAttack
        animator.SetBool(EnemyAttackHash, isAttacking);

        if (isAttacking)
        {
            animator.SetBool(EnemyWalkHash, false);
            return;
        }

        // Kiểm tra xem có đang trong tầm tiếp cận Player không
        bool inCombat = false;
        if (playerTarget != null)
        {
            inCombat = Vector2.Distance(transform.position, playerTarget.position) <= attackRange;
        }

        // Đang đi tuần tra thì EnemyWalk = true, đứng đợi hoặc áp sát thì EnemyWalk = false (về Idle)
        animator.SetBool(EnemyWalkHash, !isWaiting && !inCombat);
    }

    // Vẽ vạch tuần tra và phạm vi đòn đánh của Enemy trong cửa sổ Scene
    private void OnDrawGizmosSelected()
    {
        // 1. Vạch tuần tra
        Vector3 currentStart = Application.isPlaying ? new Vector3(startPosX, transform.position.y, transform.position.z) : transform.position;
        Vector3 leftPoint = new Vector3(currentStart.x - leftDistance, currentStart.y, currentStart.z);
        Vector3 rightPoint = new Vector3(currentStart.x + rightDistance, currentStart.y, currentStart.z);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(leftPoint, rightPoint);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(leftPoint, 0.25f);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(rightPoint, 0.25f);

        // 2. Phạm vi đòn đánh Attack của Enemy (Vòng tròn đỏ)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(GetAttackPosition(), attackRange * 0.9f);
    }

    // =========================================================
    // HỆ THỐNG ÂM THANH QUÁI (ATTACK & HURT) TỰ TẠO BẰNG CODE
    // =========================================================
    private void InitAudio()
    {
        if (!enableAudio) return;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // Âm thanh 2D rõ nét

        attackClip = customAttackClip != null ? customAttackClip : CreateEnemyAttackClip();
        hurtClip = customHurtClip != null ? customHurtClip : CreateEnemyHurtClip();
    }

    private void PlayAttackSound()
    {
        if (!enableAudio || audioSource == null || attackClip == null) return;
        audioSource.pitch = Random.Range(0.92f, 1.08f);
        audioSource.PlayOneShot(attackClip, sfxVolume);
    }

    private void PlayHurtSound()
    {
        if (!enableAudio || audioSource == null || hurtClip == null) return;
        audioSource.pitch = Random.Range(0.95f, 1.05f);
        audioSource.PlayOneShot(hurtClip, sfxVolume * 1.1f);
    }

    /// <summary>
    /// Tạo âm thanh quái vung đòn tấn công (chém vuốt quái vật) bằng code
    /// </summary>
    private AudioClip CreateEnemyAttackClip()
    {
        int sampleRate = 44100;
        float duration = 0.22f;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float progress = t / duration;

            // Âm quái vung vuốt: quét từ 460Hz xuống 90Hz có gằn giọng trầm
            float freq = Mathf.Lerp(460f, 90f, progress * progress);
            float envelope = Mathf.Clamp01(t / 0.015f) * Mathf.Exp(-progress * 4.2f);

            float wave = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.45f +
                         Mathf.Sin(2f * Mathf.PI * 95f * t) * 0.3f +
                         (Random.value * 2f - 1f) * 0.35f;

            samples[i] = wave * envelope;
        }

        AudioClip clip = AudioClip.Create("Enemy_Attack", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    /// <summary>
    /// Tạo âm thanh quái bị đánh trúng (Hurt) bằng code
    /// </summary>
    private AudioClip CreateEnemyHurtClip()
    {
        int sampleRate = 44100;
        float duration = 0.22f;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float progress = t / duration;

            // Âm rít đau của quái: va chạm mạnh + tiếng rú ngắn từ 380Hz xuống 160Hz
            float gruntFreq = Mathf.Lerp(380f, 160f, progress);
            float impactClick = Mathf.Sin(2f * Mathf.PI * 180f * t) * Mathf.Exp(-t * 50f) * 0.5f;
            float envelope = Mathf.Clamp01(t / 0.005f) * Mathf.Exp(-progress * 4.5f);

            float wave = Mathf.Sin(2f * Mathf.PI * gruntFreq * t) * 0.5f +
                         (Random.value * 2f - 1f) * 0.3f * Mathf.Exp(-progress * 6f);

            samples[i] = (wave * envelope) + impactClick;
        }

        AudioClip clip = AudioClip.Create("Enemy_Hurt", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
