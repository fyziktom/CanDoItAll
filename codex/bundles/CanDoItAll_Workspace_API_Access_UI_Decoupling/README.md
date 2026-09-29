# Workspace API Access UI decoupling

**Target:** Codex GPT-6 Astra Max · **Bundle:** CDA-WORKSPACE-API-ACCESS-UI-DECOUPLING-v1

Execute [prompt.md](prompt.md). First reproduce and repair the bounded Files identity
finding in [Core review](CORE_REVIEW.md), then extract the complete existing API Access
section. This is not a second Core extraction or an extraction of all Workspace.

The actual reviewed HEAD is `186ab60a8e1a2dd7ab79c71e1a256cb0b2fdaedf`, on
`components-decoupling`. The repeated user label “Memory” does not select a stale commit:
the latest implementation contains Workspace Settings Core and Resources readiness fixes.
Use the current assigned checkout; the SHA is provenance only. EV01, EV02.

## Read in this order

1. Current repository instructions and [shared foundation](shared/README.md).
2. [Core review](CORE_REVIEW.md), [next-slice selection](MODULE_SELECTION.md),
   [dependency design](ARCHITECTURE.md) and [API source notes](API_ACCESS_REVIEW_NOTES.md).
3. [Sensitive state](SENSITIVE_STATE.md), [validation matrix](VALIDATION_MATRIX.md)
   and [development loop](DEV_LOOP.md).

[Proof limits](PROOF_STATUS.md) separate source review, implementer-reported results
and package validation. [Sources](SOURCES.md) records exact coverage. The historical
shared module map is not the current work queue; use this bundle's selection and actual code.

## Completion scope

Repair WSC-R1 without coupling Files to API administration. Then deliver the actual status,
issuance, account management, token list and nested scope/confirmation dialogs through a
light API-specific UI boundary and its own real sandbox. Keep general Core dependencies
unchanged. Production Settings composes the API leaf through the existing active slot.
Data Sources, Storage/recovery and all unrelated modules remain working but deferred.

The archive contains guidance, provenance and validation utilities, not an application patch,
.NET build, TRX, screenshot, token, password, executable or font. All 22 files under `shared/`
are copied byte-for-byte from the prior input. No earlier module bundle is recursively nested.

```text
python tools/validate_package.py
python tools/test_package.py
python shared/tools/validate_bundle.py
python shared/tools/test_tooling.py
```

[Package validation](PACKAGE_VALIDATION.md) certifies only this handoff's integrity.
