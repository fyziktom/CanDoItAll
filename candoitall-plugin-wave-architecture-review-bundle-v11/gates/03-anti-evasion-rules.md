# Anti-evasion rules

1. Do not “close” phase11 by only adding UI pages or editor models.
2. Do not add Quartz jobs that directly execute plugin logic without going through the durable message plane.
3. Do not satisfy the background worker requirement with `Task.Run(...)`, timers, or request-path kickers.
4. Do not satisfy the internal messaging requirement with in-memory events only.
5. Do not satisfy the signal aggregation requirement by keeping singular `IAutomationSignalProvider` injection.
6. Do not make MQTT mandatory for core scheduling, dispatch, or retries.
7. Do not auto-materialize ingress messages into Workbench nodes on arrival.
8. Do not hide unfinished runtime work behind “tracked background jobs” that still run inline.
9. If alternative type names are chosen, the phase11 gate must be updated in the same change so the new structure remains machine-verifiable.
