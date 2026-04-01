# Runtime Switch Sequence

## Sequence Diagram

```mermaid
sequenceDiagram
    autonumber
    participant User
    participant UI as MainLayout / Settings UI
    participant Switch as DatabaseSwitchCoordinator
    participant Runtime as ActiveDatabaseRuntimeState
    participant Driver as Target Database Driver
    participant Catalog as ActiveProfileStore
    participant Notify as Runtime Change Notifier
    participant Browser as Browser Tabs / JS Bridge

    User->>UI: Select target database profile
    UI->>Switch: SwitchAsync(targetProfileId)
    Switch->>Runtime: Acquire switch lock and block new context leases
    Runtime-->>Switch: Wait until active lease count is zero (or timeout/fail)
    Switch->>Driver: Validate target profile and ensure database/schema ready
    Driver-->>Switch: Ready or failure
    alt ready
        Switch->>Catalog: Persist active profile + last used metadata
        Switch->>Runtime: Increment active generation and unblock new leases
        Switch->>Notify: Publish DatabaseProfileChanged(old,new,generation)
        Notify->>UI: In-circuit event
        Notify->>Browser: Broadcast profile/generation via storage or BroadcastChannel
        UI->>Browser: Navigate current tab to safe route with forceLoad
        Browser->>Browser: Other tabs detect generation change and force reload
    else failure
        Switch->>Runtime: Release switch lock and restore previous active runtime state
        Switch-->>UI: Error with actionable message
    end
```

## Contract Notes

- The switch coordinator must treat timeout while draining active operations as a **real failure**, not as silent force-switch success.
- The current tab and other open tabs must both respond to the published generation change.
- The safe route should be a page that is always valid under any profile, for example `/` or `/settings?tab=data-sources`.
- Stale artifact pages must still render a not-found/recover state if reached directly after a switch or refresh.
