using Spherewright.Bridge.Core.Factory;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.Game;
using Xunit;

namespace Spherewright.Plugin.ReadAccess.Tests;

[Collection("Plugin read-access tests")]
public sealed class Package2Tests
{
    [Fact]
    public void UnownedSaveBindsAndSavesOnlyNativeExistingIdentityWithoutProvenance()
    {
        var (tracker, coordinator) = Setup();
        var data = GameMain.data;
        Assert.True(PrepareSave(coordinator, tracker).Success);
        Assert.Equal("manual-slot", coordinator.BoundSaveNameForTest);
        Assert.Empty(GameSave.SavedNames); // Prepare is read-only.
        var result = coordinator.CommitSaveForTest();
        Assert.True(result.Succeeded);
        Assert.Equal(NormalActionStates.Completed, result.State);
        Assert.Equal(new[] { "manual-slot" }, GameSave.SavedNames);
        Assert.Equal(new[] { "manual-slot" }, GameSave.ReadNames);
        Assert.Equal("manual-slot", GameMain.gameName);
        Assert.Same(data, GameMain.data);
        Assert.False(tracker.IsCurrentSessionOwned);
        Assert.Null(tracker.OwnedSaveName);
        Assert.Equal(0, tracker.OwnedSaveCallsForTest);
        Assert.Equal(0, tracker.ResumeTicketsForTest.QuarantineArmCount);
        Assert.Null(tracker.ResumeTicketsForTest.CurrentResumeToken);
    }

    [Theory]
    [InlineData("identity")]
    [InlineData("revision")]
    [InlineData("session")]
    [InlineData("data")]
    [InlineData("planet")]
    [InlineData("factory")]
    [InlineData("slot-removed")]
    [InlineData("adoption")]
    public void SaveCommitRejectsChangedBindingBeforeCallingNativeApi(string drift)
    {
        var (tracker, coordinator) = Setup();
        Assert.True(PrepareSave(coordinator, tracker).Success);
        switch (drift)
        {
            case "identity": GameMain.gameName = "another-slot"; break;
            case "revision": tracker.IncrementRevisionOnMainThread(); break;
            case "session": tracker.ReplaceSessionForTest("new-session"); break;
            case "data": GameMain.data = new GameData(); break;
            case "planet": GameMain.data!.localPlanet!.id = 102; break;
            case "factory": GameMain.data!.localLoadedPlanetFactory = null; break;
            case "slot-removed": GameSave.Existing = false; break;
            case "adoption": tracker.SetProtectedAdoptionExpectationForTest("resume"); break;
        }
        Assert.NotNull(coordinator.RevalidateSaveForTest());
        Assert.Throws<InvalidOperationException>(() => coordinator.CommitSaveForTest());
        Assert.Empty(GameSave.SavedNames);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("recursive")]
    [InlineData("sanitize")]
    [InlineData("path")]
    public void UnprovenSaveIdentityFailsPrepareClosed(string mode)
    {
        var (tracker, coordinator) = Setup();
        if (mode == "missing") GameSave.Existing = false;
        if (mode == "recursive") GameSave.AllowRecursive = true;
        if (mode == "sanitize") GameMain.gameName = "would:rename";
        if (mode == "path") GameMain.gameName = "folder/slot";
        Assert.Equal(BridgeErrorCodes.BridgeNotReady, PrepareSave(coordinator, tracker).Error!.Code);
        Assert.Empty(GameSave.SavedNames);
    }

    [Theory]
    [InlineData("api-false")]
    [InlineData("no-header")]
    [InlineData("wrong-tick")]
    [InlineData("post-save-drift")]
    public void AcceptedSaveWithUnprovenOutcomeQuarantinesOnlyInMemory(string failure)
    {
        var (tracker, coordinator) = Setup();
        Assert.True(PrepareSave(coordinator, tracker).Success);
        if (failure == "api-false") GameSave.SaveSucceeds = false;
        if (failure == "no-header") GameSave.HeaderTick = null;
        if (failure == "wrong-tick") GameSave.HeaderTick = GameMain.gameTick - 1;
        if (failure == "post-save-drift") GameSave.AfterSave = () => GameMain.gameName = "drifted";
        var result = coordinator.CommitSaveForTest();
        Assert.Equal(NormalActionStates.OutcomeUnknown, result.State);
        Assert.Equal(WriteHealthStates.Quarantined, tracker.CaptureOnMainThread().WriteHealth);
        Assert.Single(GameSave.SavedNames);
        Assert.Null(tracker.ResumeTicketsForTest.CurrentResumeToken);
        Assert.Equal(0, tracker.ResumeTicketsForTest.QuarantineArmCount);
        Assert.False(tracker.IsCurrentSessionOwned);
    }

