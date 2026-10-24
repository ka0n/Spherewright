# Spherewright


## Fork authority and provenance

This ka0n fork integrates only the pinned official release. Upstream Lab rules, installed-candidate evidence and author-specific approvals grant no fork authority. Current fork provenance remains in docs/current-status.md; Steering controls execution and deployment. Audit windows default to ten accepted actions; larger explicit windows need separate Steering authorization.

Public rich reads and normal writes have independent default-off gates. On the exact current unowned session, public rich reads require `Safety.AllowUnownedRichReads=true`. Ordinary non-combat actions require both `Safety.AllowWrites=true` and `Safety.AllowUnownedNormalWrites=true`, independently of the read gate. `OwnedBySpherewright=false` remains unchanged. Peaceful mode, Passive mode and Dark Fog aggressiveness (including unknown telemetry) are not unowned normal-action authority gates. Combat automation remains excluded. The private action reader never expands public reads. Same-star remote reads require public rich-read authority, an already resident factory and exact identity; normal actions stay local-planet-only.

Authorized unowned Save uses the current native save identity without adoption, rename, owned copy or protected resume ticket. Unowned flight creates no owned FlightCheckpoint; unowned finite blueprint progress stays session/process-bound. Public blueprint inspection/export, Journal, Governor, Import, owned Save/Resume and checkpoint reload retain their separate protected ownership/provenance gates. Import still needs prepare disclosure followed by explicit confirmation. New worlds remain singleplayer, Peaceful, non-sandbox, 1×.

Passive-Drift evidence remains shared across separate readers, bound to the exact world/session/planet, original hash and original expiry. Internal reads do not renew evidence. Blueprint-v4 accepts only EOF or the exact trailer `04 00 00 00 00`; other suffixes reject. These rules do not bypass native validation, health/quarantine, idempotency, single-flight, terminal reconciliation or any recovery gate.

[English](README.md) · [简体中文](README.zh-CN.md)

Spherewright connects an external Agent to **Dyson Sphere Program** through a matching MCP server and BepInEx Plugin. Structured observation, planning and bounded native actions retain normal materials, research, power, construction drones and game time. The product embeds no LLM or autonomous expansion loop.

Plugin, MCP, Contracts and Bridge.Core remain one version-coupled monorepo. [Spherewright-Lab](https://github.com/Arcueid77/Spherewright-Lab) owns scenarios, operational handoffs and detailed evidence; product builds, tests and packaging are independent.

## Versions and scope

This fork integrates the official [v0.4.0 release tag](https://github.com/Arcueid77/Spherewright/releases/tag/v0.4.0) for independent Steering review. See the [fork integration report](docs/validation/fork-v040-integration.md). The upstream package-callability candidate was `e018b7f45c70f5fe029418cad987af794b5cb9bf`; its frozen production-acceptance candidate was `2a7a98d1932c404e3a7abc2c11233d9c0b8780d9`. All upstream runtime results below retain those original source/session bindings and confer no fork acceptance or deployment authority.

- **Overseer:** bounded production, power, logistics and research observations, upstream tracing and diagnosis. Recommendations grant no write authority.
- **Foundry:** multi-level recipe, material, machine, base-power and bounded site/dependency plans with explicit external supplies. Preview and execution remain separate.
- **Finite actions:** native construction, configuration, supported upgrades, procurement and limited blueprints, with per-object progress, cancellation and fresh continuation.
- **Governor:** pre-execution production declarations and game-tick observations. Inventory, rated capacity and short samples do not prove sustained production.
- **Save continuity:** owned, ticket-bound exact-primary resume with durable Journal continuity and separately confirmed disclosed migration. No arbitrary save picker or editor.

The frozen 29145 candidate passed its declared three-stage chain, continuous joint supply, preparation kit, save/recovery and both local package-layout checks. [Validation](docs/validation/v0.4.0.md) records its exact hashes, scope and limits. Those production results retain their original source/session scope. Interstellar flight/logistics remains future work.

## Install and use

Frozen production acceptance used Windows x64, DSP `0.10.35.29145`, BepInEx `5.4.17.0` and one owned peaceful/non-sandbox/1× local world. The current candidate was tested on the same local environment with DSP `0.10.35.29151`. Older releases retain their own support scope. Use matching Plugin, MCP and shared assemblies.

Both current package layouts independently passed all 64 installed MCP calls, including data/preparation success and expected native safety refusals, with no accepted or unknown matrix action. The owner-confirmed original-world migration and subsequent same-version recovery preserved durable Journal and completed history. See the [fixed runtime report](https://github.com/Arcueid77/Spherewright-Lab/blob/00f4359eef704bc7cd5c148438c05e0d3121bda6/operations/package-callability-2026-10-17.md) and [native validation](docs/validation/native-29151-preflight.md). This phase adds no new sustained-production acceptance.

- [Installation](docs/release-installation.md) and [interrupted-installation recovery](docs/installation-recovery.md)
- [Agent playbook](docs/agent-playbook.md), also embedded in MCP and included in packages
- [Protocol](docs/protocol.md), [safety model](docs/safety-model.md) and [architecture](docs/architecture.md)
- [Release notes](docs/release-notes/v0.4.0.md), [CHANGELOG](CHANGELOG.md) and [all documentation](docs/README.md)

Writes require fresh native prepare, plan-bound commit, original-action reconciliation and relevant readback. Unknown outcomes are quarantined. Item/technology injection, instant construction, teleportation and direct game-buffer writes are outside the public action surface. Dark Fog/combat, multiplayer/Nebula, broad third-party compatibility and another-computer use are unvalidated.

## Develop

```powershell
dotnet restore Spherewright.Core.slnf --locked-mode
dotnet build Spherewright.Core.slnf -c Release --no-restore
dotnet test Spherewright.Core.slnf -c Release --no-build
```

Full Plugin builds need legally installed DSP/BepInEx references; see [environment setup](docs/research/environment.md). Package scripts record this repository's own source SHA and clean state; they do not fetch or execute Lab.

Start scenario work from Lab; its fixed product submodule is read-only by default. Implement reusable fixes in an independent clone/worktree, test, commit and obtain successful exact-SHA CI before Lab updates its gitlink/lock. Read [contribution policy](CONTRIBUTING.md), [contributor rules](AGENTS.md), [product status](docs/project-status.md) and [Lab setup](docs/sample-validation.md).

Licensed under [MIT](LICENSE), with a [Chinese explanatory translation](LICENSE.zh-CN.md).
