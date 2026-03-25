# Watch Loop Observations

## What was tested

- Active watch editing on `ProjectsPage.razor`
- `candoitall_app_wait(condition="RevisionConfirmed")`
- Browser validation after each nearby change
- Fresh watch restart after divergence

## Timings observed

- Hot-reload cycle 1: about `32195 ms`
- Hot-reload cycle 2: about `11708 ms`
- Hot-reload cycle 3: about `19138 ms`

## Confirmed failure mode

The watch lane twice reported success while the browser still showed stale markup.

Observed evidence:

- `RevisionConfirmed` returned `satisfied=true`
- watch summary reported `[CanDoItAll.Web (net10.0)] Hot reload succeeded.`
- app health still stayed `Pending`
- a fresh browser load still showed the old command bar and empty quick-action text

Captured proof:

- `artifacts/watch-divergence-log.ndjson`

Logged example:

- session: `app_46fbc414d00c4ba5abf3951424a82714`
- wait elapsed: `19138 ms`
- browser still showed old text such as `New`, `View`, and blank quick-action pills instead of `+ New`, `VIEW View`, `DB`, `ST`, and `CAL`

## Recovery behavior

- Stopping the stale watch session and starting a fresh watch session immediately fixed the stale-DOM issue.
- Fresh watch session: `app_8b54d37c11b54c3184ac7db99997a083`
- After restart, the browser showed the updated compact labels correctly.

## What this means in practice

The server guidance to "wait RevisionConfirmed, then browser-check" is correct. The browser check is not optional. On this workload, `RevisionConfirmed` plus `Hot reload succeeded` was not enough to trust that the changed Razor surface actually reached the browser.
