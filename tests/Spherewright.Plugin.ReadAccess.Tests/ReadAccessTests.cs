using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BepInEx.Configuration;
using BepInEx.Logging;
using Spherewright.Contracts.Errors;
using Spherewright.Contracts.Protocol;
using Spherewright.Contracts.Sessions;
using Spherewright.Plugin.Bootstrap;
using Spherewright.Plugin.Game;
using Xunit;

namespace Spherewright.Plugin.ReadAccess.Tests;

[CollectionDefinition("Plugin read-access tests", DisableParallelization = true)]
public sealed class ReadAccessTestCollection
{
}

[Collection("Plugin read-access tests")]
public sealed class ReadAccessTests
{
    [Fact]
    public void Configuration_ObservationOptInDefaultsOffAndDoesNotEnableWritesOrImport()
    {
        var defaults = SpherewrightConfiguration.Load(new ConfigFile());
        Assert.False(defaults.AllowUnownedRichReads);
        Assert.False(defaults.AllowUnownedNormalWrites);
        Assert.False(defaults.AllowWrites);
        Assert.False(defaults.AllowUserSaveImport);

        var configured = new ConfigFile();
        configured.Set("Safety", "AllowUnownedRichReads", true);
        var enabled = SpherewrightConfiguration.Load(configured);
        Assert.True(enabled.AllowUnownedRichReads);
        Assert.False(enabled.AllowUnownedNormalWrites);
        Assert.False(enabled.AllowWrites);
        Assert.False(enabled.AllowUserSaveImport);

        var writeOptIn = new ConfigFile();
        writeOptIn.Set("Safety", "AllowUnownedNormalWrites", true);
        writeOptIn.Set("Safety", "AllowWrites", true);
        var writeEnabled = SpherewrightConfiguration.Load(writeOptIn);
        Assert.False(writeEnabled.AllowUnownedRichReads);
        Assert.True(writeEnabled.AllowUnownedNormalWrites);
        Assert.True(writeEnabled.AllowWrites);
    }

    [Fact]
    public void RestrictedUnownedState_RemainsRestrictedAndImportIsIndependentlyConfigured()
    {
        var data = CreateData(peaceful: true);
        var tracker = CreateTracker(data, observe: false, allowWrites: true, allowImport: true);

        var state = tracker.CaptureOnMainThread();

        Assert.False(state.OwnedBySpherewright);
        Assert.False(state.WritesAllowed);
        Assert.True(state.AccessRestricted);
        Assert.Equal(ReadAccessModes.Restricted, state.ReadAccessMode);
        Assert.Equal(new[] { "bridge.status", "session.safe-status", "user-save.import.prepare" }, state.Capabilities);
        Assert.Null(state.SaveName);
        Assert.Null(state.LocalPlanetId);
        Assert.Null(state.LocalPlanetName);
        Assert.Null(state.GameTick);
        Assert.DoesNotContain("player.read", state.Capabilities);
        Assert.DoesNotContain("factory.read", state.Capabilities);
        Assert.DoesNotContain("normal-game.prepare", state.Capabilities);
    }

