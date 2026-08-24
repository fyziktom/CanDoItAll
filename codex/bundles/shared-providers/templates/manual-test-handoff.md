# Shared providers manual test handoff

Generated:  
Source commit/image tag:

## Running services

| Service | URL | Health | Purpose |
| --- | --- | --- | --- |
| central | | | |
| client-a | | | |
| client-b | | | |
| PostgreSQL | internal | | |
| deterministic-upstream | internal | | |

## Ephemeral credential locations

List local ignored file paths only. Do not paste values.

## Seeded fixtures

## Suggested manual scenarios

1. Open central and inspect published versus unshared profiles.
2. Open client-a and verify personal plus shared provider.
3. Run non-streaming and streaming text.
4. Run function-tool and structured-output scenarios.
5. Open client-b and run image generation.
6. Unpublish centrally, sync clients, inspect unavailable state.
7. Re-publish and confirm stable local profile IDs/recovery.
8. Stop/restart central and verify explicit outage/no fallback.
9. Inspect invocation usage/audit metadata.

## Logs and artifacts

## Cleanup

Command, deliberately not executed:

```text
<cleanup command>
```
