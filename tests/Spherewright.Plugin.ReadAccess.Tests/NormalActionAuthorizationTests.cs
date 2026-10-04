using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.Game;
using Xunit;

namespace Spherewright.Plugin.ReadAccess.Tests;

[Collection("Plugin read-access tests")]
public sealed class NormalActionAuthorizationTests
{
    [Theory]
    [InlineData(false, false, false, false, "restricted")]
    [InlineData(false, false, true, false, "observed_unowned")]
    [InlineData(false, true, false, false, "restricted")]
    [InlineData(false, true, true, false, "observed_unowned")]
    [InlineData(true, false, false, false, "restricted")]
    [InlineData(true, false, true, false, "observed_unowned")]
    [InlineData(true, true, false, true, "restricted")]
    [InlineData(true, true, true, true, "observed_unowned")]
    public void UnownedWriteAuthorityAndRichReadAuthorityAreIndependent(
        bool allowWrites,
        bool allowUnownedNormalWrites,
        bool allowUnownedRichReads,
        bool expectedWritesAllowed,
        string expectedReadMode)
    {
        var data = CreateData(peaceful: true);
        var tracker = CreateTracker(data, allowWrites, allowUnownedNormalWrites, allowUnownedRichReads);

        var state = tracker.CaptureOnMainThread();

        Assert.True(tracker.IsCurrentExactUnownedSession);
        Assert.Equal(expectedWritesAllowed, tracker.IsCurrentSessionAuthorizedForNormalActions);
        Assert.False(state.OwnedBySpherewright);
        Assert.Equal(expectedWritesAllowed, state.WritesAllowed);
        Assert.Equal(expectedReadMode, state.ReadAccessMode);
        Assert.Equal(!allowUnownedRichReads, state.AccessRestricted);
        if (allowUnownedRichReads || expectedWritesAllowed)
        {
            Assert.Equal(10, state.LocalPlanetId);
            Assert.Equal(12345, state.GameTick);
        }
        else
        {
            Assert.Null(state.LocalPlanetId);
            Assert.Null(state.GameTick);
        }
        Assert.Equal(expectedWritesAllowed, state.Capabilities.Contains("action.read"));
        Assert.Equal(expectedWritesAllowed, state.Capabilities.Contains("normal-game.prepare"));

        var publicRichReadCapabilities = new[]
        {
            "player.read", "progression.read", "factory.read", "overseer.read", "resource.read",
        };
        foreach (var capability in publicRichReadCapabilities)
        {
            Assert.Equal(allowUnownedRichReads, state.Capabilities.Contains(capability));
        }
        Assert.Equal(allowUnownedNormalWrites && !allowWrites,
            state.WriteBlockers.Any(blocker => blocker.Code == BridgeErrorCodes.WritesDisabled));
    }

    [Fact]
    public void InternalNormalActionReaderCanReadAuthorizedExactSessionWhilePublicReaderRemainsRestricted()
    {
        var data = CreateData(peaceful: true);
        var tracker = CreateTracker(data, allowWrites: true, allowUnownedNormalWrites: true, allowUnownedRichReads: false);
        var publicReader = new GameStateReader(tracker);
        var internalReader = new GameStateReader(tracker, allowAuthorizedUnownedNormalActionReads: true);

        var publicError = publicReader.ReadablePlanet(tracker.SessionId, 10, out var publicFactory);
        var internalError = internalReader.ReadablePlanet(tracker.SessionId, 10, out var internalFactory);

        Assert.Equal(BridgeErrorCodes.SessionNotOwned, publicError?.Code);
        Assert.Null(publicFactory);
        Assert.Null(internalError);
        Assert.Same(data.localLoadedPlanetFactory, internalFactory);
        Assert.DoesNotContain("player.read", tracker.CaptureOnMainThread().Capabilities);
    }

    [Fact]
    public void ProtectedAdoptionExpectationDeniesUnownedWriteAuthorityAndInternalReads()
    {
        var data = CreateData(peaceful: true);
        var tracker = CreateTracker(data, allowWrites: true, allowUnownedNormalWrites: true, allowUnownedRichReads: false);
        tracker.SetProtectedAdoptionExpectationForTest("resume");
        var reader = new GameStateReader(tracker, allowAuthorizedUnownedNormalActionReads: true);

        var error = reader.ReadablePlanet(tracker.SessionId, 10, out var factory);
        var state = tracker.CaptureOnMainThread();

        Assert.False(tracker.IsCurrentExactUnownedSession);
        Assert.False(tracker.IsCurrentSessionAuthorizedForNormalActions);
        Assert.False(state.WritesAllowed);
        Assert.DoesNotContain("normal-game.prepare", state.Capabilities);
        Assert.Equal(BridgeErrorCodes.SessionNotOwned, error?.Code);
        Assert.Null(factory);
    }

