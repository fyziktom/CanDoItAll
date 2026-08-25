# Bundle scripts

- `validate_bundle.py`: cross-platform structural/semantic readiness validator.
- `update_file_manifest.py`: regenerates the portable SHA-256 inventory after durable bundle
  state changes.
- `run_proof_command.py`: runs a command and writes a timestamped transcript with working
  directory, duration, stdout, stderr, and exit code.
- `validate_bundle.ps1`: PowerShell 7 wrapper.
- `validate_bundle.sh`: POSIX shell wrapper.

Preparation command:

```text
python scripts/validate_bundle.py .
```

Regenerate integrity state before validation:

```text
python scripts/update_file_manifest.py .
python scripts/validate_bundle.py --stage prepared .
```

Use `--stage completed` for final closure after regenerating the manifest. Current SharedInfo
bundle/subbundle validators remain authoritative for the evidence-backed semantic review.
