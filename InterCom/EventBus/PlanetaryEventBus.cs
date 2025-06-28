using Gaia.Common.Utils.Logging;
using Godot;

namespace Gaia.InterCom.EventBus;

public partial class PlanetaryEventBus : Node
{
    [Signal]
    public delegate void TerrainQuadTreeLoadedEventHandler();

    public void OnTerrainQuadTreeLoaded()
    {
        EmitSignal(SignalName.TerrainQuadTreeLoaded);
    }
}