# Open-source intake and supply-chain governance

**Key:** `oss-intake-supply-chain-governance`  
**Criticality:** High  
**Autonomy level:** Guarded  
**Operating mode:** AssistedExecution  
**Customer name:** Engineering, legal, and security stakeholders  
**Owner name:** Open-source governance office

## Summary
Assess new third-party components through identity, license, provenance, security, SBOM, and approval steps so reuse remains deliberate and auditable.

## Value statement
Make open-source adoption safe, fast, and reusable by standardizing component intake, license obligation review, provenance checks, and durable approval records.

## Interface contract summary
A requested component or package becomes an approved or rejected intake decision with explicit obligations, provenance evidence, and SBOM updates.

## Governance notes
Do not confuse convenient package acquisition with approved component adoption. Identity, obligations, provenance, and update ownership must be explicit.

## Architecture and constitution rules
- Governance policy: No component may be adopted without verified identity, license review, security/provenance assessment, SBOM registration, and accountable ongoing ownership.
- Constitution rule: A component can be popular and still be unsuitable; approval requires explicit reasoning, not reputation transfer.

## Operating and simulation notes
- Operating mode summary: Assisted execution with human legal/security approval. Automation may collect evidence and draft analysis, but approval remains accountable.
- Simulation readiness: Works for procurement-style intake, dev-team self-service review, and AI-assisted package research pipelines.

## Source frameworks
- openchain
- spdx
- slsa
- nist-ssdf

## Process metrics
- Average component intake cycle time
- Percentage of adopted components with current SBOM entry
- Number of approvals with unresolved provenance gaps
- Time to publish obligation guidance to consuming teams

## Process risks
- Component identity is wrong or incomplete.
- License obligations are misunderstood or not passed downstream.
- Provenance or build integrity is weak relative to sensitivity of use.
- SBOM updates lag behind real adoption.

## Tailoring rules
- For internal low-risk tooling, security review may be lighter but SBOM and ownership still remain required.
- For runtime production dependencies, provenance and maintenance posture must be stricter.
- For AI/ML model packages and datasets, add the AI evaluation and dataset handling sidecars.

## Role usages
- `software-engineer` / **Software engineer** — Produce working change artifacts that satisfy the process contract, surface blockers quickly, and leave enough proof for review and reuse.
- `service-owner` / **Service owner** — Represent live-service constraints, operational history, and post-release accountability in change decisions.
- `sbom-curator` / **SBOM curator** — Keep the software bill of materials and dependency evidence accurate enough for governance, response, and later reuse.
- `license-counsel` / **License counsel** — Ensure component use decisions reflect actual license obligations and approved exception logic.
- `compliance-steward` / **Compliance steward** — Ensure required compliance considerations are identified early, translated into actionable checks, and retained as evidence.
- `security-reviewer` / **Security reviewer** — Ensure changes touching trust boundaries, sensitive data, dependencies, or operational attack surface are reviewed proportionally and documented defensibly.

## Steps
### 1. Register component intake request (`component-request`)
- Step kind: Start
- Depends on: None
- Inputs: Package name or component identifier, intended use, business context, and requesting team.
- Outputs: Typed component intake request with accountable owner.
- Evidence: Intake brief and ownership statement.
- Decision rights: Software engineer requests; service owner confirms operational ownership exists.
- Exception policy: Reject anonymous or ownerless component requests.
- Artifact expectations:
  - `component-request-intake-brief` => `intake-brief` / Component intake request
- Checklists: oss-intake-checklist, component-identity-checklist
- Validations: validation-sbom-ready
- Prompts: prompt-oss-evaluation

