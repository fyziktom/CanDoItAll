# Optional read-only Git capture

`capture_review_snapshot.py` uses Python 3.10+ and an installed Git executable. It needs an existing local checkout that already contains both references. It does not access the network or change source, refs, the index, Git settings, or signing configuration.

Run from any directory, with an output path outside the checkout and its Git metadata:

```powershell
python ./tools/capture_review_snapshot.py --repo /path/to/CanDoItAll --out /path/to/review-evidence
```

The defaults are the pinned development and UI commits reviewed in this handoff. To inspect newer locally available references, use `--development development --ui-ref ui-refactoring-v2` and record the resulting commit IDs. This does not update those branches from a remote.

The output distinguishes the colleague's selected changes from their common ancestor from a head-to-head comparison. **Do not apply the head-to-head comparison:** it also reverses development-only changes. Only the eight selected API/composition source paths are captured. Reimplement useful changes against current development owners.

The destination must be new or empty. The helper checks that checkout HEAD and Git status did not change during its reads. Do not run it concurrently with a checkout or editing operation. These checks are not an atomic filesystem snapshot. Review captured patches before sharing: tracked source can contain sensitive information. The metadata includes the local repository path.

`helper-validation.json` records seven passing checks against a synthetic temporary Git repository during package preparation. They covered divergent commit counts, the two diff meanings, unchanged checkout state, unsafe output paths, output overwrite rejection and missing references. **This is helper validation only; no CanDoItAll build, API, persistence or browser test has run.**
