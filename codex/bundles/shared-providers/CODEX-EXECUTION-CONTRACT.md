# Codex execution contract

## Operating mode

- Work only in the repositories and branches supplied by the operator.
- Re-read current repository state before every subbundle.
- Treat the prepared SHAs as evidence, not permission to reset or overwrite newer code.
- Preserve unrelated working-tree changes.
- Execute one subbundle at a time.
- Do not start a locked subbundle.
- Do not silently widen scope.
- Do not commit, push, merge, tag, publish, or open a pull request unless explicitly asked.
- All source-code comments must be in English.
- Use cross-platform .NET 10, Python, PowerShell 7, Docker Compose, and path handling.
- Keep PostgreSQL as the production application database. Do not add SQLite or an in-memory
  persistence substitute to product code.
- Prefer canonical helpers, registries, errors, secret resolution, authorization, usage
  records, and migration conventions over parallel implementations.
- Do not use reflection or service location to bypass project-reference boundaries.

## Mandatory current skills

Load and follow the current versions of:

- `bundles/candoitall-bundle-execution`
- `bundles/candoitall-bundle-validator`
- `bundles/candoitall-subbundle-validator`
- `bundles/candoitall-csharp-architecture-bundle-guard`
- `csharp-architecture-governor`
- `csharp-architecture-review-gate`
- `csharp-dependency-graph-audit`
- `csharp-provider-tool-plugin-isolation`
- `architecture-reviews/feature-block-architecture-review`
- `architecture-reviews/canonical-model-review`
- `architecture-reviews/canonical-model-review` with the persistence lens for SB02; this is
  the current semantic replacement for the retired `persistence-boundary-review` name
- `aspnet-core` plus `architecture-reviews/feature-block-architecture-review` for SB03, SB04,
  and SB11; these are the current semantic replacement for the retired `api-boundary-review`
  name
- `security-best-practices` plus `architecture-reviews/feature-block-architecture-review` for
  SB01, SB03, SB04, SB05, and SB07; these are the current semantic replacement for the retired
  `security-boundary-review` name
- `candoitall-codeanalytics-mcp`
- `candoitall-components-mcp` for SB08 and SB09
- `candoitall-api-shared-providers` after SB11 creates and installs it

If a named skill has moved, locate its current semantic replacement and record the mapping.
Do not omit the architecture or provider-isolation reviews.

## Source-of-truth hierarchy

When documents conflict, use this order:

1. current code and current repository rules;
2. explicit user requirements preserved in `inputs/00-user-request-verbatim.md`;
3. this execution contract;
4. target decisions in `architecture/`;
5. subbundle instructions;
6. preparation-time current-state notes.

A current-code change does not automatically invalidate the mission. It triggers the narrow
re-entry review named in the relevant subbundle.

## Test discipline

The repository's normal rule is affected production project plus the narrowest owning test
topic. Repeated broad runs are prohibited.

During SB00 through SB11:

- build only changed production projects;
- use exact `FullyQualifiedName=` filters where one behavior owns the change;
- otherwise use one bounded `FullyQualifiedName~SharedProvider...` topic;
- list tests first and record expected versus actual discovery;
- use `--no-build --no-restore` only after the current test assembly has been refreshed;
- do not run an unfiltered test project;
- do not run `CanDoItAll.Tests.Stable.slnx`;
- do not run Playwright except in SB09;
- do not run the multi-instance lane except in SB07 and SB12;
- do not call live OpenAI, Ollama, ComfyUI, Azure, or another paid/external service.

Only SB12 may run the stable aggregate, and only once at its frozen checkpoint. The named
invalidation triggers are new projects/references, Web composition, API authorization,
cross-cutting request context, EF model/migration changes, and OpenAPI changes.

## Security invariants

- A client never receives an upstream provider API key, secret identifier, secret name,
  environment variable, vault path, internal base URL, raw configuration JSON, private note,
  or internal provider profile ID.
- A shared source stores one credential reference. Imported profiles may reference a canonical
  source but may not duplicate credential values.
- Access-context reference is opaque correlation metadata, not authentication or
  authorization. Accept and propagate it between CanDoItAll or EGCP hops only through the
  `CanDoItAll-Access-Context-Ref` header, and strip it before calling the upstream provider.
