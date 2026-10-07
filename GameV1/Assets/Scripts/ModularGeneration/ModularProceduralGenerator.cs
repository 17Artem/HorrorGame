using System.Collections.Generic;
using UnityEngine;

public class ModularProceduralGenerator : MonoBehaviour
{
    [Header("Generation")]
    [SerializeField] private int seed = 12345;
    [SerializeField] private int moduleCount = 20;

    [Header("Start")]
    [SerializeField] private GameObject startRoom;

    [Header("Modules")]
    [SerializeField] private GameObject[] modules;

    [Header("Generation Settings")]
    [SerializeField] private int maxAttempts = 30;

    [Tooltip("Small tolerance for collider intersections.")]
    [SerializeField] private float collisionTolerance = 0.01f;

    private System.Random random;

    private readonly List<GameObject> spawnedModules = new();

    private void Start()
    {
        Generate();
    }

    public void Generate()
    {
        ClearLevel();

        random = new System.Random(seed);

        if (startRoom == null)
        {
            Debug.LogError(
                "ModularProceduralGenerator: Start Room is not assigned!"
            );

            return;
        }

        if (modules == null || modules.Length == 0)
        {
            Debug.LogError(
                "ModularProceduralGenerator: Modules array is empty!"
            );

            return;
        }

        // ------------------------------------------------------
        // CREATE START ROOM
        // ------------------------------------------------------

        GameObject start = Instantiate(
            startRoom,
            transform.position,
            transform.rotation,
            transform
        );

        // If this is a RoomModule, initialize its exits.
        RoomModule startRoomModule =
            start.GetComponent<RoomModule>();

        if (startRoomModule != null)
        {
            startRoomModule.InitializeConnections(random);
        }

        spawnedModules.Add(start);

        Debug.Log(
            $"Generation started. Seed: {seed}"
        );

        GenerateModules();

        Debug.Log(
            $"Generation finished. Modules created: {spawnedModules.Count}"
        );
    }

    private void GenerateModules()
    {
        for (int i = 0; i < moduleCount; i++)
        {
            bool created = TryCreateModule();

            if (!created)
            {
                Debug.LogWarning(
                    $"Could not create module {i + 1}"
                );
            }
        }
    }

    private bool TryCreateModule()
    {
        if (modules == null || modules.Length == 0)
        {
            Debug.LogError(
                "Modules array is empty!"
            );

            return false;
        }

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            // --------------------------------------------------
            // 1. Find existing module with free socket
            // --------------------------------------------------

            GameObject parentModule =
                GetRandomModuleWithFreeSocket();

            if (parentModule == null)
            {
                Debug.LogWarning(
                    "No free sockets available."
                );

                return false;
            }

            Socket targetSocket =
                GetRandomFreeSocket(parentModule);

            if (targetSocket == null)
                continue;

            // --------------------------------------------------
            // 2. Select random prefab
            // --------------------------------------------------

            GameObject prefab =
                modules[random.Next(modules.Length)];

            if (prefab == null)
            {
                Debug.LogWarning(
                    "One of the module slots is empty."
                );

                continue;
            }

            // --------------------------------------------------
            // 3. Create temporary module
            // --------------------------------------------------

            GameObject newModule = Instantiate(
                prefab,
                Vector3.zero,
                Quaternion.identity,
                transform
            );

            // --------------------------------------------------
            // 4. Initialize RoomModule
            // --------------------------------------------------

            RoomModule roomModule =
                newModule.GetComponent<RoomModule>();

            if (roomModule != null)
            {
                roomModule.InitializeConnections(random);
            }

            // --------------------------------------------------
            // 5. Find sockets
            // --------------------------------------------------

            Socket[] newSockets =
                newModule.GetComponentsInChildren<Socket>();

            if (newSockets.Length == 0)
            {
                Debug.LogWarning(
                    $"{prefab.name} has no Socket components!"
                );

                Destroy(newModule);
                continue;
            }

            // --------------------------------------------------
            // 6. Get only available sockets
            // --------------------------------------------------

            List<Socket> availableSockets = new();

            foreach (Socket socket in newSockets)
            {
                if (socket.IsAvailable)
                {
                    availableSockets.Add(socket);
                }
            }

            if (availableSockets.Count == 0)
            {
                Debug.LogWarning(
                    $"{prefab.name} has no available sockets."
                );

                Destroy(newModule);
                continue;
            }

            // --------------------------------------------------
            // 7. Select available socket
            // --------------------------------------------------

            Socket newSocket =
                availableSockets[
                    random.Next(availableSockets.Count)
                ];

            Debug.Log(
                $"Trying {prefab.name} " +
                $"using socket {newSocket.name} " +
                $"at position {targetSocket.transform.position}"
            );

            // --------------------------------------------------
            // 8. Connect sockets
            // --------------------------------------------------

            ConnectSockets(
                targetSocket,
                newSocket,
                newModule
            );

            // --------------------------------------------------
            // 9. Check collision
            // --------------------------------------------------

            bool collision =
                HasInvalidCollision(
                    newModule,
                    parentModule,
                    targetSocket,
                    newSocket
                );

            if (collision)
            {
                Debug.Log(
                    $"Rejected {prefab.name}: Collider overlap."
                );

                Destroy(newModule);
                continue;
            }

            // --------------------------------------------------
            // 10. Accept module
            // --------------------------------------------------

            targetSocket.SetOccupied();
            newSocket.SetOccupied();

            spawnedModules.Add(newModule);

            Debug.Log(
                $"Created {prefab.name} successfully."
            );

            return true;
        }

