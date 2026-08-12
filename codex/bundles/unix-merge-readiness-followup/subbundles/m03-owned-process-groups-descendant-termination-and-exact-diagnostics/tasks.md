# Tasks

- [ ] Introduce an OS-level owned-tree boundary at start: Windows Job Object or equivalent; Unix process group/session or equivalently proven design.
- [ ] Route graceful, force, timeout, dispose, Manager recovery, Workbench close, and MCP close through the same ownership boundary.
- [ ] On Unix, signal the owned group and verify no owned member survives even if root exits first.
- [ ] Preserve exact executable path/fingerprint/start identity and PID-reuse protection.
- [ ] Make identity mismatch diagnostics resilient to process exit, access denial, and `MainModule` races.
- [ ] Define and test cancellation semantics during cleanup without abandoning an owned tree.
- [ ] Do not add a second process-start implementation.