### 2. Verify component identity and maintenance posture (`identity-and-maintenance`)
- Step kind: Review
- Depends on: component-request
- Inputs: Component request, repository links, package registry metadata, and maintainer signals.
- Outputs: Verified identity record and maintenance posture summary.
- Evidence: Identity note, upstream mapping, and maintenance-health summary.
- Decision rights: SBOM curator verifies identity; software engineer helps interpret practical adoption implications.
- Exception policy: Do not continue if the requested package cannot be reliably tied to an upstream project and maintenance history.
- Artifact expectations:
  - `identity-and-maintenance-sbom-manifest` => `sbom-manifest` / Component identity registration draft
  - `identity-and-maintenance-provenance-report` => `provenance-report` / Upstream identity and maintenance summary
- Checklists: component-identity-checklist, oss-intake-checklist
- Validations: validation-sbom-ready

### 3. Analyze license obligations and redistribution constraints (`license-and-obligations`)
- Step kind: Review
- Depends on: identity-and-maintenance
- Inputs: Verified identity, license texts, notice files, policy guidance, and intended distribution model.
- Outputs: Obligation matrix and legal position for adoption.
- Evidence: License-obligation matrix and unresolved legal questions.
- Decision rights: License counsel owns legal interpretation; compliance steward reviews policy fit.
- Exception policy: No approval if obligations cannot be explained to downstream teams.
- Artifact expectations:
  - `license-and-obligations-license-obligation-matrix` => `license-obligation-matrix` / License obligation matrix
  - `license-and-obligations-sbom-manifest` => `sbom-manifest` / Component license metadata entry
- Checklists: oss-intake-checklist
- Validations: validate-license-obligation-coverage
- Prompts: prompt-component-approval-note

### 4. Review security posture and provenance trust (`security-and-provenance`)
- Step kind: Review
- Depends on: license-and-obligations
- Inputs: Identity record, license position, vulnerability data, provenance statements, and deployment context.
- Outputs: Supply-chain risk position with compensating controls or rejection rationale.
- Evidence: Security/provenance review note and risk position.
- Decision rights: Security reviewer may reject a component even if the license is acceptable.
- Exception policy: Do not approve runtime-critical components on weak provenance without explicit compensating controls.
- Artifact expectations:
  - `security-and-provenance-security-review-note` => `security-review-note` / Security review note
  - `security-and-provenance-provenance-report` => `provenance-report` / Provenance report
- Checklists: security-review-checklist, oss-intake-checklist
- Validations: validation-security-clear
- Prompts: prompt-oss-evaluation

### 5. Approve, reject, or constrain adoption (`approval-and-registration`)
- Step kind: Approval
- Depends on: security-and-provenance
- Inputs: License position, security/provenance review, and intended use context.
- Outputs: Approval decision with obligations, owner, and review cadence.
- Evidence: Approval record and final SBOM registration.
- Decision rights: Compliance steward approves policy fit; service owner accepts operational ownership.
- Exception policy: No approval without named ongoing owner and explicit version boundary.
- Branch outcomes: approved (Approved), rejected (Rejected)
- Artifact expectations:
  - `approval-and-registration-sbom-manifest` => `sbom-manifest` / SBOM manifest
  - `approval-and-registration-release-readiness-report` => `release-readiness-report` / Component approval note
- Checklists: oss-intake-checklist
- Validations: validation-sbom-ready, validate-license-obligation-coverage
- Prompts: prompt-component-approval-note

### 6. Hand off obligations and establish re-review triggers (`handoff-and-watch`)
- Step kind: End
- Depends on: approval-and-registration
- Inputs: Approval record, SBOM entry, obligations matrix, and owner contacts.
- Outputs: Downstream handoff package and re-review trigger list.
- Evidence: Obligation handoff note and re-review log.
- Decision rights: SBOM curator owns metadata dissemination; service owner owns runtime review cadence.
- Exception policy: If obligations cannot be passed to consuming teams, the adoption is not operationally complete.
- Artifact expectations:
  - `handoff-and-watch-license-obligation-matrix` => `license-obligation-matrix` / Published obligation handoff
  - `handoff-and-watch-retrospective-improvement-log` => `retrospective-improvement-log` / Component re-review trigger log
- Checklists: oss-intake-checklist
- Validations: validation-sbom-ready

