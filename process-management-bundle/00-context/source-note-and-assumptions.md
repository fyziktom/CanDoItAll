# Source note and assumptions

This bundle revision was checked against the uploaded:

- `CanDoItAll-development.zip`
- `CanDoItAll.AgentFramework-main.zip`
- `candoitall-process-management-execution-grade-bundle-final-pass2.zip`

Important scope assumptions:

- The public Azure sample `interview-coach-agent-framework` remains a pattern reference only; the implementation baseline in this bundle is the uploaded CanDoItAll repo plus the uploaded AgentFramework overlay repo.
- The CanDoItAll repo already provides strong seams in `Projects`, `CRM-HR`, `Workspace`, `CanvasLib`, `Activity`, `Automation`, `Validation`, `TestLab`, and `Security`.
- The process-management module should ship **before** the intelligence lake and before any hard dependency on the AgentFramework overlay.
- This revision keeps the earlier operating-model additions and further hardens the bundle around **process-native orchestration**, **baton/work brief handoffs**, **runtime overlay visibility**, and **cross-repo convergence rules**.
