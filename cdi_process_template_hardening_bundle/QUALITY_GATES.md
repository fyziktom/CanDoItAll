# Quality gates

## Global rule
After every few subbundles, the run must stop for an architecture review. If the review finds that the architecture or implementation is heading in the wrong direction, a corrective subbundle must be created and completed before any later phase may continue.

## Gate rules
### Gate A — Post-materialization architecture review
- Trigger: 01, 02, 03
- Primary focus: Bundle-application truth, template-pack materialization, and process completeness
- Must stop on: Any missing template-pack folders, misleading validation claims, or architecture simplifications still driven by old module limits
- Mandatory outputs: Architecture review memo A, gap register, go/no-go decision
- Corrective rule: Add a corrective subbundle before continuing.

### Gate B — Hardening review
- Trigger: 05, 06
- Primary focus: DI explicitness, pack-root resolution, SQLite-safe write paths, and hidden coupling
- Must stop on: Hidden static loading, non-atomic write chains, or unresolved provider-specific risks
- Mandatory outputs: Architecture review memo B, owned corrective actions, updated traceability
- Corrective rule: Add a corrective subbundle before continuing.

### Gate C — Post-decomposition review
- Trigger: 08, 09, 10, 11
- Primary focus: Maintainability after file splits, regression-net strength, and absence of behavior drift
- Must stop on: Refactors that only moved complexity, missing regression coverage, or unresolved high-severity debt
- Mandatory outputs: Architecture review memo C, residual-risk statement, final go/no-go decision
- Corrective rule: Add a corrective subbundle before continuing.

### Gate Final — Final QA closure review
- Trigger: 13
- Primary focus: Honest validation boundary, ZIP completeness, and delivery quality
- Must stop on: Missing process-template folders in the bundle, inaccurate validation claims, or undocumented residual debt
- Mandatory outputs: Final QA memo, validation result, final ZIP
- Corrective rule: Add a corrective subbundle before delivery.

## Non-negotiable completion criteria
- The final ZIP must physically contain the process-template folders.
- The pack validator must report zero errors.
- The bundle-application audit must be honest about what was and was not executed.
- Any remaining architectural debt must stay visible in the final QA memo.
