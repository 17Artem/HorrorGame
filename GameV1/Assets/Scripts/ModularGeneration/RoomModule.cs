using System.Collections.Generic;
using UnityEngine;

public class RoomModule : MonoBehaviour
{
    [Header("Room Connections")]
    [SerializeField] private int minConnections = 1;
    [SerializeField] private int maxConnections = 3;

    [Header("Connection Settings")]
    [SerializeField] private bool randomizeConnections = true;

    private Socket[] sockets;

    private void Awake()
    {
        sockets = GetComponentsInChildren<Socket>();
    }

    /// <summary>
    /// Returns all sockets belonging to this room.
    /// </summary>
    public Socket[] GetSockets()
    {
        return sockets;
    }

    /// <summary>
    /// Returns sockets that are currently available.
    /// </summary>
    public List<Socket> GetAvailableSockets()
    {
        List<Socket> available = new();

        foreach (Socket socket in sockets)
        {
            if (socket.IsAvailable)
            {
                available.Add(socket);
            }
        }

        return available;
    }

    /// <summary>
    /// Randomly decides how many exits this room should have.
    /// </summary>
    public void InitializeConnections(System.Random random)
    {
        if (sockets == null || sockets.Length == 0)
        {
            Debug.LogWarning(
                $"{name}: No sockets found."
            );

            return;
        }

        // Reset all sockets first.
        foreach (Socket socket in sockets)
        {
            socket.SetAvailable();
        }

        int maximum =
            Mathf.Min(
                maxConnections,
                sockets.Length
            );

        int minimum =
            Mathf.Clamp(
                minConnections,
                0,
                maximum
            );

        int connectionCount;

        if (randomizeConnections)
        {
            connectionCount =
                random.Next(
                    minimum,
                    maximum + 1
                );
        }
        else
        {
            connectionCount = maximum;
        }

        BlockRandomSockets(
            connectionCount,
            random
        );
    }

    /// <summary>
    /// Blocks random sockets until only
    /// the requested number remain available.
    /// </summary>
    private void BlockRandomSockets(
        int connectionCount,
        System.Random random
    )
    {
        List<Socket> available =
            new(sockets);

        while (available.Count > connectionCount)
        {
            int index =
                random.Next(
                    available.Count
                );

            Socket socket =
                available[index];

            socket.SetBlocked();

            available.RemoveAt(index);
        }
    }

    /// <summary>
    /// Returns the number of sockets that can
    /// still be used for generation.
    /// </summary>
    public int GetAvailableConnectionCount()
    {
        int count = 0;

        foreach (Socket socket in sockets)
        {
            if (socket.IsAvailable)
            {
                count++;
            }
        }

        return count;
    }

    private void OnValidate()
    {
        if (minConnections < 0)
            minConnections = 0;

        if (maxConnections < minConnections)
            maxConnections = minConnections;
    }
}