    [Fact]
    public void ObservedUnownedProjection_IsReadOnlyAndContainsNoProtectedPersistenceState()
    {
        var data = CreateData(peaceful: false);
        var tracker = CreateTracker(data, observe: true, allowWrites: true, allowImport: true);
        GameMain.gameTick = 12345;

        var state = tracker.CaptureOnMainThread();

        Assert.True(tracker.WritesConfiguredForTest);
        Assert.False(state.OwnedBySpherewright);
        Assert.False(state.WritesAllowed);
        Assert.False(state.AccessRestricted);
        Assert.Equal(ReadAccessModes.ObservedUnowned, state.ReadAccessMode);
        Assert.Equal(PeacefulModeStates.ConfirmedCombat, state.PeacefulMode);
        Assert.Equal(10, state.LocalPlanetId);
        Assert.Equal("Test Planet", state.LocalPlanetName);
        Assert.Equal(12345, state.GameTick);
        Assert.True(state.UserSaveImportConfigured);
        Assert.Equal(new[]
        {
            "bridge.status", "session.read", "player.read", "progression.read",
            "assembler.read", "build-catalog.read", "recipe-catalog.read", "resource.read",
            "factory.read", "power.read", "overseer.read",
        }, state.Capabilities);
        Assert.DoesNotContain("normal-game.prepare", state.Capabilities);
        Assert.DoesNotContain("user-save.import.prepare", state.Capabilities);
        Assert.DoesNotContain(state.Capabilities, value => value.Contains("save", StringComparison.OrdinalIgnoreCase)
            || value.Contains("resume", StringComparison.OrdinalIgnoreCase)
            || value.Contains("recovery", StringComparison.OrdinalIgnoreCase)
            || value.Contains("checkpoint", StringComparison.OrdinalIgnoreCase)
            || value.Contains("load", StringComparison.OrdinalIgnoreCase));
        Assert.Null(state.SaveName);
        Assert.Equal(OwnedSaveStates.None, state.OwnedSaveState);
        Assert.Null(state.OwnedSaveError);
        Assert.Null(state.LastOwnedSaveGameTick);
        Assert.False(state.RestartResumeAvailable);
        Assert.Null(state.RestartResumeToken);
        Assert.False(state.FlightCheckpointAvailable);
        Assert.Null(state.FlightCheckpointId);
        Assert.Null(state.FlightCheckpointReloadToken);
        Assert.False(state.CurrentSessionLoadedFromFlightCheckpoint);
    }

    [Theory]
    [InlineData(true, PeacefulModeStates.ConfirmedPeaceful)]
    [InlineData(false, PeacefulModeStates.ConfirmedCombat)]
    public void ReadablePlanet_AllowsObservedUnownedWorldRegardlessOfWorldMode(bool peaceful, string expectedMode)
    {
        var data = CreateData(peaceful);
        var tracker = CreateTracker(data, observe: true, allowWrites: false, allowImport: false);
        var reader = new GameStateReader(tracker);

        var error = reader.ReadablePlanet(tracker.SessionId, 10, out var factory);
        var state = tracker.CaptureOnMainThread();

        Assert.Null(error);
        Assert.Same(data.localLoadedPlanetFactory, factory);
        Assert.Equal(expectedMode, state.PeacefulMode);
        Assert.Equal(ReadAccessModes.ObservedUnowned, state.ReadAccessMode);
        Assert.False(state.OwnedBySpherewright);
        Assert.False(state.WritesAllowed);
    }

    [Fact]
    public void ReadAccessRequiresExplicitOptIn()
    {
        var data = CreateData(peaceful: true);
        var tracker = CreateTracker(data, observe: false, allowWrites: false, allowImport: false);
        var reader = new GameStateReader(tracker);

        var error = reader.ReadablePlanet(tracker.SessionId, 10, out var factory);

        Assert.Equal(BridgeErrorCodes.SessionNotOwned, error?.Code);
        Assert.Null(factory);
    }

