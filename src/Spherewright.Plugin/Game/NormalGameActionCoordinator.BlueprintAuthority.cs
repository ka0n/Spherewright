namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    // Public blueprint inspection remains owned-only. For a NEW manual-session
    // action, Prepare itself can produce the private native site preview. The
    // caller reviews BlueprintBuild.Site before commit; the computed hash is
    // bound in the plan and fully revalidated at commit. Resume always needs
    // the fresh existing progress hash and never accepts this preview shortcut.
    private static bool CanPrepareUnownedBlueprintPreview(bool owned, string? resumeBuildId, string? expectedHash) =>
        !owned && resumeBuildId is null && string.IsNullOrEmpty(expectedHash);

    private static bool BlueprintPlayerInspectionMatches(bool capturePrivatePlayer, string? suppliedHash, string currentHash) =>
        (capturePrivatePlayer && string.IsNullOrEmpty(suppliedHash))
        || string.Equals(suppliedHash, currentHash, StringComparison.Ordinal);
}
