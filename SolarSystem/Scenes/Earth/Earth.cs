using Godot;
using System;
using Gaia.PlanetEngine.LoDSystem;
using Gaia.PlanetEngine.MapTiles;

public partial class Earth : Node3D
{
    [Export] private bool wireFrameActive = false;

    private TerrainQuadTree m_terrainQuadTree;
    private Camera3D m_camera;
    private MapTileType m_mapTileType;

    // Use this when instantiating as a scene as the Instantiate<>() function bypasses the constructor
    public void Construct(Camera3D camera, MapTileType tileType = MapTileType.WEB_MERCATOR_EARTH)
    {
        // todo:: code smell ... why tf does the earth need a camera? This should be an exception in the
        // terrain quad tree...
        m_camera = camera ?? throw new NullReferenceException("Camera not set!");
        m_mapTileType = tileType;
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        RenderingServer.SetDebugGenerateWireframes(wireFrameActive);
        GetViewport().SetDebugDraw(
            wireFrameActive ? Viewport.DebugDrawEnum.Wireframe : Viewport.DebugDrawEnum.Disabled);
    }

    public override void _Ready()
    {
        base._Ready();
        m_terrainQuadTree = new TerrainQuadTree(m_camera, m_mapTileType);
        m_terrainQuadTree.Name = "EarthTerrainQuadTree";
        AddChild(m_terrainQuadTree);
        m_terrainQuadTree.InitializeQuadTree(1);
    }
}