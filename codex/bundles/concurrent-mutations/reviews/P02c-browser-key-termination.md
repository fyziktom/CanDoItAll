# Pending-store key termination review

Source review, 2026-10-16. No production defect was confirmed and
`projectPendingSubmissions.js` is unchanged.

The original concern applied .NET's final-newline `$` behavior to JavaScript.
The three JavaScript UUID, caller-binding and epoch regular expressions do not
use the multiline flag. ECMAScript's
[assertion algorithm](https://tc39.es/ecma262/multipage/text-processing.html#sec-compileassertion)
allows `$` before a line terminator only when multiline is enabled. These
existing expressions therefore already require the absolute end of the input.

One browser contract regression was added:
`ProjectPendingSubmissionBrowserTests.Canonical_operation_keys_reject_final_line_terminators_before_opening_a_storage_transaction`.
It uses the actual pending-store ES module and IndexedDB browser fixture. It
appends LF and CRLF separately to both UUID fields, caller binding and epoch,
requires InvalidRequest for all eight attempts, observes zero IndexedDB
transactions during them and verifies the reference inventory remains empty.
The original valid key, including the exact string Int64.MaxValue epoch, must
then prepare successfully with the first revision and unchanged key bytes.

Source count becomes **13** pending-store Facts, plus the existing **two** native
browser Facts: **15 expected discovery and execution cases** for the parent's
combined browser filter. This record does not claim discovery or execution. No
build, browser launch, database operation or static validation was performed by
the source author.
