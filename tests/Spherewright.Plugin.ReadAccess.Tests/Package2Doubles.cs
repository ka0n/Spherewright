using Spherewright.Bridge.Core.Factory;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Sessions;

// All save API calls below are doubles: these tests never open a DSP save.
public static class GameSave
{
    public static bool AllowRecursive;
    public static bool Existing = true;
    public static bool SaveSucceeds = true;
    public static long? HeaderTick;
    public static List<string> SavedNames = new();
    public static List<string> ReadNames = new();
    public static Action? AfterSave;
    public static bool SaveExist(string name) => Existing;
    public static bool SaveCurrentGame(string name)
    {
        SavedNames.Add(name); AfterSave?.Invoke(); return SaveSucceeds;
    }
    public static void ReadHeader(string name, bool image, out GameSaveHeader? header)
    {
        ReadNames.Add(name);
        header = HeaderTick.HasValue ? new GameSaveHeader { gameTick = HeaderTick.Value } : null;
    }
    public static void Reset()
    {
        AllowRecursive = false; Existing = true; SaveSucceeds = true;
        HeaderTick = GameMain.gameTick; SavedNames.Clear(); ReadNames.Clear(); AfterSave = null;
    }
}
public sealed class GameSaveHeader { public long gameTick; }
public static class CommonUtils
{
    public static string ValidFileName(string value) => value.Replace(":", "_");
}

