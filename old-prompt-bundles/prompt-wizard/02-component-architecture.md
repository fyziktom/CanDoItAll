# Component Architecture

## Composition order
1. role and mission
2. context loading
3. guardrails
4. architecture or planning
5. implementation or validation
6. stack profiles
7. toolbox snippets
8. output and handoff

## Relationship to the current app model
- `PromptBlockDefinition`: atomic reusable block
- `PromptBlueprint`: prompt type plus recommended flow
- `PromptFlowTemplate`: ordered workflow and agent sequence
- `PromptRun` and `PromptRunNode`: execution lineage
- `ValidationRun`: quality and review pass

## Groups
| Order | Group | UI Mode | Component Count | Purpose |
| --- | --- | --- | --- | --- |
| 1 | Session Framing and Role | wizard-core | 8 | Use these blocks first so the model knows whether it is architecting, reviewing, planning, implementing, or validating. |
| 2 | Mission, Scope, and Success | wizard-core | 8 | These blocks stop prompt drift and make the session outcome measurable. |
| 3 | Context Loading and Discovery | wizard-core | 8 | Most strong packs force the agent to read the repo, current state, and artifacts before proposing or changing anything. |
| 4 | Guardrails and Constraints | wizard-core | 10 | These blocks are the difference between a useful coding agent and an over-eager one. |
| 5 | Workflow Orchestration and Continuity | flow-core | 10 | The prompt packs consistently treat workflows as sequential, test-gated, and stateful. |
| 6 | Architecture and Analysis | flow-core | 8 | These blocks are typically used by the first agent or by planning-focused sessions. |
| 7 | Planning and Checklists | flow-core | 8 | These blocks convert architecture into action without leaving the next agent to improvise. |
| 8 | Implementation Execution | flow-core | 8 | The best packs force additive, low-risk implementation with continuous proof. |
| 9 | Validation, Testing, and Review | validation-core | 12 | This group turns prompts into engineering workflows instead of writing exercises. |
| 10 | Output, Delivery, and Handoff | wizard-core | 8 | Strong packs require crisp handoff artifacts after each phase. |
| 11 | Stack Profiles | stack-auto | 14 | These are auto-applied or manually inserted based on the selected stack. |
| 12 | Toolbox Snippets | toolbox | 10 | These are the right-click or quick-add blocks that users can drop into a prompt. |

## Blueprints
| Blueprint | Prompt Type | Recommended Flow | Summary |
| --- | --- | --- | --- |
| Architecture Spec | architecture | architecture-review-plan-implement-validate | Creates an implementation-ready architecture, gap analysis, risk model, and recommended next steps. |
| Repository Audit | audit | audit-plan-refactor-review | Audits the current state of the repository, identifies gaps, and creates a target map. |
| Implementation Plan | plan | architecture-review-plan-implement-validate | Converts an approved design or goal into milestones, files, tests, and acceptance checkpoints. |
| Feature Implementation | implementation | architecture-review-plan-implement-validate | Delivers a feature or enhancement in code with staged verification and handoff. |
| Safe Refactor | refactor | audit-plan-refactor-review | Improves structure while preserving behavior and locking regressions down. |
| Bugfix with Regression Lock | bugfix | bugfix-regression-proof | Fixes a bug and leaves behind targeted regression proof. |
| Senior Code Review | review | release-hardening-final-audit | Produces findings-first review output focused on behavior, risk, and missing evidence. |
| Test Strategy and Automation | testing | playwright-automation-upgrade | Designs or expands the test matrix, automation approach, and evidence expectations. |
| Validation Audit | validation | release-hardening-final-audit | Performs a final proof-oriented audit on architecture, testing, and residual risk. |
| Performance Hardening | performance | release-hardening-final-audit | Optimizes hot paths, runtime cost, or scheduling behavior with explicit proof. |
| Security Hardening | security | release-hardening-final-audit | Focuses the session on threats, trust boundaries, secret handling, and hardening proof. |
| UI/UX Delivery | ui | ui-canvas-feature-delivery | Designs and delivers user-facing flows with real interaction, accessibility, and browser proof. |
| Embedded Firmware Iteration | embedded | embedded-midi-firmware-tuning | Designs and delivers firmware or hardware-integrated changes with timing and hardware constraints visible. |

## Placeholder glossary
| Token | Meaning |
| --- | --- |
| exact_goal | The exact feature, fix, or artifact the prompt should focus on. |
| target_feature_or_problem | Short name of the feature, module, or problem space. |
| business_context | Why the work matters for the user or business. |
| in_scope_item_1 | A concrete requirement or work item that must be covered. |
| out_of_scope_item_1 | An adjacent area that should be explicitly excluded. |
| success_criterion_1 | A measurable success condition. |
| deliverable_1 | A concrete output artifact such as code, docs, tests, or a checklist. |
| solution_or_workspace_root | Top-level repo or solution root that should be confirmed. |
| primary_projects_or_modules | Main projects, services, modules, or packages that matter. |
| tests_and_validation_projects | The test suites or validation paths tied to the change. |
| build_command | Canonical build command for the workspace. |
| unit_test_command | Canonical unit test command for the workspace. |
| integration_test_command | Canonical integration or API test command. |
| ui_test_command | Canonical Playwright or UI test command. |
| docker_compose_file_or_dockerfile | Docker asset that should be used for isolated test runs. |
