using Spherewright.Bridge.Core.Factory;
using Spherewright.Contracts.Errors;

namespace Spherewright.Plugin.Game;

internal sealed partial class GameStateReader
{
    private BridgeError? ValidatePublicCurrentStarFactoryReadOnMainThread(
        string? requestedSessionId,
        int requestedPlanetId,
        out PlanetFactory? factory)
    {
        factory = null;
        var localPlanet = GameMain.localPlanet;
        var localStar = GameMain.localStar;
        if (localPlanet is null || localStar?.planets is null)
        {
            return NotReady("The current readable local star system is not ready for a same-star factory read.");
        }

        // Reuse the existing exact-readable-session and local-factory guard before
        // using the current star as the only remote-read scope.
        var localError = ValidateReadablePlanetOnMainThread(requestedSessionId, localPlanet.id, out _);
        if (localError is not null) return localError;
        var gameData = GameMain.data;
        if (gameData is null) return NotReady("The exact current game data is not ready.");
        // Remote inspection is a public read even if invoked on the private reader.
        // Normal-action authority cannot widen its scope.
        if (!_sessions.IsCurrentGameDataReadable(gameData))
            return BridgeError.Create(BridgeErrorCodes.SessionNotOwned,
                "Public remote factory reads require current rich-read authority.", false,
                "Enable AllowUnownedRichReads for this exact session or use an owned world.");

        var localPlanetMembership = localStar.planets.Count(planet =>
            planet is not null && planet.id == localPlanet.id && ReferenceEquals(planet, localPlanet));
        if (localPlanetMembership != 1)
        {
            return NotReady("The active local planet does not match the current star-system planet pool.");
        }

        var matches = localStar.planets.Where(planet => planet is not null && planet.id == requestedPlanetId).ToList();
        if (matches.Count > 1)
        {
            return NotReady("The current star has duplicate planet identities; the requested factory cannot be selected safely.");
        }

        var targetPlanet = matches.SingleOrDefault();
        var targetIsInCurrentStar = targetPlanet is not null;
        if (targetPlanet is null)
        {
            var outside = OwnedFactoryScopePolicy.EvaluateCurrentStarRead(
                requestedPlanetId, requestedPlanetId, false, false,
                -1, gameData.factoryCount, gameData.factories?.Length ?? 0,
                -1, -1, 0, false);
            return CreateOwnedFactoryReadScopeError(outside);
        }

        var resolvedTargetPlanet = targetPlanet!;

        var factoryPoolError = TryGetOwnedFactories(gameData, out var ownedFactories);
        if (factoryPoolError is not null) return factoryPoolError;

        var candidate = resolvedTargetPlanet.factoryIndex >= 0
            && resolvedTargetPlanet.factoryIndex < ownedFactories.Count
                ? ownedFactories[resolvedTargetPlanet.factoryIndex]
                : null;
        var status = OwnedFactoryScopePolicy.EvaluateCurrentStarRead(
            requestedPlanetId,
            resolvedTargetPlanet.id,
            targetIsInCurrentStar,
            resolvedTargetPlanet.factoryLoaded,
            resolvedTargetPlanet.factoryIndex,
            gameData.factoryCount,
            gameData.factories?.Length ?? 0,
            candidate?.index ?? -1,
            resolvedTargetPlanet.factoryIndex,
            candidate?.planetId ?? 0,
            candidate is not null && ReferenceEquals(candidate.planet, resolvedTargetPlanet),
            factorySimulationResident: candidate is not null
                && ReferenceEquals(resolvedTargetPlanet.factory, candidate));
        var scopeError = CreateOwnedFactoryReadScopeError(status);
        if (scopeError is not null) return scopeError;

        factory = candidate;
        return null;
    }

    private static BridgeError? CreateOwnedFactoryReadScopeError(OwnedFactoryReadStatus status) => status switch
    {
        OwnedFactoryReadStatus.Allowed => null,
        OwnedFactoryReadStatus.InvalidPlanetIdentity => InvalidRequest(
            "A same-star factory read requires a positive, identity-matched planetId.",
            "Use a planetId from the current readable star-system snapshot."),
        OwnedFactoryReadStatus.OutsideCurrentStar => BridgeError.Create(
            BridgeErrorCodes.StaleState,
            "The requested planet is not in the current local star system.",
            false,
            "Only inspect an already-created factory in the current readable star."),
        OwnedFactoryReadStatus.FactoryNotLoaded => NotReady(
            "The requested planet's factory is not loaded; no inventory was observed."),
        OwnedFactoryReadStatus.FactoryIndexUnavailable => NotReady(
            "The requested planet's factory index is not available in the owned factory pool."),
        OwnedFactoryReadStatus.FactoryIdentityMismatch => NotReady(
            "The requested planet and factory pool identities do not match; no inventory was observed."),
        _ => NotReady("The requested factory read scope could not be validated."),
    };
}
