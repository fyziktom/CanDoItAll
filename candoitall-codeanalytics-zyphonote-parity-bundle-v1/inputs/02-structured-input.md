# Structured Input

## Core Objective

- Convert the Zyphonote findings into an execution-grade parity bundle, implement the missing analysis surfaces in the sibling CodeAnalytics stack plus host MCP, and rerun the same five Zyphonote scenarios against the updated MCP.

## Hard Constraints

- Reuse the sibling `C:\repositories\CanDoItAll.CodeAnalsis` projects instead of copying analysis code into the host repo.
- Keep the host MCP thin and prefer application-service-backed logic.
- Treat this as an analysis MCP parity pass, not an editing parity pass.
- If the new MCP tool surface requires a Codex restart to become callable in this session, stop after reinstall and request the restart explicitly.

## Source Artifacts

- The Zyphonote comparison bundle and its scenario matrix.
- The two Zyphonote findings files.
- The existing sibling symbol-parity bundle and its parity inventory.
- Current host MCP source files, reinstall script, and Codex repo guidance.

## Input Coverage Signals

- The user explicitly asked for bundle preparation, execution, and validation.
- The user explicitly asked to add missing SharpTools tools that our MCP still lacks.
- The user explicitly asked to rerun the same scenarios after the update.
- The user explicitly anticipated the need for skill guidance if the MCP surface changed.

## Dependency And Sequencing Signals

- The gap inventory must be frozen before code changes continue.
- Project-navigation parity must land before the Zyphonote Scenario 1 rerun is meaningful.
- Member/source parity must land before the Zyphonote Scenario 4 rerun is meaningful.
- Reinstall and skill guidance must land before final proof.

## Validation Expectations

- Prepared-stage bundle validation must pass before implementation starts.
- Focused build and targeted tests must pass for the sibling and host repos as the implementation proceeds.
- Final closure requires reinstall success and a rerun of the same five Zyphonote scenarios.

## UI Validation Strategy

- N/A. This bundle changes an analysis MCP and repo-managed skill guidance, not browser UI.

## Browser Validation Analytics

- N/A. Record `N/A` explicitly in the execution report instead of leaving browser sections empty.

## Working Assumptions

- The benchmark-driven parity target for this pass is the SharpTools analysis surface.
- The sibling repo can accept new abstractions and query methods where needed.

## Primary Risks

- New MCP tool additions may require a Codex restart before final proof can continue.
- Scenario 4 may need a deterministic alternative tool path if focused context remains brittle.
