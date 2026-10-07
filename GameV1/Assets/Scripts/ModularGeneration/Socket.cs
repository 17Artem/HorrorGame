using UnityEngine;

public class Socket : MonoBehaviour
{
    public enum SocketState
    {
        Available,
        Occupied,
        Blocked
    }

    [Header("Socket Settings")]
    [SerializeField] private SocketState state = SocketState.Available;

    public SocketState State => state;

    public bool IsAvailable =>
        state == SocketState.Available;

    public bool IsOccupied =>
        state == SocketState.Occupied;

    public bool IsBlocked =>
        state == SocketState.Blocked;

    public Transform GetTransform()
    {
        return transform;
    }

    public void SetOccupied()
    {
        state = SocketState.Occupied;
    }

    public void SetBlocked()
    {
        state = SocketState.Blocked;
    }

    public void SetAvailable()
    {
        state = SocketState.Available;
    }

    private void OnDrawGizmos()
    {
        switch (state)
        {
            case SocketState.Available:
                Gizmos.color = Color.green;
                break;

            case SocketState.Occupied:
                Gizmos.color = Color.red;
                break;

            case SocketState.Blocked:
                Gizmos.color = Color.gray;
                break;
        }

        Gizmos.DrawSphere(
            transform.position,
            0.15f
        );

        // Forward direction
        Gizmos.color = Color.blue;

        Gizmos.DrawLine(
            transform.position,
            transform.position +
            transform.forward * 0.7f
        );
    }
}