    [Theory]
    [InlineData("new-world")]
    [InlineData("resume")]
    [InlineData("checkpoint")]
    public void ProtectedAdoptionExpectationKeepsUnownedSessionRestricted(string expectation)
    {
        var data = CreateData(peaceful: true);
        var tracker = CreateTracker(data, observe: true, allowWrites: true, allowImport: true);
        tracker.SetProtectedAdoptionExpectationForTest(expectation);
        var reader = new GameStateReader(tracker);

        var state = tracker.CaptureOnMainThread();
        var error = reader.ReadablePlanet(tracker.SessionId, 10, out var factory);

        Assert.False(tracker.IsCurrentSessionObservedUnowned);
        Assert.False(state.OwnedBySpherewright);
        Assert.False(state.WritesAllowed);
        Assert.True(state.AccessRestricted);
        Assert.Equal(ReadAccessModes.Restricted, state.ReadAccessMode);
        Assert.DoesNotContain("player.read", state.Capabilities);
        Assert.DoesNotContain("factory.read", state.Capabilities);
        Assert.DoesNotContain("normal-game.prepare", state.Capabilities);
        Assert.Equal(BridgeErrorCodes.SessionNotOwned, error?.Code);
        Assert.Null(factory);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("different-session")]
    public void ReadAccessRequiresExactSession(string? requestedSessionId)
    {
        var data = CreateData(peaceful: true);
        var tracker = CreateTracker(data, observe: true, allowWrites: false, allowImport: false);
        var reader = new GameStateReader(tracker);

        var error = reader.ReadablePlanet(requestedSessionId, 10, out var factory);

        Assert.Equal(BridgeErrorCodes.StaleSession, error?.Code);
        Assert.Null(factory);
    }

    [Fact]
    public void ReadAccessRejectsAReplacedCurrentGameData()
    {
        var observed = CreateData(peaceful: true);
        var tracker = CreateTracker(observed, observe: true, allowWrites: false, allowImport: false);
        var reader = new GameStateReader(tracker);
        GameMain.data = CreateData(peaceful: true);

        var error = reader.ReadableGameData(tracker.SessionId, out var returnedData);

        Assert.Equal(BridgeErrorCodes.SessionNotOwned, error?.Code);
        Assert.Null(returnedData);
    }

    [Fact]
    public void ReadAccessRejectsWrongPlanetAndMissingLocalFactory()
    {
        var wrongPlanetData = CreateData(peaceful: true);
        var wrongPlanetReader = new GameStateReader(CreateTracker(
            wrongPlanetData, observe: true, allowWrites: false, allowImport: false));
        var wrongPlanet = wrongPlanetReader.ReadablePlanet("session-1", 11, out var wrongPlanetFactory);

        Assert.Equal(BridgeErrorCodes.StaleState, wrongPlanet?.Code);
        Assert.Null(wrongPlanetFactory);

        var missingFactoryData = CreateData(peaceful: true, includeFactory: false);
        var missingFactoryReader = new GameStateReader(CreateTracker(
            missingFactoryData, observe: true, allowWrites: false, allowImport: false));
        var missingFactory = missingFactoryReader.ReadableSession("session-1", out var factory);

        Assert.Equal(BridgeErrorCodes.NoLocalPlanet, missingFactory?.Code);
        Assert.Null(factory);
    }

    [Theory]
    [InlineData(false, 11)]
    [InlineData(true, 10)]
    public void ReadAccessRejectsMissingOrMismatchedLocalPlanet(bool omitPlanet, int factoryPlanetId)
    {
        var data = CreateData(
            peaceful: true,
            includeLocalPlanet: !omitPlanet,
            factoryPlanetId: factoryPlanetId);
        var reader = new GameStateReader(CreateTracker(data, observe: true, allowWrites: false, allowImport: false));

        var error = reader.ReadablePlanet("session-1", 10, out var factory);

        Assert.Equal(BridgeErrorCodes.BridgeNotReady, error?.Code);
        Assert.Null(factory);
    }

    [Fact]
    public void OwnedReadValidatorStillRequiresOwnershipAndOwnedWorldRemainsReadable()
    {
        var observed = CreateData(peaceful: true);
        var observedReader = new GameStateReader(CreateTracker(observed, observe: true, allowWrites: false, allowImport: false));
        var observedError = observedReader.OwnedPlanet("session-1", 10, out _);
        Assert.Equal(BridgeErrorCodes.SessionNotOwned, observedError?.Code);

        var owned = CreateData(peaceful: true);
        var ownedTracker = CreateTracker(owned, observe: false, allowWrites: false, allowImport: false, owned: true);
        var ownedReader = new GameStateReader(ownedTracker);
        var ownedError = ownedReader.ReadablePlanet(ownedTracker.SessionId, 10, out var factory);

        Assert.Null(ownedError);
        Assert.Same(owned.localLoadedPlanetFactory, factory);
        Assert.True(ownedTracker.CaptureOnMainThread().OwnedBySpherewright);
    }

