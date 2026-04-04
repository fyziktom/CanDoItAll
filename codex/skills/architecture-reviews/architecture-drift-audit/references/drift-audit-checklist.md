# Drift audit checklist

- Are the core concepts still easy to name consistently?
- Is `Node` or another universal type absorbing too much?
- Are there more places writing canonical truth than before?
- Are projections staying projections?
- Has policy/auth logic spread into arbitrary services or models?
- Is runtime/dev tooling leaking into application or domain code?
- Did dependency direction get worse?
- Is testability of the core model improving or degrading?
