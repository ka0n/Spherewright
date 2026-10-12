using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Players;
using Spherewright.Plugin.Game;
using Xunit;

namespace Spherewright.Plugin.ReadAccess.Tests;

[Collection("Plugin read-access tests")]
public sealed class CrossReaderPassiveDriftTests
{
    [Fact]
    public void PublicInspectionIsUsableByCoordinatorWithSeparatePrivateReaderWithoutRenewal()
    {
        var (_, publicReader, privateReader, coordinator, player) = Setup();
        publicReader.PublishMoveInspectionForTest(player);
        var evidence = privateReader.LastPassiveDriftMoveInspection;
        Assert.NotNull(evidence);
        Assert.NotSame(publicReader, privateReader);
        Assert.Same(publicReader.LastPassiveDriftMoveInspection, evidence);
        var expiry = evidence!.ExpiresAtUtc;
        player.Position.X += .011f;
        Assert.True(coordinator.MoveInspectionAllowedForTest(evidence, player.StateHash, player));
        Assert.True(coordinator.MoveInspectionAllowedForTest(evidence, player.StateHash, player));
        Assert.Equal(expiry, privateReader.LastPassiveDriftMoveInspection!.ExpiresAtUtc);
    }

    [Theory]
    [InlineData("session")]
    [InlineData("planet")]
    [InlineData("expired")]
    [InlineData("displacement")]
    [InlineData("speed")]
    [InlineData("hash")]
    [InlineData("order")]
    [InlineData("active-order")]
    [InlineData("forge")]
    [InlineData("working-drone")]
    [InlineData("pending-build")]
    [InlineData("pending-repair")]
    [InlineData("movement")]
    [InlineData("world-replaced")]
    [InlineData("session-replaced")]
    [InlineData("public-refresh")]
    public void CoordinatorRejectsUnsafeOrInvalidatedCrossReaderEvidence(string change)
    {
        var (tracker, publicReader, _, coordinator, player) = Setup();
        publicReader.PublishMoveInspectionForTest(player);
        var evidence = publicReader.LastPassiveDriftMoveInspection!;
        var hash = player.StateHash;
        var hasOrder = false;
        var activeOrder = false;
        switch (change)
        {
            case "session": player.SessionId = "other"; break;
            case "planet": player.PlanetId++; break;
            case "expired":
                tracker.PassiveDriftMoveInspections.Capture(GameMain.data!, player, false,
                    GameStateReader.PassiveDriftClockSeconds() - 2.01, DateTimeOffset.UtcNow.AddSeconds(-2.01));
                break;
            case "displacement": player.Position.X += .051f; break;
            case "speed": player.Speed = .151f; break;
            case "hash": player.InventorySlotCount++; break;
            case "order": hasOrder = true; break;
            case "active-order": activeOrder = true; break;
            case "forge": player.HandcraftQueue.Add(new()); break;
            case "working-drone": player.ConstructionDrones.Working = 1; break;
            case "pending-build": player.ConstructionDrones.PendingBuildTargets = 1; break;
            case "pending-repair": player.ConstructionDrones.PendingRepairTargets = 1; break;
            case "movement": player.MovementState = "Walk"; break;
            case "world-replaced": GameMain.data = new GameData(); break;
            case "session-replaced": tracker.ChangeSessionForIntegrationTest(); break;
            case "public-refresh": publicReader.PublishMoveInspectionForTest(player); break;
            default: throw new InvalidOperationException(change);
        }
        Assert.False(coordinator.MoveInspectionAllowedForTest(evidence, hash, player, hasOrder, activeOrder));
    }

    [Fact]
    public void PublicPlayerOrderInvalidatesSharedEvidenceAndWriteOnlyReaderCannotPublish()
    {
        var (tracker, publicReader, privateReader, _, player) = Setup();
        publicReader.PublishMoveInspectionForTest(player);
        publicReader.PublishMoveInspectionForTest(player, hasOrder: true);
        Assert.Null(privateReader.LastPassiveDriftMoveInspection);
        tracker.Configure(GameMain.data!, false, true, false, allowNormalWrites: true);
        privateReader.PublishMoveInspectionForTest(player);
        Assert.Null(publicReader.LastPassiveDriftMoveInspection);
        Assert.True(tracker.IsCurrentSessionAuthorizedForNormalActions);
        Assert.False(tracker.IsCurrentGameDataReadable(GameMain.data!));
    }

    private static (GameSessionTracker, GameStateReader, GameStateReader, NormalGameActionCoordinator, PlayerStateSnapshot) Setup()
    {
        var data = new GameData
        {
            gameDesc = new GameDesc { isPeaceMode = false },
            localPlanet = new PlanetData { id = 10 },
            localLoadedPlanetFactory = new PlanetFactory { planetId = 10 },
        };
        var tracker = new GameSessionTracker();
        tracker.Configure(data, true, true, false, allowNormalWrites: true);
        var publicReader = new GameStateReader(tracker);
        var privateReader = new GameStateReader(tracker, allowAuthorizedUnownedNormalActionReads: true);
        var player = new PlayerStateSnapshot
        {
            SessionId = tracker.SessionId!, PlanetId = 10, CapturedAtGameTick = 10,
            IsAlive = true, IsOnPlanet = true, MovementState = "Drift", Speed = .12f,
            Position = new Vector3Snapshot { X = 1, Y = 200 },
            CoreEnergy = 100, CoreEnergyCapacity = 200, ReactorEnergy = 10,
            InventorySlotCount = 40,
        };
        player.StateHash = CanonicalStateHash.PlayerAction(player);
        return (tracker, publicReader, privateReader, new NormalGameActionCoordinator(tracker, privateReader), player);
    }
}
