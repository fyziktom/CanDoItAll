# Proposed Subproject Template

Every new project introduced by this bundle should include:

- `README.md` with purpose, dependencies, validation command, and ownership boundary.
- Minimal public API surface; avoid exposing implementation helpers.
- Unit tests for contracts and validation logic.
- Architecture guard coverage when the project defines a boundary.
- No native Cognitive Memory dependencies unless the project is explicitly under the native service or a temporary migration adapter.
- No Qdrant dependency unless the project is the optional native projection package.
- All source-code comments in English.

Suggested sections for project README:

```text
# ProjectName

## Purpose
## Allowed Dependencies
## Forbidden Dependencies
## Public Contracts
## Runtime Ownership
## Validation Commands
## Migration Notes
```