    [Fact]
    public void PrepareAuthorizationRejectsObservedUnownedEvenIfCapabilityIsIncorrectlyAdvertised()
    {
        var data = CreateData(peaceful: true);
        var tracker = CreateTracker(data, observe: true, allowWrites: true, allowImport: true);
        tracker.CapturedStateOverride = new SessionState
        {
            GameLoaded = true,
            OwnedBySpherewright = false,
            ReadAccessMode = ReadAccessModes.ObservedUnowned,
            SessionId = tracker.SessionId,
            LocalPlanetId = 10,
            WritesAllowed = true,
            Capabilities = new System.Collections.Generic.List<string> { "normal-game.prepare" },
        };
        var coordinator = new NormalGameActionCoordinator(tracker);

        var error = coordinator.PrepareAuthorizationForTest(tracker.SessionId, 10, 1);

        Assert.Equal(BridgeErrorCodes.SessionNotOwned, error?.Code);
    }

    [Fact]
    public void ObservedModeSuppressesImportButRestrictedModeRetainsSeparateImportFlow()
    {
        var observedData = CreateData(peaceful: true);
        var observedTracker = CreateTracker(observedData, observe: true, allowWrites: false, allowImport: true);
        var observedCanImport = observedTracker.TryGetCurrentUnownedImportCandidateOnMainThread(
            observedTracker.SessionId, out var observedCandidate, out var observedRejection);

        Assert.False(observedCanImport);
        Assert.Null(observedCandidate);
        Assert.Contains("does not authorize save import", observedRejection, StringComparison.Ordinal);
        Assert.DoesNotContain("user-save.import.prepare", observedTracker.CaptureOnMainThread().Capabilities);

        var restrictedData = CreateData(peaceful: true);
        var restrictedTracker = CreateTracker(restrictedData, observe: false, allowWrites: false, allowImport: true);
        var restrictedCanImport = restrictedTracker.TryGetCurrentUnownedImportCandidateOnMainThread(
            restrictedTracker.SessionId, out var restrictedCandidate, out _);

        Assert.True(restrictedCanImport);
        Assert.Same(restrictedData, restrictedCandidate);
        Assert.Contains("user-save.import.prepare", restrictedTracker.CaptureOnMainThread().Capabilities);
    }

    [Fact]
    public void ResearchResultAutoAcknowledgerDoesNotCloseObservedUnownedWindow()
    {
        var data = CreateData(peaceful: false);
        var tracker = CreateTracker(data, observe: true, allowWrites: true, allowImport: false);
        var tip = new ResearchResultTip { active = true, ready = true };
        UIRoot.instance = new UIRoot { uiGame = new UIGame { researchResultTip = tip } };
        var acknowledger = new ResearchResultAutoAcknowledger(true, new ManualLogSource(), tracker);

        acknowledger.UpdateOnMainThread();

        Assert.Equal(0, tip.FadeOutCount);
    }

    [Fact]
    public void ResearchResultAutoAcknowledgerPreservesOwnedWorldBehavior()
    {
        var data = CreateData(peaceful: true);
        var tracker = CreateTracker(data, observe: false, allowWrites: false, allowImport: false, owned: true);
        var tip = new ResearchResultTip { active = true, ready = true };
        UIRoot.instance = new UIRoot { uiGame = new UIGame { researchResultTip = tip } };
        var acknowledger = new ResearchResultAutoAcknowledger(true, new ManualLogSource(), tracker);

        acknowledger.UpdateOnMainThread();

        Assert.Equal(1, tip.FadeOutCount);
    }

