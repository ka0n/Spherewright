using Spherewright.Contracts.Errors;
using Spherewright.Plugin.Game;
using Xunit;

namespace Spherewright.Plugin.ReadAccess.Tests;

[Collection("Plugin read-access tests")]
public sealed class IntegrationAuthorityMatrixTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void CurrentCombatWorldMatrixAppliesToActualReaderPrepareAndCommitGates(bool reads, bool writes)
    {
        foreach (var aggression in new float?[] { null, 0, 1, 3, float.NaN })
        {
            var data = new GameData
            {
                gameDesc = new GameDesc { isPeaceMode = false },
                localPlanet = new PlanetData { id = 10 },
                localLoadedPlanetFactory = new PlanetFactory { planetId = 10 },
            };
            var tracker = new GameSessionTracker();
            tracker.Configure(data, reads, writes, false, allowNormalWrites: writes);
            GameMain.history = aggression.HasValue
                ? new GameHistoryData { combatSettings = new CombatSettings { aggressiveness = aggression.Value } }
                : null;
            var publicReader = new GameStateReader(tracker);
            var internalReader = new GameStateReader(tracker, allowAuthorizedUnownedNormalActionReads: true);
            var coordinator = new NormalGameActionCoordinator(tracker);
            Assert.Equal(reads, publicReader.ReadablePlanet(tracker.SessionId, 10, out _) is null);
            Assert.Equal(reads || writes, internalReader.ReadablePlanet(tracker.SessionId, 10, out _) is null);
            Assert.Equal(writes, coordinator.PrepareAuthorizationForTest(tracker.SessionId, 10, 1) is null);
            Assert.Equal(writes, coordinator.CommitAuthorizationForTest(tracker.SessionId, 10, tracker.SessionId, 10) is null);
            Assert.False(tracker.CaptureOnMainThread().OwnedBySpherewright);
            if (!writes) continue;
            Assert.Equal(BridgeErrorCodes.StaleSession,
                coordinator.PrepareAuthorizationForTest("stale", 10, 1)!.Code);
            tracker.SetProtectedAdoptionExpectationForTest("resume");
            Assert.NotNull(coordinator.PrepareAuthorizationForTest(tracker.SessionId, 10, 1));
            Assert.NotNull(coordinator.CommitAuthorizationForTest(tracker.SessionId, 10, tracker.SessionId, 10));
            tracker.SetProtectedAdoptionExpectationForTest("none");
            GameMain.data = new GameData();
            Assert.NotNull(internalReader.ReadablePlanet(tracker.SessionId, 10, out _));
            Assert.NotNull(coordinator.PrepareAuthorizationForTest(tracker.SessionId, 10, 1));
            Assert.NotNull(coordinator.CommitAuthorizationForTest(tracker.SessionId, 10, tracker.SessionId, 10));
        }
    }
}
