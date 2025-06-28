/*




    ,ad8888ba,         db         88         db
   d8"'    `"8b       d88b        88        d88b
  d8'                d8'`8b       88       d8'`8b
  88                d8'  `8b      88      d8'  `8b
  88      88888    d8YaaaaY8b     88     d8YaaaaY8b
  Y8,        88   d8""""""""8b    88    d8""""""""8b
   Y8a.    .a88  d8'        `8b   88   d8'        `8b
    `"Y88888P"  d8'          `8b  88  d8'          `8b

                    WEAVER OF WORLDS

*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using Gaia.Common.Utils.Godot;
using Gaia.Common.Utils.Logging;
using Gaia.PlanetEngine.MapTiles;
using Gaia.PlanetEngine.MeshGenerators;
using Gaia.PlanetEngine.Utils;
using Godot;

namespace Gaia.PlanetEngine.LoDSystem;

public sealed partial class TerrainQuadTree : Node3D
{
    
    // Size of the world this quadtree handles in the directions of latitude/longitude
    // of said world. E.g., Earth as a globe has WorldSizeLatKm as the circumference along lines of longitude 
    public double WorldSizeLatKm { get; private set; }
    public double WorldSizeLonKm { get; private set; }
    public int MaxDepth { get; private set; }
    public int MinDepth { get; private set; }
    
    public MapTileType MapTileType { get; private set; }
    
    public long MaxNodes { get; private set; }

    public double[] SplitThresholds { get; private set; }
    public double[] MergeThresholds { get; private set; }
    
    private ManualResetEventSlim CanUpdateQuadTree = new ManualResetEventSlim(false);

    public List<TerrainQuadTreeNode> RootNodes { get; private set; }

    // Reading inherent properties of nodes is not thread-safe in Godot. Here we make a custom Vector3
    // which is a copy of the camera's position updated from the TerrainQuadTree thread, so that it can
    // be accessed by the TerrainQuadTreeUpdater thread safely
    public Vector3 CameraPosition { get; private set; }
    private readonly Camera3D m_camera;

    public ConcurrentQueue<TerrainQuadTreeNode> SplitQueueNodes = new ConcurrentQueue<TerrainQuadTreeNode>();
    public ConcurrentQueue<TerrainQuadTreeNode> MergeQueueNodes = new ConcurrentQueue<TerrainQuadTreeNode>();

    // If we hit x% of the maximum allowed amount of nodes, we will begin culling unused nodes in the quadtree
    public float MaxNodesCleanupThresholdPercent { get; private set; } = 0.90F;

    private const int MaxQueueUpdatesPerFrame = 50;

    private const float MergeThresholdFactor = 1.15F;

    private const int MAX_DEPTH_LIMIT = 23;
    private const int MIN_DEPTH_LIMIT = 1;
    
    private const string NodeGroupName = "TerrainQuadTreeNodes";

    private readonly double[] m_baseAltitudeThresholds = new double[]
    {
        156000.0f, 78000.0f, 39000.0f, 19500.0f, 9750.0f, 4875.0f, 2437.5f, 1218.75f, 609.375f, 304.6875f, 152.34f,
        76.17f, 38.08f, 19.04f, 9.52f, 4.76f, 2.38f, 1.2f, 0.6f, 0.35f
    };

    public TerrainQuadTree(
        Camera3D camera, 
        MapTileType tileType, 
        int maxNodes = 10000,
        int minDepth = 0, 
        int maxDepth = 20,
        float worldSizeLat = PlanetUtils.EARTH_POLAR_CIRCUMFERENCE_KM,
        float worldSizeLon = PlanetUtils.EARTH_EQUATORIAL_CIRCUMFERENCE_KM)
    {
        if (maxDepth > MAX_DEPTH_LIMIT || maxDepth < MIN_DEPTH_LIMIT)
        {
            throw new ArgumentException($"maxDepth must be between {MIN_DEPTH_LIMIT} and {MAX_DEPTH_LIMIT}");
        }

        if (maxDepth < minDepth)
        {
            throw new ArgumentException("maxDepth must be greater than minDepth");
        }

        if (maxNodes <= 0)
        {
            throw new ArgumentException("maxNodes must be positive");
        }

        if (tileType == MapTileType.UNKNOWN)
        {
            throw new ArgumentException("Cannot make a LoD system with an unknown map tile type!");
        }

        m_camera = camera ?? throw new ArgumentNullException(nameof(camera));
        MaxNodes = maxNodes;
        MinDepth = minDepth;
        MaxDepth = maxDepth;
        MapTileType = tileType;
        WorldSizeLatKm = worldSizeLat;
        WorldSizeLonKm = worldSizeLon;

        InitializeAltitudeThresholds();
    }

    public override void _Process(double delta)
    {
        CameraPosition = m_camera.GlobalPosition;
        if (CanUpdateQuadTree.IsSet)
        {
            ProcessSplitQueue();
            ProcessMergeQueue();
        }

        if (SplitQueueNodes.IsEmpty && MergeQueueNodes.IsEmpty)
        {
            CanUpdateQuadTree.Reset();
            CanPerformCulling.Set();
        }
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        Stop();
    }

    public void InitializeQuadTree(int zoomLevel)
    {
        if (zoomLevel > MaxDepth || zoomLevel < MinDepth)
        {
            throw new ArgumentException($"zoomLevel must be between {MinDepth} and {MaxDepth}");
        }

        RootNodes = new List<TerrainQuadTreeNode>();

        int nodesPerSide = (1 << zoomLevel); // 2^z
        int nodesInLevel = nodesPerSide * nodesPerSide; // 4^z
        for (int i = 0; i < nodesInLevel; i++)
        {
            int latTileCoo = i / nodesPerSide;
            int lonTileCoo = i % nodesPerSide;
            
            TerrainQuadTreeNode n = CreateNode(latTileCoo, lonTileCoo, zoomLevel);
            
            n.IsDeepestVisible = true;
            n.Name = $"TerrainQuadTreeNode_{latTileCoo}_{lonTileCoo}";

            RootNodes.Add(n);
            AddChild(n);
            InitializeTerrainNode(n);
        }

        Start();
    }

    private void Start()
    {
        SplitOrMergeSearchThread = new Thread(DetermineSplitOrMerge)
        {
            IsBackground = true, Name = "QuadTreeUpdateThread"
        };

        CullThread = new Thread(StartCulling)
        {
            IsBackground = true, Name = "CullQuadTreeThread"
        };

        SplitOrMergeSearchThread.Start();
        CullThread.Start();
        CanPerformSearch.Set();
        m_isRunning = true;
    }

    private void Stop()
    {
        m_isRunning = false;
        if (SplitOrMergeSearchThread != null && SplitOrMergeSearchThread.IsAlive)
        {
            SplitOrMergeSearchThread.Join(THREAD_JOIN_TIMEOUT_MS);
        }

        if (CullThread != null && CullThread.IsAlive)
        {
            CullThread.Join(THREAD_JOIN_TIMEOUT_MS);
        }

        CanPerformCulling.Dispose();
        CanPerformSearch.Dispose();
    }
    
    ~TerrainQuadTree()
    {
        Stop();
    }

    private void InitializeTerrainNode(TerrainQuadTreeNode node)
    {
        if (!GodotUtils.IsValid(node))
        {
            Logger.LogError("TerrainQuadTree::InitializeTerrainNodeMesh: Invalid terrain node!");
            return;
        }

        node.Chunk.MeshInstance = MeshGenerator.GenerateMesh(MapTileType);
        node.Chunk.Load();  
        node.AddToGroup(NodeGroupName);
        
        PositionTerrainNode(node);
    }
    

    private void PositionTerrainNode(TerrainQuadTreeNode node)
    {
        switch (MapTileType)
        {
            case MapTileType.WEB_MERCATOR_EARTH:
                PositionTerrainNodeFlat(node);
                break;
            case MapTileType.WEB_MERCATOR_WGS84:
                PositionTerrainNodeGlobe(node);
                break;
        }
    }

    private void PositionTerrainNodeFlat(TerrainQuadTreeNode node)
    {
        int zoomLevel = node.Chunk.MapTile.ZoomLevel;
        int tilesPerSide = (int) Math.Pow(2, zoomLevel);
        
        double trueTileWidth = WorldSizeLonKm / tilesPerSide;
        double trueTileHeight = WorldSizeLatKm / tilesPerSide;
        
        int latCoo = PlanetUtils.LatitudeToTileCoordinate(MapTileType, node.Chunk.MapTile.Latitude, zoomLevel);
        int lonCoo = PlanetUtils.LongitudeToTileCoordinate(MapTileType, node.Chunk.MapTile.Longitude, zoomLevel);
    
        double xCoo = (-WorldSizeLonKm / 2) + (lonCoo + 0.5f) * trueTileWidth;
        double zCoo = -((WorldSizeLatKm / 2) - (latCoo + 0.5f) * trueTileHeight);
        
        node.Chunk.Scale = new Vector3((float)trueTileWidth, 1, (float)trueTileHeight);
        node.GlobalPosition = new Vector3((float)xCoo, 0.0f, (float)zCoo);
        node.GlobalPositionCpy = node.GlobalPosition;
    }

    private void PositionTerrainNodeGlobe(TerrainQuadTreeNode node)
    {
        throw new NotImplementedException();
    }

    private void InitializeAltitudeThresholds()
    {
        SplitThresholds = new double[MaxDepth + 1];
        MergeThresholds = new double[MaxDepth + 2];

        for (int zoom = 0; zoom < m_baseAltitudeThresholds.Length; zoom++)
        {
            SplitThresholds[zoom] = m_baseAltitudeThresholds[zoom];
        }

        for (int zoom = 1; zoom < m_baseAltitudeThresholds.Length; zoom++)
        {
            MergeThresholds[zoom] = SplitThresholds[zoom - 1] * MergeThresholdFactor;
        }
    }

    private void ProcessSplitQueue()
    {
        int dequeuesProcessed = 0;

        while (SplitQueueNodes.TryDequeue(out TerrainQuadTreeNode node) &&
               dequeuesProcessed++ < MaxQueueUpdatesPerFrame)
        {
            SplitNode(node);
        }
    }

    private void ProcessMergeQueue()
    {
        int dequeuesProcessed = 0;
        while (MergeQueueNodes.TryDequeue(out TerrainQuadTreeNode node) &&
               dequeuesProcessed++ < MaxQueueUpdatesPerFrame)
        {
            MergeNodeChildren(node);
        }
    }

    private void SplitNode(TerrainQuadTreeNode node)
    {
        if (!GodotUtils.IsValid(node))
        {
            return;
        }

        if (!node.HasAllChildren())
        {
            GenerateChildNodes(node);
            foreach (var childNode in node.ChildNodes)
            {
                InitializeTerrainNode(childNode);
            }
        }

        foreach (var childNode in node.ChildNodes)
        {
            childNode.IsDeepestVisible = true;
            childNode.Chunk.Visible = true;
        }

        node.IsDeepestVisible = false;
        node.Chunk.Visible = false;
    }

    private void MergeNodeChildren(TerrainQuadTreeNode parent)
    {
        if (!GodotUtils.IsValid(parent))
        {
            return;
        }

        parent.Chunk.Visible = true;
        parent.IsDeepestVisible = true;

        foreach (var childNode in parent.ChildNodes)
        {
            if (GodotUtils.IsValid(childNode))
            {
                childNode.Chunk.Visible = false;
                childNode.IsDeepestVisible = false;
            }
        }
    }

    private void GenerateChildNodes(TerrainQuadTreeNode parentNode)
    {
        // todo:: change to logerr
        if (!GodotUtils.IsValid(parentNode))
        {
            throw new ArgumentNullException(nameof(parentNode), "Cannot generate children for a null node.");
        }

        if (!GodotUtils.IsValid(parentNode.Chunk))
        {
            throw new ArgumentNullException(nameof(parentNode),
                "Cannot generate children for a node with a null terrain chunk.");
        }

        if (parentNode.Chunk.MapTile == null)
        {
            throw new ArgumentNullException(nameof(parentNode),
                "Cannot generate children for a node with a null map tile in its terrain chunk.");
        }

        int parentLatTileCoo = parentNode.Chunk.MapTile.LatitudeTileCoo;
        int parentLonTileCoo = parentNode.Chunk.MapTile.LongitudeTileCoo;
        int childZoomLevel = parentNode.Chunk.MapTile.ZoomLevel + 1;

        for (int i = 0; i < 4; i++)
        {
            Vector2I childCoos = GetChildTileCoordinates(parentLatTileCoo, parentLonTileCoo, i);
            TerrainQuadTreeNode newNode = CreateNode(childCoos.Y, childCoos.X, childZoomLevel);
            newNode.Name = $"TerrainQuadTreeNode_{newNode.Chunk.MapTile.LatitudeTileCoo}_{newNode.Chunk.MapTile.LongitudeTileCoo}";
            parentNode.ChildNodes[i] = newNode;
            parentNode.AddChild(newNode);
        }
    }

    private Vector2I GetChildTileCoordinates(int parentLatTileCoo,
        int parentLonTileCoo, int childIndex)
    {
        // Formula for the tile coordinates of a child specifically in a quadtree structure.
        // Latitude is along the Y axis (normal cartesian system -- not godot), and longitude the X axis
        int childLatTileCoo = parentLatTileCoo * 2 + ((childIndex == 2 || childIndex == 3) ? 1 : 0);
        int childLonTileCoo = parentLonTileCoo * 2 + ((childIndex == 1 || childIndex == 3) ? 1 : 0);
        return new Vector2I(childLonTileCoo, childLatTileCoo);
    }

    private TerrainQuadTreeNode CreateNode(int latTileCoo, int lonTileCoo, int zoomLevel)
    {
        double childCenterLat = PlanetUtils.ComputeCenterLatitude(MapTileType, latTileCoo, zoomLevel);
        double childCenterLon = PlanetUtils.ComputeCenterLongitude(MapTileType, lonTileCoo, zoomLevel);

        var childChunk = 
            new TerrainChunk(
                new MapTile(
                    (float)childCenterLat,
                    (float)childCenterLon,
                    zoomLevel,
                    MapTileType)
                );
        childChunk.SetName("TerrainChunk");
        
        var terrainQuadTreeNode = new TerrainQuadTreeNode(childChunk, zoomLevel);
        
        terrainQuadTreeNode.SetName("TerrainQuadTreeNode");
        
        return terrainQuadTreeNode;
    }
}