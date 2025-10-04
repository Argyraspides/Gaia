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


using Gaia.PlanetEngine.Utils;
using Godot;
using Daedalus.Logging;
using LoDCamera = Gaia.PlanetEngine.LoDSystem.LoDCamera;

public partial class SolarSystem : WorldEnvironment
{

  private LoDCamera _camera;

  private Earth _earth;
  private double _earthEquatorialCircumference
    = PlanetUtils.EarthEquatorialCircumferenceKm;
  private double _earthPolarCircumference
    = PlanetUtils.EarthPolarCircumferenceM;

  public override void _Ready()
  {
    base._Ready();
    Logger.RegisterLogging(this, true);
    _camera = GetNode<LoDCamera>("LoDCamera");

    LoadEarth();
  }

  private void LoadEarth()
  {
    if (!Daedalus.GodotUtils.GodotUtils.IsValid(_camera))
    {
      Logger.LogError(this,"SolarSystem::LoadEarth(): _camera not found!");
      return;
    }

    PackedScene sceneResource = GD.Load<PackedScene>("res://Core/SolarSystem/Scenes/Earth/Earth.tscn");
    _earth = sceneResource.Instantiate<Earth>();

    _earth.Construct(_camera);
    AddChild(_earth);
  }

}
