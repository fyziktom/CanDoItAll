# 04 Rework Packets and QA Return Flow

## Goal

QA/build/test failures should produce targeted rework packets that guide agents to complete existing work rather than rerun everything.

## Tasks

1. Add `AgentReworkPacket` and supporting DTOs.
2. Generate packets from QA rejection, failed proof, missing artifact, and manual operator rework.
3. Include findings, artifact refs, failed receipts, reusable proof refs, proofs to rerun, minimal next actions, prohibited actions, and optional human directive.
4. Update prompts so rework agents inspect existing artifacts and apply minimal delta changes.
5. Add a UI operator form for manual rework packet creation.
6. Tests: QA rejection creates packet; repair step consumes packet; completion requires addressed findings and proof rerun.

## Acceptance criteria

- QA return does not become a blind retry.
- Agents receive structured repair context and restrictions.
