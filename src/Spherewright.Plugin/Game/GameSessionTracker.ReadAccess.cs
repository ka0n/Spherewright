using Spherewright.Contracts.Sessions;

namespace Spherewright.Plugin.Game;

internal sealed partial class GameSessionTracker
{
    private bool HasProtectedWorldAdoptionExpectation =>
        _expectedOwnedSaveName is not null
        || _expectedResumeTicket is not null
        || _expectedFlightCheckpoint is not null;

    public bool IsCurrentExactUnownedSession =>
        GameLoaded && !IsCurrentSessionOwned
        && !HasProtectedWorldAdoptionExpectation
        && _observedData is not null && ReferenceEquals(_observedData, GameMain.data);

    public bool IsCurrentSessionObservedUnowned =>
        IsCurrentExactUnownedSession && _unownedRichReadsConfigured;

    public bool IsCurrentSessionAuthorizedForNormalActions =>
        IsCurrentSessionOwned
        || (_writesConfigured && _unownedNormalWritesConfigured && IsCurrentExactUnownedSession);

    public bool IsCurrentGameDataReadable(GameData data) =>
        GameLoaded && ReferenceEquals(_observedData, data) && ReferenceEquals(GameMain.data, data)
        && (IsCurrentSessionOwned || IsCurrentSessionObservedUnowned);

    public bool IsCurrentGameDataAvailableForNormalActions(GameData data) =>
        GameLoaded && ReferenceEquals(_observedData, data) && ReferenceEquals(GameMain.data, data)
        && IsCurrentSessionAuthorizedForNormalActions;

    private SessionState CaptureUnownedStateOnMainThread()
    {
        var observed = IsCurrentSessionObservedUnowned;
        var normalActionsAuthorized = IsCurrentSessionAuthorizedForNormalActions;
        var sessionDetailsAvailable = observed || normalActionsAuthorized;
        var descriptor = sessionDetailsAvailable ? _observedData!.gameDesc : null;
        var localPlanet = sessionDetailsAvailable ? _observedData!.localPlanet : null;
        var blockers = _unownedNormalWritesConfigured
            ? CreateWriteBlockers(PeacefulModeStates.Unknown, unownedNormalActions: true)
            : new List<WriteBlocker>();
        var writesAllowed = normalActionsAuthorized && blockers.Count == 0;
        var capabilities = observed ? CreateObservedUnownedCapabilities() : CreateUnownedCapabilities();
        if (normalActionsAuthorized)
        {
            capabilities.Add("action.read");
            if (writesAllowed)
            {
                capabilities.Add("normal-game.prepare");
            }
            if (string.Equals(_writeHealth, WriteHealthStates.Quarantined, StringComparison.Ordinal))
            {
                capabilities.Add("quarantine.reconcile");
            }
        }
        return new SessionState
        {
            BridgeConnected = true,
            GameLoaded = true,
            OwnedBySpherewright = false,
            AccessRestricted = !observed,
            ReadAccessMode = observed ? ReadAccessModes.ObservedUnowned : ReadAccessModes.Restricted,
            GameVersion = _gameVersion,
            SessionId = _sessionId,
            Revision = _revision,
            GameTick = sessionDetailsAvailable ? GameMain.gameTick : (long?)null,
            LocalPlanetId = localPlanet?.id,
            LocalPlanetName = localPlanet?.displayName,
            PeacefulMode = descriptor is null ? PeacefulModeStates.Unknown
                : descriptor.isPeaceMode ? PeacefulModeStates.ConfirmedPeaceful : PeacefulModeStates.ConfirmedCombat,
            SandboxMode = descriptor is null ? SandboxModeStates.Unknown
                : descriptor.isSandboxMode || GameMain.sandboxToolsEnabled ? SandboxModeStates.Enabled : SandboxModeStates.ConfirmedDisabled,
            ResourceMultiplier = descriptor?.resourceMultiplier,
            DarkFogAggressiveness = sessionDetailsAvailable ? CaptureDarkFogAggressivenessOnMainThread() : null,
            WritesAllowed = writesAllowed,
            WriteHealth = _writeHealth,
            WriteQuarantineActionId = _writeQuarantineActionId,
            WriteBlockers = blockers,
            OwnedSaveState = OwnedSaveStates.None,
            UserSaveImportConfigured = _userSaveImportConfigured,
            Capabilities = capabilities,
        };
    }

    private static float? CaptureDarkFogAggressivenessOnMainThread()
    {
        var value = GameMain.history is { } history ? history.combatSettings.aggressiveness : (float?)null;
        return value.HasValue && !float.IsNaN(value.Value) && !float.IsInfinity(value.Value) ? value : null;
    }

    private static List<string> CreateObservedUnownedCapabilities() => new List<string>
    {
        "bridge.status", "session.read", "player.read", "progression.read",
        "assembler.read", "build-catalog.read", "recipe-catalog.read", "resource.read",
        "factory.read", "power.read", "overseer.read",
    };

    private List<string> CreateUnownedCapabilities()
    {
        var capabilities = new List<string> { "bridge.status", "session.safe-status" };
        if (_userSaveImportConfigured
            && string.Equals(_writeHealth, WriteHealthStates.Healthy, StringComparison.Ordinal))
        {
            capabilities.Add("user-save.import.prepare");
        }

        return capabilities;
    }

    public bool TryGetCurrentUnownedImportCandidateOnMainThread(
        string? requestedSessionId,
        out GameData? data,
        out string rejection)
    {
        UpdateOnMainThread();
        data = null;
        rejection = string.Empty;
        if (!GameLoaded || _observedData is null || GameMain.data is null)
        {
            rejection = "No ordinary game is loaded.";
            return false;
        }

        if (IsCurrentSessionObservedUnowned)
        {
            rejection = "Read-only observation does not authorize save import.";
            return false;
        }

        if (IsCurrentSessionOwned)
        {
            rejection = "The current world is already Spherewright-owned.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(requestedSessionId)
            || !string.Equals(requestedSessionId, _sessionId, StringComparison.Ordinal))
        {
            rejection = "The requested unowned session is stale.";
            return false;
        }

        if (_expectedOwnedSaveName is not null
            || _expectedResumeTicket is not null
            || _expectedFlightCheckpoint is not null)
        {
            rejection = "Another protected world-adoption flow is active.";
            return false;
        }

        if (!ReferenceEquals(_observedData, GameMain.data))
        {
            rejection = "The current world identity changed.";
            return false;
        }

        data = _observedData;
        return true;
    }
}
