using Spherewright.Contracts.Sessions;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private static bool FlightAuthorityMatches(NormalActionPlanPayload plan, SessionState session) =>
        session.OwnedBySpherewright == plan.FlightRequiresCheckpoint
        && string.Equals(session.SessionId, plan.SessionId, StringComparison.Ordinal);

    private bool BeginFlightCheckpointLifecycle(ActionRecord action)
    {
        if (!action.Plan.FlightRequiresCheckpoint) return true;
        if (!EnsureFlightCheckpointOnMainThread(action)) return false;
        if (_flightCheckpoints.TryMarkAttemptStarted(action.FlightCheckpointId!, action.ActionId,
                GameMain.gameTick, out var rejection)) return true;
        Fail(action, $"Native launch was not started because its checkpoint lifecycle could not be armed: {rejection}");
        return false;
    }

    private static string FlightFailureAdvice(ActionRecord action) => action.Plan.FlightRequiresCheckpoint
        ? " Reload the bound pre-flight checkpoint before retrying."
        : " Inspect the current player and session before any new action; this flight has no Spherewright rollback checkpoint.";

    private sealed partial class NormalActionPlanPayload
    {
        public bool FlightRequiresCheckpoint { get; set; }
    }
}
