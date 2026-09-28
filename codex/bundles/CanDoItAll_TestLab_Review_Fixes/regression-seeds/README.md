# Proposed regression seeds — not executed

[TestLabSandboxReviewRegressionTests.cs](TestLabSandboxReviewRegressionTests.cs) contains two proposed xUnit facts against public types present at the reviewed HEAD. The reviewer did **not** compile, discover or run them. They are source seeds for a failing-first reproduction, not a ready-made validation receipt.

Adapt the tests to the current checkout and place them in the existing light TestLab UI test project, or integrate equivalent cases into its existing tests. The initial expected seed count is two only if the two unchanged Fact methods are actually included and selected. Derive/discover the real final count after integration.

The tests intentionally release any remaining controlled wait before checking settlement, permitting either a proper independent read-back lane or an ownership-safe committed-warning implementation. They never sleep to create ordering. R1 is expected from source to fail because Pending remains set; R2 because the global reference choices are empty. Verify the actual red result rather than copying that expectation as observed output.

These model-level facts are not substitutes for the required real EditForm/InputSelect regression, production-parity test, browser path, negative/failure reference cases, or successor/disposal isolation.
