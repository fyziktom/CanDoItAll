# Package validation

Prepared 2026-10-01. These checks concern the handoff only.

- UTF-8 text and JSON parsing, declared inputs, source/ref consistency, pinned source paths and valid recorded Git object IDs.
- Relative file links remain within the bundle and resolve. Remote connectivity and Markdown anchor rendering are not certified by this utility.
- Root SHA-256 manifest and exact 22-file shared-v3 inventory match the original extracted foundation; the shared foundation's own validator passes.
- Ten package negative/positive controls pass, including changed source/manifest, missing links, unauthorized live budget, small viewport, altered shared bytes and fabricated template success.
- Fourteen shared-tool controls pass. Their Git operations are confined to synthetic temporary repositories.
- ZIP CRC and a freshly extracted copy are checked separately during handoff assembly.

The final handoff has 43 text files, 23 unique source records and 16 initially NOT_RUN evidence groups. Tests do not certify CanDoItAll behavior, original private evidence, runtime performance, paid/live execution or application readiness.

Run from the extracted package root:

```text
python -B tools/validate_package.py
python -B -m unittest discover -s tools -p test_package.py -v
python -B shared/tools/validate_bundle.py --root shared
python -B -m unittest discover -s shared/tools -p test_tooling.py -v
```

Keep the sealed template unchanged. Fill a working evidence copy outside this directory. Recomputing hashes after editing source is not evidence of product correctness.
