using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.Game;
using Xunit;

namespace Spherewright.Plugin.ReadAccess.Tests;

[Collection("Plugin read-access tests")]
public sealed class MaterialInventoryReadAccessTests
{
    [Theory]
    [InlineData(true, false, false, true)]
    [InlineData(false, true, false, true)]
    [InlineData(false, true, true, true)]
    [InlineData(false, false, false, false)]
    [InlineData(false, false, true, false)]
    public void PublicCutUsesRichReadAuthorityIndependentlyOfNormalWrites(
        bool owned, bool richReads, bool normalWrites, bool allowed)
    {
        var (tracker, reader) = Create(owned, richReads, normalWrites);
        var result = reader.InspectFactoryEntityOnMainThread(tracker.SessionId, Request());

        Assert.Equal(allowed, result.Success);
        Assert.Equal(allowed ? 1 : 0, reader.CutCapturesForTest);
        Assert.Equal(owned, tracker.CaptureOnMainThread().OwnedBySpherewright);
        Assert.Equal(owned ? ReadAccessModes.Owned
            : richReads ? ReadAccessModes.ObservedUnowned : ReadAccessModes.Restricted,
            tracker.CaptureOnMainThread().ReadAccessMode);
        if (allowed)
        {
            Assert.NotNull(result.Value!.MaterialInventoryCut);
            Assert.Equal(new[] { 1, 2 }, result.Value.MaterialInventoryCut!.RequestedObjectIds);
        }
        else
        {
            Assert.Equal(BridgeErrorCodes.SessionNotOwned, result.Error!.Code);
            Assert.Null(result.Value);
        }
    }

    [Fact]
    public void PrivateActionReaderDoesNotChangePublicCutAccess()
    {
        var (tracker, publicReader) = Create(owned: false, richReads: false, normalWrites: true);
        var actionReader = new GameStateReader(tracker, allowAuthorizedUnownedNormalActionReads: true);

        Assert.True(actionReader.InspectFactoryEntityOnMainThread(tracker.SessionId, Request()).Success);
        Assert.Equal(1, actionReader.CutCapturesForTest);
        Assert.Equal(BridgeErrorCodes.SessionNotOwned,
            publicReader.InspectFactoryEntityOnMainThread(tracker.SessionId, Request()).Error!.Code);
        Assert.Equal(0, publicReader.CutCapturesForTest);
        Assert.Equal(ReadAccessModes.Restricted, tracker.CaptureOnMainThread().ReadAccessMode);
    }

    [Fact]
    public void CutRejectsStaleSessionAndDifferentGameDataBeforeCapture()
    {
        var (tracker, reader) = Create(owned: false, richReads: true, normalWrites: false);
        Assert.Equal(BridgeErrorCodes.StaleSession,
            reader.InspectFactoryEntityOnMainThread("stale-session", Request()).Error!.Code);
        GameMain.data = new GameData();
        Assert.Equal(BridgeErrorCodes.SessionNotOwned,
            reader.InspectFactoryEntityOnMainThread(tracker.SessionId, Request()).Error!.Code);
        Assert.Equal(0, reader.CutCapturesForTest);
    }

    [Fact]
    public void CutRetainsSelectionBudgetAndDuplicateRejectionBeforeCapture()
    {
        var (tracker, reader) = Create(owned: false, richReads: true, normalWrites: false);
        var request = Request();
        request.MaterialInventoryObjectIds = new List<int> { 1, 1 };
        Assert.False(reader.InspectFactoryEntityOnMainThread(tracker.SessionId, request).Success);
        request.MaterialInventoryObjectIds = Enumerable.Range(1, 257).ToList();
        Assert.False(reader.InspectFactoryEntityOnMainThread(tracker.SessionId, request).Success);
        Assert.Equal(0, reader.CutCapturesForTest);
    }

    private static InspectFactoryEntityRequest Request() => new()
    {
        PlanetId = 10, ObjectId = 1, MaterialInventoryObjectIds = new List<int> { 1, 2 },
    };

    private static (GameSessionTracker, GameStateReader) Create(bool owned, bool richReads, bool normalWrites)
    {
        var data = new GameData
        {
            gameDesc = new GameDesc { isPeaceMode = true, resourceMultiplier = 1f },
            localPlanet = new PlanetData { id = 10 },
            localLoadedPlanetFactory = new PlanetFactory { planetId = 10 },
        };
        GameMain.data = data;
        GameMain.localPlanet = data.localPlanet;
        var tracker = new GameSessionTracker();
        tracker.Configure(data, observeUnowned: richReads, allowWrites: normalWrites,
            allowImport: false, owned: owned, allowNormalWrites: normalWrites);
        return (tracker, new GameStateReader(tracker));
    }
}
