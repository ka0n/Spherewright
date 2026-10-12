using Spherewright.Contracts.Errors;

namespace Spherewright.Plugin.Game;

internal sealed partial class GameStateReader
{
    private static BridgeError? TryGetOwnedFactories(
        GameData gameData,
        out List<PlanetFactory> factories)
    {
        factories = new List<PlanetFactory>();
        var factoryPool = gameData.factories;
        if (gameData.factoryCount > MaximumOverseerFactoryCount)
        {
            return OverseerScopeExceeded();
        }

        if (factoryPool is null
            || gameData.factoryCount < 1
            || gameData.factoryCount > factoryPool.Length)
        {
            return NotReady("The owned world's factory index is not ready.");
        }

        for (var factoryIndex = 0; factoryIndex < gameData.factoryCount; factoryIndex++)
        {
            var factory = factoryPool[factoryIndex];
            var planet = factory?.planet;
            if (factory is null
                || planet is null
                || factory.index != factoryIndex
                || planet.factoryIndex != factoryIndex
                || !ReferenceEquals(planet.factory, factory)
                || factory.planetId <= 0
                || factory.planetId != planet.id)
            {
                factories.Clear();
                return NotReady("An owned factory does not match its factory and planet pool identity.");
            }

            factories.Add(factory);
        }

        return null;
    }

}
