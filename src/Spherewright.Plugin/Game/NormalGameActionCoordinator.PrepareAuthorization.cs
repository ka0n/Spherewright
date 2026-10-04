using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Sessions;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private CommonPrepareResult ValidatePrepareCommon(string? requestedSessionId, int planetId, int stateHashVersion)
    {
        if (stateHashVersion != StateHashVersion)
        {
            return CommonPrepareResult.Failed(BridgeError.Create(
                BridgeErrorCodes.StaleState,
                "Unsupported action state-hash version.",
                false,
                "Inspect current state and use the returned stateHashVersion."));
        }

        var session = _sessions.CaptureOnMainThread();
        if (!session.GameLoaded)
        {
            return CommonPrepareResult.Failed(BridgeError.Create(
                BridgeErrorCodes.GameNotLoaded,
                "No game is loaded.",
                true,
                "Create and wait for a fresh Spherewright-owned ordinary world."));
        }

        if (!_sessions.IsCurrentSessionAuthorizedForNormalActions)
        {
            return CommonPrepareResult.Failed(BridgeError.Create(
                BridgeErrorCodes.SessionNotOwned,
                "The current session is not authorized for normal-game actions.",
                false,
                "Use an owned world or explicitly enable global writes and unowned normal writes for this exact session."));
        }

        if (!string.Equals(requestedSessionId, session.SessionId, StringComparison.Ordinal))
        {
            return CommonPrepareResult.Failed(BridgeError.Create(
                BridgeErrorCodes.StaleSession,
                "The requested session is not the current authorized session.",
                false,
                "Inspect current session state and retry with its exact session ID."));
        }

        if (planetId <= 0 || session.LocalPlanetId != planetId)
        {
            return CommonPrepareResult.Failed(BridgeError.Create(
                BridgeErrorCodes.NoLocalPlanet,
                "The requested planet is not the current local planet.",
                false,
                "Use the current localPlanetId returned by session state."));
        }

        return CommonPrepareResult.Succeeded(session);
    }

    private BridgeError? ValidateCommitCommon(
        SessionState session,
        NormalActionPlanPayload plan,
        CommitNormalActionRequest request)
    {
        if (!session.GameLoaded || !_sessions.IsCurrentSessionAuthorizedForNormalActions
            || !string.Equals(session.SessionId, plan.SessionId, StringComparison.Ordinal)
            || !string.Equals(request.SessionId, plan.SessionId, StringComparison.Ordinal))
        {
            return BridgeError.Create(
                BridgeErrorCodes.StaleSession,
                "The prepared action does not belong to the current authorized session.",
                false,
                "Inspect the current session and prepare a fresh action.");
        }

        if (request.PlanetId != plan.PlanetId || session.LocalPlanetId != plan.PlanetId)
        {
            return BridgeError.Create(
                BridgeErrorCodes.StaleState,
                "Commit planet, planned planet, and current local planet do not match.",
                false,
                "Return to the planned planet and prepare a fresh action.");
        }

        if (session.WriteBlockers.Count > 0)
        {
            var blocker = session.WriteBlockers[0];
            return BridgeError.Create(
                blocker.Code,
                blocker.Message,
                false,
                "Resolve every current session write blocker, then prepare a fresh action.");
        }

        return null;
    }

    // Observing an accepted action needs its original session, not current write health.
    private bool IsActiveActionSessionCurrent(string? sessionId, int planetId, bool isFlight) =>
        _sessions.GameLoaded
        && (_sessions.IsCurrentSessionOwned || _sessions.IsCurrentExactUnownedSession)
        && string.Equals(_sessions.SessionId, sessionId, StringComparison.Ordinal)
        && (isFlight || GameMain.localPlanet?.id == planetId);

    private sealed class CommonPrepareResult
    {
        private CommonPrepareResult(SessionState? session, BridgeError? error)
        {
            Session = session;
            Error = error;
        }

        public SessionState? Session { get; }

        public BridgeError? Error { get; }

        public static CommonPrepareResult Succeeded(SessionState session) => new CommonPrepareResult(session, null);

        public static CommonPrepareResult Failed(BridgeError error) => new CommonPrepareResult(null, error);
    }
}
