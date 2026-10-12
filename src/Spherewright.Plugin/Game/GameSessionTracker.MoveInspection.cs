using Spherewright.Bridge.Core.Safety;

namespace Spherewright.Plugin.Game;

internal sealed partial class GameSessionTracker
{
    internal PassiveDriftMoveInspectionStore PassiveDriftMoveInspections { get; } = new();
}
