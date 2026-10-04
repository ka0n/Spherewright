using Spherewright.Contracts.Actions;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private void Complete(ActionRecord action, string message)
    {
        if (action.ActionKind == NormalActionKinds.InterplanetaryFlight)
        {
            AbortPlayerOrderIfOwned(action);
            ReleaseNativeAscentInput(action);
            var lifecycleRejection = "The bound checkpoint identity is missing.";
            if (action.Plan.FlightRequiresCheckpoint && (string.IsNullOrWhiteSpace(action.FlightCheckpointId)
                || !_flightCheckpoints.TryMarkFlightSucceeded(
                    action.FlightCheckpointId!,
                    action.ActionId,
                    GameMain.gameTick,
                    out lifecycleRejection)))
            {
                action.State = NormalActionStates.OutcomeUnknown;
                action.Terminal = true;
                action.Succeeded = false;
                action.CompletedAtGameTick = GameMain.gameTick;
                action.Message = $"The physical flight completed, but its rollback checkpoint could not be sealed before the primary save: {lifecycleRejection}";
                action.AfterInventory = CaptureInventory(GameMain.mainPlayer);
                action.AfterStateHash ??= CaptureAfterStateHash(action);
                _sessions.QuarantineWritesOnMainThread(action.ActionId, action.Message);
                return;
            }

            if (action.Plan.FlightRequiresCheckpoint) _sessions.ForgetCurrentFlightCheckpoint(action.FlightCheckpointId!);
        }

        action.State = NormalActionStates.Completed;
        action.Terminal = true;
        action.Succeeded = true;
        action.CompletedAtGameTick = GameMain.gameTick;
        action.Message = message;
        action.AfterInventory = CaptureInventory(GameMain.mainPlayer);
        action.AfterStateHash = CaptureAfterStateHash(action);
        _sessions.IncrementRevisionOnMainThread();
    }

    private void Fail(ActionRecord action, string message)
    {
        if (action.ActionKind == NormalActionKinds.InterplanetaryFlight)
        {
            AbortPlayerOrderIfOwned(action);
            ReleaseNativeAscentInput(action);
            if (action.Plan.FlightRequiresCheckpoint && !string.IsNullOrWhiteSpace(action.FlightCheckpointId))
            {
                action.RecoveryRequired = true;
                if (!_flightCheckpoints.TryMarkRecoveryRequired(
                        action.FlightCheckpointId!,
                        action.ActionId,
                        GameMain.gameTick,
                        out var lifecycleRejection))
                {
                    message += $" Checkpoint lifecycle persistence also failed: {lifecycleRejection}";
                }
            }
        }

        action.State = action.RecoveryRequired
            ? NormalActionStates.RecoveryRequired
            : NormalActionStates.ActionFailed;
        action.Terminal = true;
        action.Succeeded = false;
        action.CompletedAtGameTick = GameMain.gameTick;
        action.Message = message;
        action.AfterInventory = CaptureInventory(GameMain.mainPlayer);
        action.AfterStateHash = CaptureAfterStateHash(action);
    }

}
