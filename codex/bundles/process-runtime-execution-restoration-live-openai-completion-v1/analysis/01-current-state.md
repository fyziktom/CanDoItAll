# Current State Review

## What is verified so far
The current branch is not at final closure. The latest execution report for `process-runtime-live-e2e-openai-hardening-v1` says `In progress`, with SB001-SB012 completed and SB013-SB048 still pending.

Completed source-backed areas:
- transient bundle-path coupling was removed from long-lived `src` and `tests`;
- full unit no-build rerun passed with 1,134 tests in SB003;
- app startup smoke proves `/health`, `/api/processes/templates`, and process services are registered;
- global `/processes` route can select/import a template, create a launch plan, execute a ready launch, and select a process run;
- `/projects/{projectId}/processes` route keeps project context;
- project-structure node process start API can create and execute a run and preserve project-structure bridge context.

## What is not verified yet
- full run lifecycle beyond launch;
- dispatch/outbox drain path;
- route execution and finalizer transition;
- artifact projection and recovery UI;
- MAF workflow-backed role execution;
- direct-agent execution with current provider configuration;
- deterministic `.NET` create/modify scenario;
- business-analysis scenario;
- guarded live OpenAI smoke;
- scheduler-origin start beyond source assertions;
- workflow-origin start beyond source assertions;
- manager diagnostics/read-only verification projection;
- run detail UI and recovery UI;
- final release-candidate smoke.

## Critical interpretation
The app appears able to start and the UI/API can create process runs. It is not yet proven that process runs execute "like before". The next bundle must continue from SB013 onward and should not restart driver-only hardening.
