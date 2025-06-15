using Godot;
using System;
using Gaia.PlanetEngine.LoDSystem;
using Gaia.PlanetEngine.MapTiles;

public partial class Earth : Node3D
{
    private TerrainQuadTree m_terrainQuadTree;
    private Camera3D m_camera;
    private MapTileType m_mapTileType;

    // Use this when instantiating as a scene as the Instantiate<>() function bypasses the constructor
    public void Construct(Camera3D camera, MapTileType tileType = MapTileType.WEB_MERCATOR_EARTH)
    {
        m_camera = camera;
        m_mapTileType = tileType;  
    }

    public override void _Ready()
    {
        base._Ready();
        m_terrainQuadTree = new TerrainQuadTree(m_camera, m_mapTileType);
        AddChild(m_terrainQuadTree);
        m_terrainQuadTree.InitializeQuadTree(6);
    }
}