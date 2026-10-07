# Handoff tools

These tools validate the package and evidence structure. They do not run or authenticate the
CanDoItAll product, PostgreSQL, browser, models or a security audit.

```powershell
python tools/validate_handoff.py
python tools/validate_handoff.py --evidence <private-working-evidence.json> --require-demo-ready
python -m unittest discover -s tools -p 'test_*.py' -v
python tools/inspect_artifact.py <private-exported-file.xlsx> --output <private-inspection.json>
```

Artifact inspection is read-only, accepts SVG/XLSX and supported rasters, and is intentionally
bounded. Raster decoding requires an existing Pillow installation. The XML/ZIP inspector uses
only the standard library and does not evaluate Excel formulas. It is an independent demo
oracle, not a replacement for native content authorization or actual renderer/model testing.
Minimal parser fixtures in the unit tests are not customer files or application results.

Keep reports private: a workbook or SVG report may contain the inspected document's text.
