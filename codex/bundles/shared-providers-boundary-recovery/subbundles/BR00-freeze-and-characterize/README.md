# BR00 — Freeze and characterize

## Objective

Verify the actual branch baseline, characterize all provider ownership and runtime consumers, and turn the target decisions into executable guard inputs before production edits.

## Production code changes

None.

## Required steps

1. Verify repository, branch, and HEAD.
2. Review commits after audited HEAD `fdf1ff9702c376ad0ffd101a34d6bf542c9857d2`, if any.
3. Run the inventory guard:

   ```bash
   python codex/bundles/shared-providers-boundary-recovery/scripts/check_provider_boundary.py \
     --repo . \
     --mode inventory \
     --output artifacts/provider-boundary-baseline.json
   ```

4. Source-search and confirm every production caller of:
   - `ProviderExecutionService`
   - `IProviderRuntimeGateway`
   - `ProviderRegistry`
   - `IProviderAdapter`
   - `WorkspaceBackedAgentProviderProfileRegistry`
   - `WorkspaceAgentProviderProfileMapper`
   - shared-provider domain/application services
5. Identify the canonical solution and the smallest affected build/test projects. Record paths only in `RESULT.md`; do not create another inventory document.
6. Confirm the current physical provider/shared-provider table mappings and migration IDs.
7. Confirm that the original shared-provider bundle explicitly locked Workspace ownership. Do not edit it.

## Mandatory findings to validate

- Workspace contains a general provider profile/execution stack predating shared providers.
- AgentFramework also contains a provider-driver/runtime stack.
- Workbench is a known consumer of the Workspace direct execution stack.
- AgentFramework provider projection depends on Workspace provider persistence/services.
- shared-provider entities have instance/provider semantics, not workspace aggregate semantics.

When a finding is no longer true because the branch changed, record the exact newer architecture and assess it against `DECISION-LOCK.md`.

## Acceptance

- Baseline inventory JSON exists.
- Current HEAD and any delta from audited HEAD are recorded.
- All known provider runtime consumers are listed in the single `RESULT.md`.
- No production file changed.
- `git diff --check` passes.

## Validation budget

No restore, build, test, EF, Docker, or package operation is required.

## Proof tier and selection

- Proof tier: Standard.
- Owning check: the inventory guard above, expected to exit zero and create the baseline JSON.
- Automated tests: N/A because BR00 changes no production behavior.
- Broad gate: not authorized or required; no production contract changes in BR00.

## Commit

`BR00: characterize provider boundary baseline`
