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
using System.Threading;
using Gaia.Common.Utils.Godot;
using Gaia.Common.Utils.Logging;
using Gaia.InterCom.EventBus;
using Gaia.PlanetEngine.MapTiles;
using Gaia.PlanetEngine.MeshGenerators;
using Gaia.PlanetEngine.Utils;
using Godot;

namespace Gaia.PlanetEngine.LoDSystem;

public sealed partial class TerrainQuadTree : Node3D
{
  [Signal]
  public delegate void QuadTreeLoadedEventHandler();

  private const int _maxQueueUpdatesPerFrame = 50;

  private const float _mergeThresholdFactor = 1.15F;

  private const int _maxDepthLimit = 23;
  private const int _minDepthLimit = 1;

  private const string _nodeGroupName = "TerrainQuadTreeNodes";
  private readonly MainCamera _camera;

  public readonly double[] BaseAltitudeThresholds = new double[]
  {
    156_000.0f, 78_000.0f, 39_000.0f, 19_500.0f, 9_750.0f, 4_875.0f, 2_437.5f, 1_218.75f, 609.375f, 304.6875f,
    152.34f, 76.17f, 38.08f, 19.04f, 9.52f, 4.76f, 2.38f, 1.2f, 0.6f, 0.35f
  };

  private readonly ManualResetEventSlim _canUpdateQuadTree = new(false);

  private bool _quadTreeLoaded;
  public ConcurrentQueue<TerrainQuadTreeNode> MergeQueueNodes = new();
  public ConcurrentQueue<TerrainQuadTreeNode> SplitQueueNodes = new();

  public TerrainQuadTree() { }

  public TerrainQuadTree(
    MainCamera camera,
    MapTileType tileType,
    int maxNodes = 10000,
    int minDepth = 0,
    int maxDepth = 20,
    float worldSizeLat = PlanetUtils.EARTH_POLAR_CIRCUMFERENCE_KM,
    float worldSizeLon = PlanetUtils.EARTH_EQUATORIAL_CIRCUMFERENCE_KM)
  {
    if (maxDepth > _maxDepthLimit || maxDepth < _minDepthLimit)
    {
      throw new ArgumentException($"maxDepth must be between {_minDepthLimit} and {_maxDepthLimit}");
    }

    if (maxDepth < minDepth)
    {
      throw new ArgumentException("maxDepth must be greater than minDepth");
    }

    if (maxNodes <= 0)
    {
      throw new ArgumentException("maxNodes must be positive");
    }

    if (tileType == MapTileType.Unknown)
    {
      throw new ArgumentException("Cannot make a LoD system with an unknown map tile type!");
    }

    _camera = camera ?? throw new ArgumentNullException("TerrainQuadTree needs a camera!");
    MaxNodes = maxNodes;
    MinDepth = minDepth;
    MaxDepth = maxDepth;
    MapTileType = tileType;
    WorldSizeLatKm = worldSizeLat;
    WorldSizeLonKm = worldSizeLon;

    InitializeAltitudeThresholds();
    QuadTreeLoaded += GlobalEventBus.Instance.PlanetaryEventBus.OnTerrainQuadTreeLoaded;
    this.RegisterLogging(true);
  }

  // Size of the world this quadtree handles in the directions of latitude/longitude
  // of said world. E.g., Earth as a globe has WorldSizeLatKm as the circumference along lines of longitude
  public double WorldSizeLatKm { get; private set; }
  public double WorldSizeLonKm { get; private set; }
  public int MaxDepth { get; private set; }
  public int MinDepth { get; private set; }
  public int CurrDepth { get; private set; }

  public MapTileType MapTileType { get; private set; }

  public long MaxNodes { get; private set; }

  public double[] SplitThresholds { get; private set; }
  public double[] MergeThresholds { get; private set; }

  public List<TerrainQuadTreeNode> RootNodes { get; private set; }

  // Reading inherent properties of nodes is not thread-safe in Godot. Here we make a custom Vector3
  // which is a copy of the camera's position updated from the TerrainQuadTree thread, so that it can
  // be accessed by the TerrainQuadTreeUpdater thread safely
  public Vector3 CameraPosition { get; private set; }

  // If we hit x% of the maximum allowed amount of nodes, we will begin culling unused nodes in the quadtree
  public float MaxNodesCleanupThresholdPercent { get; private set; } = 0.90F;

  public override void _Process(double delta)
  {
    CameraPosition = _camera.GlobalPosition;
    if (_canUpdateQuadTree.IsSet)
    {
      ProcessSplitQueue();
      ProcessMergeQueue();
    }

    if (SplitQueueNodes.IsEmpty && MergeQueueNodes.IsEmpty)
    {
      _canUpdateQuadTree.Reset();
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

    CurrDepth = zoomLevel;
    RootNodes = new List<TerrainQuadTreeNode>();

    int nodesPerSide = 1 << zoomLevel; // 2^z
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

      n.Chunk.TerrainChunkLoaded += OnTerrainChunkLoaded;

      InitializeTerrainNode(n);
    }

    Start();
  }

