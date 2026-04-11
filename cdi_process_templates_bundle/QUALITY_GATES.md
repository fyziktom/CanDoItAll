# Quality gates

## Architecture review gate A — schema and pack alignment

**When triggered:** After the pack schema, baseline scenarios, and process-definition refresh are drafted.

**Objective:** Verify that the authored template schema fully matches the current module capabilities and no legacy simplifications remain.

**Primary checks:**
- Role usages are explicit and separate from resource libraries.
- Dependencies and artifact inputs are modeled first-class where needed.
- Baseline scenarios match the current repository expectations exactly.
- No process step is simplified solely because the legacy module lacked a feature.

**If failed:** Create a corrective subbundle immediately, block downstream work, and re-run gate A after the correction lands.

**Required evidence:** Gap review note, validator output, and corrected pack diff.

## Architecture review gate B — runtime projection and documentation parity

**When triggered:** After import-envelope projections, Mermaid exports, and supporting markdown files are generated.

**Objective:** Prove that current-module projections, sidecars, and Mermaid exports remain mutually consistent.

**Primary checks:**
- Import envelopes preserve step counts, role counts, dependencies, artifact inputs, and branch outcomes.
- Supporting markdown sidecars exist for shared and local resources used by each process.
- Mermaid flowcharts and sequence diagrams map to the same process semantics as the JSON definition.

**If failed:** Add a corrective subbundle for projection or exporter mismatches and do not begin final QA until it passes.

**Required evidence:** Projection compatibility reports, exporter test results, and sidecar inventory diff.

## Architecture review gate C — final QA and senior architect inspection

**When triggered:** After tests, validation scripts, and corrective subbundles are complete.

**Objective:** Perform final senior QA and senior C# architecture inspection before bundle closure.

**Primary checks:**
- All current-architecture expectations are satisfied.
- The remaining hardcoded canvas chrome debt is isolated behind an explicit corrective plan.
- No process template omits roles, artifacts, checklists, validations, or prompts that are required for responsible execution.

**If failed:** Add another corrective subbundle, update the traceability matrix, and repeat the final inspection.

**Required evidence:** Final QA inspection memo, validator output, traceability matrix, and bundle manifest.

## Strict rule

If any architecture review or validation stage concludes that the implementation is moving in the wrong architectural direction, execution must stop. A corrective subbundle must be added, completed, and validated before any further downstream subbundle is allowed to continue.
