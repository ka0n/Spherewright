# Fork v0.4.0 integration review

[English](fork-v040-integration.md) · [简体中文](fork-v040-integration.zh-CN.md)

2026-10-24, Europe/Berlin. Source integration for independent Steering review only. No game/Bridge calls, deployment, runtime replacement, save access, DSP lifecycle operation, main update, release publication or v0.5 source integration. The existing fork runtime acceptance remains bound to its earlier cohort; upstream installation/production evidence confers no fork authority.

## Pins and isolation

| Ref | Verified value |
| --- | --- |
| Fork local HEAD and remote main | `e3a83ddb9b17764b1e27f1947ffd0bafbd93e586` |
| Official annotated v0.4.0 tag object | `3e6f3b315e3234958be625520cb8fb2a00f5e1e1` |
| Peeled release commit | `b7c0002dbe2e8f5608c5ca140b0c6326a1818021` |
| Merge base | `8df3b7764c57cae634bab1e9762b4f116f7838ae` |
| Integration branch | `integration/upstream-v0.4.0-20261024` |

An independent source clone uses its own Git directory, index and build outputs inside an ignored local directory. Native Windows Git verified the fork remote and the tag at Arcueid77/Spherewright; the primary checkout's historical AvaloNero remote was not changed. Only the exact release tag was fetched, never experimental upstream main. The merge preserves both parents, with no cherry-pick, squash, rebase, tag modification or force update. Resulting commit, verified remote head, PR and exact-head CI are recorded in the Draft PR and Steering handoff after commit/push.

## Thirteen actual merge conflicts

| File | Resolution |
| --- | --- |
| AGENTS.md | Preserve current fork Steering, safety, ten-action defaults and deployment boundaries; Chinese companion mirrors those authoritative rules. |
| README.md | Use release product structure, add independent fork gates/provenance and correct stale unpublished-release wording. |
| docs/agent-playbook.md | Use new recovery/construction guidance with fork unowned entry, separate readers, Save readback and ten-action windows; companion updated. |
| docs/current-status.md | Preserve fork provenance and prior scoped live acceptance; do not replace with upstream Lab status. |
| docs/evidence/2026-10-04/material-inventory-cuts.md | Preserve existing historical fork/upstream provenance without new live authority. |
| docs/experience-ledger.md | Preserve existing fork record. |
| docs/gameplay-timeline.md | Preserve existing fork record. |
| docs/incident-fix-log.md | Preserve existing fork record. |
| docs/protocol.md | Keep upstream protocol/recovery additions and fork read/write/remote-session rules; companion updated. |
| docs/release-installation.md | Keep upstream bilingual installation guidance with explicit fork scope and independent gates. |
| docs/safety-model.md | Combine upstream bounded recovery policy with exact fork authority; remove owned-only normal-action and combat-posture implications. |
| scripts/SpherewrightStageTools.ps1 | CI lookup stays ka0n/Spherewright; existing default audit limit stays ten. |
| src/Spherewright.Plugin/Hosting/SpherewrightBridgeHost.cs | Add the upstream main-thread Journal provider to BlueprintBuildStore while retaining two distinct readers and fork import coordinator wiring. |

Other older evidence files that upstream replaced with Lab redirects remain unchanged from the fork baseline. The documentation validator uses an explicit SHA-256 manifest over UTF-8/LF content for these operational records; edits or missing records reject. Their original languages/relocated link context are retained; privacy/credential/fence checks still run. Maintained product documents retain bilingual, value and link checks. Five offline regression cases cover preservation, checkout newlines, content drift, private paths and missing records. Package guide links for this fork's exact source commit point to ka0n/Spherewright.

Two automatically merged test-source combinations needed adaptation: link production GameplayJournalIdentity into Plugin.ReadAccess.Tests, and expose the fork session predicates in the upstream owned-recovery test double. Production authority was not weakened to make tests compile.

## Fork safety proof

The following critical production files are unchanged from the fork baseline: SpherewrightConfiguration; GameSessionTracker and its ReadAccess/NormalActions/MoveInspection partials; GameStateReader ReadAccess/RemoteFactoryRead/MoveInspection; NormalGameActionCoordinator and its PrepareAuthorization/MoveInspection/Save partials; UserSaveImportCoordinator; TestWorldCoordinator; BoundedBlueprintReader. The parser's existing regression file is also unchanged. All existing ReadAccess test bodies are retained; only the new production dependency link was added.

