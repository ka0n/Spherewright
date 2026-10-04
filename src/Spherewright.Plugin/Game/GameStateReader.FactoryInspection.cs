using Spherewright.Bridge.Core.Factory;
using Spherewright.Contracts.Factory;

namespace Spherewright.Plugin.Game;

internal sealed partial class GameStateReader
{
    public GameCallResult<FactoryEntitySnapshot> InspectFactoryEntityOnMainThread(
        string? requestedSessionId,
        InspectFactoryEntityRequest request)
    {
        var accessError = ValidateReadablePlanetOnMainThread(requestedSessionId, request.PlanetId, out var factory);
        if (accessError is not null)
        {
            return GameCallResult<FactoryEntitySnapshot>.Failed(accessError);
        }

        // Keep the public rich-read boundary first. A missing/incorrectly named ID is an
        // invalid request, not evidence that a previously observed building vanished.
        var idError = FactoryObjectReadPolicy.ValidateObjectId(request.ObjectId);
        if (idError is not null) return GameCallResult<FactoryEntitySnapshot>.Failed(idError);
        var selectionError = MaterialInventoryCutPolicy.ValidateSelection(request.MaterialInventoryObjectIds);
        if (selectionError is not null) return GameCallResult<FactoryEntitySnapshot>.Failed(selectionError);

        FactoryEntitySnapshot? snapshot = null;
        if (request.ObjectId > 0 && request.ObjectId < factory!.entityCursor)
        {
            snapshot = TryCaptureFactoryEntity(factory, request.ObjectId);
            if (snapshot is not null)
                snapshot.SorterEndpoints = CaptureSorterEndpoints(factory, request.ObjectId);
            if (snapshot?.ComponentKind == "belt")
                snapshot.BeltCargo = CaptureBeltCargo(factory, request.ObjectId);
        }
        else if (request.ObjectId < 0 && -request.ObjectId < factory!.prebuildCursor)
        {
            snapshot = TryCapturePrebuild(factory, -request.ObjectId);
        }

        if (snapshot is not null && request.MaterialInventoryObjectIds?.Count > 0)
            snapshot.MaterialInventoryCut = CaptureMaterialInventoryCut(factory!, request.MaterialInventoryObjectIds);

        return snapshot is null
            ? InvalidFactoryEntity("The requested factory object no longer exists in the local factory.")
            : GameCallResult<FactoryEntitySnapshot>.Succeeded(snapshot);
    }

}