  private void OnTerrainChunkLoaded()
  {
    int currNodeCt = GetTree().GetNodesInGroup(_nodeGroupName).Count;
    int nodesPerSide = 1 << CurrDepth;
    int nodesInLevel = nodesPerSide * nodesPerSide;
    if (currNodeCt == nodesInLevel)
    {
      EmitSignal(SignalName.QuadTreeLoaded);
    }
  }

  private void Start()
  {
    SplitOrMergeSearchThread = new Thread(DetermineSplitOrMerge) { IsBackground = true, Name = "QuadTreeUpdateThread" };

    CullThread = new Thread(StartCulling) { IsBackground = true, Name = "CullQuadTreeThread" };

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
      this.LogError("TerrainQuadTree::InitializeTerrainNodeMesh: Invalid terrain node!");
      return;
    }

    node.Chunk.MeshInstance = MeshGenerator.GenerateMesh(MapTileType);
    node.AddToGroup(_nodeGroupName);
    node.Chunk.Load();

    PositionTerrainNode(node);
  }

  private void PositionTerrainNode(TerrainQuadTreeNode node)
  {
    switch (MapTileType)
    {
      case MapTileType.WebMercatorEarth:
        PositionTerrainNodeFlat(node);
        break;
      case MapTileType.WebMercatorWgs84:
        PositionTerrainNodeGlobe(node);
        break;
    }
  }

  private void PositionTerrainNodeFlat(TerrainQuadTreeNode node)
  {
    int zoomLevel = node.Chunk.MapTile.ZoomLevel;
    int tilesPerSide = (int)Math.Pow(2, zoomLevel);

    double trueTileWidth = WorldSizeLonKm / tilesPerSide;
    double trueTileHeight = WorldSizeLatKm / tilesPerSide;

    int latCoo = PlanetUtils.LatitudeToTileCoordinate(MapTileType, node.Chunk.MapTile.Latitude, zoomLevel);
    int lonCoo = PlanetUtils.LongitudeToTileCoordinate(MapTileType, node.Chunk.MapTile.Longitude, zoomLevel);

    double xCoo = (-WorldSizeLonKm / 2) + ((lonCoo + 0.5f) * trueTileWidth);
    double zCoo = -((WorldSizeLatKm / 2) - ((latCoo + 0.5f) * trueTileHeight));

    node.Chunk.Scale = new Vector3((float)trueTileWidth, 1, (float)trueTileHeight);
    node.GlobalPosition = new Vector3((float)xCoo, 0.0f, (float)zCoo);
    node.GlobalPositionCpy = node.GlobalPosition;
  }

  private void PositionTerrainNodeGlobe(TerrainQuadTreeNode node) => throw new NotImplementedException();

  private void InitializeAltitudeThresholds()
  {
    SplitThresholds = new double[MaxDepth + 1];
    MergeThresholds = new double[MaxDepth + 2];

    for (int zoom = 0; zoom < BaseAltitudeThresholds.Length; zoom++)
    {
      // BaseAltitudeThresholds[zoom] /= 2;
      SplitThresholds[zoom] = BaseAltitudeThresholds[zoom];
    }

    for (int zoom = 1; zoom < BaseAltitudeThresholds.Length; zoom++)
    {
      MergeThresholds[zoom] = SplitThresholds[zoom - 1] * _mergeThresholdFactor;
    }
  }

  private void ProcessSplitQueue()
  {
    int dequeuesProcessed = 0;

    while (SplitQueueNodes.TryDequeue(out TerrainQuadTreeNode node) &&
           dequeuesProcessed++ < _maxQueueUpdatesPerFrame)
    {
      SplitNode(node);
    }
  }

  private void ProcessMergeQueue()
  {
    int dequeuesProcessed = 0;
    while (MergeQueueNodes.TryDequeue(out TerrainQuadTreeNode node) &&
           dequeuesProcessed++ < _maxQueueUpdatesPerFrame)
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
      foreach (TerrainQuadTreeNode childNode in node.ChildNodes)
      {
        InitializeTerrainNode(childNode);
      }
    }

    foreach (TerrainQuadTreeNode childNode in node.ChildNodes)
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

    foreach (TerrainQuadTreeNode childNode in parent.ChildNodes)
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
      newNode.Name =
        $"TerrainQuadTreeNode_{newNode.Chunk.MapTile.LatitudeTileCoo}_{newNode.Chunk.MapTile.LongitudeTileCoo}";
      parentNode.ChildNodes[i] = newNode;
      parentNode.AddChild(newNode);
    }
  }

  private Vector2I GetChildTileCoordinates(int parentLatTileCoo,
    int parentLonTileCoo, int childIndex)
  {
    // Formula for the tile coordinates of a child specifically in a quadtree structure.
    // Latitude is along the Y axis (normal cartesian system -- not godot), and longitude the X axis
    int childLatTileCoo = (parentLatTileCoo * 2) + (childIndex == 2 || childIndex == 3 ? 1 : 0);
    int childLonTileCoo = (parentLonTileCoo * 2) + (childIndex == 1 || childIndex == 3 ? 1 : 0);
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
