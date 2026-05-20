# Professor Learning State Machine

```mermaid
stateDiagram-v2
    [*] --> Active: trusted professor anchor captured
    Active --> Comparing: related memory/cluster located
    Comparing --> Applied: operational memory/correction applied when safe
    Comparing --> ReviewRequired: target ambiguous or conflict unresolved
    Applied --> Assimilated: distinct derived memory/aggregate/use proof exists
    Comparing --> Assimilated: independent cluster already internalized anchor
    Assimilated --> Faded: raw anchor no longer critical but lineage retained
    Active --> Rejected: professor retracts or higher-trust evidence invalidates
    Comparing --> Rejected: comparison proves anchor wrong/out of scope
```

Assimilation cannot use the same memory record created directly from the anchor as its own proof. It must use an independent derived memory record, aggregate record, cluster assimilation observation, or repeated correct use observation that references but is not identical to the professor anchor.
