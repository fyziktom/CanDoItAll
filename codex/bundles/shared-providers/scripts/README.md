# Bundle scripts

- `validate_bundle.py`: cross-platform structural/semantic readiness validator.
- `validate_bundle.ps1`: PowerShell 7 wrapper.
- `validate_bundle.sh`: POSIX shell wrapper.

Preparation command:

```text
python scripts/validate_bundle.py .
```

The script validates initial readiness, not implementation closure. During execution, current
SharedInfo bundle/subbundle validators remain authoritative for proof depth and final closure.
