using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Sessions;

namespace Spherewright.Plugin.Game;

internal sealed partial class GameStateReader
{
    private BridgeError? ValidateOwnedPlanetOnMainThread(
        string? requestedSessionId,
        int requestedPlanetId,
        out PlanetFactory? factory)
        => ValidatePlanetOnMainThread(requestedSessionId, requestedPlanetId, true, out factory);

    private BridgeError? ValidateReadablePlanetOnMainThread(
        string? requestedSessionId, int requestedPlanetId, out PlanetFactory? factory)
        => ValidatePlanetOnMainThread(requestedSessionId, requestedPlanetId, false, out factory);

    // Blueprint/site data needed by the executor is available only on the private
    // action reader. The public reader retains the owned-only inspection/export gate.
    private BridgeError? ValidateBlueprintActionPlanetOnMainThread(
        string? requestedSessionId, int requestedPlanetId, out PlanetFactory? factory)
        => ValidatePlanetOnMainThread(requestedSessionId, requestedPlanetId,
            !(_allowAuthorizedUnownedNormalActionReads && _sessions.IsCurrentExactUnownedSession
                && _sessions.IsCurrentSessionAuthorizedForNormalActions), out factory);

    private BridgeError? ValidatePlanetOnMainThread(
        string? requestedSessionId, int requestedPlanetId, bool requireOwnership, out PlanetFactory? factory)
    {
        var error = ValidateSessionOnMainThread(requestedSessionId, requireOwnership, out factory);
        if (error is not null)
        {
            return error;
        }

        if (requestedPlanetId <= 0 || factory!.planetId != requestedPlanetId)
        {
            factory = null;
            return BridgeError.Create(
                BridgeErrorCodes.StaleState,
                "The requested planet does not match the currently loaded local planet.",
                true,
                "Call get_session_state and retry with its current localPlanetId.");
        }

        return null;
    }

    private BridgeError? ValidateReadableSessionOnMainThread(string? requestedSessionId, out PlanetFactory? factory)
        => ValidateSessionOnMainThread(requestedSessionId, false, out factory);

    private BridgeError? ValidateSessionOnMainThread(string? requestedSessionId, bool requireOwnership, out PlanetFactory? factory)
    {
        factory = null;
        var error = ValidateGameDataOnMainThread(requestedSessionId, requireOwnership, out var gameData);
        if (error is not null)
        {
            return error;
        }

        factory = gameData!.localLoadedPlanetFactory;
        if (factory is null)
        {
            return BridgeError.Create(
                BridgeErrorCodes.NoLocalPlanet,
                "The current session does not have a loaded local factory.",
                true,
                "Wait for the local planet factory to load and retry.");
        }

        if (gameData.localPlanet is null || gameData.localPlanet.id != factory.planetId)
        {
            factory = null;
            return BridgeError.Create(
                BridgeErrorCodes.BridgeNotReady,
                "The loaded factory does not match the current local planet.",
                true,
                "Wait for the current local planet factory to finish loading and retry.");
        }

        return null;
    }

    private BridgeError? ValidateReadableGameDataOnMainThread(string? requestedSessionId, out GameData? gameData)
        => ValidateGameDataOnMainThread(requestedSessionId, false, out gameData);

    private BridgeError? ValidateGameDataOnMainThread(string? requestedSessionId, bool requireOwnership, out GameData? gameData)
    {
        gameData = null;
        var state = _sessions.CaptureOnMainThread();
        if (!state.GameLoaded)
        {
            return BridgeError.Create(
                BridgeErrorCodes.GameNotLoaded,
                "No game session is currently loaded.",
                true,
                "Create and load a Spherewright-owned ordinary world, then retry.");
        }

        var authorizedInternalUnownedRead = _allowAuthorizedUnownedNormalActionReads
            && _sessions.IsCurrentSessionAuthorizedForNormalActions;
        if (!state.OwnedBySpherewright
            && (requireOwnership || (!_sessions.IsCurrentSessionObservedUnowned && !authorizedInternalUnownedRead)))
        {
            return BridgeError.Create(
                BridgeErrorCodes.SessionNotOwned,
                "The loaded game session was not created by this Spherewright Plugin process, so its contents are restricted.",
                false,
                "Return to the main menu and create a Spherewright-owned ordinary world.");
        }

        if (string.IsNullOrWhiteSpace(requestedSessionId)
            || !string.Equals(requestedSessionId, state.SessionId, StringComparison.Ordinal))
        {
            return BridgeError.Create(
                BridgeErrorCodes.StaleSession,
                "The supplied session ID does not match the active session.",
                true,
                "Call get_session_state and retry with its sessionId.");
        }

        gameData = GameMain.data;
        var readable = gameData is not null
            && (_sessions.IsCurrentGameDataReadable(gameData)
                || (_allowAuthorizedUnownedNormalActionReads
                    && _sessions.IsCurrentGameDataAvailableForNormalActions(gameData)));
        if (!readable)
        {
            return BridgeError.Create(
                BridgeErrorCodes.BridgeNotReady,
                "The exact current game data is not ready.",
                true,
                "Wait for the current ordinary world to finish loading and retry.");
        }

        return null;
    }
}
