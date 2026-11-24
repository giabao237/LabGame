using UnityEngine;

/// <summary>
/// Trigger kích hoạt kết thúc game (End Game / Victory):
/// - Gắn vào GameObject vùng kết thúc màn chơi (như telewin trong Scene)
/// - Tự động bật Is Trigger cho Collider2D
/// - Khi Player đi vào vùng này: Kích hoạt màn hình Victory, hiển thị tổng vàng, nút RETRY và EXIT
/// - Phát nhạc chiến thắng bgm_special_research_battle.wav từ Assets/Settings/endgame
/// </summary>
public class EndGameTrigger : MonoBehaviour
{
    private bool hasTriggered = false;

    private void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        CheckAndTriggerVictory(collision.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        CheckAndTriggerVictory(collision.gameObject);
    }

    private void CheckAndTriggerVictory(GameObject hitObj)
    {
        if (hasTriggered) return;

        // Nhận diện Player qua Tag, Component hoặc tên
        if (hitObj.CompareTag("Player") || hitObj.GetComponent<Player_Controller>() != null || hitObj.name.ToLower().Contains("player"))
        {
            hasTriggered = true;
            Debug.Log($"<color=green><b>[EndGameTrigger] Player đã chạm vào {gameObject.name}! KÍCH HOẠT VICTORY!</b></color>");

            if (GameMenu.Instance != null)
            {
                GameMenu.Instance.TriggerVictory();
            }
        }
    }

    /// <summary>
    /// Tự động tìm và gắn EndGameTrigger vào telewin / endgame trong scene khi bắt đầu chơi
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoAttachToSceneTriggers()
    {
        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (var go in allObjects)
        {
            if (go == null) continue;
            string n = go.name.ToLower();
            if (n.Contains("telewin") || n.Contains("endgame"))
            {
                if (go.GetComponent<EndGameTrigger>() == null)
                {
                    go.AddComponent<EndGameTrigger>();
                    Debug.Log($"<color=cyan><b>[EndGameTrigger] Đã tự động kết nối Trigger Victory vào GameObject: {go.name}</b></color>");
                }
            }
        }
    }
}
