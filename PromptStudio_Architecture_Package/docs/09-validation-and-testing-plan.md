# 09 — Validation and Testing Plan

## 1. Quality strategy

PromptStudio is a workflow-heavy application with:
- many business rules
- high-value UI flows
- sensitive data handling
- multiple provider/connector integrations
- growing extensibility pressure

The testing strategy therefore cannot rely on one layer only. It must validate:
- domain correctness
- persistence correctness
- UI behavior
- end-to-end workflows
- safety constraints
- traceability and review flows

## 2. Testing principles

1. **Deterministic logic should be verified deterministically**
2. **High-value workflows must be tested end-to-end**
3. **Sensitive data paths require dedicated safety tests**
4. **Provider integrations must be contract-tested behind abstractions**
5. **Generated prompts should be validated structurally, not only visually**
6. **UI evidence and screenshots should support human review, not replace it**
7. **Test planning must start early and evolve with milestones**

## 3. Test pyramid for PromptStudio

### Base layer — unit tests
Use for:
- domain rules
- internal tab lifecycle rules
- watch-state normalization rules
- capsule parsing and drift rules
- tuning request packaging rules
- prompt rendering logic
- context assembly rules
- option compatibility rules
- validation rules
- helper services
- state transition logic

### Middle layer — integration tests
Use for:
- EF Core persistence behavior
- database provider bootstrap
- browser-state-backed tab restore contracts
- manager readiness confirmation contracts
- capsule artifact generation
- secret protection round-trips
- file storage abstractions
- activity/audit writes
- provider adapter boundaries
- job queue and background worker flows

### Upper-middle layer — component tests
Use for:
- Blazor forms
- internal tab strip behavior
- dev-only tuning boundary behavior
- wizard steps
- status badges and result components
- resource editor rendering
- prompt gallery filtering
- project structure inspector surfaces
- project calendar host surfaces
- validation center components

### Top layer — end-to-end tests
Use for:
- development manager ready-signal flow
- project creation flow
- internal tab restore flow
- project structure canvas flow
- project calendar flow
- resource registration flow
- prompt factory flow
- validation flow
- test evidence flow
- settings/provider configuration flow

## 4. Validation domains

## 4.1 Product artifact validation
These validations are about the content and structure of project artifacts:
- story completeness
- use-case completeness
- layout alignment
- architecture alignment
- plan alignment
- prototype alignment
- test coverage alignment

## 4.2 Technical validation
These validations are about system behavior:
- provider settings validity
- connector profile validity
- database configuration validity
- storage path validity
- secret handling validity
- background job validity

## 4.3 Safety validation
These validations are about operational safety:
- secret redaction
- approval gates
- safe export/send behavior
- dangerous action labeling
- no raw secret logging
- no prompt leakage through diagnostics

## 5. Test suites

## 5.1 Unit test suite

### Focus
- project aggregate rules
- phase/status transitions
- option selection rules
- resource descriptor behavior
- prompt versioning rules
- blueprint rendering
- context assembly
- validation rule logic
- activity record generation

### Expectations
- fast execution
- no infrastructure dependency by default
- minimal mocking
- readable test names that map to business language

## 5.2 Integration test suite

### Focus
- `AppDbContext` mappings
- SQLite path
- PostgreSQL path or smoke coverage
- manager API contracts
- watch-to-ready correlation
- secret encryption/decryption
- file store persistence
- provider profile persistence
- prompt usage history persistence
- validation result persistence
- audit trail persistence

### Database strategy
Use:
- SQLite in-memory or temporary file database for most relational integration tests
- PostgreSQL integration path in controlled test runs where needed
- EF Core in-memory provider only for limited non-relational behavior tests when appropriate

## 5.3 Component test suite

### Focus
- project creation wizard
- stack profile editor
- tab strip and sleeping-state UI
- tuning overlay and request panel
- resource editor forms
- prompt editor
- wizard step navigation
- validation result display
- test evidence cards
- shell and right-drawer interactions

### Expectations
- test form validation behavior
- test conditional rendering
- test event callbacks and save commands
- test loading/error states

## 5.4 End-to-end test suite

### Minimum e2e scenarios
1. first-run launch and dashboard render
2. wait for the manager to emit a trustworthy ready signal
3. create project
4. add project options and notes
5. restore the internal workbench after refresh or reconnect
6. open and reorder internal tabs
7. use the project structure canvas to open a linked artifact
8. use the project calendar to open a linked artifact
9. add repository resource
10. add SSH or FTP profile with secret reference
11. create prompt draft
12. run prompt factory and save prompt
13. record prompt usage
14. run a validation workflow
15. attach test evidence
16. submit a dev-only tuning request using a fake or controlled Codex adapter

### Secondary e2e scenarios
- provider health check
- prompt export
- prompt collection management
- search and filter flows
- background job visibility
- settings persistence after restart

## 6. Playwright strategy

## 6.1 Playwright use cases
Playwright should be used for:
- end-to-end regression flows
- visual/screenshot evidence capture
- browser interaction verification
- accessibility smoke checks
- workflow coverage across the shell

## 6.2 Playwright test organization
Recommended categories:
- `smoke/`
- `manager/`
- `workbench/`
- `projects/`
- `resources/`
- `prompts/`
- `factory/`
- `validation/`
- `testlab/`
- `settings/`

