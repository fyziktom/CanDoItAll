# Structured Input

## Core Objective

- Bring the documentation back in line with the current CanDoItAll architecture and add enterprise-facing explanatory material that can be sent to non-repo stakeholders.

## Success Criteria

- `docs/architecture-beta.md` no longer uses the failing `architecture-beta` Mermaid block.
- README and docs index no longer advertise removed/suppressed Processes or ProjectStructure MCP setup as the active path.
- Technical docs explain the current HTTP API, API access, process runtime, AgentFramework bridge, advanced settings, and development validation.
- Less-technical docs explain the product value, operating model, process lifecycle, escalations, HR matching, observation/audit loop, and Economy ledger direction.
- Four generated infographic files are saved under a documentation image folder and referenced from customer-facing docs.
- Bundle and docs validation evidence is recorded before closure.

## Hard Constraints

- Preserve source-grounded technical claims.
- Do not claim removed/suppressed MCP servers are active.
- Use simple Mermaid syntax with quoted labels and no markdown links or HTML inside diagram labels.
- Do not invent shipping details for `CanDoItAll.Economy`; describe it as an adjacent/external private-ledger initiative.

## Allowed Side Effects

- Documentation files under `README.md`, `docs/`, and project-local documentation images.
- Bundle files under `.codex/bundles/docs-enterprise-refresh`.

## Source Artifacts

- User request preserved in `inputs/00-original-request.md`.
- Current README and docs under `C:/repositories/CanDoItAll/docs`.
- Current API and ProjectStructure endpoint mapping source files under `C:/repositories/CanDoItAll/src/CanDoItAll.Web`.
- Current repo-managed API skill docs under `C:/repositories/CanDoItAll/codex/skills`.

## Input Coverage Signals

- Broken Architecture Beta diagram must be directly fixed.
- Suppressed MCPs must be called out explicitly because stale docs currently point people to non-existent setup paths.
- Enterprise customer docs must be a new docs group, not only technical README edits.
- Infographics must be split by audience level.
- Economy ledger content must be included without pretending the external repo is part of this solution.

## Dependency And Sequencing Signals

- Technical architecture and API corrections must land before customer-facing docs can safely summarize the system.
- Infographic file paths must exist before customer docs reference them.
- Validation and closure depend on both technical docs and customer docs being updated.

## Validation Expectations

- Run prepared and completed bundle validators.
- Run `git diff --check`.
- Search for active setup claims around removed Processes and ProjectStructure MCP servers.
- Check Mermaid blocks are using render-safe diagram types.

## Evidence Contract

- `python C:/Users/lucys/.codex/skills/candoitall-bundle-preparation/scripts/validate_bundle.py --profile initiative --stage prepared .codex/bundles/docs-enterprise-refresh`
- `python C:/Users/lucys/.codex/skills/candoitall-bundle-preparation/scripts/validate_bundle.py --profile initiative --stage completed .codex/bundles/docs-enterprise-refresh`
- `git diff --check`
- `Select-String` searches for stale removed-MCP active setup claims.
- Generated image files under `docs/images`.

## UI Validation Strategy

- N/A. This is documentation and static image asset work, not Blazor UI behavior.

## Browser Validation Analytics

- N/A. Execution report will record `N/A` rows with documentation/image validation evidence instead of browser proof.

## Working Assumptions

- The current active replacement for the removed Processes and ProjectStructure MCP servers is the HTTP API plus repo-managed `candoitall-api-*` skills.
- `CanDoItAll.Economy` is external work and should be described as an integration direction rather than as currently shipped code in this repository.
- Generated infographics may use concise in-image labels; detailed wording belongs in Markdown captions to avoid unreadable image text.

## Primary Risks

- Mermaid renderability can regress if architecture diagrams use newer syntax not supported by GitHub.
- Customer-facing docs can overpromise if Economy or automation details are written as fully shipped instead of current/adjacent capabilities.
- Generated infographic text can be imperfect, so captions and alt text must carry the precise message.
