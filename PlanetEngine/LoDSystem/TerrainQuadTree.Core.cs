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
    public int MaxDepth { get; private set; }
    public int MinDepth { get; private set; }

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
    { 1000, 500, 250, 125, 100, 90, 80, 75, 70, 55, 30, 25, 15 };

    public TerrainQuadTree(Camera3D camera, MapTileType tileType, int maxNodes = 1000,
        int minDepth = 0, int maxDepth = 20)
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
            
            TerrainQuadTreeNode n = CreateNode(latTileCoo, lonTileCoo, MinDepth);
            
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
        
        float worldWidth = 1000;
        float worldHeight = 1000;

        int zoomLevel = node.Chunk.MapTile.ZoomLevel;
        int tilesPerSide = (int) Math.Pow(2, zoomLevel);
        
        float trueTileWidth = worldWidth / tilesPerSide;
        float trueTileHeight = worldHeight / tilesPerSide;
        
        int latCoo = PlanetUtils.LatitudeToTileCoordinateMercator(node.Chunk.MapTile.Latitude, zoomLevel);
        int lonCoo = PlanetUtils.LongitudeToTileCoordinateMercator(node.Chunk.MapTile.Longitude, zoomLevel);
    
        float xCoo = (-worldWidth / 2) + (lonCoo + 0.5f) * trueTileWidth;
        float zCoo = (worldHeight / 2) - (latCoo + 0.5f) * trueTileHeight;
        
        node.Chunk.Scale = new Vector3(trueTileWidth, 1, trueTileHeight);
        node.GlobalPosition = new Vector3(xCoo, 0.0f, zCoo);
        node.GlobalPositionCpy = node.GlobalPosition;

        node.Chunk.MeshInstance = MeshGenerator.GenerateWebMercatorMesh();
        node.Chunk.Load();  
        
        node.AddToGroup(NodeGroupName);
    }

    private void InitializeAltitudeThresholds()
    {
        SplitThresholds = new double[MaxDepth + 1];
        MergeThresholds = new double[MaxDepth + 2];

        int it = Math.Min(MaxDepth, m_baseAltitudeThresholds.Length);

        for (int zoom = 0; zoom < it; zoom++)
        {
            SplitThresholds[zoom] = m_baseAltitudeThresholds[zoom];
        }

        for (int zoom = 1; zoom < MaxDepth; zoom++)
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
        int childLatTileCoo = parentLatTileCoo * 2 + ((childIndex == 2 || childIndex == 3) ? 1 : 0);
        int childLonTileCoo = parentLonTileCoo * 2 + ((childIndex == 1 || childIndex == 3) ? 1 : 0);
        return new Vector2I(childLonTileCoo, childLatTileCoo);
    }

    private TerrainQuadTreeNode CreateNode(int latTileCoo, int lonTileCoo, int zoomLevel)
    {
        double childCenterLat = PlanetUtils.ComputeCenterLatitudeWebMercator(latTileCoo, zoomLevel);
        double childCenterLon = PlanetUtils.ComputeCenterLongitudeWebMercator(lonTileCoo, zoomLevel);

        var childChunk =
            new TerrainChunk(new MapTile(
                        (float)childCenterLat,
                        (float)childCenterLon,
                        zoomLevel,
                        MapTileType.WEB_MERCATOR_EARTH)
            );
        childChunk.SetName("TerrainChunk");
        
        var terrainQuadTreeNode = new TerrainQuadTreeNode(childChunk, zoomLevel);
        
        terrainQuadTreeNode.SetName("TerrainQuadTreeNode");
        
        return terrainQuadTreeNode;
    }
}