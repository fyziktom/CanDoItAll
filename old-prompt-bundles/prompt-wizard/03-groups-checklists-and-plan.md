# Groups, Checklists, and Creation Plan

- Recommended core blocks: 57
- Toolbox-ready blocks: 10
- Stack profile blocks: 14

## Executed creation plan
1. Analyze prompt packs for recurring structure and proof obligations.
2. Normalize the library into atomic groups, blueprints, and flows.
3. Add supported stack adapters and quick-insert toolbox snippets.
4. Validate the library against multi-agent simulation cases.
5. Export JSON, markdown snippets, CSV, and XLSX artifacts.

## Session Framing and Role
Defines the agent role, authority, and problem-solving posture.

| Component | Key | BlockKind | Toolbox | Recommended |
| --- | --- | --- | --- | --- |
| Role: Architecture Lead | role-architecture-lead | Instruction | no | yes |
| Role: Embedded and MIDI Engineer | role-embedded-midi-engineer | Instruction | no | yes |
| Role: Implementation Lead | role-implementation-lead | Instruction | no | yes |
| Role: Implementation Planner | role-implementation-planner | Instruction | no | yes |
| Role: Refactor Specialist | role-refactor-specialist | Instruction | no | yes |
| Role: Senior Reviewer | role-senior-reviewer | Instruction | no | yes |
| Role: Test and Validation Lead | role-test-validation-lead | Instruction | no | yes |
| Role: UI and UX Engineer | role-ui-ux-engineer | Instruction | no | yes |

## Mission, Scope, and Success
Pins down the actual goal, boundaries, and end-state.

| Component | Key | BlockKind | Toolbox | Recommended |
| --- | --- | --- | --- | --- |
| First Response Contract | first-response-contract | Delivery | no | no |
| Mission: Business Context | mission-business-context | Instruction | no | no |
| Mission: Exact Goal | mission-exact-goal | Instruction | no | yes |
| Required Deliverables | required-deliverables | Delivery | no | yes |
| Scope: In-Scope Items | scope-in-scope-items | Constraint | no | yes |
| Scope: Out-of-Scope Items | scope-out-of-scope-items | Constraint | no | yes |
| Stop Condition | stop-condition | Constraint | no | no |
| Success Criteria | success-criteria | Validation | no | yes |

## Context Loading and Discovery
Tells the agent what to read, inspect, and confirm before taking action.

| Component | Key | BlockKind | Toolbox | Recommended |
| --- | --- | --- | --- | --- |
| Context: Assumptions and Open Questions | assumptions-and-open-questions | Delivery | no | no |
| Context: Current State Audit | current-state-audit | Instruction | no | yes |
| Context: Dependency Inventory | dependency-inventory | Instruction | no | no |
| Context: Environment and Commands | environment-and-commands | Instruction | no | yes |
| Context: File Touch Plan | file-touch-plan | Instruction | no | no |
| Context: Relevant Artifacts and Fixtures | relevant-artifacts-and-fixtures | Instruction | no | no |
| Context: Repository Map Confirmation | repo-map-confirmation | Instruction | no | yes |
| Context: Required Reading List | required-reading-list | Instruction | no | yes |

## Guardrails and Constraints
Defines non-negotiables, limits, and safety rules.

| Component | Key | BlockKind | Toolbox | Recommended |
| --- | --- | --- | --- | --- |
| Guardrail: Comments in English | comments-in-english | Constraint | no | no |
| Guardrail: Honest Blocker Reporting | honest-blocker-reporting | Delivery | no | yes |
| Guardrail: No Destructive Changes | no-destructive-changes | Constraint | no | yes |
| Guardrail: No Placeholder-Only UI | no-placeholder-ui | Constraint | no | no |
| Guardrail: Non-Negotiable Rules | non-negotiable-rules | Constraint | no | yes |
| Guardrail: Preserve Architecture Boundaries | preserve-architecture-boundaries | Constraint | no | yes |
| Guardrail: Preserve Backward Compatibility | preserve-backward-compatibility | Constraint | no | yes |
| Guardrail: Protect Secrets and Sensitive Data | protect-secrets-and-sensitive-data | Security | no | yes |
| Guardrail: Safe Ambiguity Handling | safe-ambiguity-handling | Constraint | no | yes |
| Guardrail: Small Verifiable Increments | small-verifiable-increments | Constraint | no | yes |

