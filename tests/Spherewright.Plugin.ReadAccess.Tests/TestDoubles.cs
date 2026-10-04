using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.RuntimeDescriptor;

namespace BepInEx.Configuration
{
    internal sealed class ConfigEntry<T>
    {
        public ConfigEntry(T value) => Value = value;

        public T Value { get; }
    }

    internal sealed class ConfigFile
    {
        private readonly Dictionary<(string Section, string Key), object?> _values = new();

        public void Set<T>(string section, string key, T value) => _values[(section, key)] = value;

        public ConfigEntry<T> Bind<T>(string section, string key, T defaultValue, string description)
        {
            _ = description;
            if (!_values.TryGetValue((section, key), out var configured))
            {
                return new ConfigEntry<T>(defaultValue);
            }

            return new ConfigEntry<T>((T)configured!);
        }
    }
}

namespace BepInEx.Logging
{
    internal sealed class ManualLogSource
    {
        public void LogDebug(string message) => _ = message;
        public void LogInfo(string message) => _ = message;
        public void LogWarning(string message) => _ = message;
        public void LogError(string message) => _ = message;
    }
}

namespace Spherewright.Plugin.RuntimeDescriptor
{
    internal sealed class OwnedWorldGameplayJournalCheckpoint
    {
    }

    internal sealed class OwnedWorldResumeTicketStore
    {
        public string? CurrentResumeToken { get; private set; }

        public int QuarantineArmCount { get; private set; }

        public List<string> ConsumedTokens { get; } = new();

        public void ArmFromQuarantinedOwnedSession(
            string ownedSaveName,
            string sessionId,
            int planetId,
            long minimumGameTick,
            string quarantineActionId,
            OwnedWorldGameplayJournalCheckpoint gameplayJournalCheckpoint)
        {
            _ = ownedSaveName;
            _ = sessionId;
            _ = planetId;
            _ = minimumGameTick;
            _ = quarantineActionId;
            _ = gameplayJournalCheckpoint;
            QuarantineArmCount++;
            CurrentResumeToken = "test-resume-token";
        }

        public void Consume(string resumeToken)
        {
            ConsumedTokens.Add(resumeToken);
            CurrentResumeToken = null;
        }
    }
}

namespace Spherewright.Plugin.Game
{
    internal sealed partial class GameSessionTracker
    {
        private bool _unownedRichReadsConfigured;
        private bool _unownedNormalWritesConfigured;
        private bool _userSaveImportConfigured;
        private bool _writesConfigured;
        private string _gameVersion = "test-game";
        private string _sessionId = "session-1";
        private long _revision = 7;
        private string _writeHealth = WriteHealthStates.Healthy;
        private string? _writeQuarantineActionId = null;
        private string? _writeQuarantineReason = null;
        private string? _ownedSaveName;
        private int _lastPlanetId;
        private object? _pendingJournalResumeTicket;
        private readonly OwnedWorldResumeTicketStore _resumeTickets = new();
        private readonly BepInEx.Logging.ManualLogSource _logger = new();
        private GameData? _observedData;
        private GameData? _ownedData;
        private object? _expectedOwnedSaveName = null;
        private object? _expectedResumeTicket = null;
        private object? _expectedFlightCheckpoint = null;

        public bool GameLoaded { get; private set; }

        public bool IsCurrentSessionOwned => GameLoaded && _ownedData is not null && ReferenceEquals(_ownedData, _observedData);

        public string? SessionId => _sessionId;

        public long RevisionForTest => _revision;

        public OwnedWorldResumeTicketStore ResumeTicketsForTest => _resumeTickets;

        public SessionState? OwnedSessionState { get; set; }

        public SessionState? CapturedStateOverride { get; set; }

        public void Configure(
            GameData data,
            bool observeUnowned,
            bool allowWrites,
            bool allowImport,
            bool owned = false,
            bool allowNormalWrites = false)
        {
            GameMain.data = data;
            GameMain.localPlanet = data.localPlanet;
            _observedData = data;
            _ownedData = owned ? data : null;
            _unownedRichReadsConfigured = observeUnowned;
            _unownedNormalWritesConfigured = allowNormalWrites;
            _writesConfigured = allowWrites;
            _userSaveImportConfigured = allowImport;
            GameLoaded = true;
            _lastPlanetId = data.localPlanet?.id ?? 0;
            _ownedSaveName = owned ? "test-owned-save" : null;
            if (owned)
            {
                OwnedSessionState = new SessionState
                {
                    GameLoaded = true,
                    OwnedBySpherewright = true,
                    ReadAccessMode = ReadAccessModes.Owned,
                    SessionId = _sessionId,
                    LocalPlanetId = data.localPlanet?.id,
                    Capabilities = new List<string> { "bridge.status", "player.read", "normal-game.prepare" },
                };
            }
        }

        public SessionState CaptureOnMainThread() => CapturedStateOverride
            ?? (!GameLoaded
                ? new SessionState { BridgeConnected = true, GameLoaded = false, SessionId = _sessionId }
                : IsCurrentSessionOwned ? OwnedSessionState! : CaptureUnownedStateOnMainThread());

        public void UpdateOnMainThread()
        {
        }

        public SessionState ProjectUnownedStateForTest() => CaptureUnownedStateOnMainThread();

