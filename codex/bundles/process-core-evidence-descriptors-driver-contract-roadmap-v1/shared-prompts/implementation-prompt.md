# Implementation Prompt

You are Codex working on `fyziktom/CanDoItAll` branch `maf-processes-refactor`.

Implement this bundle phase-by-phase. Do not skip gate subbundles. Do not collapse the execution report rows. Do not broaden Process Core.

Hard constraints:
- Core may receive only deterministic read-models/rules.
- No EF, workspace/storage/filesystem, AgentFramework execution, claim lifecycle, transition execution, finalizer application, projection persistence, validation orchestration, or driver runtime APIs in Core.
- No production process-driver APIs, registries, DI hooks, manager commands or runtime selectors.
- No UI/media/mobile proof unless UI files unexpectedly change; such changes should fail this bundle unless explicitly justified.

At every critical gate:
1. Run the required build/test/source scans.
2. Record transcripts.
3. Update execution report and proof manifest.
4. Reopen prior phase if downstream proof contradicts earlier assumptions.