## Workflow Orchestration and Continuity
Enforces phases, gates, status updates, and handoff continuity.

| Component | Key | BlockKind | Toolbox | Recommended |
| --- | --- | --- | --- | --- |
| Workflow: Branch and Status Tracking | branch-and-status-tracking | Instruction | no | no |
| Workflow: Checklist Update Loop | checklist-update-loop | Instruction | no | no |
| Workflow: Decision Log Maintenance | decision-log-maintenance | Delivery | no | no |
| Workflow: Known Gaps Log | known-gaps-log | Delivery | no | no |
| Workflow: Multi-Agent Chain | multi-agent-chain | Instruction | no | yes |
| Workflow: Next Prompt Pointer | next-prompt-pointer | Delivery | no | no |
| Workflow: Persistent Progress Files | persistent-progress-files | Delivery | no | no |
| Workflow: Required Phase Output Format | required-phase-output-format | Delivery | no | yes |
| Workflow: Sequential Phases | sequential-phases | Instruction | no | yes |
| Workflow: Stop After Phase | stop-after-phase | Constraint | no | yes |

## Architecture and Analysis
Reusable sections for architecture, audits, gap analysis, and design artifacts.

| Component | Key | BlockKind | Toolbox | Recommended |
| --- | --- | --- | --- | --- |
| Architecture: API Contract Design | api-contract-design | Instruction | no | no |
| Architecture: Blueprint | architecture-blueprint | Instruction | no | yes |
| Architecture: Data Model and Migration Design | data-model-and-migration-design | Instruction | no | no |
| Architecture: Domain Model and Entities | domain-model-and-entities | Instruction | no | no |
| Architecture: Gap Analysis | gap-analysis | Instruction | no | yes |
| Architecture: Parity Matrix | parity-matrix | Instruction | no | no |
| Architecture: Risk Register | risk-register | Validation | no | yes |
| Architecture: UX Surface Map | ux-surface-map | Instruction | no | no |

## Planning and Checklists
Breaks work into milestones, files, tests, and acceptance gates.

| Component | Key | BlockKind | Toolbox | Recommended |
| --- | --- | --- | --- | --- |
| Planning: Acceptance Checklist | acceptance-checklist | Validation | no | no |
| Planning: Definition of Done | definition-of-done | Validation | no | yes |
| Planning: Dependency Ordering | dependency-ordering | Instruction | no | yes |
| Planning: Files and Modules Likely Involved | files-and-modules-likely-involved | Instruction | no | no |
| Planning: Milestone Breakdown | milestone-breakdown | Instruction | no | no |
| Planning: Rollback and Recovery Plan | rollback-and-recovery-plan | Validation | no | no |
| Planning: Step-by-Step Implementation Plan | implementation-plan-step-by-step | Instruction | no | yes |
| Planning: Test Plan Matrix | test-plan-matrix | Testing | no | yes |

## Implementation Execution
Controls how code changes are made, sliced, and verified.

| Component | Key | BlockKind | Toolbox | Recommended |
| --- | --- | --- | --- | --- |
| Implementation: Additive Refactor First | additive-refactor-first | Instruction | no | yes |
| Implementation: Document Changed Files | document-changed-files | Delivery | no | no |
| Implementation: Feature Flag Rollout | feature-flag-rollout | Instruction | no | no |
| Implementation: Keep Public Surface Minimal | keep-public-surface-minimal | Constraint | no | no |
| Implementation: Manual Verification Steps | manual-verification-steps | Validation | no | no |
| Implementation: Preserve Existing Contracts and Data | preserve-existing-contracts-and-data | Constraint | no | no |
| Implementation: Run Build After Each Slice | run-build-after-each-slice | Testing | no | yes |
| Implementation: Small Safe Slices | implement-in-small-slices | Instruction | no | yes |

## Validation, Testing, and Review
Makes quality evidence mandatory rather than optional.

