# Stop conditions
Stop and do not start the large plugin wave if any of the following is true:
- any HG-* gate still fails,
- a closure claim depends on compatibility shims in the active hot path,
- a custom plugin still needs shared page/model edits to add new fields,
- load paths still write normalization changes,
- write-side plugin work starts before the durable connector command boundary exists.
