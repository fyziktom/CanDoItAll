# Bundle scripts

All scripts are cross-platform Python 3 scripts and use `pathlib`.

## Validate bundle structure

```powershell
python ./scripts/validate_bundle.py --bundle-root .
```

## Validate test policy

```powershell
python ./scripts/check_test_policy.py --bundle-root .
```

## Validate prepared architecture boundaries

Against a repository checkout:

```powershell
python ./scripts/check_architecture_boundaries.py --repo-root ../../..
```

The architecture script is a prepared guard and may be tightened after SB00 records exact project
paths. It must not be weakened to make a violation disappear.
