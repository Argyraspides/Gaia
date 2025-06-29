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


using Gaia.Common.Utils.Godot;
using Gaia.Common.Utils.Logging;
using Godot;

public partial class SolarSystem : WorldEnvironment
{
  private MainCamera _camera;
  private Earth _earth;

  public override void _Ready()
  {
    base._Ready();
    _camera = GetNode<MainCamera>("MainCamera");

    LoadEarth();
  }

  private void LoadEarth()
  {
    if (!GodotUtils.IsValid(_camera))
    {
      this.LogError("SolarSystem::LoadEarth(): _camera not found!");
      return;
    }

    PackedScene sceneResource = GD.Load<PackedScene>("res://SolarSystem/Scenes/Earth/Earth.tscn");
    _earth = sceneResource.Instantiate<Earth>();

    _earth.Construct(_camera);
    AddChild(_earth);
  }

  private void AdjustCameraSpeed()
  {
  }
}
