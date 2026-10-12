using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Players;

namespace Spherewright.Plugin.Game;

internal sealed partial class GameStateReader
{
    internal static double PassiveDriftClockSeconds() =>
        System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency;

    internal PassiveDriftMoveInspection? LastPassiveDriftMoveInspection =>
        _sessions.PassiveDriftMoveInspections.Read(GameMain.data, _sessions.SessionId,
            GameMain.localPlanet?.id ?? 0, PassiveDriftClockSeconds());

    // Public inspection publishes evidence; private action reads never refresh it.
    private void CapturePassiveDriftMoveInspection(PlayerStateSnapshot player, bool hasPlayerOrder)
    {
        var data = GameMain.data;
        if (data is null || !_sessions.IsCurrentGameDataReadable(data))
        {
            _sessions.PassiveDriftMoveInspections.Clear();
            return;
        }
        _sessions.PassiveDriftMoveInspections.Capture(data, player, hasPlayerOrder,
            PassiveDriftClockSeconds(), DateTimeOffset.UtcNow);
    }
}
