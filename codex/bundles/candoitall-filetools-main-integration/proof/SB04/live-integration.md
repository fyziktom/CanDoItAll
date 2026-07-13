# SB04 Optional Live Integration

Status on 2026-07-12: `Skipped — no live IPFS/FTP endpoint variables were configured`.

Live tests are supplementary; the deterministic fake-transport and production HTTP-handler suites remain mandatory.

Use environment-managed secrets only:

- `CANDOITALL_TEST_IPFS_API_ENDPOINT`: Kubo API base, for example an `/api/v0/` endpoint.
- `CANDOITALL_TEST_IPFS_BEARER_TOKEN`: optional bearer credential.
- `CANDOITALL_TEST_IPFS_MFS_PATH`: mutable path owned by the test account.
- `CANDOITALL_TEST_FTP_ENDPOINT`: disposable FTP/FTPS endpoint.
- `CANDOITALL_TEST_FTP_USERNAME` and `CANDOITALL_TEST_FTP_PASSWORD`.
- `CANDOITALL_TEST_FTP_BASE_PATH`: disposable base path; the server must support RFC machine listings for browse-positive proof.

The opt-in live suite must assert fixed public error text, avoid printing environment values, create only disposable fixture content under the configured base path, and clean it in `finally`. A server without `MLSD` must pass the explicit Unsupported case rather than the positive browse case.
