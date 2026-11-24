using UnityEngine;

public class enemy_conllision : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("Tag của Player")]
    [SerializeField] private string playerTag = "Player";

    [Tooltip("Có bị thương khi chạm người Player không? (Mặc định để false để Enemy chỉ bị Hurt khi Player bấm phím 1 2 3 chém)")]
    [SerializeField] private bool hurtOnPlayerTouch = false;

    private enemy_controller enemyController;

    private void Awake()
    {
        enemyController = GetComponent<enemy_controller>();
        if (enemyController == null)
        {
            enemyController = GetComponentInParent<enemy_controller>();
        }

        if (enemyController == null)
        {
            Debug.LogWarning("[enemy_conllision] Không tìm thấy enemy_controller trên GameObject!");
        }
    }

    // 1. Nhận diện va chạm dạng Trigger
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (hurtOnPlayerTouch && collision.CompareTag(playerTag))
        {
            Debug.Log($"[enemy_conllision] Bị chạm bởi Player: {collision.gameObject.name}");
            TriggerHurt();
        }
    }

    // 2. Nhận diện va chạm vật lý thông thường
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (hurtOnPlayerTouch && collision.gameObject.CompareTag(playerTag))
        {
            Debug.Log($"[enemy_conllision] Va chạm vật lý với Player: {collision.gameObject.name}");
            TriggerHurt();
        }
    }

    private void TriggerHurt()
    {
        if (enemyController != null)
        {
            enemyController.TriggerHurt();
        }
    }
}
