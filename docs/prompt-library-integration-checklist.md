# Prompt Library Integration Checklist

## Inventory Reconciliation

Verification pass 1:
- README count: 112 components, 10 flows, 13 blueprints.
- JSON count: 112 component records, 10 flow-template records, 13 blueprint records.
- Group catalog sum: 8 + 8 + 8 + 10 + 10 + 8 + 8 + 8 + 12 + 8 + 14 + 10 = 112.

Verification pass 2:
- UI library counts must match 112 components, 10 flows, 13 blueprints.
- Screenshot artifact counts must match 112 component additions, 10 flow additions, 13 blueprint additions.

## Implementation Checklist

- [ ] Import prompt components from `output/prompt-library/prompt-component-library.json`.
- [ ] Import prompt flows from `output/prompt-library/factory-prompt-flow-templates.seed.json`.
- [ ] Import prompt blueprints from `output/prompt-library/factory-prompt-blueprints.seed.json`.
- [ ] Import group catalog metadata from `output/prompt-library/group-catalog.json`.
- [ ] Preserve component keys, groups, tags, stack tags, toolbox flags, template tokens, and deterministic ids.
- [ ] Preserve flow keys, block keys, prompt type rules, and agent-sequence metadata.
- [ ] Preserve blueprint keys, prompt types, recommended flow keys, and recommended block keys.
- [ ] Extend the factory schema for imported-library metadata and session-level component customizations.
- [ ] Extend the factory schema for prompt-session attachments added from the canvas.
- [ ] Make the prompt wizard right-click menu expose the imported catalog in layered groups.
- [ ] Group prompt components into layered menu branches:
  - root -> Components
  - Components -> Core / Delivery / Environment
  - Core -> Session Framing / Mission & Scope / Context Discovery / Guardrails / Output & Handoff
  - Delivery -> Workflow / Architecture / Planning / Implementation / Validation
  - Environment -> Stack Profiles / Toolbox
- [ ] Group blueprints into layered menu branches.
- [ ] Group flows into layered menu branches.
- [ ] Add input actions to the prompt wizard right-click menu for file, image, video, link, and note items.
- [ ] Open a canvas composer modal for any component with template tokens before it is added.
- [ ] Open a canvas composer modal for input items that need title/notes/file data.
- [ ] Render added components, flows, blueprints, and inputs as canvas subitems immediately.
- [ ] Feed selected components, selected flow, selected blueprint, and added inputs into prompt composition.
- [ ] Expose the imported component/flow/blueprint catalog in the prompt library route.
- [ ] Add automated tests for import, library counts, menu grouping, token modal behavior, and prompt composition.
- [ ] Add automated Playwright coverage that captures one screenshot per imported component addition.
- [ ] Add automated Playwright coverage that captures one screenshot per imported flow addition.
- [ ] Add automated Playwright coverage that captures one screenshot per imported blueprint addition.
- [ ] Reconcile screenshot counts against the imported inventory a second time before completion.

## Menu Coverage

### Components

Core:
- Session Framing and Role
- Mission, Scope, and Success
- Context Loading and Discovery
- Guardrails and Constraints
- Output, Delivery, and Handoff

Delivery:
- Workflow Orchestration and Continuity
- Architecture and Analysis
- Planning and Checklists
- Implementation Execution
- Validation, Testing, and Review

Environment:
- Stack Profiles
- Toolbox Snippets

### Flows

Core Delivery:
- Architecture -> Review -> Plan -> Implement -> Validate
- Audit -> Plan -> Refactor -> Review
- Bugfix -> Regression Proof -> Close
- Release Hardening and Final Audit

UI and Data:
- UI and Canvas Feature Delivery
- Full-Stack Offline-First Feature
- Data-Layer Change with Cross-DB Proof
- Playwright Automation Upgrade

Specialized:
- PHP Canvas Modernization and Migration
- Embedded MIDI and Firmware Tuning

### Blueprints

Foundation:
- Architecture Spec
- Repository Audit
- Implementation Plan
- Feature Implementation
- Safe Refactor
- Bugfix with Regression Lock

Review and Assurance:
- Senior Code Review
- Test Strategy and Automation
- Validation Audit
- Performance Hardening
- Security Hardening

Experience and Embedded:
- UI/UX Delivery
- Embedded Firmware Iteration

## Exhaustive Component Coverage

### Session Framing and Role (8)
- Role: Architecture Lead
- Role: Embedded and MIDI Engineer
- Role: Implementation Lead
- Role: Implementation Planner
- Role: Refactor Specialist
- Role: Senior Reviewer
- Role: Test and Validation Lead
- Role: UI and UX Engineer

### Mission, Scope, and Success (8)
- First Response Contract
- Mission: Business Context
- Mission: Exact Goal
- Required Deliverables
- Scope: In-Scope Items
- Scope: Out-of-Scope Items
- Stop Condition
- Success Criteria

### Context Loading and Discovery (8)
- Context: Assumptions and Open Questions
- Context: Current State Audit
- Context: Dependency Inventory
- Context: Environment and Commands
- Context: File Touch Plan
- Context: Relevant Artifacts and Fixtures
- Context: Repository Map Confirmation
- Context: Required Reading List

