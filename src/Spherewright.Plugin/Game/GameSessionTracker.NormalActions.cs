using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Sessions;

namespace Spherewright.Plugin.Game;

internal sealed partial class GameSessionTracker
{
    public void IncrementRevisionOnMainThread()
    {
        if (IsCurrentSessionAuthorizedForNormalActions)
        {
            _revision++;
        }
    }

    public void QuarantineWritesOnMainThread(string actionId, string reason)
    {
        if (!IsCurrentSessionAuthorizedForNormalActions
            || string.Equals(_writeHealth, WriteHealthStates.Quarantined, StringComparison.Ordinal))
        {
            return;
        }

        _writeHealth = WriteHealthStates.Quarantined;
        _writeQuarantineActionId = string.IsNullOrWhiteSpace(actionId) ? null : actionId;
        _writeQuarantineReason = string.IsNullOrWhiteSpace(reason) ? "A write outcome could not be proven." : reason;
        _revision++;
        if (!IsCurrentSessionOwned)
        {
            _logger.LogError("Spherewright quarantined writes in memory for the exact current unowned session");
            return;
        }
        try
        {
            if (!string.IsNullOrWhiteSpace(_ownedSaveName)
                && !string.IsNullOrWhiteSpace(_sessionId)
                && _lastPlanetId > 0
                && !string.IsNullOrWhiteSpace(_writeQuarantineActionId))
            {
                _resumeTickets.ArmFromQuarantinedOwnedSession(
                    _ownedSaveName!,
                    _sessionId!,
                    _lastPlanetId,
                    GameMain.gameTick,
                    _writeQuarantineActionId!,
                    CaptureGameplayJournalCheckpointOnMainThread());
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning($"Spherewright could not arm restart-resume after quarantine ({exception.GetType().Name})");
        }
        _logger.LogError("Spherewright quarantined writes for the current owned session");
    }

    public bool TryClearQuarantineOnMainThread(
        string expectedActionId,
        string expectedReason,
        out string? rejection)
    {
        rejection = null;
        if (!IsCurrentSessionAuthorizedForNormalActions
            || !string.Equals(_writeHealth, WriteHealthStates.Quarantined, StringComparison.Ordinal))
        {
            rejection = "The current authorized session is not quarantined.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(_writeQuarantineActionId)
            || !string.Equals(_writeQuarantineActionId, expectedActionId, StringComparison.Ordinal)
            || !string.Equals(_writeQuarantineReason, expectedReason, StringComparison.Ordinal))
        {
            rejection = "The quarantined action identity or reason changed after reconciliation was prepared.";
            return false;
        }

        var resumeToken = IsCurrentSessionOwned ? _resumeTickets.CurrentResumeToken : null;
        _writeHealth = WriteHealthStates.Healthy;
        _writeQuarantineActionId = null;
        _writeQuarantineReason = null;
        _revision++;
        if (!string.IsNullOrWhiteSpace(resumeToken))
        {
            _resumeTickets.Consume(resumeToken!);
        }
        _logger.LogInfo("Spherewright cleared write quarantine after exact action reconciliation");
        return true;
    }

    private List<WriteBlocker> CreateWriteBlockers(string peacefulState, bool unownedNormalActions = false)
    {
        var blockers = new List<WriteBlocker>();
        if (string.Equals(_writeHealth, WriteHealthStates.Quarantined, StringComparison.Ordinal))
        {
            blockers.Add(new WriteBlocker
            {
                Code = BridgeErrorCodes.WriteSubsystemQuarantined,
                Message = _writeQuarantineReason ?? "The current session write subsystem is quarantined.",
            });
        }
        if (_pendingJournalResumeTicket is not null)
        {
            blockers.Add(new WriteBlocker
            {
                Code = BridgeErrorCodes.BridgeNotReady,
                Message = "Writes are blocked until the protected gameplay journal proves the resume ticket's durable checkpoint.",
            });
        }
        if (!_writesConfigured)
        {
            blockers.Add(new WriteBlocker
            {
                Code = BridgeErrorCodes.WritesDisabled,
                Message = "Writes are disabled by configuration.",
            });
        }

        if (unownedNormalActions)
        {
            if (!_unownedNormalWritesConfigured)
            {
                blockers.Add(new WriteBlocker
                {
                    Code = BridgeErrorCodes.WritesDisabled,
                    Message = "Normal writes on unowned sessions are disabled by configuration.",
                });
            }
            if (!IsCurrentExactUnownedSession)
            {
                blockers.Add(new WriteBlocker
                {
                    Code = BridgeErrorCodes.SessionNotOwned,
                    Message = "The exact unowned session is unavailable or a protected world-adoption flow is active.",
                });
            }
            return blockers;
        }

        if (string.Equals(peacefulState, PeacefulModeStates.Unknown, StringComparison.Ordinal))
        {
            blockers.Add(new WriteBlocker
            {
                Code = BridgeErrorCodes.PeacefulModeUnknown,
                Message = "Peaceful mode could not be confirmed.",
            });
        }
        else if (!string.Equals(peacefulState, PeacefulModeStates.ConfirmedPeaceful, StringComparison.Ordinal))
        {
            blockers.Add(new WriteBlocker
            {
                Code = BridgeErrorCodes.PeacefulModeRequired,
                Message = "M0 writes require a peaceful world.",
            });
        }

        return blockers;
    }

    private void UpdateNormalActionPlanetOnMainThread(GameData currentData)
    {
        if (!IsCurrentSessionAuthorizedForNormalActions)
        {
            return;
        }

        var localPlanetId = currentData.localPlanet?.id ?? 0;
        if (_lastPlanetId != 0 && localPlanetId != _lastPlanetId)
        {
            _revision++;
        }
        _lastPlanetId = localPlanetId;
    }
}