## 6.3 Playwright evidence
Capture and store:
- screenshots on failure
- traces for major flows
- test summaries linked to project or milestone

## 6.4 Playwright + agent/MCP usage
Playwright’s AI/agent capabilities can accelerate:
- initial test-plan generation
- early test file generation
- selective healing of brittle tests
- exploratory coverage planning

However:
- generated tests must be human-reviewed
- healed tests must not silently replace product understanding
- checked-in suites remain the source of truth

## 7. Component/system validation matrix

| Area | Deterministic Rules | Human Review | Optional AI Review | Automated Tests |
|---|---|---|---|---|
| User stories | yes | yes | yes | limited |
| Use cases | yes | yes | yes | limited |
| Layouts | yes | yes | yes | limited |
| Architecture docs | yes | yes | yes | limited |
| Prompt blueprints | yes | yes | yes | yes |
| Project forms | yes | yes | low | yes |
| Secret handling | yes | yes | no | yes |
| Provider settings | yes | yes | low | yes |
| Prompt generation flows | yes | yes | yes | yes |
| Validation center | yes | yes | yes | yes |
| Test lab | yes | yes | low | yes |
| Development manager | yes | yes | low | yes |
| Capsule coverage | yes | yes | low | yes |

## 8. Test data strategy

### 8.1 Seed data types
Create reusable fixtures for:
- workspace settings
- provider profiles
- project stack profiles
- resource records by type
- prompt blueprints
- prompt versions
- validation runs
- evidence artifacts

### 8.2 Data builders
Prefer builder patterns or fixture factories for:
- projects with phases
- prompts with versions
- resources with secret references
- validation runs with findings
- test plans with evidence

This keeps tests readable.

## 9. Environments

## 9.1 Local developer environment
Used for:
- unit tests
- component tests
- fast integration tests
- Playwright local workflow tests

## 9.2 CI environment
Used for:
- build verification
- automated unit/integration/component tests
- Playwright regression pack
- artifact publishing

## 9.3 Manual review environment
Used for:
- human review of layouts
- prompt quality review
- executive release checks
- screenshot evidence inspection

## 10. Quality gates by milestone

## Gate after M0A
- manager watch-state tests passing
- runtime readiness confirmation passing
- capsule generation and drift tests passing
- tuning request lifecycle smoke passing

## Gate after M1
- persistence tests passing
- secret safety tests passing
- migration creation verified

## Gate after M3
- project creation e2e passing
- option persistence integration tests passing

## Gate after M4
- resource add/edit e2e passing
- secret reference scenarios passing

## Gate after M4A
- tab restore and sleep/wake tests passing
- project structure canvas wrapper contracts passing
- project calendar wrapper contracts passing

## Gate after M6
- prompt factory e2e passing
- prompt version and build-session tests passing
- send/export validation tests passing

## Gate after M7
- validation run persistence passing
- findings rendering/component tests passing

## Gate after M8
- test lab e2e passing
- screenshot evidence persistence passing

## Gate after M9
- regression pack green
- release checklist reviewed
- no known critical secret leaks
- no blocking usability issues in primary flows

## 11. Negative test strategy

The system must be tested not only for success but also for failure modes.

### 11.1 Examples
- invalid provider profile
- invalid SSH/FTP settings
- missing secret reference
- unsupported resource preview
- watch process exit before ready
- runtime readiness probe timeout
- malformed or missing capsule on a touched type
- tuning request that targets an invalid capsule key
- prompt factory missing required context
- export/send attempt with sensitive content warning
- failed background job
- missing storage root
- database unavailable
- validation run with incomplete checklist
- concurrent UI actions trying to save the same form repeatedly

## 12. Security and safety test plan

### 12.1 Secret safety tests
- encrypted secret storage round-trip
- no plain text secret persistence
- redacted UI display by default
- redacted logs
- secret references used by resources and providers correctly

### 12.2 Approval gate tests
- dangerous actions blocked without approval
- approval display includes action preview
- rejected approvals leave no partial execution state

### 12.3 Provider send/export tests
- warning shown when sensitive content is included
- export path excludes redacted fields when expected
- request metadata persists safely without raw secret logging

## 13. Observability validation

Verify:
- health checks exist and are meaningful
- background job failures are visible
- activity timeline records major actions
- warnings and errors are understandable
- logs help debugging without oversharing
- manager watch history helps diagnose false-ready or failed-ready transitions
- capsule drift output helps identify stale source descriptions

## 14. Manual review pack

For milestone reviews, prepare:
- demo script
- screenshot pack
- manager ready-signal evidence
- capsule coverage or drift report
- known issue list
- coverage matrix delta
- quality gate status
- open risk log

## 15. Release candidate validation checklist

Before calling the build a release candidate:
- all primary e2e flows pass
- no critical security defect is open
- provider profiles work for supported scenarios
- prompt factory is usable end-to-end
- validation center is usable end-to-end
- test lab is usable end-to-end
- activity and search are acceptable
- startup and first-run experience are acceptable

## 16. Final test strategy conclusion

The testing system for PromptStudio should combine:
- unit tests for domain trustworthiness
- integration tests for persistence and adapters
- component tests for Blazor features
- Playwright e2e tests for workflow confidence
- screenshot and evidence tracking for reviewability
- manual expert review for architecture and prompt quality

This mixed approach matches the product’s nature: part software platform, part structured content system, part secure integrations workbench.
