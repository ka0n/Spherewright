using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Journals;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.Game;
using Xunit;

namespace Spherewright.Plugin.ReadAccess.Tests;

[Collection("Plugin read-access tests")]
public sealed class ImportOverlapTests
{
    [Theory]
    [InlineData(NormalActionKinds.BlueprintBuild)]
    [InlineData(NormalActionKinds.Move)]
    [InlineData(NormalActionKinds.InterplanetaryFlight)]
    public void ActiveNormalActionRejectsImportPrepareWithoutOwnershipTransition(string kind)
    {
        var (tracker, normal, import) = Setup();
        normal.AddNormalActionForImportTest(kind);
        var result = Prepare(tracker, import);
        Assert.Equal(BridgeErrorCodes.ServerBusy, result.Error?.Code);
        Assert.Contains("non-terminal normal action", result.Error!.Message);
        Assert.True(normal.HasActiveActionsOnMainThread());
        Assert.False(tracker.IsCurrentSessionOwned);
        Assert.Null(tracker.OwnedSaveName);
        Assert.Equal(0, tracker.ImportCallsForTest);
    }

    [Fact]
    public void ActionStartedAfterImportPrepareRejectsCommitWithoutConsumingPlanOrKey()
    {
        var (tracker, normal, import) = Setup();
        var data = GameMain.data;
        var plan = Prepare(tracker, import).Value!;
        var request = Confirm(plan);
        normal.AddNormalActionForImportTest(NormalActionKinds.BlueprintBuild);
        var rejected = import.CommitOnMainThread(tracker.SessionId, request);
        Assert.Equal(BridgeErrorCodes.ServerBusy, rejected.Error?.Code);
        Assert.False(tracker.IsCurrentSessionOwned);
        Assert.Null(tracker.OwnedSaveName);
        Assert.Same(data, GameMain.data);
        Assert.True(normal.HasActiveActionsOnMainThread());
        Assert.Equal(0, tracker.ImportCallsForTest);

        normal.FinishNormalActionsForImportTest();
        // Same plan and key succeeding proves the rejection neither consumed the
        // token nor admitted/cached an import or changed the active normal action.
        var completed = import.CommitOnMainThread(tracker.SessionId, request);
        Assert.True(completed.Success);
        Assert.True(completed.Value!.Accepted);
        Assert.False(completed.Value.IdempotentReplay);
        Assert.True(tracker.IsCurrentSessionOwned);
        Assert.Equal(1, tracker.ImportCallsForTest);
    }

