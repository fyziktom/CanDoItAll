# SB04 Semantic Invariants

## Shallow-Pass Trap

A process run with green build/test artifacts but failed final writeback or a non-interactive app must fail closure.

## Adversarial Negative Proof

Treat the observed run `0cca729a-e9bc-47e7-89aa-bef9b88dbf1c` as the negative example: partial steps completed, app rendered, but process failed and app was not interactive/static-correct.

## Semantic Positive Proof

A fresh final run reaches terminal success, writes final project-structure evidence, and the delivered app passes static/no-backend gameplay proof.

## Anti-Stub Audit

Verify final output and repo changes have no placeholder/template-only app content or fake proof markers.

## Raw Note Literal Closure

- Closes N001-N007 only after API closure, project-structure closure, and final app proof all pass.

## Production Behavior Artifact Matrix

| Artifact/Signal | Producer | Consumer | Lifecycle | Negative-Test Citation |
| --- | --- | --- | --- | --- |
| Final process run detail | Processes API | Final closure gate | Created during rerun, persisted in evidence | Pending |
| Final project-structure verdict node | Writeback step | Project graph/user | Created under `Main app`, verified by read API | Pending |
| Final app gameplay proof | Browser validator | Final verifier | Captured after launch/static host | Pending |
