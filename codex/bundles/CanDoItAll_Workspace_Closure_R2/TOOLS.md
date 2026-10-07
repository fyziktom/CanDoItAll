# Local handoff tooling

From the extracted package root, with Python 3.11 or newer:

```text
python -B tools/validate_package.py --root .
python -B tools/test_package.py
python -B tools/test_closure.py
python -B shared/tools/test_tooling.py
python -B tools/validate_closure.py --plan closure-plan.json --results templates/closure-results.json
```

The blank template is valid bookkeeping but intentionally not ready. To validate an actual
external result ledger and hashes:

```text
python -B tools/validate_closure.py --plan closure-plan.json --results <result-file> --evidence-root <private-proof-root> --require-ready
```

Use the actual shell's path syntax. No script builds, tests, modifies or authorizes the
application. A readiness failure is not solved by editing required modes or dropping groups.
Package validation does not check external URLs, product source authenticity, Markdown anchor
existence or runtime truth. Closure validation similarly cannot authenticate evidence.

The shared tooling is retained byte-for-byte. Package/closure tooling is inherited from the
previous handoff; closure validation additionally reads the explicit product_fix_groups plan
field so the new DEL/NAV families cannot be omitted from product-fix closure. Added synthetic
controls test that omission; they are not C# regression tests.
