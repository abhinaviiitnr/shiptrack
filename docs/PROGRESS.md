# ShipTrack Engineering Progress Log

## Phase 0: Scaffolding and Infrastructure
- Scaffolding complete: Git repository initialized, .gitignore, .editorconfig, and Directory.Packages.props central package management configured.
- Established multi-database PostgreSQL docker-compose environment with custom init.sql creating `identity_db`, `inventory_db`, and `orders_db`.
- Setup architectural skeletons for three microservices (Identity, Inventory, Orders), YARP Gateway, and React frontend.
- Created architectural documentation including ARCHITECTURE.md, DECISIONS.md, and SECURITY.md.
- Ready for Phase 1 (Identity service completion and automated testing).

## Phase 1: Identity Service and Automated Tests
- Implemented Identity.Api with ASP.NET Core PasswordHasher, JWT token service (15-min expiry), and fixed-window IP rate limiting (5 req/min).
- Configured EF Core with Npgsql migrations for identity_db and startup admin user seeding from environment variables.
- Implemented CQRS-lite Register and Login command handlers with clean DTO projections and FluentValidation.
- Added comprehensive unit tests for validators, password hashing, and JWT claims inspection.
- Added Testcontainers PostgreSQL integration tests verifying registration, login, 409 conflict on duplicate email, and 429 on rate limit exhaustion (all 18 tests passing).

## Phase 2: Inventory Service and Automated Tests
- Implemented product catalogue CRUD with authenticated reads and AdminOnly writes, plus paged name search and batch lookup.
- Added PostgreSQL `xmin` optimistic concurrency so parallel reservations cannot oversell stock.
- Added protected internal reserve and release endpoints using the configured `X-Internal-Key`, with all-or-nothing transactions.
- Added unit tests for product handlers and validators, and Testcontainers integration tests for access control, reservation, and last-unit concurrency.
- Duplicate reservation lines are aggregated and invalid quantities return 400; the phase is ready for SDK-backed build and test verification.

## Phase 3: Orders Service and Automated Tests
- Completed order creation orchestration: product lookup, stock reservation, shipping strategy, persistence, and save-failure compensation.
- Enforced the explicit order status transition table and owner/admin access rules for reads and cancellation.
- Added resilient typed Inventory HTTP calls with timeout, retry, and circuit-breaker configuration.
- Added focused unit tests for all status transitions, shipping costs, request validation, successful creation, insufficient stock, and compensation.
- Corrected cursor pagination to use descending `(CreatedAt, Id)` ordering and bounded admin page sizes; integration tests still require Docker access.

## Phase 4: Gateway and Backend Compose Integration
- Configured YARP routes for auth, products, and orders while keeping internal Inventory routes outside the Gateway.
- Added the backend smoke flow in [smoke-test.http](smoke-test.http) for registration, login, product creation, and order creation.
- Corrected Docker build contexts, non-root runtime users, portable healthchecks, Docker migration environment, and shared JWT/internal-key mappings.
- Added the internal key-protected product batch endpoint required by Orders for service-to-service price lookup.
- Verified all backend containers become healthy and the Gateway smoke flow creates a Confirmed order; frontend remains deferred to Phase 6.

## Phase 5: SQL Optimization and Benchmarking
- Updated the large-order seeder to create 300,000 orders with one OrderItem per order using PostgreSQL binary COPY.
- Captured real `EXPLAIN (ANALYZE, BUFFERS)` output before and after the status/date and customer/date composite indexes.
- Added the EF migration for descending `CreatedAt` index order and verified both indexes exist in PostgreSQL.
- Measured admin query improvement from 13.462 ms to 0.157 ms and customer history improvement from 9.851 ms to 0.178 ms.
- Replaced synthetic benchmark output in [sql-optimization.md](sql-optimization.md) with the measured plans and keyset pagination explanation.

## Phase 6: React Frontend
- Added a Vite + React 19 + strict TypeScript frontend in `src/web`, with Tailwind/PostCSS configuration and a production Nginx image.
- Added typed Gateway API access, JWT persistence, protected customer/admin routes, and sign-in/register flows.
- Added the customer catalogue, search, persisted cart, dedicated checkout, order history, and order detail screens.
- Added the admin product creation and order status management screens.
- Added explicit checkout error rendering and shipping-method selection.
- Added focused Vitest coverage for persisted cart validation and cleanup; all 3 tests pass.
- Frontend type-checking passed locally and the complete production Docker build passed; browser-level tests remain part of the next validation phase.