    [Fact]
    public void PhaseOneWithoutUnownedWriteOptInRemainsReadOnlyAndCannotPrepare()
    {
        var data = CreateData(peaceful: true);
        var tracker = CreateTracker(data, allowWrites: true, allowUnownedNormalWrites: false, allowUnownedRichReads: false);
        var coordinator = new NormalGameActionCoordinator(tracker);
        var state = tracker.CaptureOnMainThread();

        Assert.False(state.WritesAllowed);
        Assert.DoesNotContain("normal-game.prepare", state.Capabilities);
        Assert.DoesNotContain("action.read", state.Capabilities);
        Assert.Empty(state.WriteBlockers);
        Assert.Equal(BridgeErrorCodes.SessionNotOwned,
            coordinator.PrepareAuthorizationForTest(tracker.SessionId, 10, stateHashVersion: 1)?.Code);

        Assert.Equal(BridgeErrorCodes.StaleState,
            coordinator.PrepareAuthorizationForTest(tracker.SessionId, 10, stateHashVersion: 2)?.Code);

        var unloadedTracker = CreateTracker(CreateData(peaceful: true), allowWrites: true,
            allowUnownedNormalWrites: true, allowUnownedRichReads: false);
        unloadedTracker.SetGameLoadedForTest(false);
        var unloadedCoordinator = new NormalGameActionCoordinator(unloadedTracker);
        Assert.Equal(BridgeErrorCodes.GameNotLoaded,
            unloadedCoordinator.PrepareAuthorizationForTest("session-1", 10, stateHashVersion: 1)?.Code);
    }

    [Fact]
    public void UnownedNonPeacefulModeDoesNotBlockAuthorizedNormalWritesWhileOwnedPolicyStillRequiresPeace()
    {
        var data = CreateData(peaceful: false);
        var tracker = CreateTracker(data, allowWrites: true, allowUnownedNormalWrites: true, allowUnownedRichReads: false);

        var unownedState = tracker.CaptureOnMainThread();
        var ownedPolicy = tracker.CreateWriteBlockersForTest(PeacefulModeStates.ConfirmedCombat);
        var peacefulOwnedPolicy = tracker.CreateWriteBlockersForTest(PeacefulModeStates.ConfirmedPeaceful);
        var unownedPolicy = tracker.CreateWriteBlockersForTest(PeacefulModeStates.ConfirmedCombat, unownedNormalActions: true);

        Assert.True(unownedState.WritesAllowed);
        Assert.Empty(unownedState.WriteBlockers);
        Assert.Contains(ownedPolicy, blocker => blocker.Code == BridgeErrorCodes.PeacefulModeRequired);
        Assert.Empty(peacefulOwnedPolicy);
        Assert.DoesNotContain(unownedPolicy, blocker => blocker.Code == BridgeErrorCodes.PeacefulModeRequired);
        Assert.DoesNotContain(unownedPolicy, blocker => blocker.Code.Contains("aggress", StringComparison.OrdinalIgnoreCase));

        var ownedData = CreateData(peaceful: false);
        var ownedTracker = new GameSessionTracker();
        ownedTracker.Configure(ownedData, observeUnowned: false, allowWrites: false, allowImport: false, owned: true);
        Assert.True(ownedTracker.IsCurrentSessionAuthorizedForNormalActions);
        Assert.Contains(ownedTracker.CreateWriteBlockersForTest(PeacefulModeStates.ConfirmedCombat),
            blocker => blocker.Code == BridgeErrorCodes.PeacefulModeRequired);
        Assert.DoesNotContain(ownedTracker.CreateWriteBlockersForTest(PeacefulModeStates.ConfirmedPeaceful),
            blocker => blocker.Code == BridgeErrorCodes.PeacefulModeRequired);
    }

    [Fact]
    public void PrepareAndCommitRequireExactCurrentSessionAndPlanet()
    {
        var data = CreateData(peaceful: true);
        var tracker = CreateTracker(data, allowWrites: true, allowUnownedNormalWrites: true, allowUnownedRichReads: false);
        var coordinator = new NormalGameActionCoordinator(tracker);

        Assert.Null(coordinator.PrepareAuthorizationForTest(tracker.SessionId, 10, stateHashVersion: 1));
        Assert.Equal(BridgeErrorCodes.StaleSession,
            coordinator.PrepareAuthorizationForTest("other-session", 10, stateHashVersion: 1)?.Code);
        Assert.Equal(BridgeErrorCodes.NoLocalPlanet,
            coordinator.PrepareAuthorizationForTest(tracker.SessionId, 11, stateHashVersion: 1)?.Code);

        Assert.Null(coordinator.CommitAuthorizationForTest("session-1", 10, "session-1", 10));
        Assert.Equal(BridgeErrorCodes.StaleSession,
            coordinator.CommitAuthorizationForTest("session-1", 10, "other-session", 10)?.Code);
        Assert.Equal(BridgeErrorCodes.StaleState,
            coordinator.CommitAuthorizationForTest("session-1", 10, "session-1", 11)?.Code);

        GameMain.data = CreateData(peaceful: true);
        GameMain.localPlanet = GameMain.data.localPlanet;
        Assert.False(coordinator.ActiveActionSessionCurrentForTest("session-1", 10, isFlight: false));
        Assert.Equal(BridgeErrorCodes.SessionNotOwned,
            coordinator.PrepareAuthorizationForTest("session-1", 10, stateHashVersion: 1)?.Code);
    }

