# Current development status

This page describes the public development source. It is not a report of an installed Plugin, an active game session, or completed live acceptance. Consult the actual session state and the matching package before using a capability.

## Current unowned sessions

The exact manually loaded world can use two independent, default-off settings:

- `Safety.AllowUnownedRichReads=true` enables bounded public rich reads and reports `ReadAccessMode=observed_unowned`. Owned worlds retain normal rich reads.
- `Safety.AllowWrites=true` together with `Safety.AllowUnownedNormalWrites=true` enables ordinary actions on the exact current unowned session. This does not enable public rich reads when `AllowUnownedRichReads=false`.

Neither setting grants ownership. `OwnedBySpherewright` remains false; reads never grant write authority. The private action reader can obtain the state needed by authorized actions without changing the public read surface. Dark Fog aggressiveness is nullable telemetry, not an unowned authorization gate. The action surface includes no direct offensive combat commands.

Normal unowned Save targets the current native save identity without import, adoption, an owned copy, or a protected resume ticket. Unowned interplanetary flight creates no Spherewright FlightCheckpoint. Finite unowned blueprint progress is bound to the current session and process, and is discarded after replacement or restart. Public blueprint inspection/export, protected Journal, Governor, Save Import, Owned Resume, and checkpoint reload retain their separate ownership and provenance requirements.

## Material inventory cuts

`spherewright_inspect_factory_entity` optionally accepts `materialInventoryObjectIds`: at most 256 unique positive built-object IDs in the current local factory. It uses the same public rich-read policy as factory inspection: owned, or exact observed-unowned with rich reads enabled. Normal write authorization alone does not make the public query readable.

A cut captures selected buffers and complete identity-verified native cargo paths at one game tick. Each path and cargo stack is counted once. The existing cell, member, cargo, and object budgets remain enforced; incomplete or invalid coverage is unknown, never zero. Do not add overlapping paths, prorate a path to selected belts, or stitch cuts from different ticks. A stock observation does not prove production, flow, source allocation, sustainable supply, or balance.

## Validation and remaining boundaries

Offline validation covers the read/write authority split, exact-session gates, protected provenance, material-cut budgets and duplicate-cargo rejection, and the existing Contracts, Core, MCP, and offline client checks. Local build and test results do not establish Unity behavior or live recovery for this combined source. The integration has no new deployment or live acceptance claim.

Broader sustained-supply and finite-buffer exclusion, complete Foundry/Governor acceptance, departure preparation, and cross-computer package validation remain separate gates in [ROADMAP.md](../ROADMAP.md). Ordinary materials, native construction, fresh prepare/commit, single-flight, idempotency, terminal observation, and unknown-outcome quarantine remain mandatory.

## Historical upstream material-source audit (2026-10-05, Asia/Singapore)

The upstream audit used source `4ac502c` and the installed `b1557bb` cohort with native `0.10.35.29104`. Its installation, save and recovery evidence belongs to that earlier cohort and owned world; it is not deployment or live acceptance of this Phase-2 integration. The earlier 1210 chain had produced nonzero output, been normally saved and recovered through protected restart, and produced again. Its complete factory baseline was captured at tick 90521995 (5945 built objects, no prebuilds, 11620 reciprocal edges), rather than after the later recovery. The latest normal save was tick 92128739/R1/J100; the later read-only observation was tick 93031047/R1 with unchanged durable Journal. Natural production after that save is not saved evidence.

The later source experiment observed 120 samples and 2396 reads with no writes. A 155-tick gap left only 27378 continuous qualifying ticks, below the 36000-tick gate. Exact native 1210 output in that segment was four items, below the minimum eight. Hydrogen and deuterium stocks fell by 48 and six items at their separate cut timestamps; neither these cuts nor hydrogen recycling gross output prove synchronized net supply. Full source allocation, finite-buffer exclusion, sustained supply, Governor and complete Gate 2 remain unproved.

The bounded graphite follow-up traced 100 objects in the historical topology, then freshly inspected a 12-object cut, three complete native paths and three generators. Cracker 3965 remained idle with graphite output 20. Its outlet 4185 fed belt 4165, through 4190/4191 toward graphite-burning generators 3058/3060/3062 and assembler 3404. The assembler had all three inputs present and ten super magnetic rings in its output, while idle. The generators produced 3651/3959/3944 J/t; `isWorking=false` does not prove stopped generation, and fuel inventory was not observed. Network N3 was fully served with generator ratio about 0.109; this observation is not a rated power budget for new construction.

Graphite paths 92/140/142 held 113/77/65 items and were outside the earlier 70-path candidate selection. Those earlier cuts remain complete within their declared selection; an empty observed scope-gap list cannot prove coverage of unselected byproduct branches. This supports investigating graphite downstream demand/backpressure as a hydrogen-source constraint, but proves neither a sustainable destination nor a complete repair. The next qualification must use actual terminal demand and runtime recipes for a minimal graphite destination serving 1210, account for hydrogen recycling, and verify the complete source/route/consumer, material, power and interface budget. Until the full plan is executable, only bounded reads and prepare-only checks follow; no construction, bulk material movement, buffer clearing or repeated long experiment is implied. The historical checks added no save, exit or load; the dedicated Host-exit survival test was not performed. See the [material-source audit](evidence/2026-10-04/material-inventory-cuts.md) for the dated observation boundaries.
