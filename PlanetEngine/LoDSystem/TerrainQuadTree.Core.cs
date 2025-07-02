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
using Gaia.SolarSystem.Scenes.SolarSystem;
using Godot;

namespace Gaia.PlanetEngine.LoDSystem;

public sealed partial class TerrainQuadTree : Node3D
{

  public TerrainQuadTree() { /* Godot needs an empty constructor */ }

  public TerrainQuadTree(
    LoDCamera camera,
    MapTileType tileType,
    int maxNodes = 10000,
    float worldSizeLat = PlanetUtils.EarthPolarCircumferenceKm,
    float worldSizeLon = PlanetUtils.EarthEquatorialCircumferenceKm)
  {
    if (maxNodes <= 0)
    {
      throw new ArgumentException("maxNodes must be positive");
    }

    if (tileType == MapTileType.Unknown)
    {
      throw new ArgumentException("Cannot make a LoD system with an unknown map tile type!");
    }

    _lodCamera = camera ?? throw new ArgumentNullException("TerrainQuadTree needs a camera!");
    _maxNodes = maxNodes;
    _mapTileType = tileType;
    _worldSizePolar = worldSizeLat;
    _worldSizeEquatorial = worldSizeLon;

    InitializeAltitudeThresholds();
    QuadTreeLoaded += GlobalEventBus.Instance.PlanetaryEventBus.OnTerrainQuadTreeLoaded;
    this.RegisterLogging(true);
  }


  [Signal]
  public delegate void QuadTreeLoadedEventHandler();

  private const int _maxQueueUpdatesPerFrame = 50;

  private const float _mergeThresholdFactor = 1.15F;

  private const int _maxDepthLimit = 23;
  private const int _minDepthLimit = 1;

  private const string _nodeGroupName = "TerrainQuadTreeNodes";
  private readonly LoDCamera _lodCamera;


  private readonly ManualResetEventSlim _canUpdateQuadTree = new(false);

  private bool _quadTreeLoaded;
  private ConcurrentQueue<TerrainQuadTreeNode> _mergeQueueNodes = new();
  private ConcurrentQueue<TerrainQuadTreeNode> _splitQueueNodes = new();

  // Size of the world this quadtree handles in the directions of latitude/longitude
  // of said world. E.g., Earth as a globe has WorldSizeLatKm as the circumference along lines of longitude
  private double _worldSizePolar;
  private double _worldSizeEquatorial;

  private const int _maxDepth = 20;
  private const int _minDepth = 0;
  private int _initDepth; // Depth we started at

  private MapTileType _mapTileType;

  private long _maxNodes;

  private double[] _splitThresholds;
  private double[] _mergeThresholds;
  private double[] _baseAltitudeThresholds;

  // List of maximum distances at each zoom level that you'd have to from the specified object
  // in order to still resolve a meaningful amount of detail. E.g., at 4000km from Earth, you can resolve
  // a meaningful amount of detail of Greenlands geographic features, or at 0.005km from Earth, you can resolve
  // the roof textures on houses to a meaningful level
  private double[][] _humanThresholdDistances = new double[_maxDepth][]
  {
    new double[] { 40000, 15000, 10000, 5000 },       // Zoom 0 – Earth, oceans, continents
    new double[] { 10000, 8000, 6000 },               // Zoom 1 – Continents, polar caps
    new double[] { 5000, 4000, 3500 },                // Zoom 2 – Subcontinents, Greenland
    new double[] { 3000, 2500, 2000 },                // Zoom 3 – Large countries, deserts
    new double[] { 2000, 1500, 1000 },                // Zoom 4 – Medium countries, rivers
    new double[] { 1000, 700, 500 },                  // Zoom 5 – Mid-size countries, mega-cities
    new double[] { 400, 300, 200 },                   // Zoom 6 – City shapes, peninsulas
    new double[] { 150, 120, 100 },                   // Zoom 7 – Medium cities, airports
    new double[] { 80, 60, 50 },                      // Zoom 8 – Small cities, urban blocks
    new double[] { 40, 30, 25 },                      // Zoom 9 – Towns, parks, suburbs
    new double[] { 15, 10, 8 },                       // Zoom 10 – Neighborhoods, street layout
    new double[] { 6, 5, 4 },                         // Zoom 11 – Residential grids, local roads
    new double[] { 3, 2.5, 2 },                       // Zoom 12 – Street blocks, parking lots
    new double[] { 1.5, 1.2, 1 },                     // Zoom 13 – Buildings, intersections
    new double[] { 0.8, 0.6, 0.5 },                   // Zoom 14 – Buildings separated, fences
    new double[] { 0.3, 0.2, 0.15 },                  // Zoom 15 – Car blobs, trees
    new double[] { 0.1, 0.08, 0.06 },                 // Zoom 16 – Car types, sidewalks
    new double[] { 0.04, 0.03, 0.025 },               // Zoom 17 – Parked cars, lane markings
    new double[] { 0.015, 0.01, 0.008 },              // Zoom 18 – Car types, benches, roof outlines
    new double[] { 0.005, 0.003, 0.002 }              // Zoom 19 – Roof textures, people, tree species
  };