    [Fact]
    public void OwnedSaveStillUsesOwnedSaveAndRecordedTickPath()
    {
        var (tracker, coordinator) = Setup(owned: true);
        Assert.True(PrepareSave(coordinator, tracker).Success);
        Assert.Equal("test-owned-save", coordinator.BoundSaveNameForTest);
        Assert.True(coordinator.CommitSaveForTest().Succeeded);
        Assert.Equal(1, tracker.OwnedSaveCallsForTest);
        Assert.Empty(GameSave.SavedNames);
        Assert.True(tracker.IsCurrentSessionOwned);
    }

    [Fact]
    public void UnownedFlightLaunchSkipsEveryCheckpointCall()
    {
        var (tracker, coordinator) = Setup();
        coordinator.FlightForTest(false, "start", false);
        Assert.Equal(0, coordinator.CheckpointCreateCallsForTest);
        Assert.Equal(0, coordinator.CheckpointsForTest.AttemptCount);
        Assert.Null(tracker.OwnedSaveName);
        Assert.True(coordinator.FlightModeMatchesForTest(false));
        Assert.False(coordinator.FlightModeMatchesForTest(true));
    }

    [Fact]
    public void UnownedFlightSuccessWithoutCheckpointCompletesWithoutQuarantine()
    {
        var (tracker, coordinator) = Setup();
        var result = coordinator.FlightForTest(false, "complete", false);
        Assert.True(result.Terminal); Assert.True(result.Succeeded);
        Assert.Equal(NormalActionStates.Completed, result.State);
        Assert.False(result.RecoveryRequired);
        Assert.Equal(0, coordinator.CheckpointsForTest.SuccessCount);
        Assert.Equal(0, tracker.ForgottenCheckpointsForTest);
        Assert.Equal(WriteHealthStates.Healthy, tracker.CaptureOnMainThread().WriteHealth);
        Assert.Equal(1, coordinator.PlayerOrderCleanupForTest);
        Assert.Equal(1, coordinator.AscentCleanupForTest);
    }

    [Fact]
    public void UnownedFlightFailureIsOrdinaryFailureWithoutCheckpointRecoveryInstruction()
    {
        var (tracker, coordinator) = Setup();
        var result = coordinator.FlightForTest(false, "fail", false);
        Assert.True(result.Terminal); Assert.False(result.Succeeded); Assert.False(result.RecoveryRequired);
        Assert.Equal(NormalActionStates.ActionFailed, result.State);
        Assert.DoesNotContain("Reload", result.Message!);
        Assert.Equal(0, coordinator.CheckpointsForTest.RecoveryCount);
        Assert.Equal(WriteHealthStates.Healthy, tracker.CaptureOnMainThread().WriteHealth);
    }

    [Theory]
    [InlineData("start")]
    [InlineData("complete")]
    [InlineData("fail")]
    public void OwnedFlightRetainsCheckpointLifecycle(string outcome)
    {
        var (tracker, coordinator) = Setup(owned: true);
        var result = coordinator.FlightForTest(true, outcome);
        Assert.Equal(outcome == "start" ? 1 : 0, coordinator.CheckpointCreateCallsForTest);
        Assert.Equal(outcome == "start" ? 1 : 0, coordinator.CheckpointsForTest.AttemptCount);
        Assert.Equal(outcome == "complete" ? 1 : 0, coordinator.CheckpointsForTest.SuccessCount);
        Assert.Equal(outcome == "complete" ? 1 : 0, tracker.ForgottenCheckpointsForTest);
        Assert.Equal(outcome == "fail", result.RecoveryRequired);
        Assert.Equal(outcome == "fail" ? 1 : 0, coordinator.CheckpointsForTest.RecoveryCount);
    }

    [Fact]
    public void OwnedFlightMissingSealStillQuarantines()
    {
        var (tracker, coordinator) = Setup(owned: true);
        var result = coordinator.FlightForTest(true, "complete", false);
        Assert.Equal(NormalActionStates.OutcomeUnknown, result.State);
        Assert.False(result.Succeeded);
        Assert.Equal(1, tracker.ResumeTicketsForTest.QuarantineArmCount);
    }

