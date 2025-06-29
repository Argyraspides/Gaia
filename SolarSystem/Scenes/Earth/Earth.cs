using Gaia.PlanetEngine.LoDSystem;
using Gaia.PlanetEngine.MapTiles;
using Gaia.PlanetEngine.Utils;
using Gaia.SolarSystem.Scenes.SolarSystem;
using Godot;

public partial class Earth : Node3D
{
  [Export] private bool _wireFrameActive = false;

  private MapTileType _mapTileType;

  private TerrainQuadTree _terrainQuadTree;

  private double _equatorialCircumference;
  private double _polarCircumference;

  // Use this when instantiating as a scene as the Instantiate<>() function bypasses the constructor
  public void Construct(
    LoDCamera camera, MapTileType
    tileType = MapTileType.WebMercatorEarth,
    double equatorialCircumference = PlanetUtils.EarthEquatorialCircumferenceKm,
    double polarCircumference = PlanetUtils.EarthPolarCircumferenceKm
    )
  {
    _terrainQuadTree = new TerrainQuadTree(camera, _mapTileType);
    _mapTileType = tileType;
    _equatorialCircumference = equatorialCircumference;
    _polarCircumference = polarCircumference;
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
