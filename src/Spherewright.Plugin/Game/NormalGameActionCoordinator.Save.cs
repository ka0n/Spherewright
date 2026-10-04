using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private GameCallResult<PreparedNormalAction> PrepareSavePlanOnMainThread(
        string? requestedSessionId, PrepareSaveRequest request)
    {
        var common = ValidatePrepareCommon(requestedSessionId, request.PlanetId, request.StateHashVersion);
        if (common.Error is not null) return GameCallResult<PreparedNormalAction>.Failed(common.Error);
        var session = common.Session!;
        if (request.ExpectedRevision != session.Revision)
            return StalePlan("The current session revision changed after inspection.");

        var saveName = session.OwnedBySpherewright ? session.SaveName : GetCurrentNativeSaveNameOnMainThread();
        if (string.IsNullOrWhiteSpace(saveName) || GameMain.data?.localLoadedPlanetFactory is null)
            return NotReadyPlan("The exact current save identity or local factory is unavailable. Normal unowned Save requires an existing native save slot.");

        var expectedHash = CanonicalStateHash.Combine(NormalActionKinds.Save, session.SessionId,
            request.PlanetId, request.ExpectedRevision, saveName, session.OwnedBySpherewright);
        var payload = NormalActionPlanPayload.Save(session.SessionId!, request.PlanetId, expectedHash,
            saveName!, request.ExpectedRevision);
        payload.SaveRequiresOwnership = session.OwnedBySpherewright;
        return AddPreparedPlan(payload, session, 1, session.OwnedBySpherewright
            ? "DSP saves and verifies the exact owned primary identity and records its game tick."
            : "DSP saves the exact existing current native slot without renaming or adoption; its header must prove the saved tick. No protected restart credential is issued.");
    }

    // Current DLL audit: GameMain.gameName is GameMain.data.gameName. Native ordinary
    // load assigns the slot name to that field; SavePath/SaveCurrentGame use the same
    // sanitizer. Refuse recursive/path slots and names that native saving would change.
    private string? GetCurrentNativeSaveNameOnMainThread()
    {
        if (!_sessions.IsCurrentExactUnownedSession || !_sessions.IsCurrentSessionAuthorizedForNormalActions
            || GameSave.AllowRecursive) return null;
        var name = GameMain.gameName;
        if (string.IsNullOrWhiteSpace(name)
            || name.IndexOfAny(new[] { '/', '\\' }) >= 0
            || !string.Equals(CommonUtils.ValidFileName(name), name, StringComparison.Ordinal)) return null;
        return GameSave.SaveExist(name) ? name : null;
    }

    private BridgeError? RevalidateSaveOnMainThread(NormalActionPlanPayload plan)
    {
        var session = _sessions.CaptureOnMainThread();
        var saveName = session.OwnedBySpherewright ? session.SaveName : GetCurrentNativeSaveNameOnMainThread();
        if (!_sessions.IsCurrentSessionAuthorizedForNormalActions
            || !string.Equals(session.SessionId, plan.SessionId, StringComparison.Ordinal)
            || session.OwnedBySpherewright != plan.SaveRequiresOwnership
            || session.LocalPlanetId != plan.PlanetId || session.Revision != plan.SaveExpectedRevision
            || !string.Equals(saveName, plan.SaveName, StringComparison.Ordinal)
            || GameMain.data?.localLoadedPlanetFactory is null)
            return Stale("The exact save identity, session, authority, planet, factory, or revision changed after prepare.");
        return null;
    }

    private void ExecuteSaveOnMainThread(ActionRecord action)
    {
        // Keep the final guard adjacent to the native mutation even though common
        // commit already revalidates the plan on this same main-thread invocation.
        if (RevalidateSaveOnMainThread(action.Plan) is not null)
            throw new InvalidOperationException("The exact save binding changed before execution.");
        long savedTick;
        if (action.Plan.SaveRequiresOwnership)
        {
            if (!_sessions.TrySaveOwnedWorldNowOnMainThread(out var error))
                throw new InvalidOperationException(error ?? "DSP's normal owned save API did not confirm success.");
            var owned = _sessions.CaptureOnMainThread();
            if (!owned.LastOwnedSaveGameTick.HasValue)
                throw new InvalidOperationException("The owned save completed without a recorded game tick.");
            savedTick = owned.LastOwnedSaveGameTick.Value;
        }
        else
        {
            savedTick = GameMain.gameTick;
            // Never assign GameMain.gameName and never invoke owned provenance machinery.
            if (!GameSave.SaveCurrentGame(action.Plan.SaveName))
                throw new InvalidOperationException("DSP's native save API did not confirm success.");
            GameSave.ReadHeader(action.Plan.SaveName, false, out var header);
            if (header is null || header.gameTick != savedTick
                || !string.Equals(GameMain.gameName, action.Plan.SaveName, StringComparison.Ordinal)
                || RevalidateSaveOnMainThread(action.Plan) is not null)
                throw new InvalidOperationException("Native save readback did not prove the bound current slot and exact saved tick.");
        }
        var afterInventory = CaptureInventory(GameMain.mainPlayer);
        var afterHash = CanonicalStateHash.Combine(NormalActionKinds.Save, action.SessionId,
            action.PlanetId, savedTick, _sessions.CaptureOnMainThread().Revision);
        action.State = NormalActionStates.Completed;
        action.Terminal = true;
        action.Succeeded = true;
        action.CompletedAtGameTick = GameMain.gameTick;
        action.Message = $"DSP's normal save API confirmed the exact current {(action.Plan.SaveRequiresOwnership ? "owned primary" : "native slot")} at game tick {savedTick}.";
        action.AfterInventory = afterInventory;
        action.AfterStateHash = afterHash;
    }

    private sealed partial class NormalActionPlanPayload
    {
        public bool SaveRequiresOwnership { get; set; }
        public long SaveExpectedRevision { get; private set; }
        public string SaveName { get; private set; } = string.Empty;

        public static NormalActionPlanPayload Save(
            string sessionId,
            int planetId,
            string expectedStateHash,
            string saveName,
            long expectedRevision) => new NormalActionPlanPayload
            {
                ActionKind = NormalActionKinds.Save,
                SessionId = sessionId,
                PlanetId = planetId,
                ExpectedStateHash = expectedStateHash,
                SaveName = saveName,
                SaveExpectedRevision = expectedRevision,
                EstimatedTicks = 1,
            };

    }
}
