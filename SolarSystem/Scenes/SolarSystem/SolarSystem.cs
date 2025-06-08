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


using Godot;
using System;
using Gaia.Common.Utils.Godot;
using Gaia.Common.Utils.Logging;
using Gaia.PlanetEngine.MapTiles;

public partial class SolarSystem : WorldEnvironment
{
    private Camera3D m_camera;
    private Earth m_earth;

    public override void _Ready()
    {
        base._Ready();
        m_camera = GetNode<Camera3D>("MainCamera");
        
        LoadEarth();
    }

    private void LoadEarth()
    {
        if (!GodotUtils.IsValid(m_camera))
        {
            Logger.LogError("SolarSystem::LoadEarth(): m_camera not found!");
            return;
        }
        
        PackedScene sceneResource = GD.Load<PackedScene>("res://SolarSystem/Scenes/Earth/Earth.tscn");
        m_earth = sceneResource.Instantiate<Earth>();

        m_earth.Construct(m_camera, MapTileType.WEB_MERCATOR_EARTH);
    }
}