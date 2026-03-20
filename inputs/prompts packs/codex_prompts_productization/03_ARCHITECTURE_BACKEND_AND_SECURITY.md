# 03_ARCHITECTURE_BACKEND_AND_SECURITY.md

## Goal
Add a production-grade backend API with EF Core + JWT auth, prepared for:
- accounts, entitlements, admin,
- score storage + published library,
- ratings, tags, search,
- stats storage,
- future IPFS/NFT/custodial wallet models (NO chain integration now),
- offline-first sync API.

This prompt is architecture + skeleton implementation (endpoints, entities, middleware patterns). Full sync/library UI will be implemented in later prompts.

## Existing backend patterns to reuse
Study and reuse patterns from:
- `src/OMR.Service/Program.cs` (rate limiting, JWT setup, ProblemDetails, versioning, correlation IDs, logging)
- `src/OMR.Service/Infrastructure/*` (middleware helpers, error handling)
- `src/OMR.Service/Api/*` (endpoint mapping pattern)

## New projects to add
1) `src/App.Api/App.Api.csproj` (ASP.NET Core Web API / minimal APIs)
2) `src/App.Api.Tests/App.Api.Tests.csproj` (integration tests using `Microsoft.AspNetCore.Mvc.Testing`)
3) `src/App.Shared/App.Shared.csproj` (shared DTOs/contracts between WASM and API)

Update `MusicSheetReadingLearner.slnx` accordingly (or make it consistent).

## Backend architecture (must implement)

### A) API conventions
- Base path: `/api/v1/`
- Content: JSON
- Use ProblemDetails for errors
- Add request correlation ID
- Add structured logging
- Add rate limiting (IP + user)
- Add API versioning strategy (path-based is fine)
- Add OpenAPI/Swagger

### B) Authentication & authorization
- Password-based accounts: email + password
- Password hashing: PBKDF2/Argon2 (use a standard .NET implementation; do NOT store plain)
- JWT access token: short lifetime
- Refresh token: rotate + revoke; store hashed refresh token server-side
- Roles: `user`, `admin`
- Policies: `scores:read`, `scores:write`, `admin:*` (design permission model now)

### C) Database
EF Core, provider by config:
- default: PostgreSQL (`Npgsql`)
- alternative: SQLite
- for tests: InMemory
Implement `AppDbContext` with migrations.

### D) Storage abstraction for score documents
- Server stores score bodies as JSON blobs initially (filesystem or DB).
- Implement `IScoreDocumentStorage` abstraction:
  - `LocalFileScoreDocumentStorage` (default)
  - later: `IpfsScoreDocumentStorage` (interface stub only)

### E) Sync/outbox API (skeleton now)
Implement endpoints and DTOs but the full client sync will be in prompt 09:
- `POST /api/v1/sync/pull` (get server changes since cursor)
- `POST /api/v1/sync/push` (push local changes batch)
- Define conflict response format.

### F) Admin
- Admin endpoints protected by role/policy.
- Audit log entity table (at minimum: admin actions, who/when/what).
- Admin-only endpoints for moderation of published scores (approve/reject).

### G) Future blockchain/NFT/IPFS readiness (NO chain integration)
Add entities with nullable placeholders:
- `AssetListing` (score listing, price, currency, status)
- `AssetOwnership` (user owns listing, tokenId placeholder)
- `IpfsPointer` (cid, gateway url optional)
- `CustodialWallet` (user wallet record, encrypted key material pointer)

No chain calls. Only model + API placeholders.

## Required EF entities (minimum)
Create entities in `src/App.Api/Domain/Entities/*` (or similar):

- `User`
  - `Id` (Guid)
  - `Email` (unique)
  - `PasswordHash`
  - `CreatedUtc`, `LastLoginUtc`
  - `IsEmailVerified` (future)
- `UserRole` (or roles as string list)
- `RefreshToken`
  - hashed token, expires, revoked, rotatedFrom
- `Entitlement`
  - `UserId`, `Plan` (Free/Premium), `ValidUntilUtc`, `Source` (manual/stripe/future)
- `Score`
  - `Id` (Guid)
  - `OwnerUserId` (nullable for “system” scores)
  - `Title`, `Composer`, `Tags` (many-to-many), `CreatedUtc`, `UpdatedUtc`
  - `Visibility` (Private/Unlisted/Published)
  - `Status` (Draft/PendingReview/Published/Rejected)
  - `CurrentVersionId`
- `ScoreVersion`
  - `Id`, `ScoreId`
  - `DocumentStorageKey` (file path or DB key)
  - `Format` (NotationJson vX / MusicXML subset)
  - `CreatedUtc`, `CreatedByUserId`
  - `Sha256` (for dedupe/integrity)
- `ScoreRating`
  - `ScoreId`, `UserId`, `Stars` (1-5), `CreatedUtc`
- `ScoreTag`
  - `Id`, `Name` (unique)
- `AuditLogEntry`
  - `Id`, `ActorUserId`, `Action`, `TargetType`, `TargetId`, `DataJson`, `CreatedUtc`
- `IpfsPointer` (future)
- `CustodialWallet` (future)

## API endpoints (minimum)
Implement minimal APIs or controllers. Required endpoints:

### Auth
- `POST /api/v1/auth/register`
- `POST /api/v1/auth/login`
- `POST /api/v1/auth/refresh`
- `POST /api/v1/auth/logout`
- `GET /api/v1/auth/me`

### Scores (user)
- `GET /api/v1/scores/mine`
- `POST /api/v1/scores` (create metadata)
- `PUT /api/v1/scores/{id}` (update metadata)
- `POST /api/v1/scores/{id}/versions` (upload/save document JSON)
- `GET /api/v1/scores/{id}` (metadata + current version)
- `GET /api/v1/scores/{id}/document` (fetch current doc)
- `DELETE /api/v1/scores/{id}` (soft delete)

### Published library
- `GET /api/v1/library/scores` (search/filter/sort)
- `GET /api/v1/library/scores/{id}`
- `POST /api/v1/library/scores/{id}/ratings` (upsert user rating)
- `GET /api/v1/library/scores/{id}/ratings` (aggregate)

### Admin
- `GET /api/v1/admin/audit` (paged)
- `GET /api/v1/admin/moderation/scores`
- `POST /api/v1/admin/moderation/scores/{id}/approve`
- `POST /api/v1/admin/moderation/scores/{id}/reject`

### Sync (skeleton)
- `POST /api/v1/sync/pull`
- `POST /api/v1/sync/push`

## Validation, rate limiting, security checklist (must implement)
- Validate DTOs (FluentValidation or manual validation; consistent 400 errors)
- Rate limit auth endpoints (login/register/refresh)
- Hash passwords + salt; never log secrets
- CORS locked down (same origin preferred)
- Security headers (CSP, HSTS when hosted)
- Audit admin actions
- Error responses via ProblemDetails

## Tests (must add now)
In `src/App.Api.Tests`:
- Register/login flow returns JWT
- Protected endpoints require auth
- Rate limiting returns 429 (at least one test)
- Basic score CRUD works against InMemory provider

## Verification steps
- `dotnet build` (solution)
- `dotnet test` (solution)
- Run API: `dotnet run --project src/App.Api`
- Open Swagger UI and confirm endpoints exist.

## Definition of done
- Backend compiles and integration tests pass.
- API has JWT auth, rate limiting, ProblemDetails, and EF provider switching.
- Entities and DTOs exist, including future IPFS/NFT/custody placeholders (no chain integration).
