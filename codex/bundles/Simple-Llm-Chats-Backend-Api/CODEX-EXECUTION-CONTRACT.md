# Codex execution contract

## Operating mode

- Work only on the repository and branch supplied by the operator.
- Re-read the current repository state before each subbundle. The prepared baseline is evidence, not a
  license to overwrite newer code.
- Execute one subbundle at a time.
- Do not start a locked subbundle.
- Do not silently widen scope.
- Do not commit, push, merge, or open a pull request unless the operator explicitly asks.
- Preserve unrelated working-tree changes.
- All source-code comments must be in English.
- Use cross-platform .NET and Python/PowerShell 7 commands. Do not introduce Windows-only paths,
  separators, shell assumptions, or DPAPI dependencies.
- Prefer existing canonical helpers, value objects, codecs, provider registries, database runtime
  identity, error mapping, and authorization conventions over parallel abstractions.

## Mandatory skills

Before implementation, load and follow these skills from `CanDoItAll.SharedInfo/codex/skills`:

- `bundles/candoitall-bundle-execution`
- `bundles/candoitall-csharp-architecture-bundle-guard`
- `csharp-architecture-governor`
- `architecture-reviews/feature-block-architecture-review`
- `architecture-reviews/canonical-model-review`

Use their current versions. The bundle captures the preparation-time conclusions but does not replace
the installed skill workflow.

## Test discipline

The repository contains thousands of tests. Repeated broad runs are prohibited.

During SB00–SB10:

- build only affected projects;
- run only named test classes or a narrow fully-qualified-name filter;
- reuse an existing Release build with `--no-build` where valid;
- do not run `dotnet test CanDoItAll.slnx`;
- do not run the complete Unit or Integration project without a filter;
- do not run Playwright, LiveProcess, LongRunning, or Quarantined lanes;
- record every command in the subbundle proof manifest.

Only SB11 may execute the stable solution-wide Release gate. The exact policy is in
`plan/04-test-budget-and-gates.md` and machine-readable limits are in `test-budget.json`.

## Architecture stop conditions

Stop the current subbundle and record a blocking finding when any of these occurs:

- the new module would require a reference to MAF, AgentFramework Core, tools, skills, MCP, Memory,
  Processes, or a product UI project;
- provider-specific SDK types would leak into product/domain contracts;
- a second database-profile identity mechanism would be introduced;
- the implementation would reactivate the generic file-backed conversation service globally;
- operation idempotency cannot distinguish the same request from a conflicting request;
- a cross-profile switch could allow a provider result to commit to the old profile;
- transcript completion and operation reconciliation cannot be proven after a crash;
- the implementation requires modifying agent chat Razor components;
- a full test suite appears in a non-final subbundle command.

## Evidence and handoff

Every completed subbundle must contain:

- `proof/proof-manifest.json`, based on its template;
- test/build command transcripts or summarized structured output;
- architecture assertions and changed-file inventory;
- `SESSION-HANDOFF.md` completed with residual risks and the next unlock decision.

A verbal claim is not proof.