| Component | Key | BlockKind | Toolbox | Recommended |
| --- | --- | --- | --- | --- |
| Validation: Accessibility Validation Pass | accessibility-validation-pass | Validation | no | no |
| Validation: Architecture Validation Pass | architecture-validation-pass | Validation | no | no |
| Validation: Evidence Output Required | evidence-output-required | Delivery | no | yes |
| Validation: Final Audit | final-audit | Validation | no | yes |
| Validation: Mandatory Integration Tests | mandatory-integration-tests | Testing | no | no |
| Validation: Mandatory Playwright Tests | mandatory-playwright-tests | Testing | no | no |
| Validation: Mandatory Unit Tests | mandatory-unit-tests | Testing | no | yes |
| Validation: Observability Validation Pass | observability-validation-pass | Validation | no | no |
| Validation: Performance Validation Pass | performance-validation-pass | Validation | no | no |
| Validation: Regression Proof Required | regression-proof-required | Validation | no | yes |
| Validation: Reviewer Findings First | reviewer-findings-first | Validation | no | yes |
| Validation: Security Validation Pass | security-validation-pass | Security | no | no |

## Output, Delivery, and Handoff
Standardizes the final response, evidence, and next-step instructions.

| Component | Key | BlockKind | Toolbox | Recommended |
| --- | --- | --- | --- | --- |
| Output: Artifact List and Links | artifact-list-and-links | Delivery | no | no |
| Output: Commands Executed | output-commands-executed | Delivery | no | yes |
| Output: Completion Summary | output-completion-summary | Delivery | no | yes |
| Output: Files Changed | output-files-changed | Delivery | no | no |
| Output: Handoff to Next Agent | handoff-to-next-agent | Delivery | no | no |
| Output: Implementation Plan | output-implementation-plan | Delivery | no | no |
| Output: Remaining Risks and Next Steps | output-remaining-risks-and-next-steps | Delivery | no | yes |
| Output: Scope Summary | output-scope-summary | Delivery | no | yes |

## Stack Profiles
Stack-specific constraints and guidance blocks for common technology areas.

| Component | Key | BlockKind | Toolbox | Recommended |
| --- | --- | --- | --- | --- |
| Stack: .NET Solution | stack-dotnet-solution | Instruction | no | yes |
| Stack: Arduino Firmware | stack-arduino-firmware | Instruction | no | yes |
| Stack: Blazor | stack-blazor-webapp | Instruction | no | yes |
| Stack: Canvas in HTML/JS | stack-canvas-html-js | Instruction | no | yes |
| Stack: EF Core | stack-efcore | Instruction | no | yes |
| Stack: HTML/JS/CSS | stack-html-js-css | Instruction | no | yes |
| Stack: M5Stack | stack-m5stack | Instruction | no | no |
| Stack: MIDI and Audio | stack-midi-audio | Instruction | no | yes |
| Stack: Offline-First Sync | stack-offline-first-sync | Instruction | no | yes |
| Stack: PHP Web App | stack-php-webapp | Instruction | no | yes |
| Stack: Playwright MCP | stack-playwright-mcp | Testing | no | no |
| Stack: PostgreSQL | stack-postgresql | Instruction | no | no |
| Stack: SQLite | stack-sqlite | Instruction | no | no |
| Stack: Tailwind CSS | stack-tailwind-css | Instruction | no | no |

## Toolbox Snippets
Short optional inserts for concrete actions such as Docker tests or Playwright capture.

| Component | Key | BlockKind | Toolbox | Recommended |
| --- | --- | --- | --- | --- |
| Toolbox: Cache Downloads to Save Mobile Data | toolbox-cache-downloads-mobile-data | Constraint | yes | no |
| Toolbox: Capture Browser Artifacts | toolbox-capture-browser-artifacts | Delivery | yes | no |
| Toolbox: Create Manual QA Checklist | toolbox-create-manual-qa-checklist | Validation | yes | no |
| Toolbox: Cross-DB Compatibility Check | toolbox-cross-db-compat-check | Testing | yes | no |
| Toolbox: Database Migration Dry Run | toolbox-db-migration-dry-run | Testing | yes | no |
| Toolbox: Generate Fixtures and Seed Data | toolbox-generate-fixtures-and-seed-data | Testing | yes | no |
| Toolbox: Run Integration Tests in Docker | toolbox-run-integration-tests-docker | Testing | yes | no |
| Toolbox: Run UI Tests in Docker | toolbox-run-ui-tests-docker | Testing | yes | no |
| Toolbox: Run Unit Tests in Docker | toolbox-run-unit-tests-docker | Testing | yes | no |
| Toolbox: Use Playwright MCP Now | toolbox-use-playwright-mcp-now | Testing | yes | no |
