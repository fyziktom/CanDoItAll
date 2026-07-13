# Normalized Requirements

## R1 Typed Step Capability And Proof Contract

- Process definitions must be able to declare allowed, denied, suppressed, and required tools, skills, MCPs, capability identities, operation classifications, and scoped instruction fragments.
- Process definitions must be able to declare required proof receipts for runtime tools and MCP tools.
- The contract must be strongly typed and persisted or compiled from template data without magic string decisions in orchestration code.
- Success criteria: a QA recheck step can declare that current-run browser screenshot, console, runtime launch/stop, and image analysis receipts are required.

## R2 HR Readiness And Matching

- The project-structure HR matching and launch preview must evaluate each role assignment against the effective step contract.
- The readiness result must distinguish missing agent capability, missing MCP provider, suppressed tool, unavailable project access, and missing proof contract support.
- Success criteria: before launch or dispatch, a step that requires Playwright/image analysis reports a concrete readiness gap when the selected agent or runtime cannot satisfy it.

## R3 Runtime Metadata And MAF Boundary

- Process runtime metadata must carry the effective contract to MAF through trusted governed-process metadata.
- MAF may enforce generic capability policies and receipt requirements, but must not contain software-delivery-specific prompt normalization or QA-specific proof logic.
- Success criteria: process-owned contract data can allow, suppress, or require tools without editing common workspace plugin domain prompts.

## R4 Outcome Receipt Gate

- Step finalization must reject, block, or route to fallback when an outcome claims success but required current-run receipts are absent.
- The gate must use actual recorded runtime/MCP tool receipts, not only artifact text or summary claims.
- Success criteria: the run `6f0d229f` failure pattern cannot end as accepted `Completed` or equivalent when required browser/image receipts are missing.

## R5 Manager Fallback And Process Drivers

- Manager fallback must receive typed missing-proof diagnostics.
- Recovery must choose between proof-focused redispatch, reassignment, process-driver recovery, or explicit NeedsAttention, based on the diagnostic.
- Domain-specific fallback belongs in process drivers or process-owned strategies, not generic MAF code.
- Success criteria: a QA recheck missing screenshot/image receipts triggers a proof fallback path instead of repeating artifact-only recovery.

## R6 Template Migration

- Software-delivery and screenshot/writeback process templates must move proof requirements from prose-only instructions into typed contract data while retaining concise instruction fragments for agent behavior.
- Capability, MCP, and skill templates must remain consistent with the new contract names.
- Success criteria: migrated templates produce non-empty capability/proof scope for QA steps that require browser/image proof.

## R7 Testability And Performance

- New services must be unit-testable without launching full process runs.
- Contract compilation should be cacheable per process definition snapshot and step key/hash.
- Avoid per-attempt full catalog recomposition where existing runtime capability planners can be reused.
- Success criteria: targeted unit/integration tests cover contract compilation, readiness gaps, receipt gate failures, fallback decisions, and migrated templates.
