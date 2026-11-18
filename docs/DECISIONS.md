# Architectural Decision Records (ADRs)

## ADR-001: .NET 10 LTS for Backend Microservices
- **Context:** ShipTrack is a portfolio project showcasing modern enterprise .NET.
- **Decision:** Use .NET 10 Web APIs with C# 13/14 language features, nullable reference types enabled, and `TreatWarningsAsErrors=true`.
- **Consequences:** Ensures long-term maintainability, clean syntax, top performance with Native AOT/Kestrel improvements, and strict compilation checks.

## ADR-002: YARP (Yet Another Reverse Proxy) for Gateway
- **Context:** The frontend needs a unified API endpoint on port 8080. Internal inventory routes (`/api/inventory/*`) must not be accessible externally.
- **Decision:** Use Microsoft's official YARP library in a dedicated ASP.NET Core gateway project.
- **Consequences:** Native .NET configuration, high throughput, easy header forwarding (`X-Correlation-Id`), and strict route filtering without third-party proxies like Kong or NGINX.

## ADR-003: CQRS-Lite Without MediatR
- **Context:** Clean architecture benefits from separating command handlers and query handlers. MediatR v12+ introduced licensing considerations and adds reflection overhead.
- **Decision:** Implement hand-written command and query handlers registered directly into ASP.NET Core's built-in dependency injection container.
- **Consequences:** Zero third-party license risk, immediate navigation in IDE via "Go to Definition", no magical reflection, and zero runtime performance overhead.

## ADR-004: Optimistic Concurrency with PostgreSQL `xmin`
- **Context:** Inventory reservation must prevent overselling when two requests attempt to reserve the final stock unit in parallel.
- **Decision:** Map EF Core's concurrency token to PostgreSQL's system column `xmin` (`xid`).
- **Consequences:** Eliminates the need for manual row version columns, automatically updates on row mutation, and throws `DbUpdateConcurrencyException` when a race condition occurs, allowing the service to return 409 Conflict.

## ADR-005: Keyset Pagination for Orders Queries
- **Context:** Admin order views and customer order histories must scale gracefully as the table grows to hundreds of thousands of rows.
- **Decision:** Use keyset pagination based on `(CreatedAt, Id)` cursor rather than `OFFSET / FETCH`.
- **Consequences:** Query execution remains $O(1)$ constant time regardless of page depth, eliminating expensive table scans associated with deep `OFFSET`.

## ADR-006: Vitest + React Testing Library + MSW for Frontend Tests
- **Context:** Fast and modern testing framework for React 19 + Vite.
- **Decision:** Use Vitest instead of Jest for instant ESM/Vite integration, RTL for component tests, and Mock Service Worker (MSW) for API network mocking.
- **Consequences:** Shared configuration with Vite, high execution speed, and resilient tests that mock at the HTTP network layer rather than monkey-patching fetch.
