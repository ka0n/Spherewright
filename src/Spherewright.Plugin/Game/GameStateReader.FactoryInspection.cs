using Spherewright.Bridge.Core.Factory;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Errors;

namespace Spherewright.Plugin.Game;

internal sealed partial class GameStateReader
{
    public GameCallResult<FactoryEntitySnapshot> InspectFactoryEntityOnMainThread(
        string? requestedSessionId,
        InspectFactoryEntityRequest request) =>
        InspectFactoryEntityOnMainThread(requestedSessionId, request, allowCurrentStarRemoteReadOnly: false);

    internal GameCallResult<FactoryEntitySnapshot> InspectFactoryEntityOnMainThread(
        string? requestedSessionId,
        InspectFactoryEntityRequest request,
        bool allowCurrentStarRemoteReadOnly)
    {
        // Only the direct read-only Bridge inspection route opts into this scope.
        // Action-coordinator calls retain the two-argument, strictly local overload.
        var localPlanetId = GameMain.localPlanet?.id ?? 0;
        PlanetFactory? factory;
        var accessError = allowCurrentStarRemoteReadOnly && request.PlanetId != localPlanetId
            ? ValidatePublicCurrentStarFactoryReadOnMainThread(requestedSessionId, request.PlanetId, out factory)
            : ValidateReadablePlanetOnMainThread(requestedSessionId, request.PlanetId, out factory);
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
        var geometryError = FactoryGeometryReadPolicy.ValidateRequest(request.Geometry);
        if (geometryError is not null) return GameCallResult<FactoryEntitySnapshot>.Failed(InvalidRequest(geometryError,"Use explicit bounded local geometry selections."));
        if (request.Geometry is not null && request.PlanetId != localPlanetId)
            return GameCallResult<FactoryEntitySnapshot>.Failed(InvalidRequest("factory_geometry_current_local_planet_only","Read geometry only in the currently loaded local factory."));

        FactoryEntitySnapshot? snapshot = null;
        if (request.ObjectId > 0 && request.ObjectId < factory!.entityCursor)
        {
            snapshot = TryCaptureFactoryEntity(factory, request.ObjectId);
            if (snapshot is not null)
                snapshot.SorterEndpoints = CaptureSorterEndpoints(factory, request.ObjectId);
            if (snapshot?.ComponentKind == "belt")
                snapshot.BeltCargo = CaptureBeltCargo(factory, request.ObjectId);
            if (snapshot?.ComponentKind == "power-generator")
                snapshot.FuelPowerState = CaptureFuelPowerState(factory, request.ObjectId);
        }
        else if (request.ObjectId < 0 && -request.ObjectId < factory!.prebuildCursor)
        {
            snapshot = TryCapturePrebuild(factory, -request.ObjectId);
        }

        if (snapshot is not null && request.MaterialInventoryObjectIds?.Count > 0)
            snapshot.MaterialInventoryCut = CaptureMaterialInventoryCut(factory!, request.MaterialInventoryObjectIds);
        if (snapshot is not null && request.Geometry is not null)
        {
            try { snapshot.GeometryCut = CaptureFactoryGeometryCut(factory!,request.Geometry); }
            catch (InvalidOperationException error) {return GameCallResult<FactoryEntitySnapshot>.Failed(InvalidRequest(error.Message,"Refresh the explicit geometry cut; no route qualification or write was performed."));}
        }

        return snapshot is null
            ? InvalidFactoryEntity("The requested factory object no longer exists in the local factory.")
            : GameCallResult<FactoryEntitySnapshot>.Succeeded(snapshot);
    }
}
