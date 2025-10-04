using Gaia.Common.Utils.Logging;
using Godot;

namespace Gaia.InterCom.EventBus;

/// <summary>
///   The event bus is mainly used to inform UI components about changes that have occurred in Gaia' backend.
///   The reason for this is that the UI layer's subtree is very distant from the rest of Gaia', making communication
///   between these distant components and the UI a bit awkward to do in a clean way.
///   See: https://www.gdquest.com/tutorial/godot/design-patterns/event-bus-singleton/
///   It is important that any signals that *are* routed through the event bus are kept to a minimum as to avoid
///   a barrage of signal calls being routed through one place.
/// </summary>
public partial class GaiaEventBus : Node
{
  public static GaiaEventBus Instance { get; private set; }
  public PlanetaryEventBus PlanetaryEventBus { get; private set; }

  public override void _Ready()
  {
    Instance = this;
    this.RegisterLogging(true);

    PlanetaryEventBus = new PlanetaryEventBus();
    PlanetaryEventBus.Name = "PlanetaryEventBus";
    AddChild(PlanetaryEventBus);
  }
}
