using Gaia.Common.Utils.Logging;
using Godot;

namespace Gaia.InterCom.EventBus;

public partial class PlanetaryEventBus : Node
{
    [Signal]
    public delegate void TerrainQuadTreeLoadedEventHandler();

    public void OnTerrainQuadTreeLoaded()
    {
        Logger.LogInfo("PlanetaryEventBus OnTerrainQuadTreeLoaded!!!!!!!!!");
        EmitSignal(SignalName.TerrainQuadTreeLoaded);
    }
}
