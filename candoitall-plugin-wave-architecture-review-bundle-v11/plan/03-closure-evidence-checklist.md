# Closure evidence checklist

Phase11 is only complete when all of the following exist:

- canonical trigger record/model and registry API
- Quartz bridge that projects canonical triggers into runtime jobs/triggers
- durable internal envelope record/model
- subscription registry and handler dispatch path
- hosted workers registered in startup
- connector outbox drain worker
- background job drain worker
- trigger drain / dispatch worker
- plugin ingress envelope and cursor records
- explicit materialization path from ingress envelope to domain artifacts
- composite signal provider or equivalent multi-source signal aggregation
- execution attempt / telemetry / dead-letter records
- optional MQTT bridge that can be disabled without breaking core behavior
- all required tests and a passing phase11 gate