    [Fact]
    public void RichReadRoutesUseSharedValidatorAndProtectedSurfacesStayOwnedOnly()
    {
        var sourceRoot = FindSourceRoot();
        AssertReadRoute(sourceRoot, "src/Spherewright.Plugin/Game/GameStateReader.cs", "GetPlayerStateOnMainThread", "ValidateReadablePlanetOnMainThread");
        AssertReadRoute(sourceRoot, "src/Spherewright.Plugin/Game/GameStateReader.cs", "GetProgressionStateOnMainThread", "ValidateReadablePlanetOnMainThread");
        AssertReadRoute(sourceRoot, "src/Spherewright.Plugin/Game/GameStateReader.cs", "ListResourceNodesOnMainThread", "ValidateReadablePlanetOnMainThread");
        AssertReadRoute(sourceRoot, "src/Spherewright.Plugin/Game/GameStateReader.cs", "ListFactoryEntitiesOnMainThread", "ValidateReadablePlanetOnMainThread");
        AssertReadRoute(sourceRoot, "src/Spherewright.Plugin/Game/GameStateReader.cs", "GetPowerSummaryOnMainThread", "ValidateReadablePlanetOnMainThread");
        AssertReadRoute(sourceRoot, "src/Spherewright.Plugin/Game/GameStateReader.cs", "GetOverseerProductionOnMainThread", "ValidateReadableGameDataOnMainThread");
        AssertReadRoute(sourceRoot, "src/Spherewright.Plugin/Game/GameStateReader.cs", "GetOverseerSummaryOnMainThread", "ValidateReadableGameDataOnMainThread");
        AssertReadRoute(sourceRoot, "src/Spherewright.Plugin/Game/GameStateReader.cs", "GetOverseerDiagnosticBundleOnMainThread", "ValidateReadableGameDataOnMainThread");
        AssertReadRoute(sourceRoot, "src/Spherewright.Plugin/Game/GameStateReader.cs", "GetLocalStarSystemOnMainThread", "ValidateReadablePlanetOnMainThread");
        AssertReadRoute(sourceRoot, "src/Spherewright.Plugin/Game/GameStateReader.cs", "ListAssemblersOnMainThread", "ValidateReadableSessionOnMainThread");

        AssertOwnedRoute(sourceRoot, "src/Spherewright.Plugin/Game/GameStateReader.Blueprints.cs", "InspectBlueprintOnMainThread");
        AssertOwnedRoute(sourceRoot, "src/Spherewright.Plugin/Game/GameStateReader.Blueprints.cs", "ExportBlueprintOnMainThread");
        AssertOwnedRoute(sourceRoot, "src/Spherewright.Plugin/Game/GameStateReader.Governor.cs", "GetGovernorPlanOnMainThread");
        AssertSavePrepareUsesSharedAuthorization(sourceRoot);
        AssertCommitUsesCommonAuthorization(sourceRoot);
        AssertOverseerProgressPersistenceIsOwnedOnly(sourceRoot);
    }

    private static GameSessionTracker CreateTracker(
        GameData data,
        bool observe,
        bool allowWrites,
        bool allowImport,
        bool owned = false)
    {
        var tracker = new GameSessionTracker();
        tracker.Configure(data, observe, allowWrites, allowImport, owned);
        return tracker;
    }

    private static GameData CreateData(
        bool peaceful,
        bool includeFactory = true,
        bool includeLocalPlanet = true,
        int factoryPlanetId = 10)
    {
        var data = new GameData
        {
            gameDesc = new GameDesc { isPeaceMode = peaceful, isSandboxMode = false, resourceMultiplier = 1f },
            localPlanet = includeLocalPlanet ? new PlanetData { id = 10, displayName = "Test Planet" } : null,
            localLoadedPlanetFactory = includeFactory ? new PlanetFactory { planetId = factoryPlanetId } : null,
        };
        GameMain.data = data;
        return data;
    }

    private static string FindSourceRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Spherewright.sln"))
                && Directory.Exists(Path.Combine(directory.FullName, "src")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not find the Spherewright source root from the test output directory.");
    }

