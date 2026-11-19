# ShipTrack Project Report

## Resume summary

**ShipTrack** is a production-style order and shipment tracking platform built with .NET 10 microservices, React 19, PostgreSQL, YARP, and Docker. It demonstrates secure authentication, inventory concurrency control, reliable order orchestration, keyset pagination, SQL performance optimization, and a typed customer/admin web application.

## Resume-ready bullet points

- Built a production-style order and shipment tracking platform using **.NET 10, C#, React 19, TypeScript, PostgreSQL 16, YARP, and Docker Compose**.
- Designed three independently persisted microservices for identity, inventory, and orders, exposed through a YARP reverse-proxy gateway with correlation IDs and restricted CORS.
- Implemented JWT authentication, role-based authorization, password hashing, login rate limiting, admin seeding, and protected service-to-service HTTP calls.
- Prevented inventory overselling with PostgreSQL `xmin` optimistic concurrency, transactional stock reservation/release, duplicate-line aggregation, and compensation on order persistence failure.
- Implemented an order lifecycle state machine, shipping strategy selection, customer/admin access rules, cancellation, status history, and resilient Inventory HTTP calls with timeout, retry, and circuit breaker policies.
- Improved measured PostgreSQL query latency from **13.462 ms to 0.157 ms** for admin order filtering and from **9.851 ms to 0.178 ms** for customer history by adding composite descending indexes and keyset pagination.
- Delivered a React 19 frontend with typed API access, JWT persistence, protected routes, catalogue search, cart checkout, order history/details, and admin product/order management.
- Containerized the complete stack with non-root .NET images, health checks, migration-on-startup support, and a production Nginx frontend image.

## Architecture

```text
React SPA (:3000)
        |
YARP Gateway (:8080)
   |       |       |
Identity Inventory Orders
   |       |       |
identity  inventory orders
  _db       _db      _db

Orders -> Inventory internal HTTP calls protected by X-Internal-Key
```

Each service owns its database and schema. Clients use the Gateway rather than calling backend services directly. Internal Inventory reservation and product lookup routes are not exposed through the Gateway.

## Technology stack

| Area | Implementation |
|---|---|
| Backend | .NET 10, ASP.NET Core Web API, EF Core 10 |
| Frontend | React 19, TypeScript strict mode, Vite, React Router |
| Data fetching | TanStack Query and typed `fetch` API client |
| Forms and validation foundation | react-hook-form and Zod dependencies |
| Styling | Tailwind CSS/PostCSS with responsive application styles |
| Gateway | YARP reverse proxy |
| Database | PostgreSQL 16, one database per service |
| Testing | xUnit, NSubstitute, Testcontainers integration-test foundation |
| Delivery | Docker Compose, multi-stage Dockerfiles, Nginx |

## Main capabilities

### Identity

- Customer registration and login
- JWT access tokens with role and user ID claims
- Password hashing using ASP.NET Core PasswordHasher
- Fixed-window login rate limiting
- Seeded administrator account from environment variables

### Inventory

- Authenticated product catalogue browsing and search
- Admin product creation, update, and deletion
- PostgreSQL `xmin` concurrency protection
- Internal reserve/release operations
- Atomic multi-item stock reservation

### Orders

- Product price lookup and stock reservation orchestration
- Standard and Express shipping strategies
- Confirmed, shipped, delivered, cancelled lifecycle transitions
- Customer-owned order history and details
- Admin filtering and status updates
- Compensation when order persistence fails after stock reservation
- Cursor/keyset pagination using descending `(CreatedAt, Id)` ordering

### Frontend

- Registration and sign-in
- Persistent JWT session
- Protected customer and admin routes
- Product search and cart
- Checkout with stock-backed order creation
- Customer order history and details
- Admin product creation and order status management

## Performance work

The Orders database was seeded with 300,000 orders and one item per order using PostgreSQL binary `COPY`. Measured `EXPLAIN (ANALYZE, BUFFERS)` results showed:

| Query | Before indexes | After indexes | Improvement |
|---|---:|---:|---:|
| Admin status/date filter | 13.462 ms | 0.157 ms | ~98.8% faster |
| Customer order history | 9.851 ms | 0.178 ms | ~98.2% faster |

