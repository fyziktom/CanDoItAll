# Anti-evasion rules
These patterns do **not** count as closure:

1. Moving legacy carrier fields into another partial class while leaving them active.
2. Keeping fallback-from-legacy logic in the active runtime and calling it “compatibility only.”
3. Rendering plugin fields by listing current known keys and calling it “manifest-driven.”
4. Keeping custom plugins on fake enum identities just to satisfy older flows.
5. Closing the reference-model finding by adding more enum members.
6. Leaving write-normalization in `LoadAsync` while claiming migration is complete.

Closure means the active architecture changed, not just the file location or naming.
