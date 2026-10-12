using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Players;
using Xunit;

namespace Spherewright.Bridge.Core.Tests;

public sealed class PassiveDriftMoveInspectionStoreTests
{
    [Theory]
    [InlineData("world")]
    [InlineData("session")]
    [InlineData("planet")]
    [InlineData("expired")]
    [InlineData("backwards")]
    [InlineData("nan")]
    [InlineData("infinite")]
    public void InvalidContextPermanentlyClearsEvidenceEvenIfOriginalContextReturns(string change)
    {
        var world = new object();
        var player = Player();
        var store = new PassiveDriftMoveInspectionStore();
        store.Capture(world, player, false, 10, DateTimeOffset.UtcNow);
        var inspection = store.Read(world, "session", 10, 10)!;
        Assert.NotNull(inspection);
        var actualWorld = change == "world" ? new object() : world;
        var session = change == "session" ? "other" : "session";
        var planet = change == "planet" ? 11 : 10;
        var now = change switch
        {
            "expired" => 12.001,
            "backwards" => 9.999,
            "nan" => double.NaN,
            "infinite" => double.PositiveInfinity,
            _ => 10.2,
        };
        Assert.Null(store.Read(actualWorld, session, planet, now));
        Assert.Null(store.Read(world, "session", 10, 10.3));
    }

    [Fact]
    public void ReadDoesNotRenewExpiryAndNewInspectionOrOrderInvalidatesOriginal()
    {
        var world = new object();
        var player = Player();
        var store = new PassiveDriftMoveInspectionStore();
        store.Capture(world, player, false, 10, DateTimeOffset.UtcNow);
        var original = store.Read(world, "session", 10, 10)!;
        Assert.Same(original, store.Read(world, "session", 10, 11.9));
        Assert.Null(store.Read(world, "session", 10, 12.001));
        store.Capture(world, player, false, 13, DateTimeOffset.UtcNow);
        Assert.NotSame(original, store.Read(world, "session", 10, 13));
        store.Capture(world, player, true, 13.1, DateTimeOffset.UtcNow);
        Assert.Null(store.Read(world, "session", 10, 13.1));
    }

    private static PlayerStateSnapshot Player()
    {
        var player = new PlayerStateSnapshot
        {
            SessionId = "session", PlanetId = 10, IsAlive = true, IsOnPlanet = true,
            MovementState = "Drift", Speed = .1f,
            CoreEnergy = 10, CoreEnergyCapacity = 20, ReactorEnergy = 1,
        };
        player.StateHash = CanonicalStateHash.PlayerAction(player);
        return player;
    }
}
