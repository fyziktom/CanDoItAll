# Initial scheduling of canonical tasks

JSON outline imports create canonical tasks without persisted dates. Such a task
cannot use the existing schedule-move contract: its previous interval does not exist.
`PUT /api/project-structure/projects/{projectId}/tasks/{taskId}` now accepts an optional
`initialSchedule`, also exposed on the internal task-update tool request.

Read the canonical structure with metadata first. Keep the usual title, progress,
estimate, execution, cost basis, assignment revision and `expectedProjectAdmission`
preconditions. Set `scheduleChange` to null and supply:

```json
{
  "initialSchedule": {
    "currentStartUtc": null,
    "currentEndUtc": null,
    "currentDurationSeconds": null,
    "proposedStartUtc": "2027-02-05T09:00:00Z",
    "proposedEndUtc": "2027-02-05T10:00:00Z"
  }
}
```

All five inner members must be present. Echo the current nullable dates and duration
from the node; the example's null duration is correct only when the read returned null.
The current start and end must both be null and the proposed end must follow its start.
For a task with persisted dates, keep using `scheduleChange` with its exact previous
interval. Sending both modes, an inverted interval or a snapshot of an already
scheduled task is HTTP 400. A schedule changed after the read is HTTP 409 `StaleTask`.
Missing/wrong project lifetime admission retains its existing HTTP 409 behavior.

The existing task application transaction owns assignment, pricing, execution and
date persistence. A small schedule value/policy validates the new request and compares
the persisted snapshot; Workbench's Gantt mutation owner checks the same dependency
graph and interval constraints as schedule moves. Dates and task details commit together.
The HTTP adapter carries the optional value through existing request boundaries; it
does not invent a Gantt projection or bypass canonical task mutations.

No project references or dependency directions change. The independent policy tests
cover stale dates/duration and malformed input; raw JSON integration tests import a
real outline, initialize/read back its task, then verify stale, invalid and dependency
rejections without partial title changes. Existing task-update tests cover compatibility.

Verified on 7 October 2026: 19 expected/discovered unit cases and 23 expected/discovered
raw HTTP cases passed, zero skipped, using isolated PostgreSQL 18.6 on loopback port
5544. Workbench and Web production builds passed. The complete source portability scan
included new files and final enforcement passed with all 15,312 reviewed findings
unchanged; the baseline was not rewritten. The six enforcement-tool and four secret
scanner tests passed. An isolated WindowsHeadless host emitted both identical OpenAPI
routes with all five required initial-schedule members and their source-owned
`Description` attributes. No XML documentation was introduced.
