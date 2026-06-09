# Target Runtime Restoration

## Process runtime owns
- templates and launch planning;
- run and step persistence;
- dispatch claim and outbox;
- MAF workflow/direct-agent execution;
- step finalization;
- artifact projection/validation;
- manager diagnostics;
- scheduler/workflow-origin start through process services.

## Process Core owns
Pure deterministic read models and rules only.

## Drivers own
Read-only domain verification over supplied facts. Current drivers must not mutate process state or execute external work.

## Target operator flow
User opens project or global process page, selects/imports process template, creates or executes launch plan, sees process run, run advances through dispatch/finalizer, artifacts are visible, and recovery UI explains blocked states.
