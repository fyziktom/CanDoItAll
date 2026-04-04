# MCP Finding 004: Participant Assignment Contract Is Misleading For External Authors

## What Happened

- Participant nodes created through the project-structure path exposed `artifactId = null`.
- The work-item assignment metadata field is named `assigneeParticipantArtifactId`, which strongly suggests the caller should send that `artifactId`.
- In reality, the working value had to be the GUID parsed from the `custom:` node id, not a real participant artifact id.

## Why This Matters

- An external MCP client or backfill script can easily write the wrong identifier even while following the current contract names honestly.
- This is exactly the sort of ambiguity that slows down bundle-to-plan automation.

## Evidence

- The B04 AI-assurance lane only assigned correctly after the run patched `assigneeParticipantArtifactId` to the GUID portion of the participant node ids.
- The initial participant nodes did not expose stable artifact ids that matched the assignment field name.

## Recommendation

- Either persist and expose a true participant artifact id that is meant to be assigned, or rename the work-item field to reflect what the system actually expects, such as `assigneeParticipantNodeGuid`.
- Expose the expected identifier directly in the MCP read model so external tooling does not need to reverse-engineer `custom:` node ids.