| Invariant | Evidence in this integration |
| --- | --- |
| Independent read/write gates | NormalActionAuthorizationTests, IntegrationAuthorityMatrixTests and ReadAccessTests pass, including all four rich-read/normal-write combinations and the master AllowWrites gate. |
| Exact unowned non-combat authority | Existing linked production predicates remain unchanged; matrix tests cover combat worlds and nullable aggressiveness telemetry. No Peaceful/Passive prerequisite or ownership grant added. |
| Separate public/private readers | Host still creates two reader instances; only the action reader opts in. Write-only authority cannot expose public player/factory or remote reads. |
| Same-star resident remote reads | RemoteFactoryReadAccessTests pass with public authority, identity, residency/star/pool bounds and local-action rejection. No remote factory is created or loaded. |
| Protected Save/Import/Resume | Fork Save/import paths unchanged; upstream exact-primary migrations remain disclosure/digest/confirmation/lease/consumption bound. Default resume remains same-version. Autosave modes do not migrate. |
| No Combat automation | Existing registered tool surface and normal-action policies retained; no offensive action added. Telemetry is not authority. |
| Blueprint v4 | BoundedBlueprintReaderTests retain exact EOF or `04 00 00 00 00`; shortened, changed and extended suffixes reject. |
| Passive Drift | CrossReaderPassiveDriftTests pass; exact world/session/planet/hash/original expiry binding and no internal renewal remain unchanged. |
| New world defaults | Existing TestWorldCoordinator and tests preserve singleplayer, Peaceful, non-sandbox, 1× native creation. |

The new upstream recovery code permits only researched directional links. The fifth durable transition to 29151 is available through expired-primary reauthorization; active-primary evidence versions 5/6 retain their two earlier pairs. Completed blueprint history can cross only its proven completed-history suffix; partial/unknown history rejects. Store reads preserve original bytes. These are capabilities requiring separate future authority, not executed migrations.

## Build and automated verification

SDK 8.0.426 / .NET 8.0.32 on Windows. Locked full-solution restore and complete Release build, including actual net472 Plugin: **zero warnings and errors**. The initial build exposed three linked-test compilation errors described above; all were fixed before the passing build.

Five compile references are present only in the clone's ignored directory. Current installed Assembly-CSharp was copied read-only as a compile input; its SHA-256 is `EF314425DADE51F529F184F07BE136CD3C2660A6A81515603C7226FBE7AB1D8C`, exactly the upstream 0.10.35.29151 reference. BepInEx reference SHA-256: `DC1CB6B58B962BDA5AAA1D6B5F9AE14EC174F61836A1A1F96C1A040C7E8381F7`. Unity references match installed files. The BepInEx compile reference comes from the existing protected local reference set; its presence in a live game installation was not newly certified. No DLL or private manifest is committed.

| Suite | Passed | Failed / skipped |
| --- | ---: | ---: |
| Contracts | 73 | 0 / 0 |
| Bridge.Core | 2844 | 0 / 0 |
| Plugin.ReadAccess | 145 | 0 / 0 |
| MCP | 259 | 0 / 0 |
| Total | **3321** | **0 / 0** |

Full commands: `dotnet restore Spherewright.sln --locked-mode`; `dotnet build Spherewright.sln -c Release --no-restore`; `dotnet test Spherewright.Core.slnf -c Release --no-build` with retained local TRX files.

Fourteen PowerShell checks passed as actual `pwsh -NoProfile -File` processes: bridge-client, action-client, stage-tools, belt-site-qualification, evidence-index, evidence-reader, factory-evidence, production-sampling, package-surface-policy, powershell-entrypoint, install-release-preflight, install-transaction, install-recovery and source-mcp. Fixtures make zero real game calls. Documentation validation and its five retention regressions also pass. Source MCP reports 0.4.0.0, **64 tools / one resource**, exact embedded playbook, normal exit and zero extra stdout.

Both manual and Thunderstore layouts were built locally and verified as preview packages; preview manifests explicitly report sourceDirty=true. Clean-commit rebuild, package metadata/bilingual smoke and remote CI results are recorded against the resulting head in the PR. Synthetic installer tests prove only fixture behavior, not a real installation, crash recovery or another computer. All outputs stay ignored and are not release artifacts.

## Steering and deployment boundary

Source is ready for independent review after exact-head CI passes. Native compilation and offline tests establish source compatibility, not live deployment acceptance. Before any separately authorized cold deployment, verify the actual matching BepInEx/native/runtime cohort, current session/authority, durable Journal and unresolved-action state without inheriting upstream approvals. Preserve existing backups and use the existing transactional deployment/recovery workflow.

Future bounded live acceptance should recheck the exact unowned non-combat path and same-star remote reads on 29151, all configured public-read/write boundaries, Save identity/readback, and any explicitly approved owned recovery/history scenario. Positive Passive Drift remains a separately scoped live case; prior Steering acceptance of the earlier cohort is neither repeated nor invalidated here. No new sustained-production, recovery, manager-profile or cross-computer evidence is claimed. Stop after Draft PR/CI handoff; no merge or deploy is authorized by this report.
