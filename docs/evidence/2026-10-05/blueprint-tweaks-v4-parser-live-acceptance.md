# BlueprintTweaks v4 parser live acceptance — 2026-10-05

## Scope

This record covers the bounded live acceptance of the BlueprintTweaks v4 trailer parser fix in the exact current manually loaded unowned session. It does not claim save persistence, restart persistence, sustained production, or broader blueprint compatibility.

Source/runtime:

- source commit: `c51589d4b1438477288240e4bdc3b8da0f423030`
- product/plugin version: `0.4.0`
- DSP version: `0.10.35.29104`
- cold deployment operation: `20261004-235308-65fb40fb`
- pre-deploy backup: `20261004-235310-a2c0ee03`
- deployed payload: five files, post-deploy hashes matched the preflight payload exactly
- post-deploy bridge/session health: connected, `writesAllowed=true`, `writeHealth=healthy`, no write blockers or quarantine
- session mode: exact observed-unowned session; Dark Fog telemetry reported passive aggressiveness `0`

## Exact regression blueprint

The user-provided one-Tesla-tower blueprint used for both the parser regression and live acceptance was:

```text
BLUEPRINT:1,10,0,0,0,0,0,0,639267245837041737,0.10.35.29104,New%20Blueprint,,,,"H4sIAAAAAAAACmNiQAWMUAxh/2dgOAFlMoKFZ/3//x/En8mhg6TngOQ2EP0fCtCMhJjHAiIAUK9Cjm4AAAA="1254E583D2F91540990DD1DAEFE838EC
```

The public `inspect_blueprint` call remained ownership-restricted in this unowned session and returned `SESSION_NOT_OWNED`; that result is an authorization boundary, not a parser failure. The authorized-unowned action path therefore used `prepare_blueprint_build`, as specified by the tool contract.

## Prepare-only parser acceptance

A fresh authorized-unowned `prepare_blueprint_build` with the exact code succeeded:

- `prepared=true`
- `commitAllowedNow=true`
- one object, item `2201` (Tesla Tower)
- native condition `Ok`
- native placement check passed
- technology satisfied
- inventory sufficient
- no blockers
- no `blueprint_trailing_data` error

This establishes live parser acceptance of the exact verified BlueprintTweaks v4 empty/default trailer without requiring a gameplay mutation.

## End-to-end finite build acceptance

After a fresh prepare, exactly one commit was submitted.

- build ID: `87957af0-6114-49d2-8995-83fb8dce1077`
- action ID: `bccd2e4b-d429-4036-987c-004200bf7659`
- action terminal state: `completed`
- `succeeded=true`
- object index `0`: `completed`
- resulting entity ID: `1475`
- Tesla Tower inventory: `51 -> 50`
- submitted objects: `1`
- completed objects: `1`
- no replay, blocker, failure kind, recovery requirement, or outcome-unknown reconciliation

Fresh readback showed the same build completed and the session remained authorized and healthy:

- `writesAllowed=true`
- `writeHealth=healthy`
- no write blocker
- no write quarantine

## Boundary

No save was performed after this blueprint build and no restart/recovery test followed it. Therefore this record does **not** establish persistence of entity `1475` across save/restart. It establishes the bounded live chain:

`BlueprintTweaks-v4 code parse -> authorized-unowned prepare -> native site validation -> one commit -> normal construction -> terminal completion -> exact item/entity readback`.
