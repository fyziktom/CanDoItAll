# Generic Process Core Cutline

Core may contain:
- route/stage rules;
- artifact expectation/read-model rules;
- execution/finalizer/retry/projection descriptors;
- subprocess lifecycle pure rules.

Core must not contain:
- `.NET`, Rust, Office, business-analysis, CRM, Graph, provider, MAF, EF, storage, workspace, UI, scheduler or workflow-specific runtime concepts;
- driver abstractions;
- process module runtime services;
- external calls or mutation behavior.
