using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody2D))]
public class Player_Controller : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Tốc độ di chuyển trái/phải")]
    [SerializeField] private float moveSpeed = 8f;

    [Header("Jump Basic")]
    [Tooltip("Lực nhảy")]
    [SerializeField] private float jumpForce = 14f;
    [Tooltip("Thời gian lưu lệnh nhảy trước khi chạm đất (giúp phím nhảy cực nhạy)")]
    [SerializeField] private float jumpBufferTime = 0.2f;
    [Tooltip("Thời gian cho phép nhảy sau khi vừa rời khỏi mặt đất (Coyote Time)")]
    [SerializeField] private float coyoteTime = 0.15f;

    [Header("Better Jump (Cơ chế nhảy chuyên nghiệp)")]
    [Tooltip("Hệ số rơi nhanh khi nhân vật rơi xuống (giúp rơi đầm và dứt khoát hơn lúc bay lên)")]
    [SerializeField] private float fallMultiplier = 2.5f;
    [Tooltip("Hệ số kéo rơi khi nhả phím nhảy sớm (nhấn nhắp nhảy thấp, giữ lâu nhảy cao)")]
    [SerializeField] private float lowJumpMultiplier = 2.5f;

    [Header("Attack Settings (Tấn công cận chiến)")]
    [Tooltip("Thời gian mỗi đòn đánh kéo dài (giây)")]
    [SerializeField] private float attackDuration = 0.4f;
    [Tooltip("Sát thương đòn đánh 1 (Phím 1)")]
    [SerializeField] private float attack1Damage = 5f;
    [Tooltip("Sát thương đòn đánh 2 (Phím 2)")]
    [SerializeField] private float attack2Damage = 5f;
    [Tooltip("Sát thương đòn đánh 3 (Phím 3)")]
    [SerializeField] private float attack3Damage = 5f;
    [Tooltip("Điểm xuất phát đòn đánh (để trống script sẽ tự tính ở trước mặt nhân vật)")]
    [SerializeField] private Transform attackPoint;
    [Tooltip("Tầm quét của đòn chém cận chiến")]
    [SerializeField] private float attackRange = 1.3f;
    [Tooltip("Layer của Enemy (nếu để Nothing thì sẽ tự quét theo Tag Enemy)")]
    [SerializeField] private LayerMask enemyLayers;

    [Header("Shield Settings (Đỡ đòn bằng Khiên - Phím ~)")]
    [Tooltip("Thời gian giương khiên tối đa (giây)")]
    [SerializeField] private float shieldDuration = 0.8f;
    [Tooltip("Thời gian hồi chiêu khiên (giây)")]
    [SerializeField] private float shieldCooldown = 1.0f;

    [Header("Audio Settings (Âm thanh Skill 1, 2, 3 và Khiên tạo bằng Code)")]
    [Tooltip("Bật/tắt âm thanh kỹ năng và khiên")]
    [SerializeField] private bool enableAudio = true;
    [Tooltip("Âm lượng âm thanh kỹ năng")]
    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 0.9f;
    [Tooltip("Clip âm thanh đòn đánh 1 (để trống script sẽ tự tạo bằng code)")]
    [SerializeField] private AudioClip customAttack1Clip;
    [Tooltip("Clip âm thanh đòn đánh 2 (để trống script sẽ tự tạo bằng code)")]
    [SerializeField] private AudioClip customAttack2Clip;
    [Tooltip("Clip âm thanh đòn đánh 3 (để trống script sẽ tự tạo bằng code)")]
    [SerializeField] private AudioClip customAttack3Clip;
    [Tooltip("Clip âm thanh giương khiên (để trống script sẽ tự tạo bằng code)")]
    [SerializeField] private AudioClip customShieldUpClip;
    [Tooltip("Clip âm thanh đỡ đòn thành công (để trống script sẽ tự tạo bằng code)")]
    [SerializeField] private AudioClip customShieldBlockClip;
    [Tooltip("Clip âm thanh Player bị thương khi trúng đòn (để trống script sẽ tự tạo bằng code)")]
    [SerializeField] private AudioClip customHurtClip;

    [Header("Special Head Stomp Attack (Dẫm đầu kết liễu)")]
    [Tooltip("Lực nảy bật lên của Player sau khi dẫm trúng đầu Enemy")]
    [SerializeField] private float stompBounceForce = 12f;

    [Header("Ground Check")]
    [Tooltip("Điểm kiểm tra dưới chân nhân vật (có thể để trống, script sẽ tự lấy đáy Collider)")]
    [SerializeField] private Transform groundCheck;
    [Tooltip("Kích thước vùng kiểm tra chân nhân vật")]
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.8f, 0.2f);
    [Tooltip("Layer của mặt đất (nếu để Nothing thì script sẽ tự nhận diện mọi vật cản khác Player)")]
    [SerializeField] private LayerMask groundLayer;

    [Header("Hurt Settings (Đã xóa đẩy lùi - Solo cận chiến)")]
    [Tooltip("Tag của Enemy để tự động nhận diện va chạm")]
    [SerializeField] private string enemyTag = "Enemy";
    [Tooltip("Có bị thương khi chạm người Enemy không? (để false để chỉ bị thương khi Enemy tung đòn đánh)")]
    [SerializeField] private bool hurtOnBodyTouch = false;
    [Tooltip("Thời gian nhân vật bị khựng khi trúng đòn (giây)")]
    [SerializeField] private float hurtDuration = 0.35f;
    [Tooltip("Thời gian bất tử chớp nhoáng tránh bị trừ máu liên tục (giây)")]
    [SerializeField] private float invincibilityDuration = 1f;

    [Header("Animation")]
    [Tooltip("Animator của nhân vật (nếu để trống script sẽ tự tìm trên GameObject hoặc Object con)")]
    [SerializeField] private Animator animator;

    // Animator Parameter Hashes (khớp chính xác các parameters trong Animator của bạn)
    private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
    private static readonly int IsJumpingHash = Animator.StringToHash("IsJumping");
    private static readonly int IsHurtHash = Animator.StringToHash("IsHurt");
    private static readonly int IsAttackHash = Animator.StringToHash("IsAttack");
    private static readonly int AttackIndexHash = Animator.StringToHash("AttackIndex");
    private static readonly int Attack1Hash = Animator.StringToHash("Attack1");
    private static readonly int Attack2Hash = Animator.StringToHash("Attack2");
    private static readonly int Attack3Hash = Animator.StringToHash("Attack3");
    private static readonly int IsDieHash = Animator.StringToHash("IsDie");
    private static readonly int IsShieldHash = Animator.StringToHash("IsShield");

    private Rigidbody2D rb;
    private Collider2D col;
    private SpriteRenderer spriteRenderer;
    private Health health;

    private float horizontalInput;
    private bool isJumpHeld;
    private float jumpBufferCounter;
    private float coyoteTimeCounter;
    private bool isGrounded;
    private bool isFacingRight = true;

    // Trạng thái Hurt
    private bool isHurt;
    private bool isInvincible;
    private float hurtTimer;
    private float invincibilityTimer;

    // Trạng thái Attack
    private bool isAttacking;
    private float attackTimer;
    private int currentAttackIndex;

    // Trạng thái Shield (Khiên đỡ đòn)
    private bool isShielding;
    private float shieldTimer;
    private float shieldCooldownTimer;
    private bool hasBlockedAttack;

    // Cờ kiểm tra parameters có tồn tại trong Animator không
    private bool hasIsAttack;
    private bool hasAttackIndex;
    private bool hasAttack1;
    private bool hasAttack2;
    private bool hasAttack3;
    private bool hasIsShield;

    // Quản lý âm thanh
    private AudioSource audioSource;
    private AudioClip attack1Clip;
    private AudioClip attack2Clip;
    private AudioClip attack3Clip;
    private AudioClip shieldUpClip;
    private AudioClip shieldBlockClip;
    private AudioClip playerHurtClip;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        if (col == null)
        {
            col = GetComponentInChildren<Collider2D>();
        }

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        // Tự động tìm Animator nếu chưa gán trong Inspector
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
        }

        health = GetComponent<Health>();
        if (health == null)
        {
            health = gameObject.AddComponent<Health>();
            health.SetMaxHealth(100f);
        }
        health.SetDestroyOnDeath(false); // Player không tự biến mất khi chết (để không làm mất Camera)

        CheckAnimatorParameters();

        // Khóa xoay để nhân vật không bị đổ ngã khi va chạm
        rb.freezeRotation = true;

        // Khởi tạo hệ thống âm thanh kỹ năng và khiên
        InitAudio();

        // Khởi tạo hiển thị thanh Coin 0 ngay khi Player xuất hiện
        var _ = CoinManager.Instance;

        // Khởi động nhạc nền game (BGM)
        var __ = BGM_Manager.Instance;

        // Khởi động cơ chế Camera cuộn 1 chiều khóa mép trái
        var ___ = OneWayCamera.Instance;

        // Khởi động Start Menu
        var ____ = GameMenu.Instance;
    }

    private void CheckAnimatorParameters()
    {
        if (animator == null) return;

        foreach (AnimatorControllerParameter param in animator.parameters)
        {
            if (param.nameHash == IsAttackHash) hasIsAttack = true;
            if (param.nameHash == AttackIndexHash) hasAttackIndex = true;
            if (param.nameHash == Attack1Hash) hasAttack1 = true;
            if (param.nameHash == Attack2Hash) hasAttack2 = true;
            if (param.nameHash == Attack3Hash) hasAttack3 = true;
            if (param.nameHash == IsShieldHash) hasIsShield = true;
        }
    }

    private void Update()
    {
        if (health != null && health.IsDead) return;
        // Xử lý bộ đếm thời gian Hurt và Bất tử
        HandleHurtTimers();

        // Xử lý bộ đếm thời gian Attack
        HandleAttackTimer();

        // Xử lý bộ đếm thời gian Shield
        HandleShieldTimer();

        // Chỉ nhận điều khiển khi KHÔNG bị trúng đòn
        if (!isHurt)
        {
            GetInput();

            // Đếm ngược bộ đệm nhảy (Jump Buffer)
            if (jumpBufferCounter > 0f)
            {
                jumpBufferCounter -= Time.deltaTime;
            }

            // Đếm ngược thời gian vừa rời đất (Coyote Time)
            if (isGrounded)
            {
                coyoteTimeCounter = coyoteTime;
            }
            else
            {
                coyoteTimeCounter -= Time.deltaTime;
            }

            // Lật mặt nhân vật khi đổi hướng di chuyển (chỉ khi không đang ra đòn và không đang đỡ khiên)
            if (!isAttacking && !isShielding)
            {
                if (horizontalInput > 0.01f && !isFacingRight)
                {
                    Flip();
                }
                else if (horizontalInput < -0.01f && isFacingRight)
                {
                    Flip();
                }
            }
        }

        // Cập nhật trạng thái Animation
        UpdateAnimations();
    }

    private void FixedUpdate()
    {
        isGrounded = CheckGrounded();

        // Khi đang bị dính đòn (Hurt): đứng yên tại chỗ chịu đòn (không đẩy lùi - solo cận chiến)
        if (isHurt)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
#else
            rb.velocity = new Vector2(0f, rb.velocity.y);
#endif
            return;
        }

        // Khi đang tấn công hoặc đang giương khiên trên mặt đất: đứng yên tại chỗ, không bị trượt
        if ((isAttacking || isShielding) && isGrounded)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
