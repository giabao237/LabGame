using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Cơ chế Camera cuộn 1 chiều phong cách Super Mario Bros:
/// - Khi nhân vật đi sang bên phải, Camera tự động cuộn theo.
/// - Khi nhân vật quay đầu đi sang trái, Camera KHÔNG BAO GIỜ lùi lại (X luôn chỉ tăng hoặc giữ nguyên).
/// - Khóa hoàn toàn mép trái màn hình: Tạo bức tường vật lý vô hình chặn mép trái,
///   ngăn không cho cả Camera lẫn nhân vật chạm lại hoặc đi xuyên qua vùng màn hình cũ bên trái.
/// </summary>
[DefaultExecutionOrder(100)]
public class OneWayCamera : MonoBehaviour
{
    private static OneWayCamera instance;
    public static OneWayCamera Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Object.FindFirstObjectByType<OneWayCamera>();
                if (instance == null)
                {
                    GameObject go = new GameObject("OneWayCamera_Controller");
                    instance = go.AddComponent<OneWayCamera>();
                }
            }
            return instance;
        }
    }

    [Header("Target & Camera Settings")]
    [Tooltip("Transform của Player (nếu để trống script sẽ tự tìm Player_Controller)")]
    [SerializeField] private Transform playerTransform;

    [Tooltip("Cinemachine Camera trong Scene (nếu để trống script sẽ tự tìm)")]
    [SerializeField] private CinemachineCamera cmCamera;

    [Header("Boundary Settings (Khóa mép trái màn hình)")]
    [Tooltip("Độ dày của bức tường chặn mép trái")]
    [SerializeField] private float wallThickness = 2.0f;

    [Tooltip("Khoảng cách đệm từ mép trái màn hình (để nhân vật không bị khuất 1 phần khi chạm mép)")]
    [SerializeField] private float leftPadding = 0.5f;

    [Header("Debug")]
    [Tooltip("Vẽ đường ranh giới mép trái trong Scene View để dễ quan sát")]
    [SerializeField] private bool showGizmos = true;

    private Camera mainCamera;
    private Rigidbody2D playerRb;
    private Collider2D playerCol;
    private Transform cameraFollowTarget;
    private GameObject leftWallObj;
    private BoxCollider2D leftWallCol;

    private float maxTargetX = float.MinValue;
    private float maxLeftEdgeX = float.MinValue;
    private float maxCameraX = float.MinValue;
    private float playerHalfWidth = 0.5f;

    /// <summary>
    /// Tự động kích hoạt cơ chế One-Way Camera ngay khi bấm Play
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

        FindReferences();
        SetupCameraFollowTarget();
        SetupLeftWall();
    }

    private void Start()
    {
        if (playerTransform == null)
        {
            FindReferences();
            SetupCameraFollowTarget();
            SetupLeftWall();
        }
    }

    private void FindReferences()
    {
        mainCamera = Camera.main;

        if (cmCamera == null)
        {
            cmCamera = Object.FindFirstObjectByType<CinemachineCamera>();
        }

        if (playerTransform == null)
        {
            Player_Controller player = Object.FindFirstObjectByType<Player_Controller>();
            if (player != null)
            {
                playerTransform = player.transform;
            }
            else
            {
                GameObject pObj = GameObject.FindGameObjectWithTag("Player");
                if (pObj != null) playerTransform = pObj.transform;
            }
        }

        if (playerTransform != null)
        {
            playerRb = playerTransform.GetComponent<Rigidbody2D>();
            playerCol = playerTransform.GetComponent<Collider2D>();
            if (playerCol != null)
            {
                playerHalfWidth = playerCol.bounds.extents.x;
            }
        }
    }

    /// <summary>
    /// Tạo mục tiêu trung gian (Follow Target) cho Cinemachine Camera.
    /// Mục tiêu này CHỈ di chuyển sang phải, KHÔNG BAO GIỜ lùi sang trái.
    /// </summary>
    private void SetupCameraFollowTarget()
    {
        if (playerTransform == null) return;

        if (cameraFollowTarget == null)
        {
            GameObject targetObj = new GameObject("Camera_OneWay_Target");
            cameraFollowTarget = targetObj.transform;
            cameraFollowTarget.position = playerTransform.position;
            maxTargetX = playerTransform.position.x;
        }

        if (cmCamera != null)
        {
            // Trỏ CinemachineCamera theo dõi mục tiêu cuộn 1 chiều thay vì theo dõi trực tiếp Player
            cmCamera.Follow = cameraFollowTarget;
            cmCamera.Target.TrackingTarget = cameraFollowTarget;

            Debug.Log("<color=green>[OneWayCamera] Đã kết nối CinemachineCamera với mục tiêu cuộn 1 chiều!</color>");
        }
    }

    /// <summary>
    /// Tạo bức tường vật lý vô hình chặn toàn bộ mép trái màn hình
    /// </summary>
    private void SetupLeftWall()
    {
        if (leftWallObj == null)
        {
            leftWallObj = new GameObject("OneWay_LeftScreenWall");
            leftWallCol = leftWallObj.AddComponent<BoxCollider2D>();
            leftWallCol.size = new Vector2(wallThickness, 80f); // Chiều cao tường lớn để bao trọn mọi độ cao nhảy

            // Gán layer Ground hoặc Default để Player chạm vào như vật cản cứng
            leftWallObj.layer = LayerMask.NameToLayer("Default");
        }

        if (mainCamera != null)
        {
            float halfWidth = mainCamera.orthographicSize * mainCamera.aspect;
            maxLeftEdgeX = mainCamera.transform.position.x - halfWidth;
            maxCameraX = mainCamera.transform.position.x;
            UpdateWallPosition();
        }
    }

    private void Update()
    {
        if (playerTransform == null)
        {
            FindReferences();
            if (playerTransform == null) return;
        }

        // 1. Cập nhật vị trí mục tiêu cho Camera:
        // X CHỈ TĂNG theo hướng đi sang phải của Player, không bao giờ giảm!
        float playerX = playerTransform.position.x;
        if (playerX > maxTargetX)
        {
            maxTargetX = playerX;
        }

        if (cameraFollowTarget != null)
        {
            // Giữ X ở vị trí xa nhất bên phải, Y theo chiều cao nhân vật
            cameraFollowTarget.position = new Vector3(maxTargetX, playerTransform.position.y, playerTransform.position.z);
        }
    }

    private void LateUpdate()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return;
        }

        // 2. Cập nhật vị trí Camera và Khóa không bao giờ cho lùi sang trái
        Vector3 camPos = mainCamera.transform.position;
        if (camPos.x < maxCameraX)
        {
            camPos.x = maxCameraX;
            mainCamera.transform.position = camPos;
        }
        else
        {
            maxCameraX = camPos.x;
        }

        // Khóa cả Transform của CinemachineCamera nếu có
        if (cmCamera != null)
        {
            Vector3 cmPos = cmCamera.transform.position;
            if (cmPos.x < maxCameraX)
            {
                cmPos.x = maxCameraX;
                cmCamera.transform.position = cmPos;
            }
        }

        // 3. Tính toán vị trí mép trái màn hình
        float halfWidth = mainCamera.orthographicSize * mainCamera.aspect;
        float currentLeftEdge = mainCamera.transform.position.x - halfWidth;

        // Mép trái chỉ có thể tịnh tiến sang phải, tuyệt đối không lùi lại
        if (currentLeftEdge > maxLeftEdgeX)
        {
            maxLeftEdgeX = currentLeftEdge;
        }

        // 4. Cập nhật bức tường vật lý chặn mép trái
        UpdateWallPosition();

        // 5. Khóa chặt tọa độ Player (Hard Clamp) chống xuyên tường / kẹt góc
        ClampPlayerPosition();
    }

    private void UpdateWallPosition()
    {
        if (leftWallObj != null && mainCamera != null)
        {
            float wallPosX = maxLeftEdgeX - (wallThickness * 0.5f) + leftPadding;
            float wallPosY = mainCamera.transform.position.y;
            leftWallObj.transform.position = new Vector3(wallPosX, wallPosY, 0f);
        }
    }

    private void ClampPlayerPosition()
    {
        if (playerTransform == null) return;

        float minAllowedX = maxLeftEdgeX + playerHalfWidth + leftPadding;
        if (playerTransform.position.x < minAllowedX)
        {
            // Giữ chặt nhân vật tại mép trái, không cho vượt qua
            Vector3 clampedPos = playerTransform.position;
            clampedPos.x = minAllowedX;
            playerTransform.position = clampedPos;

            // Triệt tiêu vận tốc hướng sang trái nếu nhân vật đang cố bấm chạy sang trái
            if (playerRb != null)
            {
#if UNITY_6000_0_OR_NEWER
                if (playerRb.linearVelocity.x < 0f)
                {
                    playerRb.linearVelocity = new Vector2(0f, playerRb.linearVelocity.y);
                }
#else
                if (playerRb.velocity.x < 0f)
                {
                    playerRb.velocity = new Vector2(0f, playerRb.velocity.y);
                }
#endif
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos) return;

        // Vẽ vạch đỏ hiển thị bức tường khóa mép trái trong Scene View
        Gizmos.color = Color.red;
        float x = maxLeftEdgeX != float.MinValue ? (maxLeftEdgeX + leftPadding) : transform.position.x;
        Vector3 top = new Vector3(x, 50f, 0f);
        Vector3 bottom = new Vector3(x, -50f, 0f);
        Gizmos.DrawLine(top, bottom);
    }
}
