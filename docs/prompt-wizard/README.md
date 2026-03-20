# Prompt Wizard Library

This library was generated from a direct analysis of the prompt packs in `inputs/prompts packs` and expanded with additional agentic-coding patterns needed for CanDoItAll's prompt wizard and manager.

## What is included
- 112 reusable prompt components
- 12 component groups
- 13 blueprint types
- 10 flow templates
- 5 simulation cases with coverage validation
- import-friendly JSON seed files aligned to `CanDoItAll.Modules.Factory`
- markdown snippet files for each component
- an Excel catalog at `output/spreadsheet/prompt-component-library.xlsx`

## Counts by category
| Group | Components | UI Mode | Purpose |
| --- | --- | --- | --- |
| Session Framing and Role | 8 | wizard-core | Use these blocks first so the model knows whether it is architecting, reviewing, planning, implementing, or validating. |
| Mission, Scope, and Success | 8 | wizard-core | These blocks stop prompt drift and make the session outcome measurable. |
| Context Loading and Discovery | 8 | wizard-core | Most strong packs force the agent to read the repo, current state, and artifacts before proposing or changing anything. |
| Guardrails and Constraints | 10 | wizard-core | These blocks are the difference between a useful coding agent and an over-eager one. |
| Workflow Orchestration and Continuity | 10 | flow-core | The prompt packs consistently treat workflows as sequential, test-gated, and stateful. |
| Architecture and Analysis | 8 | flow-core | These blocks are typically used by the first agent or by planning-focused sessions. |
| Planning and Checklists | 8 | flow-core | These blocks convert architecture into action without leaving the next agent to improvise. |
| Implementation Execution | 8 | flow-core | The best packs force additive, low-risk implementation with continuous proof. |
| Validation, Testing, and Review | 12 | validation-core | This group turns prompts into engineering workflows instead of writing exercises. |
| Output, Delivery, and Handoff | 8 | wizard-core | Strong packs require crisp handoff artifacts after each phase. |
| Stack Profiles | 14 | stack-auto | These are auto-applied or manually inserted based on the selected stack. |
| Toolbox Snippets | 10 | toolbox | These are the right-click or quick-add blocks that users can drop into a prompt. |

## Coverage status
5 of 5 simulation cases pass the required group, role, and stack coverage checks.
