# Structured Input

## Goal

- Add a Scheduler/Planner module that plans automatic workflow and process runs.
- Provide an operator page with tabs for active scheduled runs, new schedule setup, and run-history search.
- Use CRON scheduling with human-readable descriptions.

## Constraints

- Quartz is the triggering engine.
- Quartz must use a database-backed persistent store or equivalent Quartz-supported DB recovery path; current in-memory scheduling is not enough for this request.
- Scheduled target execution must be durable and typed. Do not embed workflow/process business logic inside the Quartz job.
- UI must follow existing Blazor module patterns and BaseLib/Radzen-style wrappers.
- Bundle preparation only. Product code implementation is out of scope for this request.

## Non-Goals

- Do not replace Quartz with Hangfire, a custom poller, or a second scheduler.
- Do not create a broad automation redesign.
- Do not add marketing/landing-page UI.
- Do not hand-roll a CRON parser/description engine when a maintained package or isolated adapter is available.

## Primary Decision

- Build a thin `SchedulerPlanner` product module over the existing `Automation` runtime. The existing Automation module remains infrastructure: trigger persistence, Quartz projection, durable envelopes, dispatcher, and operational diagnostics. SchedulerPlanner owns schedule definitions, typed workflow/process targets, schedule fire history, launch adapters, and the operator page.
