using Gaia.PlanetEngine.LoDSystem;
using Gaia.PlanetEngine.MapTiles;
using Godot;

public partial class Earth : Node3D
{
  [Export] private bool _wireFrameActive;

  private MapTileType _mapTileType;

  private TerrainQuadTree _terrainQuadTree;

  // Use this when instantiating as a scene as the Instantiate<>() function bypasses the constructor
  public void Construct(MainCamera camera, MapTileType tileType = MapTileType.WebMercatorEarth)
  {
    _terrainQuadTree = new TerrainQuadTree(camera, _mapTileType);
    _mapTileType = tileType;
  }

  public override void _Process(double delta)
  {
    base._Process(delta);

    RenderingServer.SetDebugGenerateWireframes(_wireFrameActive);
    GetViewport().SetDebugDraw(
      _wireFrameActive ? Viewport.DebugDrawEnum.Wireframe : Viewport.DebugDrawEnum.Disabled);
  }

  public override void _Ready()
  {
    base._Ready();
    _terrainQuadTree.Name = "EarthTerrainQuadTree";
    AddChild(_terrainQuadTree);
    _terrainQuadTree.InitializeQuadTree(1);
  }
}