#else
            rb.velocity = new Vector2(0f, rb.velocity.y);
#endif
            return;
        }

        // Di chuyển ngang bình thường
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = new Vector2(horizontalInput * moveSpeed, rb.linearVelocity.y);
#else
        rb.velocity = new Vector2(horizontalInput * moveSpeed, rb.velocity.y);
#endif

        // Nhảy khi có lệnh nhảy và đang trên đất (hoặc trong khoảng Coyote Time)
        if (jumpBufferCounter > 0f && coyoteTimeCounter > 0f && !isAttacking && !isShielding)
        {
            ExecuteJump();
        }

        // Áp dụng cơ chế Better Jump (rơi nhanh + kiểm soát độ cao nhảy theo thời gian giữ phím)
        ApplyBetterJump();
    }

    private void PerformAttack(int attackNumber)
    {
        if (isHurt) return;

        isAttacking = true;
        currentAttackIndex = attackNumber;
        attackTimer = attackDuration;

        Debug.Log($"<color=lime>[Player] Kích hoạt Attack {attackNumber} (Phím {attackNumber})</color>");

        // Phát âm thanh chém đòn tương ứng
        PlayAttackSound(attackNumber);

        // 1. Kích hoạt Animation
        if (animator != null)
        {
            if (hasIsAttack)
            {
                animator.SetBool(IsAttackHash, true);
            }

            if (hasAttackIndex)
            {
                animator.SetInteger(AttackIndexHash, attackNumber);
            }

            if (attackNumber == 1 && hasAttack1) animator.SetTrigger(Attack1Hash);
            else if (attackNumber == 2 && hasAttack2) animator.SetTrigger(Attack2Hash);
            else if (attackNumber == 3 && hasAttack3) animator.SetTrigger(Attack3Hash);

            string stateName = $"Animation_Attack{attackNumber}_Player";
            if (animator.HasState(0, Animator.StringToHash(stateName)))
            {
                animator.Play(stateName, 0, 0f);
            }
        }

        // 2. Quét trúng Enemy để gây Hurt cho quái
        DamageEnemiesInRange();
    }

    private Vector2 GetAttackPosition()
    {
        if (attackPoint != null)
        {
            return attackPoint.position;
        }

        // Tự động tính điểm quét ở trước mặt nhân vật theo hướng quay mặt
        Vector2 center = col != null ? (Vector2)col.bounds.center : (Vector2)transform.position;
        float direction = isFacingRight ? 1f : -1f;
        float offset = col != null ? (col.bounds.extents.x + attackRange * 0.4f) : 0.8f;

        return center + new Vector2(direction * offset, 0f);
    }

    private void DamageEnemiesInRange()
    {
        Vector2 attackPos = GetAttackPosition();
        LayerMask mask = enemyLayers.value == 0 ? ~0 : enemyLayers;

        // Quét tất cả các Collider trong tầm chém
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(attackPos, attackRange, mask);

        float damage = currentAttackIndex == 1 ? attack1Damage : currentAttackIndex == 2 ? attack2Damage : attack3Damage;

        foreach (Collider2D hit in hitColliders)
        {
            // Bỏ qua bản thân player
            if (hit == null || hit.gameObject == gameObject || hit.transform.IsChildOf(transform)) continue;

            // Kiểm tra xem có phải Enemy không (thông qua Tag hoặc Component enemy_controller)
            if (hit.CompareTag(enemyTag) || hit.GetComponent<enemy_controller>() != null || hit.GetComponentInParent<enemy_controller>() != null)
            {
                Debug.Log($"<color=green>[Player] Đòn đánh trúng Enemy: {hit.gameObject.name} (Gây {damage} damage) -> Kích hoạt Hurt cho quái!</color>");

                // Trừ máu Enemy
                Health enemyHealth = hit.GetComponent<Health>();
                if (enemyHealth == null) enemyHealth = hit.GetComponentInParent<Health>();
                if (enemyHealth != null)
                {
                    enemyHealth.TakeDamage(damage);
                }

                enemy_controller enemy = hit.GetComponent<enemy_controller>();
                if (enemy == null)
                {
                    enemy = hit.GetComponentInParent<enemy_controller>();
                }

                if (enemy != null)
                {
                    enemy.TriggerHurt();
                }
            }
        }
    }

    private void HandleAttackTimer()
    {
        if (isAttacking)
        {
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0f)
            {
                isAttacking = false;
                currentAttackIndex = 0;

                if (animator != null)
                {
                    if (hasIsAttack)
                    {
                        animator.SetBool(IsAttackHash, false);
                    }
                    if (hasAttackIndex)
                    {
                        animator.SetInteger(AttackIndexHash, 0);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Kích hoạt khiên phòng thủ (Phím ~ bên cạnh số 1)
    /// </summary>
    public void ActivateShield()
    {
        if (isHurt || isAttacking || isShielding) return;
        if (health != null && health.IsDead) return;
        if (shieldCooldownTimer > 0f) return;

        isShielding = true;
        shieldTimer = shieldDuration;
        hasBlockedAttack = false;

        Debug.Log("<color=cyan><b>[Player] Giương khiên phòng thủ (IsShield = true)! Sẵn sàng đỡ 1 đòn tấn công.</b></color>");

        // Phát âm thanh kích hoạt giương khiên
        PlayShieldUpSound();

        if (animator != null)
        {
            if (hasIsShield)
            {
                animator.SetBool(IsShieldHash, true);
            }

            if (animator.HasState(0, Animator.StringToHash("Animation_Shield_Player")))
            {
                animator.Play("Animation_Shield_Player", 0, 0f);
            }
        }
    }

    /// <summary>
    /// Kết thúc trạng thái giương khiên và bắt đầu đếm hồi chiêu
    /// </summary>
    public void EndShield()
    {
        if (!isShielding) return;

        isShielding = false;
        shieldTimer = 0f;
        shieldCooldownTimer = shieldCooldown;

        if (animator != null && hasIsShield)
        {
            animator.SetBool(IsShieldHash, false);
        }

        Debug.Log("<color=cyan>[Player] Hạ khiên (IsShield = false).</color>");
    }

    private void HandleShieldTimer()
    {
        // Đếm ngược thời gian hồi chiêu khiên
        if (shieldCooldownTimer > 0f)
        {
            shieldCooldownTimer -= Time.deltaTime;
        }

        // Đếm ngược thời gian giương khiên tối đa
        if (isShielding)
        {
            shieldTimer -= Time.deltaTime;
            if (shieldTimer <= 0f)
            {
                EndShield();
            }
        }
    }

    // Tự động bắt va chạm Trigger ngay trên Player_Controller (dự phòng cả khi chưa gắn Player_Conllision)
    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 0. Kiểm tra vùng chiến thắng / Kết thúc game (telewin, endgame, win, goal)
        if (IsEndGameZone(collision.gameObject))
        {
            if (GameMenu.Instance != null)
            {
                GameMenu.Instance.TriggerVictory();
            }
            return;
        }

        // 1. Kiểm tra vùng bẫy kết liễu / rơi xuống vực (Die, Die2)
        if (IsDeathZone(collision.gameObject))
        {
            InstantKill();
            return;
        }

        // 2. Đòn đánh đặc biệt: Nhảy dẫm lên đầu Enemy để tiêu diệt tức thì!
        if (CheckHeadStomp(collision))
        {
            return;
        }

        // 3. Va chạm người quái thông thường
        if (hurtOnBodyTouch && collision.CompareTag(enemyTag))
        {
            Debug.Log($"<color=orange>[Player_Controller] Chạm người Enemy: {collision.gameObject.name}</color>");
            TriggerHurt();
        }
    }

    // Tự động bắt va chạm vật lý thông thường
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (IsEndGameZone(collision.gameObject))
        {
            if (GameMenu.Instance != null)
            {
                GameMenu.Instance.TriggerVictory();
            }
            return;
        }

        if (IsDeathZone(collision.gameObject))
        {
            InstantKill();
            return;
        }

        // Đòn đánh đặc biệt: Dẫm lên đầu Enemy
        if (CheckHeadStomp(collision.collider))
        {
            return;
        }

        if (hurtOnBodyTouch && collision.gameObject.CompareTag(enemyTag))
        {
            Debug.Log($"<color=orange>[Player_Controller] Va chạm người Enemy: {collision.gameObject.name}</color>");
            TriggerHurt();
        }
    }

    /// <summary>
    /// Kiểm tra và thực hiện đòn đặc biệt dẫm lên đầu Enemy
    /// </summary>
    public bool CheckHeadStomp(Collider2D enemyCol)
    {
        if (enemyCol == null) return false;

        bool isEnemy = enemyCol.CompareTag(enemyTag) || enemyCol.GetComponent<enemy_controller>() != null || enemyCol.GetComponentInParent<enemy_controller>() != null;
        if (!isEnemy) return false;

        Health enemyHp = enemyCol.GetComponent<Health>();
        if (enemyHp == null) enemyHp = enemyCol.GetComponentInParent<Health>();
        if (enemyHp != null && enemyHp.IsDead) return false;

        float playerFeetY = col != null ? col.bounds.min.y : transform.position.y;
        float enemyCenterY = enemyCol.bounds.center.y;

#if UNITY_6000_0_OR_NEWER
        float yVel = rb.linearVelocity.y;
#else
        float yVel = rb.velocity.y;
#endif

        // Điều kiện: Chân Player cao hơn tâm Enemy và Player đang rơi xuống/nhảy xuống đè lên đầu
        if (playerFeetY > enemyCenterY && yVel <= 1.5f)
        {
            ExecuteHeadStomp(enemyCol.gameObject);
            return true;
        }

        return false;
    }

    private void ExecuteHeadStomp(GameObject enemyObj)
    {
        Debug.Log("<color=yellow><b>[Player] ĐÒN ĐÁNH ĐẶC BIỆT! Dẫm trúng đầu Enemy -> Tiêu diệt quái lập tức (HP = 0)!</b></color>");

        // 1. Tiêu diệt quái, đưa máu về 0 ngay lập tức
        Health enemyHp = enemyObj.GetComponent<Health>();
        if (enemyHp == null) enemyHp = enemyObj.GetComponentInParent<Health>();
        if (enemyHp != null)
        {
            enemyHp.TakeDamage(enemyHp.CurrentHealth);
        }

        enemy_controller enemy = enemyObj.GetComponent<enemy_controller>();
        if (enemy == null) enemy = enemyObj.GetComponentInParent<enemy_controller>();
        if (enemy != null)
        {
            enemy.Die();
        }

        // 2. Nảy bật Player lên cao (hiệu ứng dẫm đầu nảy lên cực sướng tay)
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, stompBounceForce);
#else
        rb.velocity = new Vector2(rb.linearVelocity.x, stompBounceForce);
#endif
        jumpBufferCounter = 0f;
    }

    private bool IsDeathZone(GameObject obj)
    {
        if (obj == null) return false;

        // Tự động nhận diện tên GameObject chứa chữ 'die', 'dead', 'kill' (khớp với Die, Die2 trong ảnh của bạn)
        string objName = obj.name.ToLower();
        if (objName.Contains("die") || objName.Contains("dead") || objName.Contains("death") || objName.Contains("kill"))
        {
            return true;
        }

        // Nhận diện theo component DeathZone
        if (obj.GetComponent<DeathZone>() != null)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Nhận diện vùng kết thúc game / Victory (như telewin trong Scene)
    /// </summary>
    private bool IsEndGameZone(GameObject obj)
    {
        if (obj == null) return false;
        string objName = obj.name.ToLower();
        if (objName.Contains("telewin") || objName.Contains("endgame") || objName.Contains("win") || objName.Contains("goal") || objName.Contains("victory") || objName.Contains("finish"))
        {
            return true;
        }
        if (obj.CompareTag("Finish")) return true;
        if (obj.GetComponent<EndGameTrigger>() != null) return true;
        return false;
    }

    /// <summary>
    /// Kích hoạt trạng thái chết khi máu về 0
    /// </summary>
    public void Die()
    {
        isHurt = false;
        isAttacking = false;
        isShielding = false;
        horizontalInput = 0f;

#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = Vector2.zero;
#else
        rb.velocity = Vector2.zero;
#endif

        if (animator != null)
        {
            // Bật cờ IsDie trong Animator
            animator.SetBool(IsDieHash, true);

            // Tắt các cờ khác để không bị xung đột hoạt cảnh
            animator.SetBool(IsRunningHash, false);
            animator.SetBool(IsJumpingHash, false);
            animator.SetBool(IsHurtHash, false);
            animator.SetBool(IsAttackHash, false);
            if (hasIsShield) animator.SetBool(IsShieldHash, false);

            // Chơi trực tiếp animation state Animation_Die_Player
            if (animator.HasState(0, Animator.StringToHash("Animation_Die_Player")))
            {
                animator.Play("Animation_Die_Player", 0, 0f);
            }
        }

        // Tắt nhận lệnh điều khiển
        this.enabled = false;

        // Tắt toàn bộ nhạc nền và chuẩn bị bảng Game Over
        if (BGM_Manager.Instance != null)
        {
            BGM_Manager.Instance.StopBGM();
        }
        if (GameMenu.Instance != null)
        {
            GameMenu.Instance.TriggerGameOver(1.2f);
        }
    }

    /// <summary>
    /// Kết liễu nhân vật lập tức khi rơi vào bẫy / vực tử thần (Die)
    /// </summary>
    public void InstantKill()
    {
        if (health != null && health.IsDead) return;

        Debug.Log("<color=red><b>[Player_Controller] Chạm vào vùng tử thần (Die)! Máu đưa về 0!</b></color>");

        if (health != null)
        {
            health.TakeDamage(health.CurrentHealth); // Gọi hàm này sẽ kích hoạt Die() trong Health
        }
        else
        {
            Die();
        }
    }

    /// <summary>
    /// Kích hoạt trạng thái bị thương (Hurt) khi trúng đòn của Enemy (KHÔNG ĐẨY LÙI - Solo cận chiến)
    /// </summary>
    public void TriggerHurt(float damage = 0f)
    {
        // Đang trong thời gian bất tử thì bỏ qua
        if (isInvincible) return;
        if (health != null && health.IsDead) return;

        // KIỂM TRA ĐỠ ĐÒN BẰNG KHIÊN: nếu đang giương khiên, chặn hoàn toàn 1 đòn tấn công!
        if (isShielding)
        {
            Debug.Log("<color=cyan><b>[Player] ĐÃ ĐỠ ĐÒN THÀNH CÔNG BẰNG KHIÊN! Chặn hoàn toàn đòn đánh (0 damage), không mất máu!</b></color>");
            hasBlockedAttack = true;

            // Phát âm thanh kim loại KENG đỡ đòn thành công
            PlayShieldBlockSound();

            EndShield();
            return;
        }

        Debug.Log($"<color=red>[Player_Controller] KÍCH HOẠT HURT! IsHurt = true (Nhận {damage} damage)</color>");

        // Phát âm thanh khi Player bị đánh trúng (Hurt)
        PlayHurtSound();

        if (health != null && damage > 0f)
        {
            health.TakeDamage(damage);
        }

        isHurt = true;
        isInvincible = true;
        hurtTimer = hurtDuration;
        invincibilityTimer = invincibilityDuration;

        // Hủy trạng thái tấn công nếu đang đánh
        isAttacking = false;
        attackTimer = 0f;
        if (animator != null && hasIsAttack)
        {
            animator.SetBool(IsAttackHash, false);
        }

        // Reset các cờ di chuyển và nhảy
        horizontalInput = 0f;
        jumpBufferCounter = 0f;

        // Giữ nguyên vị trí cận chiến, không thêm bất kỳ lực đẩy lùi nào
    }

    private void HandleHurtTimers()
    {
        // Đếm ngược thời gian bị khựng (Hurt)
        if (isHurt)
        {
            hurtTimer -= Time.deltaTime;
            if (hurtTimer <= 0f)
            {
                isHurt = false;
                Debug.Log("<color=cyan>[Player_Controller] Hết thời gian Hurt -> IsHurt = false</color>");
            }
        }

        // Đếm ngược thời gian bất tử + hiệu ứng nhấp nháy
        if (isInvincible)
        {
            invincibilityTimer -= Time.deltaTime;

            if (spriteRenderer != null)
            {
                float alpha = Mathf.PingPong(Time.time * 12f, 1f) > 0.4f ? 0.3f : 1f;
                Color c = spriteRenderer.color;
                c.a = alpha;
                spriteRenderer.color = c;
            }

            if (invincibilityTimer <= 0f)
            {
                isInvincible = false;
                if (spriteRenderer != null)
                {
                    Color c = spriteRenderer.color;
                    c.a = 1f;
                    spriteRenderer.color = c;
                }
            }
        }
    }

    private void ExecuteJump()
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
#else
        rb.velocity = new Vector2(rb.velocity.x, jumpForce);
#endif
        jumpBufferCounter = 0f;
        coyoteTimeCounter = 0f;
    }

    private void ApplyBetterJump()
    {
#if UNITY_6000_0_OR_NEWER
        float yVel = rb.linearVelocity.y;
#else
        float yVel = rb.velocity.y;
#endif

        // Khi nhân vật đang rơi xuống -> tăng trọng lực để rơi nhanh và đầm hơn
        if (yVel < 0)
        {
            Vector2 extraGravity = Vector2.up * Physics2D.gravity.y * rb.gravityScale * (fallMultiplier - 1) * Time.fixedDeltaTime;
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity += extraGravity;
#else
            rb.velocity += extraGravity;
#endif
        }
        // Khi nhân vật đang bay lên nhưng người chơi đã nhả phím nhảy sớm -> rơi sớm để nhảy thấp
        else if (yVel > 0 && !isJumpHeld)
        {
            Vector2 extraGravity = Vector2.up * Physics2D.gravity.y * rb.gravityScale * (lowJumpMultiplier - 1) * Time.fixedDeltaTime;
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity += extraGravity;
#else
            rb.velocity += extraGravity;
#endif
        }
    }

    private void UpdateAnimations()
    {
        if (animator == null) return;

        // Nếu đã chết thì giữ nguyên cờ IsDie và không ghi đè bất kỳ hoạt cảnh nào khác
        if (health != null && health.IsDead)
        {
            animator.SetBool(IsDieHash, true);
            animator.SetBool(IsRunningHash, false);
            animator.SetBool(IsJumpingHash, false);
            animator.SetBool(IsHurtHash, false);
            animator.SetBool(IsAttackHash, false);
            if (hasIsShield) animator.SetBool(IsShieldHash, false);
            return;
        }

        // Cập nhật trạng thái IsShield (Khiên phòng thủ)
        if (hasIsShield)
        {
            animator.SetBool(IsShieldHash, isShielding);
        }

        // Khi đang giương khiên đỡ đòn, ưu tiên hoạt cảnh khiên, không cho chạy/nhảy
        if (isShielding)
        {
            animator.SetBool(IsRunningHash, false);
            animator.SetBool(IsJumpingHash, false);
            return;
        }

        // Cập nhật trạng thái IsHurt
        animator.SetBool(IsHurtHash, isHurt);

        if (isHurt)
        {
            // Khi bị đau: ưu tiên animation hurt, tắt running và jumping
            animator.SetBool(IsRunningHash, false);
            animator.SetBool(IsJumpingHash, false);
            return;
        }

        // Khi đang tấn công thì không ghi đè animation chạy/nhảy
        if (isAttacking)
        {
            animator.SetBool(IsRunningHash, false);
            return;
        }

        bool isMoving = Mathf.Abs(horizontalInput) > 0.01f;

        // 1. Nếu không chạm đất -> IsJumping = true (chuyển sang animation nhảy)
        animator.SetBool(IsJumpingHash, !isGrounded);

        // 2. Nếu đang chạm đất và có bấm di chuyển -> IsRunning = true (chuyển sang animation chạy)
        animator.SetBool(IsRunningHash, isGrounded && isMoving);
    }

    private void GetInput()
    {
        horizontalInput = 0f;
        isJumpHeld = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            // Bấm phím ~ (dấu ngã / backquote cạnh phím số 1) để giương khiên đỡ đòn
            if (Keyboard.current.backquoteKey.wasPressedThisFrame)
            {
                ActivateShield();
            }

            // Bấm các phím số 1, 2, 3 trên hàng phím QWERTY để đánh
            if (Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                PerformAttack(1);
            }
            else if (Keyboard.current.digit2Key.wasPressedThisFrame)
            {
                PerformAttack(2);
            }
            else if (Keyboard.current.digit3Key.wasPressedThisFrame)
            {
                PerformAttack(3);
            }

            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            {
                horizontalInput -= 1f;
            }
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            {
                horizontalInput += 1f;
            }

            // Kiểm tra trạng thái giữ phím nhảy
            if (Keyboard.current.spaceKey.isPressed ||
                Keyboard.current.wKey.isPressed ||
                Keyboard.current.upArrowKey.isPressed)
            {
                isJumpHeld = true;
            }

            // Bấm Space / W / ↑ để kích hoạt lệnh nhảy
            if (Keyboard.current.spaceKey.wasPressedThisFrame ||
                Keyboard.current.wKey.wasPressedThisFrame ||
                Keyboard.current.upArrowKey.wasPressedThisFrame)
            {
                jumpBufferCounter = jumpBufferTime;
            }
        }

        // Hỗ trợ Gamepad
        if (Gamepad.current != null)
        {
            // Đỡ đòn bằng phím Left Shoulder (LB / L1) trên tay cầm
            if (Gamepad.current.leftShoulder.wasPressedThisFrame)
            {
                ActivateShield();
            }

            // Đòn đánh bằng các nút bấm trên tay cầm
            if (Gamepad.current.buttonWest.wasPressedThisFrame) PerformAttack(1);
            if (Gamepad.current.buttonNorth.wasPressedThisFrame) PerformAttack(2);
            if (Gamepad.current.rightShoulder.wasPressedThisFrame) PerformAttack(3);

            float stickX = Gamepad.current.leftStick.x.ReadValue();
            if (Mathf.Abs(stickX) > 0.1f)
            {
                horizontalInput = stickX;
            }

            if (Gamepad.current.buttonSouth.isPressed)
            {
                isJumpHeld = true;
            }

            if (Gamepad.current.buttonSouth.wasPressedThisFrame)
            {
                jumpBufferCounter = jumpBufferTime;
            }
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        // Bấm phím ~ (dấu ngã / backquote cạnh phím số 1) để giương khiên đỡ đòn
        if (Input.GetKeyDown(KeyCode.BackQuote))
        {
            ActivateShield();
        }

        // Phím 1, 2, 3 trên hàng phím QWERTY (Alpha1, Alpha2, Alpha3)
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            PerformAttack(1);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            PerformAttack(2);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            PerformAttack(3);
        }

        horizontalInput = Input.GetAxisRaw("Horizontal");

        if (Input.GetKey(KeyCode.Space) || Input.GetButton("Jump") || Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
        {
            isJumpHeld = true;
        }

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
        {
            jumpBufferCounter = jumpBufferTime;
        }
#endif
    }

    private bool CheckGrounded()
    {
        // Nếu groundLayer chưa được chọn trong Inspector (giá trị 0 / Nothing), tự động quét tất cả các layer
        LayerMask mask = groundLayer.value == 0 ? ~0 : groundLayer;

        Vector2 checkPos = GetGroundCheckPosition();
        Vector2 checkSize = GetGroundCheckSize();

        // Quét tất cả các collider trong vùng chân
        Collider2D[] hits = Physics2D.OverlapBoxAll(checkPos, checkSize, 0f, mask);

        foreach (Collider2D hit in hits)
        {
            // Bỏ qua nếu là collider của chính nhân vật (hoặc các collider con) hoặc là Trigger
            if (hit == null || hit.isTrigger) continue;
            if (hit.gameObject == gameObject || hit.transform.IsChildOf(transform)) continue;

            return true;
        }

        return false;
    }

    private Vector2 GetGroundCheckPosition()
    {
        if (groundCheck != null)
        {
            return groundCheck.position;
        }

        if (col != null)
        {
            // Điểm đáy của collider nhân vật
            return new Vector2(col.bounds.center.x, col.bounds.min.y);
        }

        return transform.position;
    }

    private Vector2 GetGroundCheckSize()
    {
        if (col != null && groundCheck == null)
        {
            // Tự động chỉnh độ rộng quét chân bằng 80% độ rộng của nhân vật
            return new Vector2(col.bounds.size.x * 0.8f, groundCheckSize.y);
        }

        return groundCheckSize;
    }

    private void Flip()
    {
        isFacingRight = !isFacingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1f;
        transform.localScale = scale;
    }

    private void OnDrawGizmosSelected()
    {
        // 1. Vẽ vùng kiểm tra mặt đất (Xanh lá / Đỏ)
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Vector2 checkPos = GetGroundCheckPosition();
        Vector2 checkSize = GetGroundCheckSize();
        Gizmos.DrawWireCube(checkPos, checkSize);

        // 2. Vẽ tầm chém cận chiến (Vòng tròn Đỏ phía trước mặt)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(GetAttackPosition(), attackRange);
    }

    // =========================================================
    // HỆ THỐNG ÂM THANH KỸ NĂNG VÀ KHIÊN TỰ TẠO BẰNG CODE
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
        audioSource.spatialBlend = 0f; // Âm thanh 2D nổi bật cho người chơi

        // Sử dụng clip tùy chỉnh nếu có gán trong Inspector, ngược lại tự động tổng hợp bằng code
        attack1Clip = customAttack1Clip != null ? customAttack1Clip : CreateAttackSoundClip(1);
        attack2Clip = customAttack2Clip != null ? customAttack2Clip : CreateAttackSoundClip(2);
        attack3Clip = customAttack3Clip != null ? customAttack3Clip : CreateAttackSoundClip(3);
        shieldUpClip = customShieldUpClip != null ? customShieldUpClip : CreateShieldUpClip();
        shieldBlockClip = customShieldBlockClip != null ? customShieldBlockClip : CreateShieldBlockClip();
        playerHurtClip = customHurtClip != null ? customHurtClip : CreatePlayerHurtClip();
    }

    private void PlayAttackSound(int attackNumber)
    {
        if (!enableAudio || audioSource == null) return;
        AudioClip clip = attackNumber == 1 ? attack1Clip : attackNumber == 2 ? attack2Clip : attack3Clip;
        if (clip != null)
        {
            // Thay đổi cao độ ngẫu nhiên từ 0.95 đến 1.05 để mỗi cú chém nghe tự nhiên, không bị lặp âm
            audioSource.pitch = Random.Range(0.95f, 1.05f);
            audioSource.PlayOneShot(clip, sfxVolume);
        }
    }

    private void PlayShieldUpSound()
    {
        if (!enableAudio || audioSource == null || shieldUpClip == null) return;
        audioSource.pitch = 1f;
        audioSource.PlayOneShot(shieldUpClip, sfxVolume * 0.9f);
    }

    private void PlayShieldBlockSound()
    {
        if (!enableAudio || audioSource == null || shieldBlockClip == null) return;
        audioSource.pitch = Random.Range(0.98f, 1.04f);
        audioSource.PlayOneShot(shieldBlockClip, sfxVolume * 1.15f);
    }

    private void PlayHurtSound()
    {
        if (!enableAudio || audioSource == null || playerHurtClip == null) return;
        audioSource.pitch = Random.Range(0.95f, 1.05f);
        audioSource.PlayOneShot(playerHurtClip, sfxVolume * 1.1f);
    }

    /// <summary>
    /// Tạo âm thanh chém kiếm (Slash) bằng code
    /// - Attack 1: Đòn chém nhanh, vút gió sắc bén
    /// - Attack 2: Đòn chém đôi, uy lực trung bình
    /// - Attack 3: Đòn chém kết liễu hạng nặng, rền vang và đầm tay
    /// </summary>
    private AudioClip CreateAttackSoundClip(int attackNumber)
    {
        int sampleRate = 44100;
        float duration = attackNumber == 1 ? 0.14f : attackNumber == 2 ? 0.18f : 0.25f;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float progress = t / duration;

            if (attackNumber == 1)
            {
                // Đòn 1: Vút gió nhanh từ 750Hz xuống 160Hz
                float freq = Mathf.Lerp(750f, 160f, progress * progress);
                float envelope = Mathf.Clamp01(t / 0.015f) * Mathf.Exp(-progress * 4.5f);
                float wave = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.5f + (Random.value * 2f - 1f) * 0.35f;
                samples[i] = wave * envelope;
            }
            else if (attackNumber == 2)
            {
                // Đòn 2: Chém 2 tầng cộng hưởng từ 580Hz xuống 120Hz
                float freq = Mathf.Lerp(580f, 120f, progress * progress);
                float envelope = Mathf.Clamp01(t / 0.02f) * Mathf.Exp(-progress * 4.0f);
                float wave = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.45f +
                             Mathf.Sin(2f * Mathf.PI * (freq * 2.1f) * t) * 0.25f +
                             (Random.value * 2f - 1f) * 0.3f;
                samples[i] = wave * envelope;
            }
            else
            {
                // Đòn 3: Đòn kết liễu bùng nổ, có âm bass trầm 180Hz xuống 45Hz + vệt kiếm 1100Hz
                float lowFreq = Mathf.Lerp(180f, 45f, progress);
                float highFreq = Mathf.Lerp(1100f, 220f, progress * progress);
                float envelope = Mathf.Clamp01(t / 0.025f) * Mathf.Exp(-progress * 3.5f);
                float wave = Mathf.Sin(2f * Mathf.PI * lowFreq * t) * 0.5f +
                             Mathf.Sin(2f * Mathf.PI * highFreq * t) * 0.3f +
                             (Random.value * 2f - 1f) * 0.4f;
                samples[i] = wave * envelope;
            }
        }

        AudioClip clip = AudioClip.Create($"Player_Attack_{attackNumber}", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    /// <summary>
    /// Tạo âm thanh kích hoạt khiên (Shield Up) - tiếng năng lượng vút lên
    /// </summary>
    private AudioClip CreateShieldUpClip()
    {
        int sampleRate = 44100;
        float duration = 0.26f;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float progress = t / duration;

            // Tần số quét từ 280Hz tăng dần lên 760Hz
            float freq = Mathf.Lerp(280f, 760f, Mathf.Sqrt(progress));
            float envelope = Mathf.Clamp01(t / 0.03f) * (1f - progress);
            float wave = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.5f +
                         Mathf.Sin(2f * Mathf.PI * (freq * 1.5f) * t) * 0.25f +
                         (Random.value * 2f - 1f) * 0.1f;
            samples[i] = wave * envelope;
        }

        AudioClip clip = AudioClip.Create("Player_ShieldUp", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    /// <summary>
    /// Tạo âm thanh kim loại va đập KENG (Parry / Block) khi khiên chặn trúng đòn
    /// </summary>
    private AudioClip CreateShieldBlockClip()
    {
        int sampleRate = 44100;
        float duration = 0.35f;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        // 4 tần số kim loại vang đặc trưng của tiếng kiếm chém trúng giáp sắt
        float m1 = 1080f;
        float m2 = 1840f;
        float m3 = 2650f;
        float m4 = 3400f;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float attack = Mathf.Clamp01(t / 0.002f); // Bắt đầu cực nhanh trong 2ms
            float decay = Mathf.Exp(-t * 11f);       // Ngân vang kim loại dài

            float impactThud = Mathf.Sin(2f * Mathf.PI * 140f * t) * Mathf.Exp(-t * 40f) * 0.4f;

            float metalRing = (Mathf.Sin(2f * Mathf.PI * m1 * t) * 0.35f +
                               Mathf.Sin(2f * Mathf.PI * m2 * t) * 0.3f +
                               Mathf.Sin(2f * Mathf.PI * m3 * t) * 0.2f +
                               Mathf.Sin(2f * Mathf.PI * m4 * t) * 0.15f);

            samples[i] = attack * (impactThud + metalRing * decay);
        }

        AudioClip clip = AudioClip.Create("Player_ShieldBlock", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    /// <summary>
    /// Tạo âm thanh Player bị thương khi trúng đòn
    /// </summary>
    private AudioClip CreatePlayerHurtClip()
    {
        int sampleRate = 44100;
        float duration = 0.20f;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float progress = t / duration;

            // Âm va đập trầm (thud) + tiếng khựng đau
            float thudFreq = Mathf.Lerp(140f, 50f, progress);
            float groanFreq = Mathf.Lerp(260f, 130f, progress * progress);
            float envelope = Mathf.Clamp01(t / 0.005f) * Mathf.Exp(-progress * 5f);

            float wave = Mathf.Sin(2f * Mathf.PI * thudFreq * t) * 0.5f +
                         Mathf.Sin(2f * Mathf.PI * groanFreq * t) * 0.35f +
                         (Random.value * 2f - 1f) * 0.25f * Mathf.Exp(-progress * 8f);

            samples[i] = wave * envelope;
        }

        AudioClip clip = AudioClip.Create("Player_Hurt", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
