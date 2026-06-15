# Role Candidate Readiness Validation

## Purpose

This validation plan proves that Process launch candidate selection is no longer score-only. Future implementation must show that every selected role candidate has a deterministic readiness assessment and that missing tools or rights are clearly surfaced, provisioned, approved, or blocked.

## Required Automated Tests

| Test area | Required proof |
| --- | --- |
| Requirement compilation | Role execution requirement set includes skills, tools, rights, access scopes, approvals, and artifact/operation-derived constraints. |
| HR recommendation boundary | HR-proposed high-score candidate is re-evaluated by deterministic readiness evaluator. |
| Missing tool blocker | Candidate missing a required tool receives `MissingRequiredTool`, blocks execution, and shows suggested provisioning. |
| Missing right blocker | Candidate missing a required right receives `MissingRequiredRight`, blocks execution, and shows a user-safe summary. |
| Missing optional capability | Optional missing capability is warning-level and does not block execution. |
| Provisioning plan | Missing provisionable items create itemized provisioning tasks linked to findings. |
| Reassessment | Provisioning completion does not clear blockers until readiness reassessment proves evidence changed. |
| Approval plan | Findings requiring approval create approval tasks and block until decision is recorded. |
| Override policy | Override requires explicit policy allowance and decision record; safety-critical blockers cannot be silently overridden. |
| Runtime assignment | Runtime assignment snapshot contains candidate id, requirement hash, readiness assessment hash, and unresolved warning list. |
| Redaction | Sensitive missing-right/tool details are hidden from unauthorized users and available only via restricted evidence references. |
| UI projection | Candidate matrix shows score, readiness status, missing tools, missing rights, blockers, warnings, provisioning, approvals, and resolution actions. |

## Required Browser Proof

SB21 must capture Playwright proof for:

- candidate matrix showing at least one ready candidate,
- candidate matrix showing one candidate with missing required tool,
- candidate matrix showing one candidate with missing required right,
- selected candidate readiness summary on the role card,
- launch approval/execution blocked by unresolved readiness blocker,
- provisioning or approval action visible for a resolvable missing item.

Screenshots must be stored in the SB21 proof directory during implementation.

## Negative Cases

- A candidate with the highest score but missing required tool must not be auto-selected as executable.
- A selected candidate with missing required right must not allow execution.
- A text-only `ReadinessSummary` without typed findings is insufficient.
- HR agent output alone must not create `Ready` status.
- A provisioning task status change without reassessment must not clear the finding.
- UI must not hide blockers behind a generic "Provisioning" badge.

## Required Execution Report Fields

SB21 execution report must include:

| Field | Required content |
| --- | --- |
| Candidate requirement model | New or modified model/source files. |
| Readiness evaluator | Source proof and tests for tool/right/capability findings. |
| HR boundary | Proof that HR recommendations are advisory. |
| UI proof | Screenshot paths and Playwright assertions. |
| Security proof | Redaction and restricted-detail behavior. |
| Runtime handoff | Proof that readiness assessment hash reaches runtime assignment. |

## Stop Conditions

Stop implementation if:

- required tools or rights cannot be represented as typed requirements,
- current HR candidate score must be trusted without deterministic evidence,
- UI can only show a generic readiness string instead of itemized findings,
- launch execution cannot be blocked by unresolved required tool/right findings,
- sensitive right/tool details cannot be safely redacted.
