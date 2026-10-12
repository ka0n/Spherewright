using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Players;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private bool CanUsePassiveDriftMoveInspection(PassiveDriftMoveInspection? inspection,
        string expectedHash, PlayerStateSnapshot player, bool hasPlayerOrder,
        bool hasActivePlayerOrder = false) =>
        inspection is not null
        && ReferenceEquals(inspection, _reader.LastPassiveDriftMoveInspection)
        && !hasPlayerOrder && !hasActivePlayerOrder
        && inspection.Allows(expectedHash, player, GameStateReader.PassiveDriftClockSeconds());
}
