using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Factory;
using Spherewright.Plugin.Game;
using Xunit;

namespace Spherewright.Plugin.ReadAccess.Tests;

[Collection("Plugin read-access tests")]
public sealed class RemoteFactoryReadAccessTests
{
    [Theory]
    [InlineData(true, false, false, 10, true)]
    [InlineData(true, false, false, 11, true)]
    [InlineData(false, true, false, 10, true)]
    [InlineData(false, true, false, 11, true)]
    [InlineData(false, true, true, 11, true)]
    [InlineData(false, false, true, 11, false)]
    [InlineData(false, false, false, 11, false)]
    public void PublicLocalAndRemoteInspectionUseRichReadAuthority(
        bool owned, bool reads, bool writes, int planet, bool allowed)
    {
        var (tracker, reader, _) = World(owned, reads, writes);
        var result = Inspect(reader, tracker.SessionId, planet);
        Assert.Equal(allowed, result.Success);
        Assert.Equal(owned, tracker.CaptureOnMainThread().OwnedBySpherewright);
        if (allowed) Assert.Equal(planet, result.Value!.PlanetId);
        else Assert.Equal(BridgeErrorCodes.SessionNotOwned, result.Error!.Code);
    }

    [Fact]
    public void PrivateWriteAuthorityCannotGrantRemotePublicReadsOrRemoteActions()
    {
        var (tracker, _, _) = World(false, false, true);
        var reader = new GameStateReader(tracker, allowAuthorizedUnownedNormalActionReads: true);
        Assert.True(reader.InspectFactoryEntityOnMainThread(tracker.SessionId, Request(10)).Success);
        Assert.Equal(BridgeErrorCodes.SessionNotOwned, Inspect(reader, tracker.SessionId, 11).Error!.Code);
        var coordinator = new NormalGameActionCoordinator(tracker);
        Assert.Equal(BridgeErrorCodes.NoLocalPlanet,
            coordinator.PrepareAuthorizationForTest(tracker.SessionId, 11, 1)!.Code);
        Assert.Equal(BridgeErrorCodes.StaleState,
            coordinator.CommitAuthorizationForTest(tracker.SessionId, 10, tracker.SessionId, 11)!.Code);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TwoArgumentActionInspectionRemainsLocalEvenWithPublicReads(bool owned)
    {
        var (tracker, reader, _) = World(owned, true, true);
        Assert.Equal(BridgeErrorCodes.StaleState,
            reader.InspectFactoryEntityOnMainThread(tracker.SessionId, Request(11)).Error!.Code);
        Assert.Equal(BridgeErrorCodes.NoLocalPlanet,
            new NormalGameActionCoordinator(tracker).PrepareAuthorizationForTest(tracker.SessionId, 11, 1)!.Code);
    }

    [Theory]
    [InlineData("other-star")]
    [InlineData("missing-factory")]
    [InlineData("not-resident")]
    [InlineData("pool-index")]
    [InlineData("factory-planet-id")]
    [InlineData("planet-reference")]
    [InlineData("factory-reference")]
    [InlineData("local-star-membership")]
    [InlineData("duplicate-planet")]
    [InlineData("stale-session")]
    [InlineData("replaced-world")]
    [InlineData("protected-adoption")]
    public void RejectsUnavailableMismatchedOrStaleRemoteScope(string change)
    {
        var (tracker, reader, data) = World(false, true, true);
        var session = tracker.SessionId;
        switch (change)
        {
            case "other-star": GameMain.localStar!.planets = new[] { data.localPlanet! }; break;
            case "missing-factory": data.factories = null; break;
            case "not-resident": data.factories![1].planet!.factory = null; break;
            case "pool-index": data.factories![1].planet!.factoryIndex = 9; break;
            case "factory-planet-id": data.factories![1].planetId = 12; break;
            case "planet-reference": data.factories![1].planet = new PlanetData { id = 11 }; break;
            case "factory-reference": data.factories![1].planet!.factory = new PlanetFactory { planetId = 11 }; break;
            case "local-star-membership": GameMain.localStar!.planets![0] = new PlanetData { id = 10 }; break;
            case "duplicate-planet": GameMain.localStar!.planets = new[] { data.localPlanet!, data.factories![1].planet!, new PlanetData { id = 11 } }; break;
            case "stale-session": session = "stale"; break;
            case "replaced-world": GameMain.data = new GameData(); break;
            case "protected-adoption": tracker.SetProtectedAdoptionExpectationForTest("resume"); break;
            default: throw new InvalidOperationException(change);
        }
        Assert.False(Inspect(reader, session, 11).Success);
    }

    [Fact]
    public void RemoteGeometryRemainsUnavailableWithoutLosingOrdinaryRemoteRead()
    {
        var (tracker, reader, _) = World(false, true, false);
        var request = Request(11);
        request.Geometry = new FactoryGeometryReadRequest { ObjectIds = new() { 1 } };
        Assert.Equal(BridgeErrorCodes.InvalidRequest,
            reader.InspectFactoryEntityOnMainThread(tracker.SessionId, request, true).Error!.Code);
        Assert.True(Inspect(reader, tracker.SessionId, 11).Success);
    }

    private static GameCallResult<FactoryEntitySnapshot> Inspect(GameStateReader reader, string? session, int planet) =>
        reader.InspectFactoryEntityOnMainThread(session, Request(planet), allowCurrentStarRemoteReadOnly: true);

    private static InspectFactoryEntityRequest Request(int planet) => new() { PlanetId = planet, ObjectId = 1 };

    private static (GameSessionTracker, GameStateReader, GameData) World(bool owned, bool reads, bool writes)
    {
        var local = new PlanetData { id = 10, factoryIndex = 0, factoryLoaded = true };
        var remote = new PlanetData { id = 11, factoryIndex = 1, factoryLoaded = false };
        var localFactory = new PlanetFactory { planetId = 10, index = 0, planet = local };
        var remoteFactory = new PlanetFactory { planetId = 11, index = 1, planet = remote };
        local.factory = localFactory;
        remote.factory = remoteFactory; // Simulation-resident, display unloaded.
        var data = new GameData
        {
            gameDesc = new GameDesc { isPeaceMode = false },
            localPlanet = local, localLoadedPlanetFactory = localFactory,
            factoryCount = 2, factories = new[] { localFactory, remoteFactory },
        };
        GameMain.localStar = new StarData { planets = new[] { local, remote } };
        var tracker = new GameSessionTracker();
        tracker.Configure(data, reads, writes, false, owned, writes);
        return (tracker, new GameStateReader(tracker), data);
    }
}
