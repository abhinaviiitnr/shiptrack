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