    [Fact]
    public void FreshImportPrepareSucceedsOnlyAfterEveryNormalActionIsTerminal()
    {
        var (tracker, normal, import) = Setup();
        normal.AddNormalActionForImportTest(NormalActionKinds.BlueprintBuild, terminal: true);
        normal.AddNormalActionForImportTest(NormalActionKinds.Move);
        Assert.False(Prepare(tracker, import).Success);
        normal.FinishNormalActionsForImportTest();
        Assert.False(normal.HasActiveActionsOnMainThread());
        var prepared = Prepare(tracker, import);
        Assert.True(prepared.Success);
        Assert.True(prepared.Value!.UserConfirmationRequired);
        Assert.False(prepared.Value.CommitAllowedNow);
        Assert.False(tracker.IsCurrentSessionOwned);
        Assert.Equal(0, tracker.ImportCallsForTest);
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void EveryExistingConfirmationDeclarationRemainsRequired(bool confirm, bool original, bool journal)
    {
        var (tracker, _, import) = Setup();
        var request = Confirm(Prepare(tracker, import).Value!);
        request.UserConfirmedInConversation = confirm;
        request.AcknowledgeOriginalSaveRemainsUnchanged = original;
        request.AcknowledgeJournalStartsAtImport = journal;
        Assert.Equal(BridgeErrorCodes.UserConfirmationRequired,
            import.CommitOnMainThread(tracker.SessionId, request).Error?.Code);
        Assert.False(tracker.IsCurrentSessionOwned);
        Assert.Equal(0, tracker.ImportCallsForTest);
        request.UserConfirmedInConversation = request.AcknowledgeOriginalSaveRemainsUnchanged
            = request.AcknowledgeJournalStartsAtImport = true;
        Assert.True(import.CommitOnMainThread(tracker.SessionId, request).Success);
    }

    [Fact]
    public void ConfirmedImportPreservesDisclosureSessionAndIdempotentReplay()
    {
        var (tracker, _, import) = Setup();
        var sessionId = tracker.SessionId;
        var plan = Prepare(tracker, import).Value!;
        Assert.True(plan.OriginalSavePreserved);
        Assert.Equal(GameplayJournalTrackingModes.AttachedExistingSave, plan.JournalTrackingMode);
        Assert.False(plan.HistoricalCoverageComplete);
        var request = Confirm(plan);
        var result = import.CommitOnMainThread(sessionId, request).Value!;
        Assert.Equal(NormalActionStates.Completed, result.State);
        Assert.Equal(sessionId, result.SessionId);
        Assert.Equal(sessionId, tracker.SessionId);
        Assert.True(result.OriginalSavePreserved);
        Assert.Equal(GameplayJournalTrackingModes.AttachedExistingSave, result.JournalTrackingMode);
        Assert.False(result.HistoricalCoverageComplete);
        Assert.True(import.CommitOnMainThread(sessionId, request).Value!.IdempotentReplay);
        Assert.Equal(1, tracker.ImportCallsForTest);
    }

    [Fact]
    public void OwnedWorldStillCannotBeImported()
    {
        var (tracker, normal, import) = Setup(owned: true);
        Assert.False(normal.HasActiveActionsOnMainThread());
        Assert.Equal(BridgeErrorCodes.SessionNotOwned, Prepare(tracker, import).Error?.Code);
        Assert.True(tracker.IsCurrentSessionOwned);
        Assert.Equal("test-owned-save", tracker.OwnedSaveName);
        Assert.Equal(0, tracker.ImportCallsForTest);
    }

    [Fact]
    public void NonPeacefulWorldStillCannotBecomeOwnedThroughConfirmedImport()
    {
        var (tracker, _, import) = Setup(peaceful: false);
        Assert.True(tracker.IsCurrentSessionAuthorizedForNormalActions);
        var request = Confirm(Prepare(tracker, import).Value!);
        Assert.Equal(BridgeErrorCodes.PeacefulModeRequired,
            import.CommitOnMainThread(tracker.SessionId, request).Error?.Code);
        Assert.False(tracker.IsCurrentSessionOwned);
        Assert.Equal(0, tracker.ImportCallsForTest);
    }

    private static (GameSessionTracker, NormalGameActionCoordinator, UserSaveImportCoordinator) Setup(
        bool owned = false, bool peaceful = true)
    {
        var data = new GameData { gameDesc = new GameDesc { isPeaceMode = peaceful },
            localPlanet = new PlanetData { id = 101 }, localLoadedPlanetFactory = new PlanetFactory { planetId = 101 } };
        var tracker = new GameSessionTracker();
        tracker.Configure(data, observeUnowned: false, allowWrites: true, allowImport: true,
            owned: owned, allowNormalWrites: true);
        var normal = new NormalGameActionCoordinator(tracker);
        return (tracker, normal, new UserSaveImportCoordinator(true, true, 60, 30, 32, tracker,
            normal.HasActiveActionsOnMainThread));
    }

    private static GameCallResult<PreparedUserSaveImportPlan> Prepare(GameSessionTracker tracker, UserSaveImportCoordinator import) =>
        import.PrepareOnMainThread(tracker.SessionId, new PrepareUserSaveImportRequest { ExpectedRevision = tracker.Revision });

    private static CommitUserSaveImportRequest Confirm(PreparedUserSaveImportPlan plan) => new()
    {
        PlanToken = plan.PlanToken, IdempotencyKey = Guid.NewGuid().ToString("D"),
        UserConfirmedInConversation = true, AcknowledgeOriginalSaveRemainsUnchanged = true,
        AcknowledgeJournalStartsAtImport = true,
    };
}