    [Fact]
    public void UnownedBlueprintProgressIsDeepCopiedSessionOnlyAndSupportsRealCancelPath()
    {
        var (tracker, coordinator) = Setup();
        var directory = Path.Combine(Path.GetTempPath(), "spherewright-package2-" + Guid.NewGuid());
        var store = Store(tracker, directory);
        var build = Build(tracker.SessionId!);
        build.Begin(tracker.SessionId!, Guid.NewGuid().ToString("D"), 1, GameMain.gameTick);
        Assert.True(store.TryPut(build));
        build.Phase = "tampered";
        Assert.True(store.TryRead(out var builds));
        Assert.Equal("running", builds.Single().Phase);
        var progress = builds.Single();
        Assert.True(coordinator.PrepareCancelForTest(store, progress.BuildId,
            progress.ProgressHash(tracker.SessionId!, tracker.CaptureOnMainThread().Revision)).Success);
        Assert.True(coordinator.CommitCancelForTest().Succeeded);
        Assert.True(store.TryRead(out builds)); Assert.Equal("cancelled", builds.Single().Phase);
        Assert.False(Directory.Exists(directory));
        Assert.False(tracker.IsCurrentSessionOwned);
        Assert.Null(tracker.OwnedSaveName); Assert.Null(tracker.ResumeTicketsForTest.CurrentResumeToken);
    }