### Guardrails and Constraints (10)
- Guardrail: Comments in English
- Guardrail: Honest Blocker Reporting
- Guardrail: No Destructive Changes
- Guardrail: No Placeholder-Only UI
- Guardrail: Non-Negotiable Rules
- Guardrail: Preserve Architecture Boundaries
- Guardrail: Preserve Backward Compatibility
- Guardrail: Protect Secrets and Sensitive Data
- Guardrail: Safe Ambiguity Handling
- Guardrail: Small Verifiable Increments

### Workflow Orchestration and Continuity (10)
- Workflow: Branch and Status Tracking
- Workflow: Checklist Update Loop
- Workflow: Decision Log Maintenance
- Workflow: Known Gaps Log
- Workflow: Multi-Agent Chain
- Workflow: Next Prompt Pointer
- Workflow: Persistent Progress Files
- Workflow: Required Phase Output Format
- Workflow: Sequential Phases
- Workflow: Stop After Phase

### Architecture and Analysis (8)
- Architecture: API Contract Design
- Architecture: Blueprint
- Architecture: Data Model and Migration Design
- Architecture: Domain Model and Entities
- Architecture: Gap Analysis
- Architecture: Parity Matrix
- Architecture: Risk Register
- Architecture: UX Surface Map

### Planning and Checklists (8)
- Planning: Acceptance Checklist
- Planning: Definition of Done
- Planning: Dependency Ordering
- Planning: Files and Modules Likely Involved
- Planning: Milestone Breakdown
- Planning: Rollback and Recovery Plan
- Planning: Step-by-Step Implementation Plan
- Planning: Test Plan Matrix

### Implementation Execution (8)
- Implementation: Additive Refactor First
- Implementation: Document Changed Files
- Implementation: Feature Flag Rollout
- Implementation: Keep Public Surface Minimal
- Implementation: Manual Verification Steps
- Implementation: Preserve Existing Contracts and Data
- Implementation: Run Build After Each Slice
- Implementation: Small Safe Slices

### Validation, Testing, and Review (12)
- Validation: Accessibility Validation Pass
- Validation: Architecture Validation Pass
- Validation: Evidence Output Required
- Validation: Final Audit
- Validation: Mandatory Integration Tests
- Validation: Mandatory Playwright Tests
- Validation: Mandatory Unit Tests
- Validation: Observability Validation Pass
- Validation: Performance Validation Pass
- Validation: Regression Proof Required
- Validation: Reviewer Findings First
- Validation: Security Validation Pass

### Output, Delivery, and Handoff (8)
- Output: Artifact List and Links
- Output: Commands Executed
- Output: Completion Summary
- Output: Files Changed
- Output: Handoff to Next Agent
- Output: Implementation Plan
- Output: Remaining Risks and Next Steps
- Output: Scope Summary

### Stack Profiles (14)
- Stack: .NET Solution
- Stack: Arduino Firmware
- Stack: Blazor
- Stack: Canvas in HTML/JS
- Stack: EF Core
- Stack: HTML/JS/CSS
- Stack: M5Stack
- Stack: MIDI and Audio
- Stack: Offline-First Sync
- Stack: PHP Web App
- Stack: Playwright MCP
- Stack: PostgreSQL
- Stack: SQLite
- Stack: Tailwind CSS

### Toolbox Snippets (10)
- Toolbox: Cache Downloads to Save Mobile Data
- Toolbox: Capture Browser Artifacts
- Toolbox: Create Manual QA Checklist
- Toolbox: Cross-DB Compatibility Check
- Toolbox: Database Migration Dry Run
- Toolbox: Generate Fixtures and Seed Data
- Toolbox: Run Integration Tests in Docker
- Toolbox: Run UI Tests in Docker
- Toolbox: Run Unit Tests in Docker
- Toolbox: Use Playwright MCP Now

## Tokenized Component Coverage

These 41 components require token-aware modal support before insertion:

- Role: Architecture Lead
- Role: Embedded and MIDI Engineer
- Role: Implementation Lead
- Role: Implementation Planner
- Role: Refactor Specialist
- Role: Senior Reviewer
- Role: Test and Validation Lead
- Role: UI and UX Engineer
- Mission: Business Context
- Mission: Exact Goal
- Required Deliverables
- Scope: In-Scope Items
- Scope: Out-of-Scope Items
- Stop Condition
- Success Criteria
- Context: Current State Audit
- Context: Environment and Commands
- Context: File Touch Plan
- Context: Repository Map Confirmation
- Context: Required Reading List
- Guardrail: Non-Negotiable Rules
- Architecture: API Contract Design
- Architecture: Blueprint
- Architecture: Data Model and Migration Design
- Architecture: Domain Model and Entities
- Architecture: Gap Analysis
- Architecture: Parity Matrix
- Architecture: Risk Register
- Architecture: UX Surface Map
- Planning: Definition of Done
- Planning: Rollback and Recovery Plan
- Planning: Step-by-Step Implementation Plan
- Implementation: Feature Flag Rollout
- Implementation: Preserve Existing Contracts and Data
- Validation: Accessibility Validation Pass
- Validation: Observability Validation Pass
- Validation: Performance Validation Pass
- Validation: Regression Proof Required
- Validation: Security Validation Pass
- Stack: .NET Solution
- Toolbox: Run Unit Tests in Docker
