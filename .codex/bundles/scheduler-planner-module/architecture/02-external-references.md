# External References

These references were consulted during bundle preparation to avoid stale assumptions about Quartz persistence and CRON description packages.

## Quartz.NET

- Quartz.NET Job Stores: `https://www.quartz-scheduler.net/documentation/quartz-3.x/tutorial/job-stores.html`
  - Relevant point: `RAMJobStore` loses scheduling information when the process terminates; ADO.NET JobStore stores scheduler data in a database and requires Quartz tables.
  - Relevant point: Quartz recommends string job data properties and JSON serialization for persistent stores.
- Quartz.NET Configuration Reference: `https://www.quartz-scheduler.net/documentation/quartz-3.x/configuration/reference.html`
  - Relevant point: `JobStoreTX` is the normal ADO.NET persistent store and requires provider/delegate/table prefix/data source configuration.

## CRON Description

- NuGet `CronExpressionDescriptor`: `https://www.nuget.org/packages/CronExpressionDescriptor/`
  - Relevant point: current package page lists version `2.45.0`, MIT license, no dependencies for `net6.0`, and describes CRON expressions in multiple languages.
  - Implementation must still verify Quartz-style seconds/year expression behavior before committing to package use.