    [Fact]
    public void UnownedBlueprintCannotCrossSessionOrStoreInstance()
    {
        var (tracker, _) = Setup();
        var directory = Path.Combine(Path.GetTempPath(), "spherewright-package2-" + Guid.NewGuid());
        var store = Store(tracker, directory);
        var old = Build(tracker.SessionId!);
        Assert.True(store.TryPut(old));
        Assert.True(Store(tracker, directory).TryRead(out var restarted)); Assert.Empty(restarted);
        tracker.ReplaceSessionForTest("session-2");
        Assert.True(store.TryRead(out var replaced)); Assert.Empty(replaced);
        Assert.False(store.TryPut(old));
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public void UnownedBlueprintWorldReplacementDiscardsStateAndStalesPreparedCancel()
    {
        var (tracker, coordinator) = Setup();
        var store = Store(tracker, Path.Combine(Path.GetTempPath(), "spherewright-package2-" + Guid.NewGuid()));
        var build = Build(tracker.SessionId!);
        Assert.True(store.TryPut(build));
        Assert.True(coordinator.PrepareCancelForTest(store, build.BuildId,
            build.ProgressHash(tracker.SessionId!, tracker.CaptureOnMainThread().Revision)).Success);
        GameMain.data = new GameData();
        Assert.False(store.TryRead(out _));
        Assert.Throws<InvalidOperationException>(() => coordinator.CommitCancelForTest());
        tracker.Configure(GameMain.data, true, true, false, allowNormalWrites: true);
        tracker.ReplaceSessionForTest("replacement-session");
        Assert.True(store.TryRead(out var after)); Assert.Empty(after);
    }

    [Fact]
    public void OwnedBlueprintStoreStillPersistsAcrossStoreInstances()
    {
        var (tracker, _) = Setup(owned: true);
        var directory = Path.Combine(Path.GetTempPath(), "spherewright-package2-" + Guid.NewGuid());
        try
        {
            var store = Store(tracker, directory);
            var build = Build(tracker.SessionId!);
            Assert.True(store.TryPut(build));
            build.Stop("cancelled"); Assert.True(store.TryPut(build));
            Assert.True(Store(tracker, directory).TryRead(out var restarted));
            Assert.Equal(build.BuildId, restarted.Single().BuildId);
            Assert.Equal("cancelled", restarted.Single().Phase);
            Assert.Single(Directory.GetFiles(Path.Combine(directory, "foundry")));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public void PublicBlueprintGateStaysOwnedWhileInternalGateUsesActionAuthority(bool owned, bool internalReader)
    {
        var (tracker, _) = Setup(owned);
        var reader = new GameStateReader(tracker, internalReader);
        var error = reader.BlueprintActionPlanetForTest(tracker.SessionId, 101);
        Assert.Equal(owned || internalReader, error is null);
    }

    [Fact]
    public void BlueprintPreparePreviewIsOnlyForNewUnownedAction()
    {
        Assert.True(NormalGameActionCoordinator.BlueprintPreviewForTest(false, null, ""));
        Assert.False(NormalGameActionCoordinator.BlueprintPreviewForTest(true, null, ""));
        Assert.False(NormalGameActionCoordinator.BlueprintPreviewForTest(false, "old-build", ""));
        Assert.False(NormalGameActionCoordinator.BlueprintPreviewForTest(false, null, "supplied-site-hash"));
        var (tracker, _) = Setup();
        tracker.Configure(GameMain.data!, true, true, false, allowNormalWrites: false);
        Assert.Equal(BridgeErrorCodes.SessionNotOwned,
            new GameStateReader(tracker, true).BlueprintActionPlanetForTest(tracker.SessionId, 101)!.Code);
    }

    [Fact]
    public void UnownedBlueprintPreviewCapturesPrivatePlayerStateWithoutRichReadOptIn()
    {
        var (tracker, _) = Setup();
        tracker.Configure(GameMain.data!, false, true, false, allowNormalWrites: true);
        Assert.True(new GameStateReader(tracker, true).BlueprintActionPlanetForTest(tracker.SessionId, 101) is null);
        Assert.Equal(BridgeErrorCodes.SessionNotOwned,
            new GameStateReader(tracker).BlueprintActionPlanetForTest(tracker.SessionId, 101)!.Code);
        Assert.True(NormalGameActionCoordinator.BlueprintPlayerHashMatchesForTest(true, "", "native-current"));
        Assert.False(NormalGameActionCoordinator.BlueprintPlayerHashMatchesForTest(false, "", "native-current"));
        Assert.False(NormalGameActionCoordinator.BlueprintPlayerHashMatchesForTest(true, "stale", "native-current"));
        Assert.True(NormalGameActionCoordinator.BlueprintPlayerHashMatchesForTest(false, "native-current", "native-current"));
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(0f)]
    [InlineData(1f)]
    [InlineData(3f)]
    public void DarkFogProjectsNativeValueWithoutChangingAuthority(float aggression)
    {
        var (tracker, _) = Setup();
        GameMain.history = new GameHistoryData { combatSettings = new CombatSettings { aggressiveness = aggression } };
        var state = tracker.CaptureOnMainThread();
        Assert.Equal(aggression, state.DarkFogAggressiveness);
        Assert.True(state.WritesAllowed); Assert.False(state.OwnedBySpherewright);
        Assert.Equal(PeacefulModeStates.ConfirmedCombat, state.PeacefulMode);
        var revision = state.Revision;
        GameMain.history.combatSettings.aggressiveness = aggression + 1;
        Assert.Equal(revision, tracker.CaptureOnMainThread().Revision);
    }

    [Fact]
    public void UnavailableOrRestrictedDarkFogTelemetryIsNull()
    {
        var (tracker, _) = Setup();
        GameMain.history = null; Assert.Null(tracker.CaptureOnMainThread().DarkFogAggressiveness);
        GameMain.history = new GameHistoryData { combatSettings = new CombatSettings { aggressiveness = float.NaN } };
        Assert.Null(tracker.CaptureOnMainThread().DarkFogAggressiveness);
        GameMain.history.combatSettings.aggressiveness = float.PositiveInfinity;
        Assert.Null(tracker.CaptureOnMainThread().DarkFogAggressiveness);
        GameMain.history.combatSettings.aggressiveness = 2;
        tracker.Configure(GameMain.data!, false, true, false, allowNormalWrites: false);
        Assert.Null(tracker.CaptureOnMainThread().DarkFogAggressiveness);
    }

    private static (GameSessionTracker Tracker, NormalGameActionCoordinator Coordinator) Setup(bool owned = false)
    {
        GameMain.gameTick = 100; GameMain.history = null;
        var data = new GameData { gameName = "manual-slot", gameDesc = new GameDesc { isPeaceMode = false },
            localPlanet = new PlanetData { id = 101 }, localLoadedPlanetFactory = new PlanetFactory { planetId = 101 } };
        var tracker = new GameSessionTracker();
        tracker.Configure(data, true, true, false, owned, allowNormalWrites: true);
        if (owned)
        {
            tracker.OwnedSessionState!.SaveName = tracker.OwnedSaveName;
            tracker.OwnedSessionState.Revision = tracker.RevisionForTest;
        }
        GameSave.Reset();
        return (tracker, new NormalGameActionCoordinator(tracker));
    }
    private static GameCallResult<PreparedNormalAction> PrepareSave(NormalGameActionCoordinator coordinator, GameSessionTracker tracker) =>
        coordinator.PrepareSaveForTest(tracker.SessionId!, new PrepareSaveRequest
        { PlanetId = 101, StateHashVersion = 1, ExpectedRevision = tracker.CaptureOnMainThread().Revision });
    private static BlueprintBuildStore Store(GameSessionTracker tracker, string directory) =>
        new(directory, "test-game", tracker, new BepInEx.Logging.ManualLogSource());
    private static BlueprintBuildState Build(string sessionId) => BlueprintBuildState.Create(new BlueprintSiteSnapshot
    {
        SessionId = sessionId, PlanetId = 101, CapturedAtGameTick = 100,
        NativeCheckPerformed = true, NativeCheckPassed = true, InventorySufficient = true, TechnologySatisfied = true,
        Objects = new List<BlueprintSiteObject> { new() { Index = 0, ItemId = 2001 } },
    });
}
