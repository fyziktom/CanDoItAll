# Implementation Agent Prompt

You are implementing `process-driver-domain-gateway-adapters-stabilization-v1`.

Do not trust the previous execution report alone. Start by reading live source files and current tests.
Work phase by phase. Every critical gate must pass before downstream work starts.

Hard non-goals:
- no generic driver runtime host;
- no registry/selector/DI/manager command;
- no scheduler/workflow hook;
- no shell/Graph/file/network/workspace/storage/process mutation;
- no broad Process Core runtime extraction;
- no UI/media drift.

Prefer larger coherent changes within each phase, but never bypass gates.
