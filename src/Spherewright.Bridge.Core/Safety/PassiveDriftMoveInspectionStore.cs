using Spherewright.Contracts.Players;

namespace Spherewright.Bridge.Core.Safety;

// Shared only by readers of one tracker. Access occurs on the Unity main thread.
// World reference, session, planet and original age all invalidate the evidence.
public sealed class PassiveDriftMoveInspectionStore
{
    private object? _world;
    private string? _sessionId;
    private int _planetId;
    private double _capturedAtSeconds;
    private PassiveDriftMoveInspection? _inspection;

    public void Clear()
    {
        _world = null;
        _sessionId = null;
        _planetId = 0;
        _inspection = null;
    }

    public void Capture(object world, PlayerStateSnapshot player, bool hasPlayerOrder,
        double nowSeconds, DateTimeOffset utcNow)
    {
        Clear();
        var inspection = hasPlayerOrder ? null
            : PassiveDriftMoveInspection.Capture(player, nowSeconds, utcNow);
        if (inspection is null) return;
        _world = world;
        _sessionId = player.SessionId;
        _planetId = player.PlanetId;
        _capturedAtSeconds = nowSeconds;
        _inspection = inspection;
    }

    public PassiveDriftMoveInspection? Read(object? world, string? sessionId,
        int planetId, double nowSeconds)
    {
        var age = nowSeconds - _capturedAtSeconds;
        if (_inspection is null || world is null || !ReferenceEquals(_world, world)
            || !string.Equals(_sessionId, sessionId, StringComparison.Ordinal)
            || planetId != _planetId || double.IsNaN(age) || double.IsInfinity(age)
            || age < 0d || age > PassiveDriftMoveInspection.MaximumAgeSeconds)
        {
            Clear();
            return null;
        }
        return _inspection;
    }
}
