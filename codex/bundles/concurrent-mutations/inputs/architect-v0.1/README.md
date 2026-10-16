# CanDoItAll · Concurrent Mutations Architecture
## Version 0.1 · 28 September 2026 · Proposed for review

**Purpose:** make edits by people, agents and automations safe without turning the product into a full real-time collaboration SaaS. This package is an architectural working set, not an implementation bundle, patch or instruction to execute every phase automatically.

The first deliverable is a complete **Projects vertical slice**: consistent edit reads, expected-content checks, safe writes through every enabled entry point, unambiguous outcomes, preserved drafts and a non-regressing search projection. Subsequent work adds explicit intentions, validated decision bases and execution-authority integration where needed. Existing owner, admission, governance and UI-seam guarantees remain in place.

### Read first

1. [Executive recommendation](docs/01-executive-recommendation.md) and [evidence/coverage limits](docs/02-current-state-and-evidence.md).
2. [Target architecture](docs/04-target-architecture.md), [write/lock protocol](docs/06-write-and-transaction-protocol.md) and [foundation prerequisites](docs/12-foundation-prerequisites.md).
3. [Phase plan](docs/13-phase-plan.md), [performance analysis](docs/11-performance-and-capacity.md) and [test strategy](docs/16-test-strategy.md).
4. [Decision register](docs/19-decisions-and-open-questions.md) before agreeing to implementation scope.

The remaining documents cover revision boundaries, UI reconciliation, agent bases/approval, outcomes, external effects, migrations and the coexistence with component extraction. [The complete index](INDEX.md) lists every document. Stable identifiers in the JSON registers connect findings, invariants, decisions, phases, tests and performance scenarios.

### What is verified, and what is not

Selected code was read on `development` at `7db3543ab437376baeca55089cb331fbe1b30483`. The in-progress component branch was inspected at `e55a780b36e75db39a05b7400408d8267d0f32d5`. The latter was three commits ahead of that development baseline. Branch metadata and selected files were inspected; the complete comparison and call graph were **not** audited.

No application build, database migration, concurrency reproduction, browser test or benchmark was executed for this package. Existing documentation's historical test results are not new validation evidence. Proposed tests are marked as proposed. Performance thresholds are candidate review gates, not measured results or promised service levels.

These commits describe **evidence**, not a required implementation branch. Before any implementation, resolve the then-current working tree and remap moved contracts and owners. The package deliberately has a discovery phase; it does not pretend that every writer in every module is already enumerated.

### Important conclusions

- Expected content is different from project lifetime, current authority, a decision basis and the identity of a retry.
- A simple conditional database update does not require a distributed coordination layer or a second full entity read merely to compare a token.
- The existing relational striped-lock path, per-key advisory SQL, repeated admission reads and full-surface query paths deserve measurement before broad reuse. The authorized PostgreSQL source path is different and must not be accidentally replaced.
- Existing `CrmHrDraftReconciler` and canonical UI-seam rules already distinguish the submitted snapshot from subsequent typing. Reuse their semantics without making another feature depend on CRM implementation.
- Rejecting stale target writes alone does not stop an agent from acting on outdated objectives or a revoked execution mandate.
- Each phase declares residual risks and a safe stopping state. Never advertise application-wide concurrency protection after only the Projects pilot.

### Using and updating the package

`registers/` contains editable JSON inventories. `phases/` contains independent phase review cards. `templates/` contains evidence, benchmark and next-review templates. `tools/` contains standard-library Python utilities to validate/regenerate package records and perform **read-only** local Git evidence refresh. They do not implement the feature, modify application source, fetch remotes, run application tests or migrate a database. The utility self-tests use only disposable temporary Git fixtures.

```powershell
python tools/validate_package.py .
python tools/refresh_evidence.py --repo C:\src\CanDoItAll --ref HEAD --out C:\review-output\concurrency-refresh.json
```

The Windows paths above are examples, not assumptions about a machine. The tools accept normal absolute paths on Linux and macOS too. For evidence refresh, the output directory must be outside the repository; no credentials are requested. Unchanged Git blobs support reusing the file observation, **not** a conclusion that all its callers still behave the same.

Start the next iteration from [the review prompt](templates/NEXT_ITERATION_REVIEW.md). Update stable logical IDs when paths move; do not rename an owner merely because its renderer moved. All decisions remain proposed until explicitly recorded as accepted.

### Package maintenance tools

After deliberate register/document edits, run `python tools/render_register_views.py .` to regenerate readable register views. Review narrative tables and wording separately, then run `python tools/rebuild_manifest.py .` and `python tools/validate_package.py .`. The manifest detects accidental content changes; it is not a signature or a product test. Run `python tools/test_package_tools.py` for the utilities' isolated self-tests. These operations update this package only. Python 3.10+ and Git are required for the refresh/self-test utilities.