        return false;
    }

    // ==========================================================
    // FIND MODULE WITH FREE SOCKET
    // ==========================================================

    private GameObject GetRandomModuleWithFreeSocket()
    {
        List<GameObject> availableModules = new();

        foreach (GameObject module in spawnedModules)
        {
            if (module == null)
                continue;

            Socket[] sockets =
                module.GetComponentsInChildren<Socket>();

            foreach (Socket socket in sockets)
            {
                if (socket.IsAvailable)
                {
                    availableModules.Add(module);
                    break;
                }
            }
        }

        if (availableModules.Count == 0)
            return null;

        return availableModules[
            random.Next(availableModules.Count)
        ];
    }

    // ==========================================================
    // FIND FREE SOCKET
    // ==========================================================

    private Socket GetRandomFreeSocket(
        GameObject module
    )
    {
        Socket[] sockets =
            module.GetComponentsInChildren<Socket>();

        List<Socket> freeSockets = new();

        foreach (Socket socket in sockets)
        {
            if (socket.IsAvailable)
            {
                freeSockets.Add(socket);
            }
        }

        if (freeSockets.Count == 0)
            return null;

        return freeSockets[
            random.Next(freeSockets.Count)
        ];
    }

    // ==========================================================
    // CONNECT SOCKETS
    // ==========================================================

    private void ConnectSockets(
        Socket targetSocket,
        Socket newSocket,
        GameObject newModule
    )
    {
        Transform target =
            targetSocket.transform;

        Transform source =
            newSocket.transform;

        // Make socket directions face each other.
        Quaternion targetRotation =
            target.rotation *
            Quaternion.Euler(0f, 180f, 0f) *
            Quaternion.Inverse(source.rotation);

        newModule.transform.rotation =
            targetRotation;

        // Move module so socket positions match.
        Vector3 positionOffset =
            target.position -
            source.position;

        newModule.transform.position +=
            positionOffset;
    }

    // ==========================================================
    // COLLISION CHECK
    // ==========================================================

    private bool HasInvalidCollision(
        GameObject newModule,
        GameObject connectedModule,
        Socket targetSocket,
        Socket newSocket
    )
    {
        Collider[] newColliders =
            newModule.GetComponentsInChildren<Collider>();

        if (newColliders.Length == 0)
        {
            Debug.LogWarning(
                $"{newModule.name} has no Colliders."
            );

            return false;
        }

        Vector3 connectionPoint =
            (targetSocket.transform.position +
             newSocket.transform.position) * 0.5f;

        foreach (GameObject existingModule in spawnedModules)
        {
            if (existingModule == null)
                continue;

            Collider[] existingColliders =
                existingModule.GetComponentsInChildren<Collider>();

            foreach (Collider newCollider in newColliders)
            {
                if (newCollider == null ||
                    !newCollider.enabled)
                {
                    continue;
                }

                foreach (Collider existingCollider in existingColliders)
                {
                    if (existingCollider == null ||
                        !existingCollider.enabled)
                    {
                        continue;
                    }

                    // ------------------------------------------
                    // Quick Bounds check
                    // ------------------------------------------

                    if (!newCollider.bounds.Intersects(
                        existingCollider.bounds))
                    {
                        continue;
                    }

                    // ------------------------------------------
                    // The two modules are intentionally touching
                    // at the socket connection.
                    // ------------------------------------------

                    if (existingModule == connectedModule)
                    {
                        float newDistance =
                            DistanceToCollider(
                                connectionPoint,
                                newCollider
                            );

                        float existingDistance =
                            DistanceToCollider(
                                connectionPoint,
                                existingCollider
                            );

                        const float connectionRadius = 0.75f;

                        if (newDistance <= connectionRadius &&
                            existingDistance <= connectionRadius)
                        {
                            continue;
                        }
                    }

                    // ------------------------------------------
                    // Real penetration test
                    // ------------------------------------------

                    if (HasRealPenetration(
                        newCollider,
                        existingCollider))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    // ==========================================================
    // PHYSICS PENETRATION
    // ==========================================================

    private bool HasRealPenetration(
        Collider first,
        Collider second
    )
    {
        Vector3 direction;
        float distance;

        bool overlapped =
            Physics.ComputePenetration(
                first,
                first.transform.position,
                first.transform.rotation,
                second,
                second.transform.position,
                second.transform.rotation,
                out direction,
                out distance
            );

        if (!overlapped)
            return false;

        return distance > collisionTolerance;
    }

    // ==========================================================
    // DISTANCE TO COLLIDER
    // ==========================================================

    private float DistanceToCollider(
        Vector3 point,
        Collider collider
    )
    {
        Vector3 closest =
            collider.ClosestPoint(point);

        return Vector3.Distance(
            point,
            closest
        );
    }

    // ==========================================================
    // CLEAR LEVEL
    // ==========================================================

    private void ClearLevel()
    {
        for (int i = transform.childCount - 1;
             i >= 0;
             i--)
        {
            DestroyImmediate(
                transform.GetChild(i).gameObject
            );
        }

        spawnedModules.Clear();
    }
}