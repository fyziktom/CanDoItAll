# Gate C Candidate Assembly Parity Source Assertions

- Invariant ID: SB12-INV-001
- Source proof: changed source files are listed in `proof/SB12/manifest.md` with SHA-256 hashes.
- Assertion: artifact input construction and prompt preparation are owned by ProcessDispatchArtifactInputAssembler while existing service wrappers remain.
- Assertion: branch outcome dependency shaping is owned by ProcessDispatchBranchDependencyContext.
- Assertion: assignment/workflow route recognition is owned by ProcessDispatchAssignmentRouteHelper.
