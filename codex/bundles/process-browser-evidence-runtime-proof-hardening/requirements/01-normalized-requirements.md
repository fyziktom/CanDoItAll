# Normalized Requirements

| Requirement | Source | Owner | Acceptance |
| --- | --- | --- | --- |
| `R001` Artifact-backed browser proof | `N002`, DB artifact gap | `SB01` | A UI/browser proof step cannot satisfy screenshot/snapshot/console expectations solely by mentioning paths in markdown or result summaries; process artifact records must reference durable files under the scoped run artifact root. |
| `R002` Provider-native MCP evidence ingestion | `.playwright-mcp` outputs and execution logs | `SB01` | Browser MCP screenshot, snapshot/DOM, console, and evaluate outputs are imported or mirrored even when chat history is empty and the provider wrote default `.playwright-mcp` names. |
| `R003` Missing evidence blocks or repairs | `N005` | `SB02` | If required browser evidence files are missing, empty, unreadable, or detached, the step selects a repair/blocking outcome and records a conformance observation. |
| `R004` Console phase classification | `N004` | `SB02` | Active validation console errors block acceptance; post-stop disconnect noise is classified separately and cannot be summarized as warning-free without the phase boundary. |
| `R005` Representative interaction proof | `N001`, `N003` | `SB02` | Interactive UI steps must assert a representative visible behavior from project structure or step contract. A pause toggle, page title, or non-empty DOM alone is insufficient for game/canvas/custom-control acceptance. |
| `R006` Generic process core | `N006`, `N007` | `SB03` | Process runtime enforces generic proof categories and artifact lifecycle only; domain-specific criteria live in project structure, process step definitions, skills, and agent instructions. |
| `R007` Exact generic artifact paths in process definitions | `N002`, DB expectation prose | `SB03` | Multi-team software-delivery QA steps declare exact generic browser artifact paths or typed proof requirements for screenshot, console log, snapshot/DOM, and interaction summary. |
| `R008` Agent instruction hardening | `N003`, `N007` | `SB03` | QA and implementation agents are instructed to capture, review, and cite process-visible browser artifacts and to use project-structure acceptance hints for representative interaction proof. |
| `R009` Regression fixture for DB failure shape | DB run `4f218d64-...` | `SB04` | Tests reproduce the completed-run failure shape: screenshot expected in prose, browser calls in logs, `.playwright-mcp` files exist, no process image artifact records, no conformance observations, and quality acceptance should fail or require repair. |
| `R010` Clean development DB validation | User asked to test whole flow again after repair | `SB04` | Final execution prepares a clean development DB, reruns workflow and multi-team software delivery, and proves browser screenshots and console logs are visible in process artifacts. |
| `R011` UI proof analytics | `N001`, `N002` | `SB04` | Execution report records route, viewport, Playwright MCP actions, screenshots, console log path, assertions, result, and screenshot review questions. |
| `R012` Source-of-truth diagnostics | Current code gaps | `SB01`, `SB02` | Logs and conformance observations include enough actionable state to explain missing evidence without exposing secrets. |

## Non-Requirements

- The bundle does not fix or rewrite the generated Tetris application.
- The bundle does not make Playwright MCP mandatory for non-UI process steps.
- The bundle does not require exhaustive cross-browser or long-duration gameplay testing for every generic process run; it requires representative proof strong enough for the step contract.