    private static void AssertReadRoute(string sourceRoot, string relativePath, string methodName, string validatorName)
    {
        var text = File.ReadAllText(Path.Combine(sourceRoot, relativePath));
        var method = MethodPrefix(text, methodName);
        Assert.Contains(validatorName, method, StringComparison.Ordinal);
    }

    private static void AssertOwnedRoute(string sourceRoot, string relativePath, string methodName)
    {
        var text = File.ReadAllText(Path.Combine(sourceRoot, relativePath));
        var method = MethodPrefix(text, methodName);
        Assert.Contains("ValidateOwnedPlanetOnMainThread", method, StringComparison.Ordinal);
    }

    private static void AssertSavePrepareUsesSharedAuthorization(string sourceRoot)
    {
        var entry = File.ReadAllText(Path.Combine(sourceRoot, "src/Spherewright.Plugin/Game/NormalGameActionCoordinator.cs"));
        var saveEntryIndex = entry.IndexOf("PrepareSaveOnMainThread", StringComparison.Ordinal);
        Assert.True(saveEntryIndex >= 0, "Source method PrepareSaveOnMainThread was not found.");
        var saveEntry = entry.Substring(saveEntryIndex, Math.Min(260, entry.Length - saveEntryIndex));
        Assert.Contains("PrepareSavePlanOnMainThread", saveEntry, StringComparison.Ordinal);
        var implementation = File.ReadAllText(Path.Combine(sourceRoot, "src/Spherewright.Plugin/Game/NormalGameActionCoordinator.Save.cs"));
        Assert.Contains("ValidatePrepareCommon", MethodPrefix(implementation, "PrepareSavePlanOnMainThread"), StringComparison.Ordinal);
    }

    private static void AssertCommitUsesCommonAuthorization(string sourceRoot)
    {
        var authorization = File.ReadAllText(Path.Combine(sourceRoot, "src/Spherewright.Plugin/Game/NormalGameActionCoordinator.PrepareAuthorization.cs"));
        var validateCommit = MethodPrefix(authorization, "private BridgeError? ValidateCommitCommon");
        Assert.Contains("_sessions.IsCurrentSessionAuthorizedForNormalActions", validateCommit, StringComparison.Ordinal);
        Assert.Contains("session.WriteBlockers.Count > 0", validateCommit, StringComparison.Ordinal);
        Assert.DoesNotContain("!session.OwnedBySpherewright", validateCommit, StringComparison.Ordinal);

        var coordinator = File.ReadAllText(Path.Combine(sourceRoot, "src/Spherewright.Plugin/Game/NormalGameActionCoordinator.cs"));
        Assert.Contains("ValidateCommitCommon(session, plan, request)", MethodPrefix(coordinator, "CommitOnMainThread"), StringComparison.Ordinal);
    }

    private static void AssertOverseerProgressPersistenceIsOwnedOnly(string sourceRoot)
    {
        var readerSource = File.ReadAllText(Path.Combine(sourceRoot, "src/Spherewright.Plugin/Game/GameStateReader.cs"));
        Assert.Contains("_sessions.IsCurrentSessionOwned ? _overseerLogisticsProgressStore : null", readerSource, StringComparison.Ordinal);
        var source = File.ReadAllText(Path.Combine(sourceRoot, "src/Spherewright.Plugin/Game/GameStateReader.OverseerDiagnostics.cs"));
        Assert.Contains("if (_progressStore is null || _progressObservations.Count == 0)", source, StringComparison.Ordinal);
        Assert.Contains("TryObserveBatch", source, StringComparison.Ordinal);
    }

    private static string MethodPrefix(string text, string methodName)
    {
        var start = text.IndexOf(methodName, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Source method {methodName} was not found.");
        var methodText = text.Substring(start);
        var end = Regex.Match(methodText, @"(?m)^    \}");
        Assert.True(end.Success, $"Could not find a body boundary for source method {methodName}.");
        return methodText.Substring(0, end.Index);
    }
}
