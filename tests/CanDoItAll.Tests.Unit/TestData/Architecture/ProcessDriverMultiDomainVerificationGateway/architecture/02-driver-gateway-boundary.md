# Verification Gateway Boundary

## Intent
Create a controlled gateway for already-approved read-only verification lanes. The gateway is not a runtime host. It has no discovery, registry, selector, DI registration, manager command, scheduler hook, workflow hook, or external tool execution.

## Allowed Lanes
- DotNetRustTranscriptVerification
- RuntimeEvidenceConsistency
- ArtifactEvidenceConsistency
- OfficeEvidenceRead
- BusinessAnalysisRead

## Input Rule
All lanes receive supplied immutable payloads. No lane may resolve arbitrary paths, call external systems, or read workspace/storage.

## Output Rule
Every lane returns:
- Accepted / DenialReason
- Diagnostics
- EvidenceReferences
- RedactionDescriptor
- NoMutationPerformed = true
- AuditFacts
- ContractVersion