  private List<TerrainQuadTreeNode> _rootNodes;

  // Reading inherent properties of nodes is not thread-safe in Godot. Here we make a custom Vector3
  // which is a copy of the camera's position updated from the TerrainQuadTree thread, so that it can
  // be accessed by the TerrainQuadTreeUpdater thread safely
  private Vector3 _cameraPosition;

  // If we hit x% of the maximum allowed amount of nodes, we will begin culling unused nodes in the quadtree
  private float _maxNodesCleanupThresholdPercent = 0.90F;

  public override void _Process(double delta)
  {
    _cameraPosition = _lodCamera.GlobalPosition;
    if (_canUpdateQuadTree.IsSet)
    {
      ProcessSplitQueue();
      ProcessMergeQueue();
    }

    if (_splitQueueNodes.IsEmpty && _mergeQueueNodes.IsEmpty)
    {
      _canUpdateQuadTree.Reset();
      _canPerformCulling.Set();
    }
  }

  public override void _ExitTree()
  {
    base._ExitTree();
    Stop();
  }

  public void InitializeQuadTree(int zoomLevel)
  {
    if (zoomLevel > _maxDepth || zoomLevel < _minDepth)
    {
      throw new ArgumentException($"zoomLevel must be between {_minDepth} and {_maxDepth}");
    }

    _initDepth = zoomLevel;
    _rootNodes = new List<TerrainQuadTreeNode>();

    int nodesPerSide = 1 << zoomLevel; // 2^z
    int nodesInLevel = nodesPerSide * nodesPerSide; // 4^z
    for (int i = 0; i < nodesInLevel; i++)
    {
      int latTileCoo = i / nodesPerSide;
      int lonTileCoo = i % nodesPerSide;

      TerrainQuadTreeNode n = CreateNode(latTileCoo, lonTileCoo, zoomLevel);

      n.IsDeepestVisible = true;
      n.Name = $"TerrainQuadTreeNode_{latTileCoo}_{lonTileCoo}";

      _rootNodes.Add(n);
      AddChild(n);

      n.Chunk.TerrainChunkLoaded += OnTerrainChunkLoaded;

      InitializeTerrainNode(n);
    }

    Start();
  }

  private void OnTerrainChunkLoaded()
  {
    int currNodeCt = GetTree().GetNodesInGroup(_nodeGroupName).Count;
    int nodesPerSide = 1 << _initDepth;
    int nodesInLevel = nodesPerSide * nodesPerSide;
    if (currNodeCt == nodesInLevel)
    {
      EmitSignal(SignalName.QuadTreeLoaded);
    }
  }

  private void Start()
  {
    _splitOrMergeSearchThread = new Thread(DetermineSplitOrMerge) { IsBackground = true, Name = "QuadTreeUpdateThread" };

    _cullThread = new Thread(StartCulling) { IsBackground = true, Name = "CullQuadTreeThread" };

    _splitOrMergeSearchThread.Start();
    _cullThread.Start();
    _canPerformSearch.Set();
    _isRunning = true;
  }

