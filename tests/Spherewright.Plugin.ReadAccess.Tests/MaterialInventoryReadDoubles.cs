using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Factory;

namespace Spherewright.Plugin.Game;

// Only native capture is doubled. The public entry point, access gates and
// selection policy are linked production code; cargo accounting has Core tests.
internal sealed partial class GameStateReader
{
    public int CutCapturesForTest { get; private set; }

    private FactoryEntitySnapshot? TryCaptureFactoryEntity(PlanetFactory factory, int objectId)
        => new() { ObjectId = objectId, PlanetId = factory.planetId };
    private FactoryEntitySnapshot? TryCapturePrebuild(PlanetFactory factory, int objectId) => null;
    private static SorterEndpointObservation CaptureSorterEndpoints(PlanetFactory factory, int objectId) => new();
    private static BeltCargoSnapshot CaptureBeltCargo(PlanetFactory factory, int objectId) => new();
    private MaterialInventoryCutSnapshot CaptureMaterialInventoryCut(PlanetFactory factory, IReadOnlyList<int> ids)
    {
        CutCapturesForTest++;
        return new() { State = "observed", PlanetId = factory.planetId, RequestedObjectIds = ids.ToList() };
    }
    private static GameCallResult<FactoryEntitySnapshot> InvalidFactoryEntity(string message)
        => GameCallResult<FactoryEntitySnapshot>.Failed(BridgeError.Create(
            BridgeErrorCodes.InvalidEntity, message, false, "Refresh the factory objects."));
}
