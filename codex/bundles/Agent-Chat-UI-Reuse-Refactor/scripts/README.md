# Bundle scripts

All scripts use only the Python standard library.

## Preparation/bundle validation

Run from the bundle root:

```text
python scripts/validate_all.py
```

It validates:

- bundle structure and status;
- requirement/subbundle/checkpoint traceability;
- SharedInfo-aligned test policy;
- hard phase exclusions;
- checksums.

## Repository boundary guard

During implementation, run:

```text
python <bundle>/scripts/check_repo_boundaries.py <repository-root> --base-sha <subbundle-base-sha>
```

The guard checks:

- the neutral project for forbidden namespaces, services, persistence, and project references;
- the Phase 1 diff for Simple Chat backend/UI changes;
- the Phase 1 diff for newly added partial files on named large Agent UI types.

The script is supporting proof. Codex must still inspect semantic diff and CodeAnalytics dependency evidence.
