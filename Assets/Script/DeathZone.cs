using UnityEngine;

public class DeathZone : MonoBehaviour
{
    [Header("Death Zone Settings")]
    [Tooltip("Tag của đối tượng bị kết liễu (mặc định Player)")]
    [SerializeField] private string targetTag = "Player";

    private void OnTriggerEnter2D(Collider2D collision)
    {
        CheckAndKill(collision.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        CheckAndKill(collision.gameObject);
    }

    private void CheckAndKill(GameObject target)
    {
        // 1. Nếu là Player
        Player_Controller player = target.GetComponent<Player_Controller>();
        if (player == null) player = target.GetComponentInParent<Player_Controller>();

        if (player != null)
        {
            player.InstantKill();
            return;
        }

        // 2. Nếu là bất kỳ Entity nào khác có Health (ví dụ quái rơi xuống vực)
        Health hp = target.GetComponent<Health>();
        if (hp == null) hp = target.GetComponentInParent<Health>();

        if (hp != null)
        {
            hp.TakeDamage(hp.CurrentHealth);
        }
    }
}
