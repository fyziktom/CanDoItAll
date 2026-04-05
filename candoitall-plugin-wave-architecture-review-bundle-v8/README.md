# CanDoItAll Plugin-Wave Architecture Review Bundle v8

- Analysis date: `2026-04-05`
- Source analyzed: `CanDoItAll-canonical-model-refactor.zip`
- Runtime validation: `BLOCKED` in this environment because `dotnet` is not installed
- Bundle state: `Prepared`
- Readiness gate for large plugin wave (email / LinkedIn / custom API): `NO-GO`
- Small/local feature work: `Possible with caution`

## Executive verdict

This refactor wave **did close some of the most serious earlier problems**. The workbench no longer looks like it is persisting mirrored cross-module projection truth, node binding/reference extraction now exists, lifecycle history exists, and a real connector-manifest foundation has been added.

However, the architecture is **still not stable enough for the next major plugin wave**.

The remaining blockers are not cosmetic:

1. the universal node carrier is still not fully sealed from binding/reference leakage,
2. editable-node hierarchy is still stored twice,
3. node capability and assignment policy are still split across registry + UI + CRM/HR service code,
4. the connector platform is not yet fully plugin-first in the active provider/resource flows,
5. write-side connector integrations still do not have a durable operation boundary.

## What changed relative to earlier bundles

### Clearly improved
- persisted synchronized projection truth appears resolved
- binding/reference side tables exist
- lifecycle history exists for note promotion / subtype mutation
- connector manifests and registries exist
- service extraction and architecture tests are healthier

### Still not closed
- node-core vs binding/facet boundary
- dual hierarchy representation
- incomplete capability centralization
- legacy-enum driven plugin flows
- durable side-effect boundary for future connectors

## Hard gates

The plugin wave must not start until these gates pass:

- `HG-01` Node core is sealed and binding/reference truth is externalized
- `HG-02` Editable-node hierarchy has one canonical owner
- `HG-03` Registry owns node-scoped capability and assignment policy
- `HG-04` Connector platform is plugin-first, not enum-first
- `HG-05` Write-side connectors have a durable operation boundary

## Contents

- detailed findings
- target architecture notes
- a sequenced refactor plan
- hard-gate + symbol-retirement rules
- subbundles for each finding
- XLSX workbook for tracking and execution
- a repo gate script that currently fails on the remaining blockers
