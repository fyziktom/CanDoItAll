# Finding to subbundle map

- in-memory queue with no consumer -> `p11-003`, `p11-004`
- connector outbox pending processor not runtime-driven -> `p11-004`
- no hosted worker / no scheduler seam / no Quartz -> `p11-002`, `p11-004`
- singular automation signal provider -> `p11-001`
- no ingress inbox -> `p11-005`
- no delivery telemetry / dead-letter / broker seam -> `p11-006`
