using UnityEngine;

public class PlayerSpawnPoint : MonoBehaviour
{
    [SerializeField] private int _playerIndex;

    public int PlayerIndex => _playerIndex;

    private void OnDrawGizmos()
    {
        Gizmos.color = _playerIndex == 0 ? Color.blue : Color.red;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        Gizmos.DrawRay(transform.position, transform.forward);
    }
}