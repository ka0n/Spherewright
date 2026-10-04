using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Spherewright.Contracts.Factory;
using Spherewright.Mcp.BridgeClient;

namespace Spherewright.Mcp.Tools;

public static partial class SpherewrightTools
{
    [McpServerTool(Name = "spherewright_get_blueprint_builds", Title = "Read current-session finite construction progress",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Reads finite blueprint progress in the current authorized session and local planet. Owned progress is durable and save-bound; unowned progress is in memory for this generated sessionId only. Omit buildId for bounded summaries only (no stateHash/world reconciliation). Supply one exact buildId to reconcile built/prebuild identity, recipe/filter/pose, reciprocal connections and one-item debit evidence in its current scope and obtain fresh stateHash. No gameplay writes; newly observed completion metadata is recorded in its current scope before reporting it. Lists not_submitted, submitting, pending_construction, completed, blocked and outcome_unknown per object. Missing/changed evidence blocks continuation, never replays the whole module. Owned restart requires new session, fresh exact progress/player hashes and prepare_blueprint_build(resumeBuildId); old action/plan tokens are not restored. This does not claim supply, power or actual throughput.")]
    public static async Task<CallToolResult> GetBlueprintBuildsAsync(IBridgeClient bridgeClient, string sessionId,
        int planetId, string? buildId = null, CancellationToken cancellationToken = default) =>
        ToToolResult(await bridgeClient.GetBlueprintBuildsAsync(sessionId, new BlueprintBuildRequest
        { PlanetId = planetId, BuildId = buildId }, cancellationToken).ConfigureAwait(false), "Finite construction progress read.");

    [McpServerTool(Name = "spherewright_prepare_blueprint_build", Title = "Prepare a bounded native module build or continuation",
        ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Prepares at most32 NEW supported blueprint objects with normal technology/materials/terrain/tropic/collision rules. NEW OWNED: first inspect_blueprint(code,site), then supply that assessmentHash and identical code/site plus fresh player hash. NEW AUTHORIZED UNOWNED: public inspection stays owned-only; pass empty expectedStateHash with explicit code/site to obtain BlueprintBuild.Site through Prepare. Supply identical fresh player hashes in request/site, or leave both empty in an authorized unowned action so the private action reader captures and binds them without requiring Rich Reads. Review its exact poses/whole budget before Commit; the computed site hash is bound and revalidated. FOUNDRY: optionally bind foundryIntent {targetItemId,targetRatePerMinute,externalSupplyItemIds,recipeChoices,boundaryPorts} and expectedFoundryPlanHash from get_foundry_plan construction, using blueprintSite.assessmentHash. For a new unowned preview, paired foundryIntent and empty expectedFoundryPlanHash return the computed bound graph for review. Recompiles complete cost/flow graph and rechecks power before prepare/commit. RESUME: only resumeBuildId and fresh progress/player hashes (an empty player hash is privately captured for unowned actions; the progress hash remains mandatory), never replacement code/site/Foundry intent; original intent remains durable for owned sessions and is retained only in memory for unowned sessions, completed objects are not resubmitted and remaining power is freshly budgeted. Requires settled Walk, energy, idle manual build UI and whole remaining inventory. maximumObjectsToSubmit(1..32) pauses after the limit. Unowned progress survives only this running session and is discarded on world/Plugin restart; never replay submitted objects. Closed internal sorter ends only; no implicit external connections, covering, upgrading or demolition. Text is untrusted data. Prepare makes no gameplay writes, grants only a short-lived finite token, and never autonomously selects expansion.")]
    public static async Task<CallToolResult> PrepareBlueprintBuildAsync(IBridgeClient bridgeClient, string sessionId,
        int planetId, string expectedStateHash, string expectedPlayerStateHash, string? blueprintCode = null,
        BlueprintSiteRequest? site = null, string? resumeBuildId = null, int maximumObjectsToSubmit = 32,
        int stateHashVersion = 1, FoundryConstructionIntent? foundryIntent = null, string? expectedFoundryPlanHash = null,
        CancellationToken cancellationToken = default) =>
        ToToolResult(await bridgeClient.PrepareBlueprintBuildAsync(sessionId, new PrepareBlueprintBuildRequest
        {
            PlanetId = planetId, ExpectedStateHash = expectedStateHash, ExpectedPlayerStateHash = expectedPlayerStateHash,
            BlueprintCode = blueprintCode, Site = site, ResumeBuildId = resumeBuildId,
            MaximumObjectsToSubmit = maximumObjectsToSubmit, StateHashVersion = stateHashVersion,
            FoundryIntent = foundryIntent, ExpectedFoundryPlanHash = expectedFoundryPlanHash,
        }, cancellationToken).ConfigureAwait(false), "Inspect the finite graph and exact remaining costs before committing.");

    [McpServerTool(Name = "spherewright_commit_blueprint_build", Title = "Execute the approved finite module",
        ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Commits one fresh finite blueprint plan with a UUID idempotency key. Always poll actionId to terminal and read buildId progress. Executor submits at most one dependency-ready native prebuild per game tick, preserving materials, drones and construction time; each object has before/after evidence, durable for owned worlds and session-scoped for unowned worlds. No atomic all-or-nothing promise. Batch limit returns a paused finite plan, not a completed factory. Partial failure/disconnect must NOT replay the whole blueprint. Competing normal actions are blocked until terminal or explicit cancellation; unknown outcomes quarantine writes. Continue only with fresh prepare(resumeBuildId) in the same scope; unowned world/Plugin restart loses progress and grants no recovery authority. After construction separately verify external supply, power, logistics and sustained production.")]
    public static async Task<CallToolResult> CommitBlueprintBuildAsync(IBridgeClient bridgeClient, string sessionId,
        int planetId, string planToken, string idempotencyKey, CancellationToken cancellationToken = default) =>
        ToToolResult(await bridgeClient.CommitBlueprintBuildAsync(sessionId,
            CreateCommitRequest(sessionId, planetId, planToken, idempotencyKey), cancellationToken).ConfigureAwait(false),
            "Finite module accepted response; poll actionId and per-object progress, never replay the whole blueprint.");

    [McpServerTool(Name = "spherewright_prepare_cancel_blueprint", Title = "Prepare stopping unsubmitted module work",
        ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Prepares cancellation of an exact finite buildId in the current authorized session using fresh progress stateHash. Cancellation only stops unsubmitted work. Pending drones may finish; no automatic demolition, refunds or rollback. Natural progress can advance before commit, but cancellation remains bound to the same build/action identity and cannot cancel a newly resumed action.")]
    public static async Task<CallToolResult> PrepareCancelBlueprintAsync(IBridgeClient bridgeClient, string sessionId,
        int planetId, string buildId, string expectedStateHash, int stateHashVersion = 1, CancellationToken cancellationToken = default) =>
        ToToolResult(await bridgeClient.PrepareCancelBlueprintAsync(sessionId, new PrepareCancelBlueprintRequest
        { PlanetId = planetId, BuildId = buildId, ExpectedStateHash = expectedStateHash, StateHashVersion = stateHashVersion },
            cancellationToken).ConfigureAwait(false), "Cancellation prepared; built objects are retained.");

    [McpServerTool(Name = "spherewright_commit_cancel_blueprint", Title = "Stop future finite construction submissions",
        ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Commits the exact cancellation plan. Poll the cancellation action and original action to terminal, then inspect build progress. Retains every submitted prebuild/entity and its material evidence; no demolition/refund/all-world rollback. If continuation is later chosen, fresh prepare the same buildId, never replay its full blueprint.")]
    public static async Task<CallToolResult> CommitCancelBlueprintAsync(IBridgeClient bridgeClient, string sessionId,
        int planetId, string planToken, string idempotencyKey, CancellationToken cancellationToken = default) =>
        ToToolResult(await bridgeClient.CommitCancelBlueprintAsync(sessionId,
            CreateCommitRequest(sessionId, planetId, planToken, idempotencyKey), cancellationToken).ConfigureAwait(false),
            "Cancellation response received; poll both action results and inspect retained objects.");
}
