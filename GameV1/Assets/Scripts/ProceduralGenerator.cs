using System;
using System.Collections.Generic;
using UnityEngine;

public class ProceduralGenerator : MonoBehaviour
{
    // =========================================================
    // ENUMS
    // =========================================================

    private enum Direction
    {
        North,
        South,
        East,
        West
    }

    private enum CorridorType
    {
        Short,
        Straight,
        Long,
        L,
        T
    }

    // =========================================================
    // GENERATION
    // =========================================================

    [Header("========== GENERATION ==========")]
    [SerializeField] private string seed = "HORROR_001";
    [Min(1)] [SerializeField] private int roomCount = 20;
    [SerializeField] private bool generateOnStart = true;
    [SerializeField] private bool clearBeforeGenerate = true;

    // =========================================================
    // ROOMS
    // =========================================================

    [Header("========== ROOMS ==========")]
    [Min(4f)] [SerializeField] private float minRoomWidth = 6f;
    [Min(4f)] [SerializeField] private float maxRoomWidth = 16f;
    [Min(4f)] [SerializeField] private float minRoomDepth = 6f;
    [Min(4f)] [SerializeField] private float maxRoomDepth = 16f;
    [Min(1f)] [SerializeField] private float roomHeight = 3f;

    // =========================================================
    // BRANCHING
    // =========================================================

    [Header("========== REAL BRANCHING ==========")]
    [Range(0f, 1f)] [SerializeField] private float branchChance = 0.65f;
    [Range(0f, 1f)] [SerializeField] private float lCorridorChance = 0.30f;
    [Range(0f, 1f)] [SerializeField] private float tCorridorChance = 0.25f;
    [Range(0f, 1f)] [SerializeField] private float extraConnectionChance = 0.08f;
    [Min(1)] [SerializeField] private int maxConnectionsPerRoom = 4;
    [Min(1)] [SerializeField] private int maxAttemptsPerRoom = 100;

    // =========================================================
    // CORRIDORS
    // =========================================================

    [Header("========== CORRIDORS ==========")]
    [Min(0.5f)] [SerializeField] private float corridorWidth = 3f;
    [Min(0.5f)] [SerializeField] private float minCorridorLength = 3f;
    [Min(0.5f)] [SerializeField] private float maxCorridorLength = 12f;

    // =========================================================
    // WALLS / FLOOR
    // =========================================================

    [Header("========== WALLS / FLOOR ==========")]
    [Min(0.05f)] [SerializeField] private float wallThickness = 0.25f;
    [Min(0.5f)] [SerializeField] private float doorwayWidth = 2.5f;
    [Min(0.05f)] [SerializeField] private float floorThickness = 0.25f;

    // =========================================================
    // GRID
    // =========================================================

    [Header("========== GRID ==========")]
    [Min(10f)] [SerializeField] private float cellSize = 24f;
    [Min(0f)] [SerializeField] private float roomPadding = 0.5f;
    [SerializeField] private bool showConnectionSockets = false;

    // =========================================================
    // INTERNAL
    // =========================================================

    private System.Random random;

    private readonly List<RoomData> rooms = new List<RoomData>();
    private readonly List<CorridorData> corridors = new List<CorridorData>();
    private readonly List<TJunctionPlan> tJunctions = new List<TJunctionPlan>();

    private Transform generatedRoot;

    // =========================================================
    // ROOM DATA
    // =========================================================

    private class RoomData
    {
        public Vector2Int grid;
        public Vector3 position;

        public float width;
        public float depth;

        public bool north;
        public bool south;
        public bool east;
        public bool west;

        public RoomData(Vector2Int grid, Vector3 position, float width, float depth)
        {
            this.grid = grid;
            this.position = position;
            this.width = width;
            this.depth = depth;
        }
    }

    // =========================================================
    // CORRIDOR DATA
    // =========================================================

    private class CorridorData
    {
        public RoomData a;
        public RoomData b;

        // Direction in which the corridor leaves room A.
        public Direction directionA;

        // Direction from which the corridor enters room B.
        public Direction directionB;

        public CorridorType type;
        public float length;

        // Exact connection sockets chosen during generation.
        public Vector3 startSocket;
        public Vector3 endSocket;

        // For L corridors this is the exact world-space turning point.
        // Storing it prevents generation-time and build-time route mismatch.
        public Vector3 corner;
    }

    // =========================================================
    // T JUNCTION PLAN
    // =========================================================

    private class TJunctionPlan
    {
        public Vector3 center;
        public Direction openA;
        public Direction openB;
        public Direction openC;
        public RoomData roomA;
        public RoomData roomB;
        public RoomData roomC;
    }

    // =========================================================
    // FRONTIER
    // =========================================================

    private class Frontier
    {
        public RoomData parent;
        public Vector2Int targetGrid;
        public CorridorType type;

