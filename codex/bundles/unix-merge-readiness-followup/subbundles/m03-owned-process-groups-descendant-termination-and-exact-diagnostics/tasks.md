# Tasks

- [x] Introduce an OS-level owned-tree boundary at start: Windows Job Object or equivalent; Unix process group/session or equivalently proven design.
- [x] Route graceful, force, timeout, dispose, Manager recovery, Workbench close, and MCP close through the same ownership boundary.
- [x] On Unix, signal the owned group and verify no owned member survives even if root exits first.
- [x] Preserve exact executable path/fingerprint/start identity and PID-reuse protection.
- [x] Make identity mismatch diagnostics resilient to process exit, access denial, and `MainModule` races.
- [x] Define and test cancellation semantics during cleanup without abandoning an owned tree.
- [x] Do not add a second process-start implementation.