        public void SetProtectedAdoptionExpectationForTest(string expectation)
        {
            _expectedOwnedSaveName = expectation == "new-world" ? new object() : null;
            _expectedResumeTicket = expectation == "resume" ? new object() : null;
            _expectedFlightCheckpoint = expectation == "checkpoint" ? new object() : null;
        }

        public bool WritesConfiguredForTest => _writesConfigured;

        public void SetGameLoadedForTest(bool gameLoaded) => GameLoaded = gameLoaded;

        public void SetPendingJournalResumeTicketForTest(bool pending)
            => _pendingJournalResumeTicket = pending ? new object() : null;

        public void UpdateNormalActionPlanetForTest(GameData currentData)
            => UpdateNormalActionPlanetOnMainThread(currentData);

        public List<WriteBlocker> CreateWriteBlockersForTest(string peacefulState, bool unownedNormalActions = false)
            => CreateWriteBlockers(peacefulState, unownedNormalActions);

        private OwnedWorldGameplayJournalCheckpoint CaptureGameplayJournalCheckpointOnMainThread()
            => new();
    }

    internal sealed partial class GameStateReader
    {
        private readonly GameSessionTracker _sessions;
        private readonly bool _allowAuthorizedUnownedNormalActionReads;

        public GameStateReader(GameSessionTracker sessions, bool allowAuthorizedUnownedNormalActionReads = false)
        {
            _sessions = sessions;
            _allowAuthorizedUnownedNormalActionReads = allowAuthorizedUnownedNormalActionReads;
        }

        public BridgeError? ReadableGameData(string? sessionId, out GameData? data)
            => ValidateReadableGameDataOnMainThread(sessionId, out data);

        public BridgeError? ReadableSession(string? sessionId, out PlanetFactory? factory)
            => ValidateReadableSessionOnMainThread(sessionId, out factory);

        public BridgeError? ReadablePlanet(string? sessionId, int planetId, out PlanetFactory? factory)
            => ValidateReadablePlanetOnMainThread(sessionId, planetId, out factory);

        public BridgeError? OwnedPlanet(string? sessionId, int planetId, out PlanetFactory? factory)
            => ValidateOwnedPlanetOnMainThread(sessionId, planetId, out factory);

        public BridgeError? BlueprintActionPlanetForTest(string? sessionId, int planetId)
            => ValidateBlueprintActionPlanetOnMainThread(sessionId, planetId, out _);
    }

    internal sealed partial class NormalGameActionCoordinator
    {
        private const int StateHashVersion = 1;
        private readonly GameSessionTracker _sessions;

        public NormalGameActionCoordinator(GameSessionTracker sessions) => _sessions = sessions;

        public BridgeError? PrepareAuthorizationForTest(string? sessionId, int planetId, int stateHashVersion)
            => ValidatePrepareCommon(sessionId, planetId, stateHashVersion).Error;

        public BridgeError? CommitAuthorizationForTest(
            string? planSessionId,
            int planPlanetId,
            string? requestSessionId,
            int requestPlanetId)
            => ValidateCommitCommon(
                _sessions.CaptureOnMainThread(),
                new NormalActionPlanPayload { SessionId = planSessionId ?? string.Empty, PlanetId = planPlanetId },
                new CommitNormalActionRequest { SessionId = requestSessionId ?? string.Empty, PlanetId = requestPlanetId });

        public bool ActiveActionSessionCurrentForTest(string? sessionId, int planetId, bool isFlight)
            => IsActiveActionSessionCurrent(sessionId, planetId, isFlight);

        private sealed partial class NormalActionPlanPayload
        {
            public string SessionId { get; set; } = string.Empty;
            public int PlanetId { get; set; }
        }
    }
}

public sealed class GameDesc
{
    public bool isPeaceMode { get; set; }
    public bool isSandboxMode { get; set; }
    public float resourceMultiplier { get; set; } = 1f;
}

public sealed class PlanetData
{
    public int id { get; set; }
    public string displayName { get; set; } = "Test Planet";
}

public sealed class PlanetFactory
{
    public int planetId { get; set; }
    public int entityCursor { get; set; } = 3;
    public int prebuildCursor { get; set; } = 1;
}

public sealed class GameData
{
    public string gameName { get; set; } = "manual-slot";
    public GameDesc? gameDesc { get; set; }
    public PlanetData? localPlanet { get; set; }
    public PlanetFactory? localLoadedPlanetFactory { get; set; }
}

public static class GameMain
{
    public static GameData? data { get; set; }
    public static PlanetData? localPlanet { get; set; }
    public static long gameTick { get; set; }
    public static bool sandboxToolsEnabled { get; set; }
    public static string gameName { get => data?.gameName ?? string.Empty; set { if (data is not null) data.gameName = value; } }
    public static GameHistoryData? history { get; set; }
    public static object? mainPlayer { get; set; }
}

public sealed class GameHistoryData
{
    public CombatSettings combatSettings;
}

public struct CombatSettings
{
    public float aggressiveness;
}

public sealed class UIRoot
{
    public static UIRoot? instance { get; set; }
    public UIGame? uiGame { get; set; }
}

public sealed class UIGame
{
    public ResearchResultTip? researchResultTip { get; set; }
}

public sealed class ResearchResultTip
{
    public bool active { get; set; }
    public bool ready { get; set; }
    public int FadeOutCount { get; private set; }
    public void FadeOut() => FadeOutCount++;
}
