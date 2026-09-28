# Execution brief for Codex

Use `CDA-UI-DECOUPLING-SHARED-v3` with the owner's **specific module/surface assignment**. This shared bundle alone is not an instruction to modify the product. Choose the simplest correct implementation; no required number of interfaces, partial classes, DTOs, projects or commits.

## Establish the actual starting point

Read the current repository instructions, canonical UI seams document, testing guide, relevant CI steps and the chosen module's extraction record. Record actual branch, HEAD, relevant sibling revisions, local changes, SDK and dependency mode. The audit SHA in this bundle is historical provenance, never a checkout command. Reinspect changed audit files and any new consumers. Use the connected Code Analytics MCP for symbols, references and affected-test discovery when available; corroborate its results with source and test discovery. If unavailable, use repository search/build metadata and state that limitation. Never invent a successful MCP call.

Assess the **rendered closure**: routed page, descendants, deferred tabs, dialogs, slots, shared widgets, imports, public types, JS/CSS, registrations and real consumers. Identify the existing read/write owners, editor lifetime and externally observable behavior before moving files. A cross-module dependency is resolved through its owner, not hidden behind a page facade.

## Make a bounded change

Separate feature renderers into an existing or justified lightweight Razor library and keep routing, application orchestration and production effects with their module/host. Reuse existing contracts and component families. Choose presentation records with typed intents or a cohesive workspace view contract according to the surface; do not convert all existing variants to one framework. Narrow query ports and browser-only rendering effects are legitimate where their role is explicit.

Preserve existing HTTP control planes where they already exist. Do not introduce HTTP between co-located UI and services merely to achieve separation. Do not pull EF, provider implementations, runtime hosting or the production composition root into renderers or their sandbox, even transitively. Inspect evaluated project references and package/asset closure, not just imports or assembly names.

A module assignment may authorize the necessary UI library, sandbox, moved contracts and narrow consumer fixes without asking again for each routine choice. It does not authorize unrelated owner redesign, schema migration, API/auth changes, broad sibling rewrites, publication or merges. Record a newly discovered external prerequisite as a bounded dependency repair or a blocked sub-scope; continue independent useful work without pretending the blocked dimension passed.

## Prove the result

Use the same real renderers in production and a deterministic backend-free sandbox. Cover the meaningful loading, error, stale, restricted, edit and overlay states. Retain draft/validation lifetime, target-bound effects and backend authorization. A successful write followed by a failed refresh is not a failed write and must not cause a second submission.

Build affected production projects, discover and execute the narrow owning tests, then exercise the actual production route through Playwright with its relevant user actions. Include cross-module journeys when the changed seam participates in them. Run the current required static/documentation gates. Broader stable, live-provider, container and cross-platform gates are triggered by affected contracts and the repository's current rules, not by the end of every small phase. Report blocked or skipped lanes explicitly.

Measure the edit-to-visible loop and graph with comparable before/after conditions. Do not disable watching important source or use stale packages to manufacture improvement. No percentage claim without corresponding samples and provenance.

## Handoff

Update the canonical module boundary record and local READMEs; do not append another independent shared rulebook. State retained responsibilities, remaining debt, exact test/discovery evidence, graph/asset results and observed performance. Keep code, UI text, comments, documentation and test identifiers in English; the final owner-facing message may be Czech.

Preserve the user's worktree and existing signed-commit policy. Commit only when authorized by the module assignment; keep the ordinary unlocked GPG agent/session instead of disabling signing, storing a passphrase or weakening its configuration. Push, merge, release and publication require their own authorization. A signed checkpoint is not a claim that unexecuted validation is green.