namespace Spherewright.Plugin.RuntimeDescriptor
{
    internal static class RuntimeDescriptorPublisher
    {
        public static string ResolveRuntimeDirectory(string directory) => directory;
    }
    internal sealed class FlightCheckpointStore
    {
        public int AttemptCount, SuccessCount, RecoveryCount;
        public bool Succeeds = true;
        public bool TryMarkAttemptStarted(string id, string actionId, long tick, out string rejection)
        { AttemptCount++; rejection = "test rejection"; return Succeeds; }
        public bool TryMarkFlightSucceeded(string id, string actionId, long tick, out string rejection)
        { SuccessCount++; rejection = "test rejection"; return Succeeds; }
        public bool TryMarkRecoveryRequired(string id, string actionId, long tick, out string rejection)
        { RecoveryCount++; rejection = "test rejection"; return Succeeds; }
    }
}
namespace Spherewright.Plugin.Security
{
    // Store tests exercise real serialization/replace behavior in synthetic temp
    // directories. ACL enforcement itself is covered separately by package checks.
    internal static class WindowsCurrentUserSecurity
    {
        public static void EnsureSecureDirectory(string path) => Directory.CreateDirectory(path);
        public static void WriteSecureNewFile(string path, byte[] bytes) => File.WriteAllBytes(path, bytes);
    }
}
namespace Spherewright.Plugin.Game
{
    internal sealed partial class GameSessionTracker
    {
        public string? OwnedSaveName => _ownedSaveName;
        public int OwnedSaveCallsForTest, ForgottenCheckpointsForTest;
        public void ReplaceSessionForTest(string id) => _sessionId = id;
        public void ForgetCurrentFlightCheckpoint(string id) => ForgottenCheckpointsForTest++;
        public bool TrySaveOwnedWorldNowOnMainThread(out string? error)
        {
            OwnedSaveCallsForTest++; error = null;
            OwnedSessionState!.LastOwnedSaveGameTick = GameMain.gameTick;
            return true;
        }
    }
    internal sealed partial class NormalGameActionCoordinator
    {
        private readonly RuntimeDescriptor.FlightCheckpointStore _flightCheckpoints = new();
        private BlueprintBuildStore _blueprints = null!;
        private readonly TestActions _actions = new();
        private NormalActionPlanPayload? _preparedForTest;
        public int CheckpointCreateCallsForTest, PlayerOrderCleanupForTest, AscentCleanupForTest;
        public RuntimeDescriptor.FlightCheckpointStore CheckpointsForTest => _flightCheckpoints;
        public GameCallResult<PreparedNormalAction> PrepareSaveForTest(string id, PrepareSaveRequest request)
            => PrepareSavePlanOnMainThread(id, request);
        public string? BoundSaveNameForTest => _preparedForTest?.SaveName;
        public BridgeError? RevalidateSaveForTest() => RevalidateSaveOnMainThread(_preparedForTest!);
        public ActionResultSnapshot CommitSaveForTest()
        {
            var plan = _preparedForTest!;
            var error = ValidateCommitCommon(_sessions.CaptureOnMainThread(), plan,
                new CommitNormalActionRequest { SessionId = plan.SessionId, PlanetId = plan.PlanetId })
                ?? RevalidateSaveOnMainThread(plan);
            if (error is not null) throw new InvalidOperationException(error.Code);
            var action = Record(plan);
            try { ExecuteSaveOnMainThread(action); }
            catch (Exception) { action.State = NormalActionStates.OutcomeUnknown; action.Terminal = true;
                _sessions.QuarantineWritesOnMainThread(action.ActionId, "save proof failed"); }
            return Snapshot(action);
        }
        public ActionResultSnapshot FlightForTest(bool owned, string outcome, bool checkpointPresent = true)
        {
            var plan = new NormalActionPlanPayload { ActionKind = NormalActionKinds.InterplanetaryFlight,
                SessionId = _sessions.SessionId!, PlanetId = 101, FlightRequiresCheckpoint = owned };
            var action = Record(plan);
            if (checkpointPresent && owned) action.FlightCheckpointId = "checkpoint";
            if (outcome == "start") BeginFlightCheckpointLifecycle(action);
            else if (outcome == "complete") Complete(action, "Physical flight completed.");
            else Fail(action, "Physical flight failed." + FlightFailureAdvice(action));
            return Snapshot(action);
        }
        public bool FlightModeMatchesForTest(bool requiresCheckpoint) => FlightAuthorityMatches(
            new NormalActionPlanPayload { SessionId = _sessions.SessionId!, FlightRequiresCheckpoint = requiresCheckpoint },
            _sessions.CaptureOnMainThread());
        public static bool BlueprintPreviewForTest(bool owned, string? resumeId, string? hash)
            => CanPrepareUnownedBlueprintPreview(owned, resumeId, hash);
        public static bool BlueprintPlayerHashMatchesForTest(bool preview, string? suppliedHash, string currentHash)
            => BlueprintPlayerInspectionMatches(preview, suppliedHash, currentHash);
        public GameCallResult<PreparedNormalAction> PrepareCancelForTest(BlueprintBuildStore store, string id, string hash)
        {
            _blueprints = store;
            return PrepareCancelBlueprintOnMainThread(_sessions.SessionId,
                new PrepareCancelBlueprintRequest { PlanetId = 101, BuildId = id, ExpectedStateHash = hash, StateHashVersion = 1 });
        }
        public ActionResultSnapshot CommitCancelForTest()
        {
            var plan = _preparedForTest!;
            var error = ValidateCommitCommon(_sessions.CaptureOnMainThread(), plan,
                new CommitNormalActionRequest { SessionId = plan.SessionId, PlanetId = plan.PlanetId })
                ?? RevalidateBlueprintCancelOnMainThread(plan);
            if (error is not null) throw new InvalidOperationException(error.Code);
            var action = Record(plan); CancelBlueprintBuildOnMainThread(action); return Snapshot(action);
        }
        private GameCallResult<PreparedNormalAction> AddPreparedPlan(NormalActionPlanPayload plan,
            SessionState session, long ticks, string condition)
        {
            _preparedForTest = plan;
            return GameCallResult<PreparedNormalAction>.Succeeded(new PreparedNormalAction
            { Prepared = true, ActionKind = plan.ActionKind, ExpectedStateHash = plan.ExpectedStateHash, CompletionCondition = condition });
        }
        private static BridgeError Stale(string message) => BridgeError.Create(BridgeErrorCodes.StaleState, message, true, "Fresh prepare.");
        private static BridgeError BlueprintError(string message) => BridgeError.Create(BridgeErrorCodes.ActionRejected, message, false, "Inspect progress.");
        private static GameCallResult<PreparedNormalAction> StalePlan(string message) => GameCallResult<PreparedNormalAction>.Failed(Stale(message));
        private static GameCallResult<PreparedNormalAction> NotReadyPlan(string message) => GameCallResult<PreparedNormalAction>.Failed(
            BridgeError.Create(BridgeErrorCodes.BridgeNotReady, message, true, "Inspect session."));
        private static GameCallResult<PreparedNormalAction> InvalidPlan(string message) => GameCallResult<PreparedNormalAction>.Failed(
            BridgeError.Create(BridgeErrorCodes.InvalidRequest, message, false, "Correct request."));
        private bool EnsureFlightCheckpointOnMainThread(ActionRecord action)
        { CheckpointCreateCallsForTest++; action.FlightCheckpointId = "checkpoint"; return true; }
        private void AbortPlayerOrderIfOwned(ActionRecord action) => PlayerOrderCleanupForTest++;
        private void ReleaseNativeAscentInput(ActionRecord action) => AscentCleanupForTest++;
        private static Dictionary<int, int> CaptureInventory(object? player) => new();
        private static string CaptureAfterStateHash(ActionRecord action) => "test-after-hash";
        private static ActionRecord Record(NormalActionPlanPayload plan) => new()
        { ActionId = "test-action", ActionKind = plan.ActionKind, SessionId = plan.SessionId, PlanetId = plan.PlanetId, Plan = plan };
        private static ActionResultSnapshot Snapshot(ActionRecord action) => new()
        { State = action.State, Terminal = action.Terminal, Succeeded = action.Succeeded, RecoveryRequired = action.RecoveryRequired, Message = action.Message };
        private sealed class TestActions { public List<ActionRecord> ActiveValues { get; } = new(); }
        private sealed class ActionRecord
        {
            public string ActionId = "", ActionKind = "", SessionId = "", State = "";
            public int PlanetId; public bool Terminal, Succeeded, RecoveryRequired;
            public long? CompletedAtGameTick; public string? Message, AfterStateHash, FlightCheckpointId, FailureKind;
            public Dictionary<int, int>? AfterInventory;
            public BlueprintBuildState? BlueprintBuild;
            public NormalActionPlanPayload Plan = null!;
        }
        private sealed partial class NormalActionPlanPayload
        {
            public string ActionKind { get; set; } = "";
            public string ExpectedStateHash { get; set; } = "";
            public long EstimatedTicks { get; set; }
            public BlueprintBuildState? BlueprintState { get; set; }
            public static NormalActionPlanPayload Blueprint(string sessionId, int planetId, long revision,
                string playerHash, BlueprintBuildState state, string inputHash, int limit, string? code,
                Spherewright.Contracts.Factory.BlueprintSiteRequest? site, bool cancel) => new()
            { SessionId = sessionId, PlanetId = planetId, BlueprintState = state, ExpectedStateHash = inputHash,
                ActionKind = cancel ? NormalActionKinds.CancelBlueprintBuild : NormalActionKinds.BlueprintBuild };
        }
    }
}
