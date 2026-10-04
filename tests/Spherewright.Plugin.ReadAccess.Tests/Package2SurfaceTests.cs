using System.Reflection;
using System.Text.RegularExpressions;
using Spherewright.Contracts.Actions;
using Xunit;

namespace Spherewright.Plugin.ReadAccess.Tests;

public sealed class Package2SurfaceTests
{
    [Fact]
    public void NormalActionKindsHaveNoNewOffensiveSurfaceOrCommonOwnershipGate()
    {
        var expected = new[] { "Move", "InterplanetaryFlight", "Harvest", "Handcraft", "SelectResearch", "Build",
            "Dismantle", "Upgrade", "BlueprintBuild", "CancelBlueprintBuild", "Transfer", "LogisticsStationFleetTransfer",
            "ConfigureBuilding", "Refuel", "Save", "ReconcileQuarantine", "UserSaveImport" };
        Assert.Equal(expected.OrderBy(x => x), typeof(NormalActionKinds).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => f.Name).OrderBy(x => x));
        var root = Root();
        foreach (var path in Directory.GetFiles(Path.Combine(root, "src/Spherewright.Plugin/Game"), "NormalGameActionCoordinator*.cs"))
        {
            var source = File.ReadAllText(path);
            // The one explicit owned gate belongs to checkpoint creation, behind the
            // prepared mode. Every ordinary action uses the shared authority instead.
            if (Path.GetFileName(path) == "NormalGameActionCoordinator.InterplanetaryFlight.cs")
            {
                Assert.Contains("BeginFlightCheckpointLifecycle(action)", source);
                var gate = source.IndexOf("if (!session.OwnedBySpherewright", StringComparison.Ordinal);
                Assert.True(gate > source.IndexOf("private bool EnsureFlightCheckpointOnMainThread", StringComparison.Ordinal));
                Assert.True(gate < source.IndexOf("private void UpdateInterplanetaryFlight", StringComparison.Ordinal));
                source = source.Remove(gate, "if (!session.OwnedBySpherewright".Length);
            }
            Assert.DoesNotMatch(new Regex(@"if\s*\(\s*!\s*(?:_sessions\.IsCurrentSessionOwned|session\.OwnedBySpherewright)\b"), source);
            Assert.DoesNotContain("CombatAuthorizationEpoch", source);
            Assert.DoesNotContain("aggressiveness", source, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void BlueprintPrepareAndCommitBindPrivatePreviewWithoutBroadeningExport()
    {
        var source = Read("NormalGameActionCoordinator.BlueprintBuild.cs");
        Assert.Contains("CanPrepareUnownedBlueprintPreview(common.Session!.OwnedBySpherewright", source);
        Assert.Contains("var capturePrivatePlayer = !common.Session.OwnedBySpherewright", source);
        Assert.Contains("BlueprintPlayerInspectionMatches(capturePrivatePlayer", source);
        Assert.Contains("inputHash = site.AssessmentHash", source);
        Assert.Contains("player.Value.StateHash, build, inputHash", source);
        Assert.Contains("siteRequest.ExpectedPlayerStateHash = player.Value.StateHash", source);
        Assert.Contains("resume ? null : request.BlueprintCode, siteRequest, false", source);
        Assert.Contains("current.Value.Site.AssessmentHash == plan.BlueprintInputHash", source);
        Assert.Contains("current.Value.Site.NativeCheckPassed", source);
        Assert.Contains("_reader.InspectBlueprintOnMainThread", source);
        Assert.Contains("allowAuthorizedUnownedNormalActionReads: true",
            File.ReadAllText(Path.Combine(Root(), "src/Spherewright.Plugin/Hosting/SpherewrightBridgeHost.cs")));
        var reader = Read("GameStateReader.Blueprints.cs");
        Assert.Contains("_allowAuthorizedUnownedNormalActionReads", reader);
        var export = reader.Substring(reader.IndexOf("public GameCallResult<BlueprintInspection> ExportBlueprintOnMainThread", StringComparison.Ordinal));
        Assert.Contains("ValidateOwnedPlanetOnMainThread", export);
        Assert.DoesNotContain("ValidateBlueprintActionPlanetOnMainThread", export);
    }

    [Fact]
    public void ProtectedRecoveryFlowsStillRequireOwnedProvenanceAndNormalSaveNeverAssignsName()
    {
        Assert.Contains("OwnedBySpherewright", Read("FlightCheckpointReloadCoordinator.cs"));
        Assert.Contains("OwnedSaveName", Read("FlightCheckpointReloadCoordinator.cs"));
        Assert.Contains("TryGetCurrentUnownedImportCandidateOnMainThread", Read("UserSaveImportCoordinator.cs"));
        Assert.Contains("OwnedSaveRecoveryLease.Open", Read("OwnedWorldResumeCoordinator.cs"));
        var save = Read("NormalGameActionCoordinator.Save.cs");
        Assert.DoesNotMatch(new Regex(@"GameMain\.gameName\s*="), save);
        Assert.DoesNotContain("OwnedSavePrefixReader", save);
        Assert.DoesNotContain("ArmFrom", save);
        Assert.DoesNotContain("ExpectNextSession", save);
    }

    private static string Read(string name) => File.ReadAllText(Path.Combine(Root(), "src/Spherewright.Plugin/Game", name));
    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Spherewright.Core.slnf"))) return directory.FullName;
        throw new DirectoryNotFoundException("Source root not found.");
    }
}