        public Frontier(RoomData parent, Vector2Int targetGrid, CorridorType type)
        {
            this.parent = parent;
            this.targetGrid = targetGrid;
            this.type = type;
        }
    }

    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        if (generateOnStart)
        {
            Generate();
        }
    }

    // =========================================================
    // MAIN GENERATION
    // =========================================================

    [ContextMenu("Generate Level")]
    public void Generate()
    {
        if (clearBeforeGenerate)
        {
            ClearGenerated();
        }

        if (!ValidateSettings())
        {
            return;
        }

        random = new System.Random(CreateStableSeed(seed));

        rooms.Clear();
        corridors.Clear();
        tJunctions.Clear();

        generatedRoot = new GameObject("Generated Level").transform;
        generatedRoot.SetParent(transform, false);

        GenerateRooms();
        AddExtraConnections();
        BuildRealTJunctionPlan();

        BuildRooms();
        BuildCorridors();
        BuildTJunctions();

        Debug.Log(
            "=== PROCEDURAL GENERATION COMPLETE ===\n" +
            "Seed: " + seed + "\n" +
            "Rooms: " + rooms.Count + "\n" +
            "Corridors: " + corridors.Count
        );
    }

    // =========================================================
    // VALIDATION
    // =========================================================

    private bool ValidateSettings()
    {
        if (minRoomWidth > maxRoomWidth)
        {
            Debug.LogError("minRoomWidth > maxRoomWidth");
            return false;
        }

        if (minRoomDepth > maxRoomDepth)
        {
            Debug.LogError("minRoomDepth > maxRoomDepth");
            return false;
        }

        if (minCorridorLength > maxCorridorLength)
        {
            Debug.LogError("minCorridorLength > maxCorridorLength");
            return false;
        }

        if (corridorWidth <= 0f)
        {
            Debug.LogError("corridorWidth must be greater than zero.");
            return false;
        }

        if (doorwayWidth <= 0f ||
            doorwayWidth > Mathf.Min(minRoomWidth, minRoomDepth))
        {
            Debug.LogError(
                "doorwayWidth is too large for the smallest room."
            );
            return false;
        }

        // Cell size only needs to leave room for the largest room
        // plus a playable corridor gap.  The old formula included
        // maxCorridorLength, which made the grid unnecessarily huge.
        float recommendedCellSize =
            Mathf.Max(maxRoomWidth, maxRoomDepth) +
            corridorWidth +
            wallThickness * 2f +
            roomPadding * 2f +
            1f;

        if (cellSize < recommendedCellSize)
        {
            Debug.LogWarning(
                "Cell Size was too small. Automatically changed to " +
                recommendedCellSize
            );

            cellSize = recommendedCellSize;
        }

        // The corridor must not be narrower than the doorway.
        if (doorwayWidth < corridorWidth)
        {
            doorwayWidth = corridorWidth;
        }

        return true;
    }

    // =========================================================
    // ROOM GENERATION
    // =========================================================

    private void GenerateRooms()
    {
        RoomData first = CreateRoom(Vector2Int.zero);
        rooms.Add(first);

        List<Frontier> frontier = new List<Frontier>();

        AddFrontier(first, frontier);

        int attempts = 0;
        int maxAttempts = maxAttemptsPerRoom * Mathf.Max(roomCount, 1);

        while (rooms.Count < roomCount &&
               attempts < maxAttempts)
        {
            if (frontier.Count == 0)
            {
                RebuildFrontier(frontier);

                if (frontier.Count == 0)
                {
                    break;
                }
            }

            attempts++;

            int index = random.Next(frontier.Count);
            Frontier selected = frontier[index];
            frontier.RemoveAt(index);

            if (selected.parent == null)
            {
                continue;
            }

            if (CountConnections(selected.parent) >= maxConnectionsPerRoom)
            {
                continue;
            }

            if (FindRoom(selected.targetGrid) != null)
            {
                continue;
            }

            RoomData newRoom = CreateRoom(selected.targetGrid);

            if (IsRoomOverlapping(newRoom))
            {
                continue;
            }

            if (!TryConnectRooms(
                    selected.parent,
                    newRoom,
                    selected.type))
            {
                continue;
            }

            rooms.Add(newRoom);

            AddFrontier(newRoom, frontier);
        }

        if (rooms.Count < roomCount)
        {
            Debug.LogWarning(
                "Generation stopped at " +
                rooms.Count +
                "/" +
                roomCount +
                " rooms."
            );
        }
    }

    // =========================================================
    // FRONTIER
    // =========================================================

    private void AddFrontier(
        RoomData room,
        List<Frontier> frontier)
    {
        if (CountConnections(room) >= maxConnectionsPerRoom)
        {
            return;
        }

        List<Vector2Int> cardinal =
            new List<Vector2Int>
            {
                Vector2Int.up,
                Vector2Int.down,
                Vector2Int.right,
                Vector2Int.left
            };

        Shuffle(cardinal);

        bool firstCardinalAdded = false;

        foreach (Vector2Int offset in cardinal)
        {
            Vector2Int target = room.grid + offset;

            if (FindRoom(target) != null ||
                FrontierContains(frontier, target))
            {
                continue;
            }

            // Always keep at least one normal continuation.
            // Additional exits are controlled by branchChance.
            if (firstCardinalAdded &&
                random.NextDouble() > branchChance)
            {
                continue;
            }

            CorridorType type = ChooseStraightType();

            frontier.Add(
                new Frontier(
                    room,
                    target,
                    type
                )
            );

            firstCardinalAdded = true;
        }

        // L-corridors use diagonal rooms.  Before adding one to the
        // frontier, check that at least one of its two possible 90-degree
        // routes is free.  This prevents an L corridor from cutting through
        // an already generated room.
        if (random.NextDouble() <= lCorridorChance)
        {
            List<Vector2Int> diagonal =
                new List<Vector2Int>
                {
                    new Vector2Int(1, 1),
                    new Vector2Int(1, -1),
                    new Vector2Int(-1, 1),
                    new Vector2Int(-1, -1)
                };

            Shuffle(diagonal);

            foreach (Vector2Int offset in diagonal)
            {
                Vector2Int target = room.grid + offset;

                if (FindRoom(target) != null ||
                    FrontierContains(frontier, target))
                {
                    continue;
                }

                if (!HasFreeLRoute(room.grid, target))
                {
                    continue;
                }

                frontier.Add(
                    new Frontier(
                        room,
                        target,
                        CorridorType.L
                    )
                );

                // One L branch per room is enough.
                break;
            }
        }

        // Reduce excessive frontier growth.
        if (frontier.Count > roomCount * 4)
        {
            frontier.RemoveRange(
                roomCount * 4,
                frontier.Count - roomCount * 4
            );
        }
    }

    private void RebuildFrontier(List<Frontier> frontier)
    {
        foreach (RoomData room in rooms)
        {
            AddFrontier(room, frontier);
        }
    }

    private bool FrontierContains(
        List<Frontier> frontier,
        Vector2Int target)
    {
        foreach (Frontier item in frontier)
        {
            if (item.targetGrid == target)
            {
                return true;
            }
        }

        return false;
    }

    // =========================================================
    // ROOM CREATION
    // =========================================================

    private RoomData CreateRoom(Vector2Int grid)
    {
        float width = RandomRoomSize(minRoomWidth, maxRoomWidth);
        float depth = RandomRoomSize(minRoomDepth, maxRoomDepth);

        return new RoomData(
            grid,
            GridToWorld(grid),
            width,
            depth
        );
    }

    private float RandomRoomSize(float min, float max)
    {
        float value = (float)random.NextDouble();

        value = Mathf.Pow(value, 0.85f);

        float result = Mathf.Lerp(min, max, value);

        return Mathf.Round(result * 2f) / 2f;
    }

    // =========================================================
    // OVERLAP
    // =========================================================

    private bool IsRoomOverlapping(RoomData newRoom)
    {
        foreach (RoomData room in rooms)
        {
            float dx = Mathf.Abs(
                room.position.x - newRoom.position.x
            );

            float dz = Mathf.Abs(
                room.position.z - newRoom.position.z
            );

            float requiredX =
                (room.width + newRoom.width) / 2f +
                roomPadding;

            float requiredZ =
                (room.depth + newRoom.depth) / 2f +
                roomPadding;

            if (dx < requiredX && dz < requiredZ)
            {
                return true;
            }
        }

        return false;
    }

    // =========================================================
    // CONNECTION
    // =========================================================

    private bool TryConnectRooms(
        RoomData a,
        RoomData b,
        CorridorType type)
    {
        Vector2Int delta = b.grid - a.grid;

        // Normal corridor: adjacent cardinal cells.
        if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1)
        {
            Direction directionA = DirectionFromDelta(delta);
            Direction directionB = Opposite(directionA);

            if (IsDoorwayOccupied(a, directionA) ||
                IsDoorwayOccupied(b, directionB))
            {
                return false;
            }

            if (!CanUseStraightConnection(a, b, directionA, directionB))
            {
                return false;
            }

            ConnectRooms(
                a,
                b,
                directionA,
                directionB,
                type
            );

            return true;
        }

        // L corridor: diagonal cells only.
        if (Mathf.Abs(delta.x) == 1 &&
            Mathf.Abs(delta.y) == 1 &&
            type == CorridorType.L)
        {
            Direction directionA;
            Direction directionB;
            Vector3 corner;

            if (!TryChooseSafeLDirections(
                    a,
                    b,
                    delta,
                    out directionA,
                    out directionB,
                    out corner))
            {
                return false;
            }

            if (IsDoorwayOccupied(a, directionA) ||
                IsDoorwayOccupied(b, directionB))
            {
                return false;
            }

            if (!CanUseLConnection(
                    a,
                    b,
                    directionA,
                    directionB,
                    corner))
            {
                return false;
            }

            ConnectRooms(
                a,
                b,
                directionA,
                directionB,
                CorridorType.L,
                corner
            );

            return true;
        }

        return false;
    }

    private bool CanUseStraightConnection(
        RoomData a,
        RoomData b,
        Direction directionA,
        Direction directionB)
    {
        Vector3 start = GetDoorPoint(a, directionA);
        Vector3 end = GetDoorPoint(b, directionB);

        return IsAxisSegmentClear(
            start,
            end,
            a,
            b,
            null
        );
    }

    private bool CanUseLConnection(
        RoomData a,
        RoomData b,
        Direction directionA,
        Direction directionB,
        Vector3 junctionCenter)
    {
        Vector3 start = GetDoorPoint(a, directionA);
        Vector3 end = GetDoorPoint(b, directionB);

        float half = corridorWidth * 0.5f;

        // The L turn is a tiny square room exactly corridorWidth x corridorWidth.
        // The first corridor ends at one side of that square and the second
        // corridor starts at the adjacent side.
        Vector3 firstSocket =
            junctionCenter - DirectionToVector3(directionA) * half;

        Vector3 secondSocket =
            junctionCenter - DirectionToVector3(directionB) * half;

        if (Vector3.Distance(start, firstSocket) < 0.05f ||
            Vector3.Distance(secondSocket, end) < 0.05f)
        {
            return false;
        }

        if (!IsAxisSegmentClear(start, firstSocket, a, b, null))
        {
            return false;
        }

        if (!IsAxisSegmentClear(secondSocket, end, a, b, null))
        {
            return false;
        }

        if (!IsJunctionRoomClear(junctionCenter, a, b))
        {
            return false;
        }

        if (!IsJunctionClearOfExistingCorridors(junctionCenter, a, b))
        {
            return false;
        }

        return true;
    }

    private bool IsJunctionRoomClear(
        Vector3 center,
        RoomData a,
        RoomData b)
    {
        float half = corridorWidth * 0.5f + wallThickness + roomPadding;

        float minX = center.x - half;
        float maxX = center.x + half;
        float minZ = center.z - half;
        float maxZ = center.z + half;

        foreach (RoomData room in rooms)
        {
            // The junction square must be outside BOTH connected rooms.
            // It may touch a room boundary, but it must never overlap the
            // room interior. This prevents the "tiny room" from spawning
            // inside a large room on unlucky room-size combinations.
            float roomMinX = room.position.x - room.width * 0.5f;
            float roomMaxX = room.position.x + room.width * 0.5f;
            float roomMinZ = room.position.z - room.depth * 0.5f;
            float roomMaxZ = room.position.z + room.depth * 0.5f;

            if (RectsOverlap(
                    minX, maxX, minZ, maxZ,
                    roomMinX, roomMaxX, roomMinZ, roomMaxZ))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsJunctionClearOfExistingCorridors(
        Vector3 center,
        RoomData a,
        RoomData b)
    {
        float half = corridorWidth * 0.5f + wallThickness;

        float minX = center.x - half;
        float maxX = center.x + half;
        float minZ = center.z - half;
        float maxZ = center.z + half;

        foreach (CorridorData existing in corridors)
        {
            // Do not skip corridors merely because they share one of the
            // rooms. Another corridor can leave that same room and still
            // occupy the exact place where this junction would be.
            foreach (Segment2D segment in GetCorridorSegments(existing))
            {
                Rect2D rect = MakeCorridorRect(
                    segment.start,
                    segment.end
                );

                if (RectsOverlap(
                        minX, maxX, minZ, maxZ,
                        rect.minX, rect.maxX,
                        rect.minZ, rect.maxZ))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool IsPointClear(
        Vector3 point,
        RoomData a,
        RoomData b)
    {
        float half = corridorWidth * 0.5f;

        foreach (RoomData room in rooms)
        {
            if (room == a || room == b)
            {
                continue;
            }

            if (PointInsideExpandedRoom(
                    point,
                    room,
                    half + wallThickness))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsCorridorRectClear(
        Vector3 start,
        Vector3 end,
        RoomData a,
        RoomData b)
    {
        Rect2D rect = MakeCorridorRect(start, end);

        foreach (RoomData room in rooms)
        {
            if (room == a || room == b)
            {
                continue;
            }

            float minX =
                room.position.x -
                room.width * 0.5f -
                wallThickness;

            float maxX =
                room.position.x +
                room.width * 0.5f +
                wallThickness;

            float minZ =
                room.position.z -
                room.depth * 0.5f -
                wallThickness;

            float maxZ =
                room.position.z +
                room.depth * 0.5f +
                wallThickness;

            if (RectsOverlap(
                    rect.minX,
                    rect.maxX,
                    rect.minZ,
                    rect.maxZ,
                    minX,
                    maxX,
                    minZ,
                    maxZ))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsAxisSegmentClear(
        Vector3 start,
        Vector3 end,
        RoomData a,
        RoomData b,
        CorridorData ignored)
    {
        start.y = 0f;
        end.y = 0f;

        float halfWidth = corridorWidth * 0.5f;
        float padding = halfWidth + wallThickness * 0.5f;

        // This generator only creates axis-aligned corridor legs.

        float minX = Mathf.Min(start.x, end.x) - padding;
        float maxX = Mathf.Max(start.x, end.x) + padding;
        float minZ = Mathf.Min(start.z, end.z) - padding;
        float maxZ = Mathf.Max(start.z, end.z) + padding;

        foreach (RoomData room in rooms)
        {
            if (room == a || room == b)
            {
                continue;
            }

            float roomMinX = room.position.x - room.width * 0.5f;
            float roomMaxX = room.position.x + room.width * 0.5f;
            float roomMinZ = room.position.z - room.depth * 0.5f;
            float roomMaxZ = room.position.z + room.depth * 0.5f;

            if (RectsOverlap(
                    minX,
                    maxX,
                    minZ,
                    maxZ,
                    roomMinX,
                    roomMaxX,
                    roomMinZ,
                    roomMaxZ))
            {
                return false;
            }
        }

        // Do not allow a new corridor to cut through an existing corridor.
        // Touching at the same room is fine; crossing in open space is not.
        foreach (CorridorData existing in corridors)
        {
            if (existing == ignored)
            {
                continue;
            }

            // A corridor is allowed to belong to the same room, but it is
            // NOT allowed to cross another corridor after leaving that room.
            // The previous code skipped every corridor sharing A or B, which
            // made the result dependent on generation order/seed.
            List<Segment2D> existingSegments = GetCorridorSegments(existing);

            foreach (Segment2D segment in existingSegments)
            {
                if (AxisSegmentsOverlap(
                        start,
                        end,
                        segment.start,
                        segment.end,
                        halfWidth))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool RectsOverlap(
        float minAX,
        float maxAX,
        float minAZ,
        float maxAZ,
        float minBX,
        float maxBX,
        float minBZ,
        float maxBZ)
    {
        return minAX < maxBX &&
               maxAX > minBX &&
               minAZ < maxBZ &&
               maxAZ > minBZ;
    }

    private bool PointInsideExpandedRoom(
        Vector3 point,
        RoomData room,
        float padding)
    {
        return point.x >= room.position.x - room.width * 0.5f - padding &&
               point.x <= room.position.x + room.width * 0.5f + padding &&
               point.z >= room.position.z - room.depth * 0.5f - padding &&
               point.z <= room.position.z + room.depth * 0.5f + padding;
    }

    private bool AxisSegmentsOverlap(
        Vector3 a1,
        Vector3 a2,
        Vector3 b1,
        Vector3 b2,
        float padding)
    {
        bool aHorizontal = Mathf.Abs(a2.x - a1.x) >= Mathf.Abs(a2.z - a1.z);
        bool bHorizontal = Mathf.Abs(b2.x - b1.x) >= Mathf.Abs(b2.z - b1.z);

        if (aHorizontal && bHorizontal)
        {
            if (Mathf.Abs(a1.z - b1.z) > corridorWidth + wallThickness)
            {
                return false;
            }

            float aMin = Mathf.Min(a1.x, a2.x) - padding;
            float aMax = Mathf.Max(a1.x, a2.x) + padding;
            float bMin = Mathf.Min(b1.x, b2.x) - padding;
            float bMax = Mathf.Max(b1.x, b2.x) + padding;

            return aMin < bMax && aMax > bMin;
        }

        if (!aHorizontal && !bHorizontal)
        {
            if (Mathf.Abs(a1.x - b1.x) > corridorWidth + wallThickness)
            {
                return false;
            }

            float aMin = Mathf.Min(a1.z, a2.z) - padding;
            float aMax = Mathf.Max(a1.z, a2.z) + padding;
            float bMin = Mathf.Min(b1.z, b2.z) - padding;
            float bMax = Mathf.Max(b1.z, b2.z) + padding;

            return aMin < bMax && aMax > bMin;
        }

        Vector3 h1 = aHorizontal ? a1 : b1;
        Vector3 h2 = aHorizontal ? a2 : b2;
        Vector3 v1 = aHorizontal ? b1 : a1;
        Vector3 v2 = aHorizontal ? b2 : a2;

        float hMin = Mathf.Min(h1.x, h2.x) - padding;
        float hMax = Mathf.Max(h1.x, h2.x) + padding;
        float vMin = Mathf.Min(v1.z, v2.z) - padding;
        float vMax = Mathf.Max(v1.z, v2.z) + padding;

        return v1.x >= hMin &&
               v1.x <= hMax &&
               h1.z >= vMin &&
               h1.z <= vMax;
    }

    private struct Segment2D
    {
        public Vector3 start;
        public Vector3 end;

        public Segment2D(Vector3 start, Vector3 end)
        {
            this.start = start;
            this.end = end;
        }
    }

    private List<Segment2D> GetCorridorSegments(CorridorData corridor)
    {
        List<Segment2D> result = new List<Segment2D>();

        Vector3 start = corridor.startSocket;
        Vector3 end = corridor.endSocket;

        if (corridor.type != CorridorType.L)
        {
            result.Add(new Segment2D(start, end));
            return result;
        }

        Vector3 corner = corridor.corner;

        result.Add(new Segment2D(start, corner));
        result.Add(new Segment2D(corner, end));

        return result;
    }

    private Vector3 GetDoorPoint(
        RoomData room,
        Direction direction)
    {
        return room.position +
               DirectionToVector3(direction) *
               GetRoomHalfExtent(room, direction);
    }

    private void ConnectRooms(
        RoomData a,
        RoomData b,
        Direction directionA,
        Direction directionB,
        CorridorType type,
        Vector3? forcedCorner = null)
    {
        SetDoorway(a, directionA);
        SetDoorway(b, directionB);

        float halfA = GetRoomHalfExtent(a, directionA);
        float halfB = GetRoomHalfExtent(b, directionB);

        Vector3 start =
            a.position +
            DirectionToVector3(directionA) * halfA;

        Vector3 end =
            b.position +
            DirectionToVector3(directionB) * halfB;

        float length = Vector3.Distance(start, end);

        Vector3 storedCorner = Vector3.zero;

        if (type == CorridorType.L)
        {
            storedCorner = forcedCorner.HasValue
                ? forcedCorner.Value
                : CalculateLCorner(start, end, directionA);
        }

        corridors.Add(
            new CorridorData
            {
                a = a,
                b = b,
                directionA = directionA,
                directionB = directionB,
                type = type,
                length = length,
                startSocket = start,
                endSocket = end,
                corner = storedCorner
            }
        );
    }

    private Vector3 CalculateLCorner(
        Vector3 start,
        Vector3 end,
        Direction directionA)
    {
        start.y = 0f;
        end.y = 0f;

        bool horizontalFirst =
            directionA == Direction.East ||
            directionA == Direction.West;

        return horizontalFirst
            ? new Vector3(end.x, 0f, start.z)
            : new Vector3(start.x, 0f, end.z);
    }

    // =========================================================
    // L DIRECTION SELECTION
    // =========================================================

    private bool TryChooseSafeLDirections(
        RoomData a,
        RoomData b,
        Vector2Int delta,
        out Direction directionA,
        out Direction directionB,
        out Vector3 corner)
    {
        directionA = Direction.East;
        directionB = Direction.West;
        corner = Vector3.zero;

        bool positiveX = delta.x > 0;
        bool positiveZ = delta.y > 0;

        Direction xA = positiveX ? Direction.East : Direction.West;
        Direction zA = positiveZ ? Direction.North : Direction.South;
        Direction zB = positiveZ ? Direction.South : Direction.North;
        Direction xB = positiveX ? Direction.West : Direction.East;

        // Route 1: horizontal first.
        Vector3 start = GetDoorPoint(a, xA);
        Vector3 end = GetDoorPoint(b, zB);
        Vector3 corner1 = new Vector3(end.x, 0f, start.z);

        bool route1 =
            FindRoom(a.grid + DirectionToVector(xA)) == null &&
            IsJunctionRoomClear(corner1, a, b) &&
            IsJunctionClearOfExistingCorridors(corner1, a, b) &&
            IsAxisSegmentClear(
                start,
                corner1 - DirectionToVector3(xA) * (corridorWidth * 0.5f),
                a,
                b,
                null) &&
            IsAxisSegmentClear(
                corner1 - DirectionToVector3(zB) * (corridorWidth * 0.5f),
                end,
                a,
                b,
                null);

        // Route 2: vertical first.
        start = GetDoorPoint(a, zA);
        end = GetDoorPoint(b, xB);
        Vector3 corner2 = new Vector3(start.x, 0f, end.z);

        bool route2 =
            FindRoom(a.grid + DirectionToVector(zA)) == null &&
            IsJunctionRoomClear(corner2, a, b) &&
            IsJunctionClearOfExistingCorridors(corner2, a, b) &&
            IsAxisSegmentClear(
                start,
                corner2 - DirectionToVector3(zA) * (corridorWidth * 0.5f),
                a,
                b,
                null) &&
            IsAxisSegmentClear(
                corner2 - DirectionToVector3(xB) * (corridorWidth * 0.5f),
                end,
                a,
                b,
                null);

        if (!route1 && !route2)
        {
            return false;
        }

        bool chooseRoute1 = route1 && (!route2 || random.NextDouble() < 0.5);

        if (chooseRoute1)
        {
            directionA = xA;
            directionB = zB;
            corner = corner1;
        }
        else
        {
            directionA = zA;
            directionB = xB;
            corner = corner2;
        }

        return true;
    }

    private bool HasFreeLRoute(
        Vector2Int from,
        Vector2Int target)
    {
        Vector2Int delta = target - from;

        if (Mathf.Abs(delta.x) != 1 ||
            Mathf.Abs(delta.y) != 1)
        {
            return false;
        }

        Vector2Int xMiddle =
            from + new Vector2Int(delta.x, 0);

        Vector2Int zMiddle =
            from + new Vector2Int(0, delta.y);

        // A diagonal L connection needs one completely free grid cell
        // between the two rooms.  Check both possible bend cells.
        bool xRouteFree =
            FindRoom(xMiddle) == null &&
            !GridCellUsedByCorridor(xMiddle);

        bool zRouteFree =
            FindRoom(zMiddle) == null &&
            !GridCellUsedByCorridor(zMiddle);

        return xRouteFree || zRouteFree;
    }

    private bool GridCellUsedByCorridor(Vector2Int grid)
    {
        Vector3 center = GridToWorld(grid);

        float halfCell = cellSize * 0.5f;
        float corridorHalf = corridorWidth * 0.5f + wallThickness;

        foreach (CorridorData corridor in corridors)
        {
            List<Segment2D> segments =
                GetCorridorSegments(corridor);

            foreach (Segment2D segment in segments)
            {
                float minX =
                    Mathf.Min(segment.start.x, segment.end.x) -
                    corridorHalf;

                float maxX =
                    Mathf.Max(segment.start.x, segment.end.x) +
                    corridorHalf;

                float minZ =
                    Mathf.Min(segment.start.z, segment.end.z) -
                    corridorHalf;

                float maxZ =
                    Mathf.Max(segment.start.z, segment.end.z) +
                    corridorHalf;

                // Only consider the central area of the grid cell.
                if (center.x >= minX &&
                    center.x <= maxX &&
                    center.z >= minZ &&
                    center.z <= maxZ)
                {
                    return true;
                }
            }
        }

        return false;
    }

    // =========================================================
    // REAL T JUNCTIONS
    // =========================================================

    private void BuildRealTJunctionPlan()
    {
        if (tCorridorChance <= 0f || rooms.Count < 3)
        {
            return;
        }

        // A T is represented by a tiny room occupying an EMPTY grid cell.
        // Three real rooms must be directly adjacent to that cell. This is
        // deliberately simple: all three legs are cardinal, so every leg
        // terminates exactly at a real room doorway.
        HashSet<Vector2Int> candidateCells = new HashSet<Vector2Int>();

        foreach (RoomData room in rooms)
        {
            candidateCells.Add(room.grid + Vector2Int.up);
            candidateCells.Add(room.grid + Vector2Int.down);
            candidateCells.Add(room.grid + Vector2Int.left);
            candidateCells.Add(room.grid + Vector2Int.right);
        }

        List<Vector2Int> candidates =
            new List<Vector2Int>(candidateCells);

        Shuffle(candidates);

        int desired = Mathf.Max(
            1,
            Mathf.RoundToInt(roomCount * tCorridorChance * 0.5f)
        );

        int created = 0;

        foreach (Vector2Int junctionGrid in candidates)
        {
            if (created >= desired)
            {
                break;
            }

            if (random.NextDouble() > tCorridorChance)
            {
                continue;
            }

            // The junction cell itself must remain empty.
            if (FindRoom(junctionGrid) != null)
            {
                continue;
            }

            List<Direction> available =
                new List<Direction>();

            foreach (Direction direction in GetShuffledDirections())
            {
                RoomData room = FindRoom(
                    junctionGrid + DirectionToVector(direction)
                );

                if (room == null)
                {
                    continue;
                }

                // The room is on 'direction' from the junction, therefore
                // its doorway is on the opposite side.
                if (IsDoorwayOccupied(
                        room,
                        Opposite(direction)))
                {
                    continue;
                }

                available.Add(direction);
            }

            // Exactly three open sides = T.
            if (available.Count < 3)
            {
                continue;
            }

            Direction openA = available[0];
            Direction openB = available[1];
            Direction openC = available[2];

            RoomData roomA = FindRoom(
                junctionGrid + DirectionToVector(openA)
            );
            RoomData roomB = FindRoom(
                junctionGrid + DirectionToVector(openB)
            );
            RoomData roomC = FindRoom(
                junctionGrid + DirectionToVector(openC)
            );

            if (roomA == null || roomB == null || roomC == null)
            {
                continue;
            }

            Vector3 center = GridToWorld(junctionGrid);

            bool tooCloseToExistingT = false;
            foreach (TJunctionPlan existing in tJunctions)
            {
                if (Vector3.Distance(existing.center, center) < cellSize * 0.75f)
                {
                    tooCloseToExistingT = true;
                    break;
                }
            }

            if (tooCloseToExistingT)
            {
                continue;
            }

            // The 3x3 junction room must not overlap any large room.
            if (!IsJunctionRoomClear(center, roomA, roomB) ||
                !IsJunctionRoomClear(center, roomA, roomC) ||
                !IsJunctionRoomClear(center, roomB, roomC))
            {
                continue;
            }

            // The junction square must be completely free.
            // A simple point/bounding-box test is not enough because an
            // existing corridor can run exactly along one edge of the square
            // and its walls would still overlap the T room.
            if (!IsTJunctionFootprintClear(center))
            {
                continue;
            }

            bool clear = true;

            foreach (Direction direction in new[]
            {
                openA,
                openB,
                openC
            })
            {
                RoomData room = FindRoom(
                    junctionGrid + DirectionToVector(direction)
                );

                Vector3 roomSocket = GetDoorPoint(
                    room,
                    Opposite(direction)
                );

                Vector3 junctionSocket =
                    center -
                    DirectionToVector3(direction) *
                    (corridorWidth * 0.5f);

                if (!IsAxisSegmentClear(
                        roomSocket,
                        junctionSocket,
                        room,
                        null,
                        null))
                {
                    clear = false;
                    break;
                }
            }

            if (!clear)
            {
                continue;
            }

            // Reserve the three real doorways BEFORE adding the plan.
            // This prevents another T or extra connection from reusing them.
            SetDoorway(roomA, Opposite(openA));
            SetDoorway(roomB, Opposite(openB));
            SetDoorway(roomC, Opposite(openC));

            tJunctions.Add(
                new TJunctionPlan
                {
                    center = center,
                    openA = openA,
                    openB = openB,
                    openC = openC,
                    roomA = roomA,
                    roomB = roomB,
                    roomC = roomC
                }
            );

            created++;
        }
    }

    private bool IsTJunctionFootprintClear(Vector3 center)
    {
        // The physical T room is exactly corridorWidth x corridorWidth.
        // Add a small safety margin for wall thickness and floating point
        // contact. If any existing corridor occupies this square, do not
        // place a T junction here.
        float half = corridorWidth * 0.5f + wallThickness + 0.06f;

        float minX = center.x - half;
        float maxX = center.x + half;
        float minZ = center.z - half;
        float maxZ = center.z + half;

        foreach (CorridorData existing in corridors)
        {
            foreach (Segment2D segment in GetCorridorSegments(existing))
            {
                Rect2D rect = MakeCorridorRect(segment.start, segment.end);

                // Expand existing corridor by wall thickness because the
                // corridor's visible/physical walls occupy more space than
                // its floor rectangle.
                rect.minX -= wallThickness;
                rect.maxX += wallThickness;
                rect.minZ -= wallThickness;
                rect.maxZ += wallThickness;

                if (RectsOverlap(
                        minX, maxX, minZ, maxZ,
                        rect.minX, rect.maxX,
                        rect.minZ, rect.maxZ))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool HasExistingCorridorNear(
        Vector3 center,
        float radius)
    {
        foreach (CorridorData corridor in corridors)
        {
            foreach (Segment2D segment in
                     GetCorridorSegments(corridor))
            {
                float minX =
                    Mathf.Min(segment.start.x, segment.end.x) - radius;
                float maxX =
                    Mathf.Max(segment.start.x, segment.end.x) + radius;
                float minZ =
                    Mathf.Min(segment.start.z, segment.end.z) - radius;
                float maxZ =
                    Mathf.Max(segment.start.z, segment.end.z) + radius;

                if (center.x >= minX &&
                    center.x <= maxX &&
                    center.z >= minZ &&
                    center.z <= maxZ)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void BuildTJunctions()
    {
        foreach (TJunctionPlan plan in tJunctions)
        {
            // IMPORTANT: the T is built as ONE continuous floor/wall system.
            // We do not create three independent corridors and a fourth
            // little room anymore. That old approach allowed corridor walls
            // to survive through the intersection.
            BuildTShapedCorridor(plan);
        }
    }

    // =========================================================
    // T SHAPED CORRIDOR - SINGLE CONTINUOUS GEOMETRY
    // =========================================================

    private void BuildTShapedCorridor(TJunctionPlan plan)
    {
        GameObject tObject = new GameObject("T_Corridor");
        tObject.transform.SetParent(generatedRoot, false);
        // Generation coordinates are absolute world-space coordinates in
        // this generator, so keep the T parent at world origin.
        tObject.transform.position = Vector3.zero;

        Vector3 center = plan.center;
        float half = corridorWidth * 0.5f;

        // The central square belongs to the T itself.
        CreateBlock(
            tObject.transform,
            "T_Center_Floor",
            center + Vector3.down * (floorThickness * 0.5f),
            new Vector3(corridorWidth, floorThickness, corridorWidth)
        );

        // Build each of the three arms only up to the edge of the central
        // square. There is deliberately NO overlap into the center.
        BuildTArmFloorAndWalls(
            tObject.transform,
            plan.roomA,
            plan.center,
            plan.openA,
            "T_Arm_A"
        );

        BuildTArmFloorAndWalls(
            tObject.transform,
            plan.roomB,
            plan.center,
            plan.openB,
            "T_Arm_B"
        );

        BuildTArmFloorAndWalls(
            tObject.transform,
            plan.roomC,
            plan.center,
            plan.openC,
            "T_Arm_C"
        );

        // The three connected directions stay open. The fourth side is the
        // actual closed end of the T.
        Direction closedSide = GetClosedTSide(
            plan.openA,
            plan.openB,
            plan.openC
        );

        CreateTBoundaryWall(
            tObject.transform,
            center,
            closedSide
        );
    }

    private void BuildTArmFloorAndWalls(
        Transform parent,
        RoomData room,
        Vector3 center,
        Direction openSide,
        string name)
    {
        Direction roomDoor = Opposite(openSide);
        Vector3 start = GetDoorPoint(room, roomDoor);

        Vector3 dir = DirectionToVector3(openSide);
        Vector3 end = center - dir * (corridorWidth * 0.5f);

        start.y = 0f;
        end.y = 0f;

        float length = Vector3.Distance(start, end);

        if (length <= 0.05f)
        {
            return;
        }

        Vector3 armCenter = (start + end) * 0.5f;
        armCenter.y = 0f;

        GameObject arm = new GameObject(name);
        arm.transform.SetParent(parent, false);
        arm.transform.position = armCenter;
        arm.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

        // Floor. The arm stops exactly at the central square.
        CreateBlock(
            arm.transform,
            "Floor",
            new Vector3(0f, -floorThickness * 0.5f, 0f),
            new Vector3(corridorWidth, floorThickness, length)
        );

        // Only the two OUTER side walls belong to this arm. They stop at the
        // central square, so no wall can cross the T intersection.
        CreateBlock(
            arm.transform,
            "Wall_Left",
            new Vector3(
                -corridorWidth * 0.5f,
                roomHeight * 0.5f,
                0f
            ),
            new Vector3(
                wallThickness,
                roomHeight,
                length
            )
        );

        CreateBlock(
            arm.transform,
            "Wall_Right",
            new Vector3(
                corridorWidth * 0.5f,
                roomHeight * 0.5f,
                0f
            ),
            new Vector3(
                wallThickness,
                roomHeight,
                length
            )
        );
    }

    private Direction GetClosedTSide(
        Direction a,
        Direction b,
        Direction c)
    {
        if (a != Direction.North &&
            b != Direction.North &&
            c != Direction.North)
        {
            return Direction.North;
        }

        if (a != Direction.South &&
            b != Direction.South &&
            c != Direction.South)
        {
            return Direction.South;
        }

        if (a != Direction.East &&
            b != Direction.East &&
            c != Direction.East)
        {
            return Direction.East;
        }

        return Direction.West;
    }

    private void CreateTBoundaryWall(
        Transform parent,
        Vector3 center,
        Direction direction)
    {
        float half = corridorWidth * 0.5f;
        Vector3 position;
        Vector3 scale;

        switch (direction)
        {
            case Direction.North:
                position = center + Vector3.forward * half;
                scale = new Vector3(
                    corridorWidth,
                    roomHeight,
                    wallThickness
                );
                break;

            case Direction.South:
                position = center + Vector3.back * half;
                scale = new Vector3(
                    corridorWidth,
                    roomHeight,
                    wallThickness
                );
                break;

            case Direction.East:
                position = center + Vector3.right * half;
                scale = new Vector3(
                    wallThickness,
                    roomHeight,
                    corridorWidth
                );
                break;

            default:
                position = center + Vector3.left * half;
                scale = new Vector3(
                    wallThickness,
                    roomHeight,
                    corridorWidth
                );
                break;
        }

        // The T object is intentionally kept at world origin, so the
        // generation-space coordinate can be used directly as local position.
        Vector3 local = position;

        CreateBlock(
            parent,
            "T_Closed_Wall",
            local,
            scale
        );
    }

    private void BuildTJunctionRoom(
        TJunctionPlan plan)
    {
        GameObject junction =
            new GameObject("T_Junction_Room");

        junction.transform.SetParent(
            generatedRoot,
            false
        );

        junction.transform.position =
            plan.center;

        float size = corridorWidth;

        // Exactly the same physical construction as the working L junction:
        // a tiny square room with floor + walls. Only three sides are open.
        CreateBlock(
            junction.transform,
            "Floor",
            new Vector3(
                0f,
                -floorThickness * 0.5f,
                0f
            ),
            new Vector3(
                size,
                floorThickness,
                size
            )
        );

        bool northOpen =
            plan.openA == Direction.North ||
            plan.openB == Direction.North ||
            plan.openC == Direction.North;

        bool southOpen =
            plan.openA == Direction.South ||
            plan.openB == Direction.South ||
            plan.openC == Direction.South;

        bool eastOpen =
            plan.openA == Direction.East ||
            plan.openB == Direction.East ||
            plan.openC == Direction.East;

        bool westOpen =
            plan.openA == Direction.West ||
            plan.openB == Direction.West ||
            plan.openC == Direction.West;

        if (!northOpen)
        {
            CreateJunctionWall(
                junction.transform,
                Direction.North,
                size
            );
        }

        if (!southOpen)
        {
            CreateJunctionWall(
                junction.transform,
                Direction.South,
                size
            );
        }

        if (!eastOpen)
        {
            CreateJunctionWall(
                junction.transform,
                Direction.East,
                size
            );
        }

        if (!westOpen)
        {
            CreateJunctionWall(
                junction.transform,
                Direction.West,
                size
            );
        }
    }

    private void CreateJunctionWall(
        Transform parent,
        Direction direction,
        float size)
    {
        Vector3 position;
        Vector3 scale;

        switch (direction)
        {
            case Direction.North:
                position = new Vector3(
                    0f,
                    roomHeight * 0.5f,
                    size * 0.5f
                );
                scale = new Vector3(
                    size,
                    roomHeight,
                    wallThickness
                );
                break;

            case Direction.South:
                position = new Vector3(
                    0f,
                    roomHeight * 0.5f,
                    -size * 0.5f
                );
                scale = new Vector3(
                    size,
                    roomHeight,
                    wallThickness
                );
                break;

            case Direction.East:
                position = new Vector3(
                    size * 0.5f,
                    roomHeight * 0.5f,
                    0f
                );
                scale = new Vector3(
                    wallThickness,
                    roomHeight,
                    size
                );
                break;

            default:
                position = new Vector3(
                    -size * 0.5f,
                    roomHeight * 0.5f,
                    0f
                );
                scale = new Vector3(
                    wallThickness,
                    roomHeight,
                    size
                );
                break;
        }

        CreateBlock(
            parent,
            direction + "Wall",
            position,
            scale
        );
    }

    private void CreateTLeg(
        RoomData room,
        Vector3 junctionCenter,
        Direction openSide,
        string name)
    {
        Direction roomDoor = Opposite(openSide);

        Vector3 roomSocket = GetDoorPoint(
            room,
            roomDoor
        );

        // The junction room owns the entire square intersection.
        // Therefore the T corridor must stop EXACTLY at the junction
        // boundary instead of using the normal corridor overlap.
        Vector3 junctionSocket =
            junctionCenter -
            DirectionToVector3(openSide) *
            (corridorWidth * 0.5f);

        CreateTLegSegmentBetween(
            roomSocket,
            junctionSocket,
            name
        );
    }

    private void CreateTLegSegmentBetween(
        Vector3 start,
        Vector3 end,
        string name)
    {
        start.y = 0f;
        end.y = 0f;

        Vector3 delta = end - start;
        delta.y = 0f;

        float length = delta.magnitude;

        if (length < 0.05f)
        {
            return;
        }

        GameObject corridor = new GameObject(name);
        corridor.transform.SetParent(generatedRoot, false);
        corridor.transform.position = (start + end) * 0.5f;
        corridor.transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);

        // IMPORTANT: no overlap here. V1 used the normal corridor helper,
        // which deliberately extends floor/walls by 0.05 at both ends.
        // That is useful for ordinary corridors, but at a T junction it
        // makes the three legs physically overlap the junction room.
        CreateBlock(
            corridor.transform,
            "Floor",
            new Vector3(0f, -floorThickness * 0.5f, 0f),
            new Vector3(corridorWidth, floorThickness, length)
        );

        CreateBlock(
            corridor.transform,
            "Wall_Left",
            new Vector3(-corridorWidth * 0.5f, roomHeight * 0.5f, 0f),
            new Vector3(wallThickness, roomHeight, length)
        );

        CreateBlock(
            corridor.transform,
            "Wall_Right",
            new Vector3(corridorWidth * 0.5f, roomHeight * 0.5f, 0f),
            new Vector3(wallThickness, roomHeight, length)
        );
    }

    // =========================================================
    // EXTRA CONNECTIONS
    // =========================================================

    private void AddExtraConnections()
    {
        if (extraConnectionChance <= 0f)
        {
            return;
        }

        List<RoomData> shuffled =
            new List<RoomData>(rooms);

        Shuffle(shuffled);

        foreach (RoomData room in shuffled)
        {
            if (CountConnections(room) >= maxConnectionsPerRoom)
            {
                continue;
            }

            if (random.NextDouble() > extraConnectionChance)
            {
                continue;
            }

            List<Direction> directions =
                GetShuffledDirections();

            foreach (Direction direction in directions)
            {
                RoomData other =
                    FindRoom(
                        room.grid +
                        DirectionToVector(direction)
                    );

                if (other == null)
                {
                    continue;
                }

                if (HasConnection(room, other))
                {
                    continue;
                }

                if (CountConnections(other) >= maxConnectionsPerRoom)
                {
                    continue;
                }

                // Extra connections must go through the same safety
                // validation as normal generation.  The old version called
                // ConnectRooms() directly, which could create corridors
                // through existing corridors and produce broken junctions
                // on only some seeds.
                if (TryConnectRooms(
                        room,
                        other,
                        ChooseStraightType()))
                {
                    break;
                }
            }
        }
    }

    private bool HasConnection(RoomData a, RoomData b)
    {
        foreach (CorridorData corridor in corridors)
        {
            if ((corridor.a == a && corridor.b == b) ||
                (corridor.a == b && corridor.b == a))
            {
                return true;
            }
        }

        return false;
    }

    // =========================================================
    // BUILD ROOMS
    // =========================================================

    private void BuildRooms()
    {
        foreach (RoomData room in rooms)
        {
            GameObject roomObject =
                new GameObject(
                    "Room_" +
                    room.grid.x +
                    "_" +
                    room.grid.y
                );

            roomObject.transform.SetParent(
                generatedRoot,
                false
            );

            roomObject.transform.position =
                room.position;

            CreateBlock(
                roomObject.transform,
                "Floor",
                new Vector3(
                    0f,
                    -floorThickness / 2f,
                    0f
                ),
                new Vector3(
                    room.width,
                    floorThickness,
                    room.depth
                )
            );

            CreateNorthWall(
                roomObject.transform,
                room.north,
                room.width,
                room.depth
            );

            CreateSouthWall(
                roomObject.transform,
                room.south,
                room.width,
                room.depth
            );

            CreateEastWall(
                roomObject.transform,
                room.east,
                room.width,
                room.depth
            );

            CreateWestWall(
                roomObject.transform,
                room.west,
                room.width,
                room.depth
            );
        }
    }

    // =========================================================
    // ROOM WALLS
    // =========================================================

    private void CreateNorthWall(
        Transform parent,
        bool doorway,
        float width,
        float depth)
    {
        float z = depth / 2f;

        if (doorway)
        {
            CreateHorizontalWallWithDoor(
                parent,
                "NorthWall",
                z,
                width
            );
        }
        else
        {
            CreateBlock(
                parent,
                "NorthWall",
                new Vector3(
                    0f,
                    roomHeight / 2f,
                    z
                ),
                new Vector3(
                    width,
                    roomHeight,
                    wallThickness
                )
            );
        }
    }

    private void CreateSouthWall(
        Transform parent,
        bool doorway,
        float width,
        float depth)
    {
        float z = -depth / 2f;

        if (doorway)
        {
            CreateHorizontalWallWithDoor(
                parent,
                "SouthWall",
                z,
                width
            );
        }
        else
        {
            CreateBlock(
                parent,
                "SouthWall",
                new Vector3(
                    0f,
                    roomHeight / 2f,
                    z
                ),
                new Vector3(
                    width,
                    roomHeight,
                    wallThickness
                )
            );
        }
    }

    private void CreateEastWall(
        Transform parent,
        bool doorway,
        float width,
        float depth)
    {
        float x = width / 2f;

        if (doorway)
        {
            CreateVerticalWallWithDoor(
                parent,
                "EastWall",
                x,
                depth
            );
        }
        else
        {
            CreateBlock(
                parent,
                "EastWall",
                new Vector3(
                    x,
                    roomHeight / 2f,
                    0f
                ),
                new Vector3(
                    wallThickness,
                    roomHeight,
                    depth
                )
            );
        }
    }

    private void CreateWestWall(
        Transform parent,
        bool doorway,
        float width,
        float depth)
    {
        float x = -width / 2f;

        if (doorway)
        {
            CreateVerticalWallWithDoor(
                parent,
                "WestWall",
                x,
                depth
            );
        }
        else
        {
            CreateBlock(
                parent,
                "WestWall",
                new Vector3(
                    x,
                    roomHeight / 2f,
                    0f
                ),
                new Vector3(
                    wallThickness,
                    roomHeight,
                    depth
                )
            );
        }
    }

    // =========================================================
    // DOORWAYS
    // =========================================================

    private void CreateHorizontalWallWithDoor(
        Transform parent,
        string wallName,
        float z,
        float roomWidthValue)
    {
        float safeDoor =
            Mathf.Min(
                doorwayWidth,
                roomWidthValue - 0.1f
            );

        float side =
            (roomWidthValue - safeDoor) / 2f;

        CreateBlock(
            parent,
            wallName + "_Left",
            new Vector3(
                -(safeDoor + side) / 2f,
                roomHeight / 2f,
                z
            ),
            new Vector3(
                side,
                roomHeight,
                wallThickness
            )
        );

        CreateBlock(
            parent,
            wallName + "_Right",
            new Vector3(
                (safeDoor + side) / 2f,
                roomHeight / 2f,
                z
            ),
            new Vector3(
                side,
                roomHeight,
                wallThickness
            )
        );
    }

    private void CreateVerticalWallWithDoor(
        Transform parent,
        string wallName,
        float x,
        float roomDepthValue)
    {
        float safeDoor =
            Mathf.Min(
                doorwayWidth,
                roomDepthValue - 0.1f
            );

        float side =
            (roomDepthValue - safeDoor) / 2f;

        CreateBlock(
            parent,
            wallName + "_Bottom",
            new Vector3(
                x,
                roomHeight / 2f,
                -(safeDoor + side) / 2f
            ),
            new Vector3(
                wallThickness,
                roomHeight,
                side
            )
        );

        CreateBlock(
            parent,
            wallName + "_Top",
            new Vector3(
                x,
                roomHeight / 2f,
                (safeDoor + side) / 2f
            ),
            new Vector3(
                wallThickness,
                roomHeight,
                side
            )
        );
    }

    // =========================================================
    // BUILD CORRIDORS
    // =========================================================

    private void BuildCorridors()
    {
        foreach (CorridorData corridor in corridors)
        {
            if (corridor.type == CorridorType.L)
            {
                BuildLCorridor(corridor);
            }
            else
            {
                BuildStraightCorridor(corridor);
            }
        }
    }

    // =========================================================
    // STRAIGHT CORRIDOR
    // =========================================================

    private void BuildStraightCorridor(
        CorridorData corridor)
    {
        Vector3 start = corridor.startSocket;
        Vector3 end = corridor.endSocket;

        start.y = 0f;
        end.y = 0f;

        Vector3 segment =
            end - start;

        segment.y = 0f;

        float length = segment.magnitude;

        if (length < 0.1f)
        {
            return;
        }

        Vector3 direction =
            segment.normalized;

        Vector3 center =
            (start + end) / 2f;

        CreateCorridorSegment(
            center,
            direction,
            length,
            corridor.type + "_Connected"
        );
    }

    // =========================================================
    // L CORRIDOR
    // =========================================================

    private void BuildLCorridor(
        CorridorData corridor)
    {
        // L-corridor is deliberately implemented as:
        //
        // BIG ROOM -> CORRIDOR -> [tiny room] -> CORRIDOR -> BIG ROOM
        //
        // The tiny room is exactly corridorWidth x corridorWidth.
        // Two adjacent sides are completely open, giving a guaranteed
        // physical 90-degree turn.

        Vector3 start = corridor.startSocket;
        Vector3 end = corridor.endSocket;
        Vector3 center = corridor.corner;

        start.y = 0f;
        end.y = 0f;
        center.y = 0f;

        float half = corridorWidth * 0.5f;

        Vector3 firstSocket =
            center -
            DirectionToVector3(corridor.directionA) * half;

        Vector3 secondSocket =
            center -
            DirectionToVector3(corridor.directionB) * half;

        CreateCorridorSegmentBetween(
            start,
            firstSocket,
            "L_First_Corridor"
        );

        BuildLJunctionRoom(
            center,
            corridor.directionA,
            corridor.directionB
        );

        CreateCorridorSegmentBetween(
            secondSocket,
            end,
            "L_Second_Corridor"
        );
    }

    private void BuildLJunctionRoom(
        Vector3 center,
        Direction incoming,
        Direction outgoing)
    {
        GameObject junction =
            new GameObject("L_Junction_Room");

        junction.transform.SetParent(
            generatedRoot,
            false
        );

        junction.transform.position = center;

        float size = corridorWidth;

        CreateBlock(
            junction.transform,
            "Floor",
            new Vector3(
                0f,
                -floorThickness * 0.5f,
                0f
            ),
            new Vector3(
                size,
                floorThickness,
                size
            )
        );

        // IMPORTANT: directionA/directionB describe the direction FROM
        // the rooms toward the connection, not the side of the junction
        // that must be open. Therefore the junction opening is on the
        // OPPOSITE side.
        //
        // Example:
        // Room A --EAST--> junction
        // The junction must have its WEST wall open.
        //
        // This was the main V8 bug: V8 opened the same side as the incoming
        // direction, which put walls across the actual corridor sockets.
        Direction firstOpenSide = Opposite(incoming);
        Direction secondOpenSide = Opposite(outgoing);

        bool northOpen =
            firstOpenSide == Direction.North ||
            secondOpenSide == Direction.North;

        bool southOpen =
            firstOpenSide == Direction.South ||
            secondOpenSide == Direction.South;

        bool eastOpen =
            firstOpenSide == Direction.East ||
            secondOpenSide == Direction.East;

        bool westOpen =
            firstOpenSide == Direction.West ||
            secondOpenSide == Direction.West;

        if (!northOpen)
        {
            CreateBlock(
                junction.transform,
                "NorthWall",
                new Vector3(
                    0f,
                    roomHeight * 0.5f,
                    size * 0.5f
                ),
                new Vector3(
                    size,
                    roomHeight,
                    wallThickness
                )
            );
        }

        if (!southOpen)
        {
            CreateBlock(
                junction.transform,
                "SouthWall",
                new Vector3(
                    0f,
                    roomHeight * 0.5f,
                    -size * 0.5f
                ),
                new Vector3(
                    size,
                    roomHeight,
                    wallThickness
                )
            );
        }

        if (!eastOpen)
        {
            CreateBlock(
                junction.transform,
                "EastWall",
                new Vector3(
                    size * 0.5f,
                    roomHeight * 0.5f,
                    0f
                ),
                new Vector3(
                    wallThickness,
                    roomHeight,
                    size
                )
            );
        }

        if (!westOpen)
        {
            CreateBlock(
                junction.transform,
                "WestWall",
                new Vector3(
                    -size * 0.5f,
                    roomHeight * 0.5f,
                    0f
                ),
                new Vector3(
                    wallThickness,
                    roomHeight,
                    size
                )
            );
        }
    }

    private void CreateCorridorSegmentBetween(
        Vector3 start,
        Vector3 end,
        string name)
    {
        start.y = 0f;
        end.y = 0f;

        Vector3 delta = end - start;
        delta.y = 0f;

        float length = delta.magnitude;

        if (length < 0.05f)
        {
            return;
        }

        CreateCorridorSegment(
            (start + end) * 0.5f,
            delta.normalized,
            length,
            name
        );
    }

    private struct Rect2D
    {
        public float minX;
        public float maxX;
        public float minZ;
        public float maxZ;

        public Rect2D(
            float minX,
            float maxX,
            float minZ,
            float maxZ)
        {
            this.minX = minX;
            this.maxX = maxX;
            this.minZ = minZ;
            this.maxZ = maxZ;
        }
    }

    private struct Edge2D
    {
        public Vector3 a;
        public Vector3 b;

        public Edge2D(Vector3 a, Vector3 b)
        {
            this.a = a;
            this.b = b;
        }
    }

    private Rect2D MakeCorridorRect(
        Vector3 a,
        Vector3 b)
    {
        float half = corridorWidth * 0.5f;

        bool horizontal =
            Mathf.Abs(b.x - a.x) >=
            Mathf.Abs(b.z - a.z);

        if (horizontal)
        {
            return new Rect2D(
                Mathf.Min(a.x, b.x),
                Mathf.Max(a.x, b.x),
                a.z - half,
                a.z + half
            );
        }

        return new Rect2D(
            a.x - half,
            a.x + half,
            Mathf.Min(a.z, b.z),
            Mathf.Max(a.z, b.z)
        );
    }

    // =========================================================
    // CORRIDOR SEGMENT
    // =========================================================

    private void CreateCorridorSegment(
        Vector3 center,
        Vector3 direction,
        float length,
        string name)
    {
        if (length <= 0.05f)
        {
            return;
        }

        GameObject corridor =
            new GameObject(name);

        corridor.transform.SetParent(
            generatedRoot,
            false
        );

        corridor.transform.position =
            center;

        corridor.transform.rotation =
            Quaternion.LookRotation(
                direction,
                Vector3.up
            );

        // Slight overlap at the ends prevents tiny floor gaps.
        float overlap = 0.05f;
        float actualLength = length + overlap * 2f;

        // FLOOR
        CreateBlock(
            corridor.transform,
            "Floor",
            new Vector3(
                0f,
                -floorThickness / 2f,
                0f
            ),
            new Vector3(
                corridorWidth,
                floorThickness,
                actualLength
            )
        );

        // LEFT WALL
        CreateBlock(
            corridor.transform,
            "Wall_Left",
            new Vector3(
                -corridorWidth / 2f,
                roomHeight / 2f,
                0f
            ),
            new Vector3(
                wallThickness,
                roomHeight,
                actualLength
            )
        );

        // RIGHT WALL
        CreateBlock(
            corridor.transform,
            "Wall_Right",
            new Vector3(
                corridorWidth / 2f,
                roomHeight / 2f,
                0f
            ),
            new Vector3(
                wallThickness,
                roomHeight,
                actualLength
            )
        );
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private CorridorType ChooseStraightType()
    {
        float roll = (float)random.NextDouble();

        if (roll < 0.25f)
        {
            return CorridorType.Short;
        }

        if (roll < 0.70f)
        {
            return CorridorType.Straight;
        }

        return CorridorType.Long;
    }

    private int CountConnections(RoomData room)
    {
        int count = 0;

        if (room.north) count++;
        if (room.south) count++;
        if (room.east) count++;
        if (room.west) count++;

        return count;
    }

    private bool IsDoorwayOccupied(
        RoomData room,
        Direction direction)
    {
        switch (direction)
        {
            case Direction.North:
                return room.north;

            case Direction.South:
                return room.south;

            case Direction.East:
                return room.east;

            default:
                return room.west;
        }
    }

    private void SetDoorway(
        RoomData room,
        Direction direction)
    {
        switch (direction)
        {
            case Direction.North:
                room.north = true;
                break;

            case Direction.South:
                room.south = true;
                break;

            case Direction.East:
                room.east = true;
                break;

            case Direction.West:
                room.west = true;
                break;
        }
    }

    private Direction Opposite(Direction direction)
    {
        switch (direction)
        {
            case Direction.North:
                return Direction.South;

            case Direction.South:
                return Direction.North;

            case Direction.East:
                return Direction.West;

            default:
                return Direction.East;
        }
    }

    private Direction DirectionFromDelta(Vector2Int delta)
    {
        if (delta.x > 0) return Direction.East;
        if (delta.x < 0) return Direction.West;
        if (delta.y > 0) return Direction.North;

        return Direction.South;
    }

    private float GetRoomHalfExtent(
        RoomData room,
        Direction direction)
    {
        if (direction == Direction.East ||
            direction == Direction.West)
        {
            return room.width / 2f;
        }

        return room.depth / 2f;
    }

    private Vector2Int DirectionToVector(
        Direction direction)
    {
        switch (direction)
        {
            case Direction.North:
                return Vector2Int.up;

            case Direction.South:
                return Vector2Int.down;

            case Direction.East:
                return Vector2Int.right;

            default:
                return Vector2Int.left;
        }
    }

    private Vector3 DirectionToVector3(
        Direction direction)
    {
        switch (direction)
        {
            case Direction.North:
                return Vector3.forward;

            case Direction.South:
                return Vector3.back;

            case Direction.East:
                return Vector3.right;

            default:
                return Vector3.left;
        }
    }

    private Vector3 GridToWorld(Vector2Int grid)
    {
        return new Vector3(
            grid.x * cellSize,
            0f,
            grid.y * cellSize
        );
    }

    private RoomData FindRoom(Vector2Int grid)
    {
        foreach (RoomData room in rooms)
        {
            if (room.grid == grid)
            {
                return room;
            }
        }

        return null;
    }

    private List<Direction> GetShuffledDirections()
    {
        List<Direction> directions =
            new List<Direction>
            {
                Direction.North,
                Direction.South,
                Direction.East,
                Direction.West
            };

        Shuffle(directions);

        return directions;
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);

            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }

    private GameObject CreateBlock(
        Transform parent,
        string name,
        Vector3 localPosition,
        Vector3 localScale)
    {
        GameObject block =
            GameObject.CreatePrimitive(
                PrimitiveType.Cube
            );

        block.name = name;

        block.transform.SetParent(
            parent,
            false
        );

        block.transform.localPosition =
            localPosition;

        block.transform.localScale =
            localScale;

        return block;
    }

    // =========================================================
    // DEBUG SOCKETS
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        if (!showConnectionSockets || rooms == null)
        {
            return;
        }

        foreach (RoomData room in rooms)
        {
            DrawSocket(room, Direction.North, room.north);
            DrawSocket(room, Direction.South, room.south);
            DrawSocket(room, Direction.East, room.east);
            DrawSocket(room, Direction.West, room.west);
        }

        if (corridors == null)
        {
            return;
        }

        foreach (TJunctionPlan plan in tJunctions)
        {
            Vector3 c = plan.center + Vector3.up * 0.08f;

            Gizmos.DrawWireCube(
                c,
                new Vector3(corridorWidth, 0.1f, corridorWidth)
            );

            Gizmos.DrawLine(
                c,
                c + DirectionToVector3(plan.openA) * corridorWidth
            );
            Gizmos.DrawLine(
                c,
                c + DirectionToVector3(plan.openB) * corridorWidth
            );
            Gizmos.DrawLine(
                c,
                c + DirectionToVector3(plan.openC) * corridorWidth
            );
        }

        foreach (CorridorData corridor in corridors)
        {
            Vector3 a = corridor.startSocket;
            Vector3 b = corridor.endSocket;

            Gizmos.DrawLine(a + Vector3.up * 0.08f, b + Vector3.up * 0.08f);

            if (corridor.type == CorridorType.L)
            {
                Vector3 c = corridor.corner;
                Gizmos.DrawLine(a + Vector3.up * 0.08f, c + Vector3.up * 0.08f);
                Gizmos.DrawLine(c + Vector3.up * 0.08f, b + Vector3.up * 0.08f);
            }
        }
    }

    private void DrawSocket(RoomData room, Direction direction, bool used)
    {
        if (!used) return;

        Vector3 point = GetDoorPoint(room, direction);
        point.y = 0.15f;
        Gizmos.DrawSphere(point, Mathf.Max(0.12f, corridorWidth * 0.12f));
    }

    // =========================================================
    // STABLE SEED
    // =========================================================

    private int CreateStableSeed(string value)
    {
        unchecked
        {
            uint hash = 2166136261u;

            if (string.IsNullOrEmpty(value))
            {
                value = "DEFAULT";
            }

            foreach (char c in value)
            {
                hash ^= c;
                hash *= 16777619u;
            }

            return (int)hash;
        }
    }

    // =========================================================
    // CLEAR
    // =========================================================

    [ContextMenu("Clear Generated Level")]
    public void ClearGenerated()
    {
        Transform oldRoot =
            transform.Find("Generated Level");

        if (oldRoot == null)
        {
            return;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            DestroyImmediate(oldRoot.gameObject);
        }
        else
        {
            Destroy(oldRoot.gameObject);
        }
#else
        Destroy(oldRoot.gameObject);
#endif
    }
}
