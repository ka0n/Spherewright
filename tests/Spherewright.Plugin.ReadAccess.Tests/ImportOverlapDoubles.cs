using Spherewright.Bridge.Core.Safety;

namespace UnityEngine
{
    public class Object
    {
        public static T? FindObjectOfType<T>() where T : class => null;
    }
}

public sealed class GameLoader { }

namespace Spherewright.Plugin.Game
{
    internal sealed partial class GameSessionTracker
    {
        public long Revision => _revision;
        public string WriteHealth => _writeHealth;
        public int ImportCallsForTest { get; private set; }

        // Native save/adoption boundary double. Candidate and confirmation checks
        // are the linked production implementations; no real save is touched.
        public bool TryImportCurrentSessionAsOwnedCopyOnMainThread(
            string sessionId, long revision, GameData data, string name, string actionId,
            out long? savedTick, out bool unknown, out string? rejection)
        {
            ImportCallsForTest++;
            savedTick = null; unknown = false; rejection = null;
            if (!UserSaveImportSafetyPolicy.IsEnabled(_writesConfigured, _userSaveImportConfigured)
                || !UserSaveImportSafetyPolicy.MatchesPreparedCandidate(
                    sessionId, _sessionId, revision, _revision, data, _observedData!))
                return false;
            _ownedData = data; _ownedSaveName = name;
            savedTick = GameMain.gameTick;
            return true;
        }
    }

    internal sealed partial class NormalGameActionCoordinator
    {
        public void AddNormalActionForImportTest(string kind, bool terminal = false) =>
            _actions.ActiveValues.Add(new ActionRecord { ActionKind = kind, Terminal = terminal });
        public void FinishNormalActionsForImportTest()
        {
            foreach (var action in _actions.ActiveValues) action.Terminal = true;
        }
    }
}
