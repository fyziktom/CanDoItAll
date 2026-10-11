# Instruction package verification

`tools/verify_package.py` is a standard-library-only read-only verifier. It checks the task
identity, one-directory/no-shared-copy policy, reviewed-source shapes, acceptance IDs and
finding references, exact carry-forward IDs/counts, local Markdown file links, initial
NOT_RUN evidence and SHA-256 manifest coverage. It rejects traversal/symlink/duplicate file
paths and unexpected unmanifested files. It does not use a network, change the repository,
run Git/.NET, read keys, build the application or create product test evidence.

`tools/test_verify_package.py` tests the verifier using owned temporary copies, including
intentional corruption. No product behavior or C# test is exercised by that Python suite.
The completed verification report is [PACKAGE_CHECKS.json](PACKAGE_CHECKS.json). It must be
read with this limitation; all product-lane entries in the evidence template remain NOT_RUN.

The manifest covers every deliverable except itself (ordinary self-hash exclusion).
Interpreter cache directories are not distributed. Verification of a downloaded artifact
is not an instruction to reseal edited shared or historical bundles. If the package is
intentionally amended, review its changes first and regenerate its own metadata; do not
silently overwrite the immutable review facts or fake test status.
