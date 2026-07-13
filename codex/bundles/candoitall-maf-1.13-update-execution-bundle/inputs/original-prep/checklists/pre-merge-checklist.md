# Pre-Merge Checklist: MAF 1.13 Conservative Update

## Package evidence

- [ ] `Microsoft.Agents.AI` is updated to `1.13.0`.
- [ ] `Microsoft.Agents.AI.OpenAI` is updated to `1.13.0`.
- [ ] `Microsoft.Agents.AI.Workflows` is updated to `1.13.0`.
- [ ] `Microsoft.Extensions.AI.Abstractions` direct reference is updated to `10.6.0`.
- [ ] `Microsoft.Extensions.DependencyInjection.Abstractions` direct reference is updated to `10.0.9`.
- [ ] A2A preview package decision is documented from NuGet CLI output.
- [ ] Mem0 preview package decision is documented from NuGet CLI output.
- [ ] No unrelated package upgrades are mixed into the same change.

## Architecture guardrails

- [ ] No new `ProcessAgentRuntimeToolProvider`.
- [ ] No new direct `processes_*` runtime tools.
- [ ] No expansion of `/api/processes` routes.
- [ ] No new dependency from generic process projects to Microsoft Agent Framework packages.
- [ ] No product-module dependency added into generic MAF core projects.
- [ ] No central package management introduced.
- [ ] No broad warning suppression added.
- [ ] No new Foundry hosting, Durable workflow, DevUI, FileMemory/FileAccess feature adoption.

## Runtime behavior

- [ ] Approval behavior is preserved.
- [ ] Required finalizer behavior is preserved.
- [ ] Structured output behavior is preserved.
- [ ] Provider lane gates and streaming timeouts are preserved.
- [ ] Provider failure redaction is preserved.
- [ ] Runtime tool ownership tracing is preserved.
- [ ] Context manifests and context contribution traces are preserved.
- [ ] Session serialization/approval continuation remains compatible or fails with clear diagnostics.

## Validation evidence

- [ ] `dotnet restore CanDoItAll.slnx` completed or failure is explained.
- [ ] `dotnet build CanDoItAll.slnx --configuration Release --no-restore` completed.
- [ ] Focused unit tests completed or replaced with nearest existing tests and documented.
- [ ] Focused integration tests completed or replaced with nearest existing tests and documented.
- [ ] Broad unit/integration/component tests completed or explicitly deferred with reason.
- [ ] Playwright smoke completed or explicitly skipped with environment reason.
- [ ] `git diff --check` passed.
- [ ] Source scan for stale/direct process tool surfaces passed.
- [ ] Evidence doc committed.
