# Implementation order

1. Repair the bundle package so prepared-stage validation can pass.
2. Implement `P14-001` and `P14-002` together because trigger retirement and returned trigger snapshots share the same canonical trigger persistence surface.
3. Implement `P14-003` and `P14-004` together because ingress cursor normalization and materialization concurrency both sit in `PluginIngressInbox`.
4. Implement `P14-005` after the ingress work so the remaining single-executor gap is isolated to the connector surface.
5. Add the required integration tests before final closure and keep the execution report synchronized with actual proof.
