# Pinned fork/upstream source integration

2026-10-12 (Europe/Berlin). Scope: source merge, offline tests and isolated full Plugin build. No game action, save/load, DSP start/stop, installation, cold deploy or existing MCP/tunnel runtime change.

## Git

- Fork baseline and verified initial origin/main, backup/pre-upstream-integration-2026-10-12 and integration/upstream-2026-10-12: `b6b54a4ad1940911e60a62c8d589b61aa7a3f49f`.
- Annotated recovery tag `pre-upstream-integration-2026-10-12` created and pushed at that exact baseline; message: “Known-good pre-upstream integration state with Dark Fog unowned normal-action support”.
- Pinned upstream: `8df3b7764c57cae634bab1e9762b4f116f7838ae`, `docs: record native route survey and maintenance boundary`.
- Verified merge-base: `eec562b249ce18eb22baf00b48c7b98c4e4c1578`.
- Separate managed worktree, branch `integration/upstream-2026-10-12`, real two-parent merge. This record belongs to the merge; its resulting SHA is reported with the verified remote branch SHA after push.
- No rebase, force push, history rewrite, main update, upstream write, PR merge or release. Only the named recovery tag and integration branch are pushed.
- The pre-existing primary local main checkout was older (`c51589d`) and left untouched; required origin/main was verified independently.
- Windows Core CI now also triggers on this exact integration branch, without creating a PR. Matching-SHA remote CI is collected after pushing, independently of the local results below.

## All seven actual conflicts

| File | Semantic resolution |
|---|---|
| AGENTS.md | Fork Steering, independent read/write gates, exact unowned non-combat authority, ten-write default and per-deployment authorization retained; portable engineering rules retained. No upstream author/model/writer hierarchy or live approvals imported. |
| docs/current-status.md | Actual fork source/provenance and prior bounded fork parser live evidence retained. Upstream author's save/ticks/counts/install claims are not fork status. |
| docs/agent-playbook.md | Combine upstream Passive Drift with separate fork owned/unowned Save readback and authority instructions. |
| docs/evidence/2026-10-04/material-inventory-cuts.md | Preserve both histories; explicitly label the upstream continuation historical, not fork live evidence or authority. |
| src/Spherewright.Mcp/Tools/SpherewrightTools.cs | Preserve upstream geometry request/echo checks, fuel guidance and Move opt-in; retain explicit fork read/write independence and readable-session terminology. |
| src/Spherewright.Plugin/Game/GameStateReader.cs | Keep two readers and fork access gates. Move extended inspection into the existing FactoryInspection partial and remote identity/scope checks into a dedicated partial using public rich-read authority. Do not install duplicate upstream owned-only validators. |
| src/Spherewright.Plugin/Game/NormalGameActionCoordinator.cs | Keep fork PrepareAuthorization and both central IsCurrentSessionAuthorizedForNormalActions gates; preserve upstream actions and strict drift checks with shared evidence. |

Auto-merged safety-model/protocol were reviewed and corrected. Timeline/incident/experience records identify upstream provenance; portable conclusions remain references, author-specific approvals do not become fork authority. StageTools CI lookup targets ka0n/Spherewright. Its default audit limit stays ten; explicit upstream 20/50 support requires separately authorized windows.

## Preserved invariants

