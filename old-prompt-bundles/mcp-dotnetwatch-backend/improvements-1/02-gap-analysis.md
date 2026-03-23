# Gap Analysis

## Current state
1. Each workspace backend writes only a workspace-local registration file.
2. The manager UI reads only the current backend status snapshot.
3. The manager UI is read-only.
4. `dotnet watch` already runs with:
   - `--non-interactive`
   - `DOTNET_WATCH_RESTART_ON_RUDE_EDIT=1`
5. App and operation log APIs currently return raw log-buffer output with only source-type filtering.

## Confirmed gaps
1. There is no machine-wide catalog of live backends, so one backend manager cannot see another backend.
2. There is no manager command channel for remote session control.
3. Agent-facing logs include:
   - compiler warning floods
   - restore chatter
   - repetitive framework request traces
   - blank lines and low-value success noise
4. There is no quantitative measurement of raw versus agent-optimized log volume.

## Non-obvious needs
1. Stale entries in a machine-wide backend catalog must be pruned safely.
2. Cross-backend manager actions must proxy with the correct auth token per backend.
3. Rebuild actions must favor watch-native restart behavior before falling back to stop/start.
4. Log reduction must not hide:
   - exceptions
   - `fail:` lines
   - `error` lines
   - watch lifecycle transitions
   - health/liveness evidence
   - final build and test outcomes