Indexes:

- `(Status, CreatedAt DESC)`
- `(CustomerId, CreatedAt DESC)`

Keyset pagination avoids the increasing cost of large `OFFSET` values and uses the stable `(CreatedAt, Id)` cursor.

## Verification completed

- Backend solution build passed with **0 warnings and 0 errors**.
- Orders unit tests: **19 passed**.
- Identity unit tests: **16 passed**.
- Inventory unit tests: **14 passed**.
- Backend Docker images built successfully.
- Gateway smoke flow verified registration, login, admin product creation, product browsing, and confirmed order creation.
- Frontend TypeScript check passed.
- Frontend production Docker image built successfully.
- Frontend production dependency audit reported **0 vulnerabilities**.

## Local run

From the repository root:

```powershell
Copy-Item .env.example .env
docker compose --profile frontend up --build
```

Open [http://localhost:3000](http://localhost:3000). The API Gateway is available at [http://localhost:8080](http://localhost:8080).

Default development admin:

```text
Email:    admin@shiptrack.dev
Password: Admin@123456
```

## Remaining hardening work

- Expand Vitest/React Testing Library coverage to page-level API states and checkout flows.
- Add GitHub Actions and Azure Pipelines CI workflows.
- Run the full-stack browser smoke flow with a fresh PostgreSQL volume.
- Run Testcontainers integration tests from a host process with Docker access.
- Add production secret management and deployment configuration.

## Goal assessment

### Original goal

Build a credible, production-style microservices portfolio project that demonstrates backend architecture, security, database performance, testing, Docker deployment, and a usable web client.

### Current status

**Yes, the core project goal has been reached.** The system is runnable end-to-end on localhost and demonstrates the complete business path:

```text
Register/Login -> Browse products -> Add to cart -> Checkout ->
Reserve inventory -> Persist order -> View order history/details
```

The admin path is also functional:

```text
Admin login -> Create products -> Review orders -> Update order status
```

The project is portfolio-ready for demonstrating engineering ability. It is not presented as a fully production-deployed commercial product yet; the remaining work is primarily delivery hardening, broader automated UI coverage, and cloud deployment.

## What remains

| Area | Status | Remaining effort |
|---|---|---|
| Core backend services | Complete | Optional refinements |
| Gateway routing and security boundaries | Complete | Add rate/observability dashboards |
| React customer/admin flows | Complete | Add more page-level tests |
| Checkout flow | Complete | Add browser automation |
| Backend unit tests | Complete | Run host-level integration suite |
| Frontend tests | Started | Add API mocks and component coverage |
| Docker Compose local deployment | Complete | Fresh-volume acceptance run |
| CI/CD | Not started | GitHub Actions/Azure Pipelines |
| Cloud deployment | Not started | Azure/container registry/managed PostgreSQL |
| Production secrets/observability | Partially complete | Secret store, metrics, tracing |

A reasonable final-hardening estimate is **2–4 focused working days**. A cloud-deployed showcase with CI/CD, screenshots, and a polished test suite would take approximately **1–2 additional weeks part-time**.

## Important bugs and how they were handled

### Cursor pagination returned the wrong ordering

The first Orders implementation mixed cursor comparisons with ascending ordering. That could duplicate or skip rows when moving between pages. It was corrected to use consistent descending `(CreatedAt, Id)` comparisons and bounded page sizes, with focused tests for cursor behavior.

### Orders did not contain complete test coverage

The service had orchestration complexity but insufficient tests. Tests were added for successful creation, validation, insufficient stock, compensation after save failure, shipping strategies, and lifecycle transitions. This made the failure paths explicit rather than relying only on happy-path smoke tests.

### Cancellation was converted into an inventory failure

The order handler initially caught broad exceptions. `OperationCanceledException` was excluded from expected business-failure handling so request cancellation now propagates correctly, while real inventory failures still return a controlled result and log the cause.

### Orders could not call Inventory through the public product endpoint

The public Inventory endpoint requires JWT authentication, but Orders uses internal service-to-service communication. A separate internal batch endpoint protected by `X-Internal-Key` was added. This preserved the public security boundary without sharing user tokens between services.

### Docker builds failed because of build context assumptions

The Dockerfiles copy repository-root files such as `Directory.Packages.props`. Compose originally used service-directory contexts, so builds failed. The contexts were corrected to the repository root.

### Runtime images lacked expected Linux utilities

The .NET runtime images did not provide the assumed `adduser` and `curl` commands. Dockerfiles now use the image-provided non-root `$APP_UID`, and health checks use portable Bash TCP probes.

### Migrations were not discovered on a fresh startup

The original Orders initial migration lacked generated designer metadata. Designer files and a model-sync migration were added so EF can discover the migration chain and rename the legacy `OrderItem` table to the current `OrderItems` model.

### SQL benchmark output was initially synthetic

The benchmark documentation was replaced with real PostgreSQL `EXPLAIN (ANALYZE, BUFFERS)` output from a 300,000-order dataset. This made the performance claims reproducible and interview-defensible.

### Checkout appeared to do nothing

The first frontend version submitted checkout directly from the cart, kept cart state only in memory, and did not render mutation errors. The fix added a dedicated checkout route, localStorage-backed cart state, batch product loading, shipping selection, visible error messages, and redirect to order details after success.

### Development dependency audit findings

Adding the first Vitest version introduced vulnerable development-only transitive packages. Production dependencies remained clean, and Vitest was upgraded to the patched major release. The final production audit and full audit both report zero vulnerabilities.

## Engineering lessons

- Keep service ownership explicit: each service owns its database and domain rules.
- Use an internal contract for service-to-service calls instead of weakening public authentication.
- Treat compensation as a first-class part of distributed workflows.
- Catch expected failures narrowly; do not convert cancellation or unexpected exceptions into success-shaped responses.
- Use real database plans and measurements rather than synthetic benchmark claims.
- Make Docker assumptions explicit: build context, runtime user, health checks, and startup migrations all matter.
- Build the frontend around the actual API contract instead of duplicating backend DTO logic manually in multiple screens.
- Always render mutation errors in the UI; a silent failure is indistinguishable from a broken button to a user.
- Separate production and development dependency audits because test tooling has a different runtime exposure.

## Interview discussion guide

### “Why microservices for a portfolio project?”

The point was not to maximize service count. Three boundaries map to independent responsibilities: identity, inventory, and orders. Each owns its data, while the Gateway gives clients one stable entry point. This demonstrates boundaries and integration without creating dozens of low-value services.

### “How do you prevent overselling?”

Inventory reservations execute transactionally and use PostgreSQL `xmin` as an optimistic concurrency token. If two requests compete for the last unit, one update succeeds and the other detects a concurrency conflict. The Orders handler treats reservation as all-or-nothing.

### “What happens if order persistence fails after reservation?”

The handler calls Inventory release as compensation and logs a critical event if compensation itself fails. It rethrows the original persistence exception so the API does not return a false success.

### “Why use keyset pagination?”

Large offsets require the database to scan and discard increasingly many rows. The `(CreatedAt, Id)` cursor gives stable continuation semantics and avoids that work. The second key makes the ordering deterministic when timestamps collide.

### “How was performance verified?”

The Orders database was populated with 300,000 orders and one item per order. `EXPLAIN (ANALYZE, BUFFERS)` was captured before and after composite descending indexes. The measured admin query improved from 13.462 ms to 0.157 ms, and customer history from 9.851 ms to 0.178 ms.

### “What would you improve next?”

I would add CI pipelines, browser-level Playwright tests, OpenTelemetry traces, centralized secret management, a fresh-volume migration acceptance job, and a cloud deployment using managed PostgreSQL. These are delivery and operations improvements rather than missing core business behavior.

## Final validation snapshot

At the current checkpoint:

- Local stack runs with `docker compose --profile frontend up --build`.
- Frontend is available at `http://localhost:3000`.
- Gateway is available at `http://localhost:8080`.
- All backend containers and PostgreSQL become healthy.
- Backend build: 0 warnings and 0 errors.
- Frontend type-check: passed.
- Frontend production build: passed.
- Frontend focused tests: 3 passed.
- Production dependency audit: 0 vulnerabilities.
- Full dependency audit after Vitest upgrade: 0 vulnerabilities.
