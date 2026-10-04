namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    // Called only from the same Unity main-thread work item as protected import.
    public bool HasActiveActionsOnMainThread() => _actions.ActiveValues.Any(action => !action.Terminal);
}