  private void Stop()
  {
    _isRunning = false;
    if (_splitOrMergeSearchThread != null && _splitOrMergeSearchThread.IsAlive)
    {
      _splitOrMergeSearchThread.Join(_threadJoinTimeoutMs);
    }

    if (_cullThread != null && _cullThread.IsAlive)
    {
      _cullThread.Join(_threadJoinTimeoutMs);
    }

    _canPerformCulling.Dispose();
    _canPerformSearch.Dispose();
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

    node.Chunk.MeshInstance = MeshGenerator.GenerateMesh(_mapTileType);
    node.AddToGroup(_nodeGroupName);
    node.Chunk.Load();

    PositionTerrainNode(node);
  }

  private void PositionTerrainNode(TerrainQuadTreeNode node)
  {
    switch (_mapTileType)
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

    double trueTileWidth = _worldSizeEquatorial / tilesPerSide;
    double trueTileHeight = _worldSizePolar / tilesPerSide;

    int latCoo = PlanetUtils.LatToTileCoo(_mapTileType, node.Chunk.MapTile.Latitude, zoomLevel);
    int lonCoo = PlanetUtils.LonToTileCoo(_mapTileType, node.Chunk.MapTile.Longitude, zoomLevel);

    double xCoo = (-_worldSizeEquatorial / 2) + ((lonCoo + 0.5f) * trueTileWidth);
    double zCoo = -((_worldSizePolar / 2) - ((latCoo + 0.5f) * trueTileHeight));

    node.Chunk.Scale = new Vector3((float)trueTileWidth, 1, (float)trueTileHeight);
    node.GlobalPosition = new Vector3((float)xCoo, 0.0f, (float)zCoo);
    node.GlobalPositionCpy = node.GlobalPosition;
  }

  private void PositionTerrainNodeGlobe(TerrainQuadTreeNode node) => throw new NotImplementedException();

  private void InitializeAltitudeThresholds()
  {
    _baseAltitudeThresholds = new double[_maxDepth]
    {
      156000.0f, 78000.0f, 39000.0f, 19500.0f, 9750.0f, 4875.0f, 2437.5f, 1218.75f, 609.375f, 304.6875f, 152.34f,
      76.17f, 38.08f, 19.04f, 9.52f, 4.76f, 2.38f, 1.2f, 0.6f, 0.35f
    };

    _splitThresholds = new double[_maxDepth + 1];
    _mergeThresholds = new double[_maxDepth + 2];

    for (int i = 0; i < _baseAltitudeThresholds.Length; i++)
    {
      _baseAltitudeThresholds[i] /= 2;
    }

    for (int zoom = 0; zoom < _baseAltitudeThresholds.Length; zoom++)
    {
      _splitThresholds[zoom] = _baseAltitudeThresholds[zoom];
    }

    for (int zoom = 1; zoom < _baseAltitudeThresholds.Length; zoom++)
    {
      _mergeThresholds[zoom] = _splitThresholds[zoom - 1] * _mergeThresholdFactor;
    }
  }

  private void InitializeCameraParams(LoDCamera camera) => camera.AltitudeThresholds = _baseAltitudeThresholds;

  private void ProcessSplitQueue()
  {
    int dequeuesProcessed = 0;

    while (_splitQueueNodes.TryDequeue(out TerrainQuadTreeNode node) &&
           dequeuesProcessed++ < _maxQueueUpdatesPerFrame)
    {
      SplitNode(node);
    }
  }

  private void ProcessMergeQueue()
  {
    int dequeuesProcessed = 0;
    while (_mergeQueueNodes.TryDequeue(out TerrainQuadTreeNode node) &&
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
    double childCenterLat = PlanetUtils.ComputeCenterLatitude(_mapTileType, latTileCoo, zoomLevel);
    double childCenterLon = PlanetUtils.ComputeCenterLongitude(_mapTileType, lonTileCoo, zoomLevel);

    var childChunk =
      new TerrainChunk(
        new MapTile(
          (float)childCenterLat,
          (float)childCenterLon,
          zoomLevel,
          _mapTileType)
      );
    childChunk.SetName("TerrainChunk");

    var terrainQuadTreeNode = new TerrainQuadTreeNode(childChunk, zoomLevel);

    terrainQuadTreeNode.SetName("TerrainQuadTreeNode");

    return terrainQuadTreeNode;
  }
}
