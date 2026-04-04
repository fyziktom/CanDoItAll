# Structured Input

## Active Next-Wave Findings

- `NW-01`: lifecycle reconciliation is still non-atomic across Workbench and CRM/HR persistence boundaries
- `NW-02`: Workbench metadata still persists canonical-looking party identifiers and richer linked-party payloads
- `NW-03`: the Workbench bridge still uses raw node-key strings for canonical node-scoped operations
- `NW-04`: the universal Workbench node remains broad and needs explicit extension guardrails, not an impulsive rewrite

## Execution Intent

- Implement the smallest safe code changes that materially reduce future refactor risk.
- Use ADRs for structural guidance where a production refactor would be unjustified in this wave.
- Preserve the existing repaired browser behavior and revalidate it.