- Authorized exact unowned actions work with Dark Fog / isPeaceMode=false. Aggressiveness, including non-Passive and unknown values, remains telemetry. No Combat action or offensive authority was added.
- Rich reads and writes are independent. Public reads require owned or exact observed_unowned plus AllowUnownedRichReads. Unowned normal actions require both AllowWrites and AllowUnownedNormalWrites, with no rich-read dependency or ownership grant.
- OwnedBySpherewright remains false; stale session, replaced GameData, adoption-in-progress, health/quarantine, native construction, single-flight and idempotency gates remain intact.
- Public remote reads retain current-star membership, already-created simulation residency, bounded pool/index and reciprocal planet/factory identity checks. No loading or factory creation. Write-only authority cannot grant remote public reads, even through the private reader. Two-argument action inspection and prepare/commit remain local-planet-only; remote geometry is rejected.
- PassiveDriftMoveInspectionStore is shared by one tracker across the separate public/action readers. Public inspection publishes immutable evidence; internal reads do not renew it. Exact GameData reference, session, planet and original monotonic two-second lifetime invalidate stale evidence. World/session transitions clear it; invalid reads permanently discard it.
- Both Move prepare/commit require the same evidence reference/hash, at most 0.05m displacement and 0.15m/s speed, exact capacity, unchanged canonical non-position state, no player/active order, forge or active/pending drones. Native terrain, movement/stall and terminal Walk checks remain unchanged.
- Fuel bufferedFuelCount is items, loadedFuelEnergyJoules is residual energy; null means unknown. Fuel/geometry/material metadata remains observation; existing action/configuration/endpoint hashes are unchanged and regression-tested.
- BlueprintTweaks parser fix c51589d and its tests are unchanged from the fork baseline: exact EOF or exactly 04 00 00 00 00, otherwise blueprint_trailing_data.
- Unowned native in-place Save, Import/Adoption, owned Resume, protected owned FlightCheckpoint and persistent owned blueprint progress remain separate. Unowned normal flight retains its checkpoint-free lifecycle.
- Reviewed new empty-belt dismantle/recovery, source/destination reuse, unpowered-miner feed and structured actions use central action admission/local targets and introduce no hidden ownership gate.

## Local commands and results

`dotnet restore Spherewright.sln --locked-mode`: all eight projects restored successfully.

`dotnet build Spherewright.sln --configuration Release --no-restore --verbosity quiet`: complete Release build, **0 warnings / 0 errors**, actual net472 Plugin included. Five existing compile DLLs were copied only into the worktree's ignored local reference directory and hash-verified against their sources; Assembly-CSharp also matches current installed native DSP. No installation file was written.

`dotnet test Spherewright.Core.slnf --configuration Release --no-restore --logger "trx;LogFilePrefix=integration-final" --results-directory .local/test-results --verbosity quiet`:

| Suite | Passed | Failed / skipped |
|---|---:|---:|
| Contracts | 73 | 0 / 0 |
| Bridge.Core | 2,638 | 0 / 0 |
| Plugin.ReadAccess | 145 | 0 / 0 |
| MCP | 240 | 0 / 0 |
| Total | **3,096** | **0 / 0** |

Includes all existing fork tests and upstream Factory Inspection, Material Inventory Cut, Fuel Power State, geometry/native belt-segment batches, empty-belt dismantle, belt reuse, unpowered miner feed, Passive Drift, Production/Overseer and package-surface tests. Added linked-production reader/coordinator matrix tests in combat worlds across telemetry values, remote authority/identity/local-action regressions and cross-reader drift/invalidation/expiry tests.

Each script ran as `pwsh -NoProfile -File scripts/<name>.ps1`, exit 0:

| Script | Passed checks |
|---|---:|
| test-action-client | 87 |
| test-stage-tools | 48 stage + 28 storage + 52 material |
| test-belt-site-qualification | 101 |
| test-evidence-index | 11 |
| test-evidence-reader | 15 |
| test-factory-evidence | 31 |
| test-production-sampling | 120 |
| test-package-surface-policy | 95 |
| test-source-mcp | metadata smoke |

Offline fixtures make zero real game calls. Source stdio smoke: version 0.4.0.0, **64 tools / 1 resource**, exact source/playbook resource match, exit 0 and no extra stdout; no Bridge/gameplay requests. Raw TRX/script results stay in ignored local directories. No protected receipts or private paths are committed.

## Review boundary

The installed working runtime is untouched. This merged cohort has source, automated regression and actual Plugin compile evidence; it has no new live Passive-Drift/remote-read/action/save/restart acceptance. No final ZIP, deployment transaction or cross-computer validation is claimed. Those remain separate Steering/runtime gates. Local checks do not imply remote CI success. No deployment recommendation or authorization is issued.
