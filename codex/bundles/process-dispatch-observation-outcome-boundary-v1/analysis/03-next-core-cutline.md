# Not Core Yet Cutline

This bundle intentionally stops before Process Core.

A future Process Core split becomes safer when these are true:

- observation snapshots are stable and do not expose raw MAF session JSON;
- declared outcome parsing is module-local and covered by parity tests;
- completion status/reason decisions use explicit input snapshots;
- retry/no-progress decisions consume observation facts rather than parsing detail/session state directly;
- all remaining side-effectful coordinators are easy to identify;
- driver-readiness maps describe evidence families without production API.

Until then, continue extracting module-local helpers and wrappers inside `CanDoItAll.Modules.Processes`.
