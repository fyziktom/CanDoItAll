# Gate D Runtime Smoke And Line Count Review Source Assertions

- Invariant ID: SB16-INV-001
- Source proof: changed source files are listed in `proof/SB16/manifest.md` with SHA-256 hashes.
- Assertion: ProcessDispatchTechnicalAgentBindingCoordinator keeps SaveAgentAsync and project-structure read access mutation explicit and outside the hydration loader.
- Assertion: ProcessDispatchRecoveryQueryHelper owns manual recovery directive and recoverable execution query access.
- Runtime proof: the processes module build transcript records a clean build.