    [Fact]
    public void UnownedQuarantineBlocksCommitsWithoutArmingOwnedResumeTicketAndCanBeClearedInPlace()
    {
        var data = CreateData(peaceful: false);
        var tracker = CreateTracker(data, allowWrites: true, allowUnownedNormalWrites: true, allowUnownedRichReads: false);
        var coordinator = new NormalGameActionCoordinator(tracker);
        var startingRevision = tracker.RevisionForTest;

        tracker.QuarantineWritesOnMainThread("action-1", "outcome needs reconciliation");

        var quarantined = tracker.CaptureOnMainThread();
        Assert.Equal(WriteHealthStates.Quarantined, quarantined.WriteHealth);
        Assert.Equal("action-1", quarantined.WriteQuarantineActionId);
        Assert.False(quarantined.WritesAllowed);
        Assert.Contains(quarantined.WriteBlockers, blocker => blocker.Code == BridgeErrorCodes.WriteSubsystemQuarantined);
        Assert.Contains("quarantine.reconcile", quarantined.Capabilities);
        Assert.DoesNotContain("normal-game.prepare", quarantined.Capabilities);
        Assert.Equal(startingRevision + 1, quarantined.Revision);
        Assert.Equal(0, tracker.ResumeTicketsForTest.QuarantineArmCount);
        Assert.Empty(tracker.ResumeTicketsForTest.ConsumedTokens);
        Assert.Equal(BridgeErrorCodes.WriteSubsystemQuarantined,
            coordinator.CommitAuthorizationForTest("session-1", 10, "session-1", 10)?.Code);

        Assert.False(tracker.TryClearQuarantineOnMainThread(
            "wrong-action", "outcome needs reconciliation", out _));
        Assert.False(tracker.TryClearQuarantineOnMainThread(
            "action-1", "wrong-reason", out _));

        GameMain.data = CreateData(peaceful: false);
        GameMain.localPlanet = GameMain.data.localPlanet;
        Assert.False(tracker.TryClearQuarantineOnMainThread(
            "action-1", "outcome needs reconciliation", out _));
        GameMain.data = data;
        GameMain.localPlanet = data.localPlanet;

        Assert.True(tracker.TryClearQuarantineOnMainThread(
            "action-1", "outcome needs reconciliation", out var rejection));
        Assert.Null(rejection);

        var cleared = tracker.CaptureOnMainThread();
        Assert.Equal(WriteHealthStates.Healthy, cleared.WriteHealth);
        Assert.True(cleared.WritesAllowed);
        Assert.Contains("normal-game.prepare", cleared.Capabilities);
        Assert.Equal(startingRevision + 2, cleared.Revision);
        Assert.Equal(0, tracker.ResumeTicketsForTest.QuarantineArmCount);
        Assert.Empty(tracker.ResumeTicketsForTest.ConsumedTokens);
    }

    [Fact]
    public void OwnedQuarantineStillArmsAndConsumesItsResumeTicket()
    {
        var data = CreateData(peaceful: true);
        var tracker = new GameSessionTracker();
        tracker.Configure(data, observeUnowned: false, allowWrites: false, allowImport: false, owned: true);

        tracker.QuarantineWritesOnMainThread("owned-action", "owned action needs reconciliation");

        Assert.Equal(1, tracker.ResumeTicketsForTest.QuarantineArmCount);
        Assert.Equal("test-resume-token", tracker.ResumeTicketsForTest.CurrentResumeToken);
        Assert.True(tracker.TryClearQuarantineOnMainThread(
            "owned-action", "owned action needs reconciliation", out var rejection));
        Assert.Null(rejection);
        Assert.Equal(new[] { "test-resume-token" }, tracker.ResumeTicketsForTest.ConsumedTokens);
    }

