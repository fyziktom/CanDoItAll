# Package validation

These checks validate the handoff, not the application.

- 39 new/copied package-tool tests passed; 14 unchanged shared-tool tests passed. Total: 53.
- 39 product groups retain NOT_RUN in the sealed template; no product PASS is prefilled.
- The strict completion checker rejects the unfinished template as intended.
- 16 selected source records state exact refs, Git blobs where available and read coverage.
- 45 local relative Markdown links resolve inside the package.
- All 22 shared-v3 files match the input PP2 archive byte-for-byte.
- The finalized archive contains 54 files including its manifest. SHA-256 manifest and ZIP CRC
  were checked, then the freshly extracted archive was validated again.

Reviewer execution: no dotnet, PostgreSQL, Docker, Playwright or watch run. Existing S0 results
are attributed to the implementer's maintained report. Private raw product artifacts were not
provided. The model snapshot checker and closure validator only check supplied-data consistency;
they cannot authenticate evidence or prove a test, image, signature or native write occurred.

Run from this package:

```text
python tools/validate_handoff.py
python tools/validate_handoff.py --evidence PATH_TO_WORKING_EVIDENCE --require-complete
python tools/check_model_parity.py PATH_TO_SAFE_OBSERVED_MODEL_SNAPSHOT
```

The second command must fail for templates/evidence.json. The third must fail for the unfilled
model template. Use actual safe exports in a separate working directory, not invented values.
