using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Players;

public sealed class StarData
{
    public PlanetData[]? planets { get; set; }
}

namespace Spherewright.Plugin.Game
{
    internal sealed partial class GameSessionTracker
    {
        public void ChangeSessionForIntegrationTest() => _sessionId = "session-2";
    }

    internal sealed partial class GameStateReader
    {
        private const int MaximumOverseerFactoryCount = 512;
        private static BridgeError NotReady(string message) =>
            BridgeError.Create(BridgeErrorCodes.BridgeNotReady, message, true, "Refresh current state.");
        private static BridgeError InvalidRequest(string message, string recovery) =>
            BridgeError.Create(BridgeErrorCodes.InvalidRequest, message, false, recovery);
        private static BridgeError OverseerScopeExceeded() => NotReady("Factory scope exceeded.");
        private static FuelPowerStateSnapshot CaptureFuelPowerState(PlanetFactory factory, int id) => new();
        private static FactoryGeometryCutSnapshot CaptureFactoryGeometryCut(
            PlanetFactory factory, FactoryGeometryReadRequest request) => new();

        public void PublishMoveInspectionForTest(PlayerStateSnapshot player, bool hasOrder = false) =>
            CapturePassiveDriftMoveInspection(player, hasOrder);
    }

    internal sealed partial class NormalGameActionCoordinator
    {
        private readonly GameStateReader _reader = null!;
        public NormalGameActionCoordinator(GameSessionTracker sessions, GameStateReader reader) : this(sessions) =>
            _reader = reader;

        public bool MoveInspectionAllowedForTest(PassiveDriftMoveInspection? inspection,
            string hash, PlayerStateSnapshot player, bool hasOrder = false, bool activeOrder = false) =>
            CanUsePassiveDriftMoveInspection(inspection, hash, player, hasOrder, activeOrder);
    }
}
