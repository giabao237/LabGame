using UnityEngine;

public class Player_Conllision : MonoBehaviour
{
    [Header("Enemy Collision Settings")]
    [Tooltip("Tag của Enemy để nhận diện va chạm")]
    [SerializeField] private string enemyTag = "Enemy";

    [Tooltip("Có bị thương khi chạm người Enemy không? (để false để chỉ bị thương khi Enemy vung đòn đánh Attack)")]
    [SerializeField] private bool hurtOnBodyTouch = false;

    private Player_Controller playerController;

    private void Awake()
    {
        playerController = GetComponent<Player_Controller>();
        if (playerController == null)
        {
            playerController = GetComponentInParent<Player_Controller>();
        }

        if (playerController == null)
        {
            Debug.LogWarning("[Player_Conllision] Không tìm thấy Player_Controller trên GameObject này! Hãy chắc chắn script này được gắn vào Player.");
        }
    }

    // 1. Nhận diện va chạm dạng Trigger
    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Kiểm tra vùng chiến thắng / End Game (telewin, endgame, win, goal)
        if (IsEndGameZone(collision.gameObject))
        {
            if (GameMenu.Instance != null) GameMenu.Instance.TriggerVictory();
            return;
        }

        // Kiểm tra vùng tử thần / vực thẳm (Die, Die2)
        if (IsDeathZone(collision.gameObject))
        {
            if (playerController != null) playerController.InstantKill();
            return;
        }

        // Đòn đánh đặc biệt: Nhảy dẫm lên đầu Enemy
        if (playerController != null && playerController.CheckHeadStomp(collision))
        {
            return;
        }

        if (hurtOnBodyTouch && collision.CompareTag(enemyTag))
        {
            Debug.Log($"[Player_Conllision] Đã chạm người Enemy: {collision.gameObject.name}");
            if (playerController != null)
            {
                playerController.TriggerHurt();
            }
        }
    }

    // 2. Nhận diện khi dùng Collider vật lý thông thường
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (IsEndGameZone(collision.gameObject))
        {
            if (GameMenu.Instance != null) GameMenu.Instance.TriggerVictory();
            return;
        }

        if (IsDeathZone(collision.gameObject))
        {
            if (playerController != null) playerController.InstantKill();
            return;
        }

        // Đòn đánh đặc biệt: Nhảy dẫm lên đầu Enemy
        if (playerController != null && playerController.CheckHeadStomp(collision.collider))
        {
            return;
        }

        if (hurtOnBodyTouch && collision.gameObject.CompareTag(enemyTag))
        {
            Debug.Log($"[Player_Conllision] Đã va chạm người Enemy: {collision.gameObject.name}");
            if (playerController != null)
            {
                playerController.TriggerHurt();
            }
        }
    }

    private bool IsDeathZone(GameObject obj)
    {
        if (obj == null) return false;

        string objName = obj.name.ToLower();
        if (objName.Contains("die") || objName.Contains("dead") || objName.Contains("death") || objName.Contains("kill"))
        {
            return true;
        }

        if (obj.GetComponent<DeathZone>() != null)
        {
            return true;
        }

        return false;
    }

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
}