    [Fact]
    public void ActiveActionObservationSurvivesQuarantineButFailsAfterSessionReplacement()
    {
        var data = CreateData(peaceful: true);
        var tracker = CreateTracker(data, allowWrites: true, allowUnownedNormalWrites: true, allowUnownedRichReads: false);
        var coordinator = new NormalGameActionCoordinator(tracker);

        Assert.True(coordinator.ActiveActionSessionCurrentForTest("session-1", 10, isFlight: false));
        Assert.False(coordinator.ActiveActionSessionCurrentForTest("different-session", 10, isFlight: false));
        Assert.False(coordinator.ActiveActionSessionCurrentForTest("session-1", 11, isFlight: false));

        tracker.QuarantineWritesOnMainThread("action-1", "accepted action is still being observed");
        Assert.True(coordinator.ActiveActionSessionCurrentForTest("session-1", 10, isFlight: false));

        GameMain.data = CreateData(peaceful: true);
        GameMain.localPlanet = GameMain.data.localPlanet;
        Assert.False(coordinator.ActiveActionSessionCurrentForTest("session-1", 10, isFlight: false));

        var sourceRoot = FindSourceRoot();
        var source = File.ReadAllText(Path.Combine(sourceRoot, "src/Spherewright.Plugin/Game/NormalGameActionCoordinator.cs"));
        Assert.Contains("IsActiveActionSessionCurrent(action.SessionId, action.PlanetId, isFlight)",
            MethodPrefix(source, "private void UpdateActionOnMainThread"), StringComparison.Ordinal);
    }

    [Fact]
    public void RevisionChangesOnlyForAuthorizedUnownedSessionAndTracksItsPlanet()
    {
        var deniedData = CreateData(peaceful: true);
        var denied = CreateTracker(deniedData, allowWrites: true, allowUnownedNormalWrites: false, allowUnownedRichReads: false);
        var deniedRevision = denied.RevisionForTest;
        denied.IncrementRevisionOnMainThread();
        deniedData.localPlanet!.id = 11;
        denied.UpdateNormalActionPlanetForTest(deniedData);
        Assert.Equal(deniedRevision, denied.RevisionForTest);

        var authorizedData = CreateData(peaceful: true);
        var authorized = CreateTracker(authorizedData, allowWrites: true, allowUnownedNormalWrites: true, allowUnownedRichReads: false);
        var authorizedRevision = authorized.RevisionForTest;
        authorized.IncrementRevisionOnMainThread();
        Assert.Equal(authorizedRevision + 1, authorized.RevisionForTest);

        authorizedData.localPlanet!.id = 11;
        authorized.UpdateNormalActionPlanetForTest(authorizedData);
        Assert.Equal(authorizedRevision + 2, authorized.RevisionForTest);

        var ownedData = CreateData(peaceful: true);
        var owned = new GameSessionTracker();
        owned.Configure(ownedData, observeUnowned: false, allowWrites: false, allowImport: false, owned: true);
        var ownedRevision = owned.RevisionForTest;
        owned.IncrementRevisionOnMainThread();
        Assert.Equal(ownedRevision + 1, owned.RevisionForTest);
    }

    [Fact]
    public void SessionUpdateCallsNormalActionPlanetTracker()
    {
        var sourceRoot = FindSourceRoot();
        var source = File.ReadAllText(Path.Combine(sourceRoot, "src/Spherewright.Plugin/Game/GameSessionTracker.cs"));
        Assert.Contains("UpdateNormalActionPlanetOnMainThread(currentData)", MethodPrefix(source, "public void UpdateOnMainThread()"), StringComparison.Ordinal);
    }

    private static GameSessionTracker CreateTracker(
        GameData data,
        bool allowWrites,
        bool allowUnownedNormalWrites,
        bool allowUnownedRichReads)
    {
        var tracker = new GameSessionTracker();
        tracker.Configure(
            data,
            observeUnowned: allowUnownedRichReads,
            allowWrites: allowWrites,
            allowImport: false,
            allowNormalWrites: allowUnownedNormalWrites);
        GameMain.gameTick = 12345;
        return tracker;
    }

    private static GameData CreateData(bool peaceful)
    {
        var data = new GameData
        {
            gameDesc = new GameDesc { isPeaceMode = peaceful, isSandboxMode = false, resourceMultiplier = 1f },
            localPlanet = new PlanetData { id = 10, displayName = "Test Planet" },
            localLoadedPlanetFactory = new PlanetFactory { planetId = 10 },
        };
        GameMain.data = data;
        GameMain.localPlanet = data.localPlanet;
        return data;
    }

    private static string FindSourceRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Spherewright.sln"))
                && Directory.Exists(Path.Combine(directory.FullName, "src")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not find the Spherewright source root from the test output directory.");
    }

    private static string MethodPrefix(string text, string methodName)
    {
        var start = text.IndexOf(methodName, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Source method {methodName} was not found.");
        var methodText = text.Substring(start);
        var end = Regex.Match(methodText, @"(?m)^    \}");
        Assert.True(end.Success, $"Could not find a body boundary for source method {methodName}.");
        return methodText.Substring(0, end.Index);
    }
}