- The central token subject remains the authenticated caller.
- Redirects are disabled or revalidated. Source and upstream URIs are normalized and checked
  against the explicit network policy.
- No request body, response body, attachment, generated image, tool arguments, or secret
  header is written to invocation audit records or routine logs.
- Built-in remote tools, provider-side storage, background execution, MCP, web search,
  file search, code interpreter, computer use, and provider-managed file IDs are denied
  unless a later explicit policy implements and tests them.
- Function-tool schemas may be relayed only so the client-side CanDoItAll runtime can execute
  its own tools.
- Unknown or unsupported compatibility fields fail explicitly. They are not silently
  forwarded.
- Client outage handling never silently falls back to a personal provider.

## Architecture stop conditions

Stop the current subbundle and mark it `BLOCKED` when implementation would require:

- exposing MAF or Workspace internal provider records as HTTP DTOs;
- an inner MAF/Core project referencing Workspace, Web, UI, EF, or SharedProviders.Http;
- Workspace referencing the HTTP implementation rather than an abstraction;
- Http integration referencing Web, Razor components, EF entities, or provider SDK types in
  public contracts;
- a new `ProviderKind.Shared` branch throughout the agent runtime when an OpenAI-compatible
  runtime projection can satisfy the behavior;
- duplicating the complete provider execution path in both legacy Workspace and MAF runtime;
- storing raw source tokens in provider configuration or one token value per import;
- using `ExtraSettingsJson` as the only relational source/import/publication model;
- direct SQL fixture mutation as proof of application behavior;
- treating a zero-test discovery as passing;
- weakening URI/TLS policy merely to make Docker tests pass;
- claiming tool, structured-output, vision, image, Responses, or streaming support without
  a positive and negative contract test;
- adding a production test bypass or unauthenticated administration endpoint;
- growing the existing large `WorkspaceModels.cs` or a runtime partial instead of adding
  cohesive top-level types;
- a broad test command before SB12;
- proceeding to UI while SB07 is not green.

## Required evidence

Every completed subbundle must contain:

- completed `proof/proof-manifest.json`;
- changed-file inventory;
- production build commands and results;
- list-tests command, expected discovery, actual discovery, and filtered test result;
- semantic positive and negative evidence appropriate to the proof tier;
- architecture assertion and project-reference delta;
- redaction/secret scan result where applicable;
- completed `SESSION-HANDOFF.md`;
- progression decision in `STATUS.md`.

A verbal summary is not proof.

For every `Governed` subbundle, the existing machine-state
`proof/proof-manifest.json` is supplemented at closure by `proof/manifest.md` and
`proof/semantic-invariants.md` under that subbundle. Those files use portable `repo://` and
`bundle://` references and satisfy the active artifact-backed proof contract.

## Final stable gate failure rule

The SB12 stable aggregate is one evidence-bearing command and consumes its budget whether it
passes or fails. Before running it, all affected builds, focused tests, Docker preconditions,
documentation checks, and OpenAPI/SharedInfo checks must already be green at the named frozen
checkpoint. If the stable aggregate fails:

- record the complete failure and keep SB12 blocked;
- diagnose and repair only with focused affected-scope checks;
- do not run the stable aggregate again under the current budget;
- a replacement aggregate requires explicit operator authorization and a durable amendment to
  `test-budget.json`, this contract, and the SB12 proof manifest.

## Final-state contract

SB12 exits only when:

- the final stable gate has run no more than once and passed;
- the final multi-instance lane passes from a clean E2E state;
- central, client-a, and client-b app containers remain healthy and running; SB12 must
  leave the validated stack running for operator testing;
- the deterministic upstream and PostgreSQL dependencies remain running;
- the operator handoff gives URLs, non-secret fixture names, source/catalog state, tested
  actions, log paths, and the cleanup command that was deliberately not run;
- OpenAPI and SharedInfo are synchronized to the final implementation commit;
- no bundle requirement is left in an ambiguous state.

A successful `DONE` closure requires every mandatory requirement to be `Solved`. `Partially
solved` and `Not solved` remain valid honest classifications for a blocked handoff, but they do
not support success language or a `DONE` final state.
