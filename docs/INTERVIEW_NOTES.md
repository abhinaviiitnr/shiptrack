# ShipTrack: 25 Core Full-Stack & System Design Interview Questions

These questions and concise answers are designed to help you explain every technical decision in ShipTrack during technical interviews.

---

### 1. Architecture & Microservices
**Q1: Why did you choose microservices instead of a monolithic architecture for ShipTrack?**
> **A:** While a monolith is easier to deploy initially, microservices allowed me to demonstrate clean domain boundaries, independent database isolation (`identity_db`, `inventory_db`, `orders_db`), autonomous deployment cycles, and independent scaling (e.g. Orders handles checkout spikes without affecting Identity).

**Q2: What is the Database-per-Service pattern, and why is it important here?**
> **A:** Each service owns its own schema and data store. Orders cannot perform SQL joins directly on Inventory tables. This prevents architectural erosion, ensures loose coupling, and allows services to optimize their storage independently.

**Q3: How does YARP fit into the architecture?**
> **A:** YARP (Yet Another Reverse Proxy) acts as the API Gateway. The React client speaks exclusively to YARP on port 8080. YARP routes `/api/auth/*` to Identity, `/api/products/*` to Inventory, and `/api/orders/*` to Orders. Crucially, YARP hides internal endpoints like `/api/inventory/reserve` from the outside world.

---

### 2. .NET 10, C#, & Backend Patterns
**Q4: Why did you avoid MediatR in this project?**
> **A:** MediatR v12 introduced licensing considerations. More importantly, MediatR adds reflection overhead and obscures code flow. In ShipTrack, I implemented a CQRS-lite pattern with dedicated command and query handler classes injected directly via ASP.NET Core DI. It is type-safe, simple to navigate in an IDE, and has zero third-party licensing impact.

**Q5: How does the Strategy pattern work for shipping calculations?**
> **A:** I defined an `IShippingStrategy` interface with `StandardShipping` (flat 50, free if total $\ge$ 1000) and `ExpressShipping` (150). A `ShippingStrategySelector` resolves the appropriate strategy at runtime. This adheres to the Open/Closed Principle: new shipping options (e.g. NextDay, Drone) can be added without modifying existing order creation logic.

**Q6: How does the State pattern govern order transitions?**
> **A:** The `OrderStateMachine` maintains an explicit state transition table: `Confirmed -> Packed -> Shipped -> Delivered`, and `Confirmed/Packed -> Cancelled`. Any illegal transition throws a domain conflict and returns RFC 7807 409 Conflict.

**Q7: What is the Options pattern, and why did you use it?**
> **A:** The Options pattern maps configuration sections (like `JwtOptions` and `InventoryServiceOptions`) to strongly-typed C# classes, validated at application startup using `.ValidateOnStart()`. It eliminates magic string configuration and ensures missing environment variables fail fast during boot.

---

### 3. Concurrency, Data Integrity, & Compensation
**Q8: How do you prevent overselling when two users try to buy the last product simultaneously?**
> **A:** We use optimistic concurrency backed by PostgreSQL's `xmin` system column. In `InventoryDbContext`, `RowVersion` is mapped to `xmin` as a concurrency token. When two parallel requests read the same product version, the first commit increments `xmin`; the second commit detects the version mismatch and EF Core throws `DbUpdateConcurrencyException`. The handler rolls back and returns a 409 Conflict ProblemDetails.

**Q9: How do you handle failure if reserving stock succeeds but saving the order fails?**
> **A:** We implement a compensating transaction pattern. The `CreateOrderHandler` reserves stock in Inventory. If saving the order to `orders_db` fails, a catch block immediately issues a compensating HTTP call to `/api/inventory/release` with the reserved items, ensuring inventory is restored without manual database intervention.

**Q10: What HTTP resilience policies did you configure for calling Inventory?**
> **A:** Using `Microsoft.Extensions.Http.Resilience`, we configured:
> 1. A 3-second timeout per attempt.
> 2. Exponential backoff retry (2 retries) for transient HTTP errors (5xx, 408).
> 3. Circuit breaker to fail fast if Inventory becomes completely unresponsive, preventing thread pool exhaustion in the Orders service.

---

### 4. PostgreSQL & SQL Optimization
**Q11: Why did you add composite indexes on the Orders table?**
> **A:** The admin dashboard filters orders by `(Status, CreatedAt)` and customers filter by `(CustomerId, CreatedAt)`. Adding composite indexes `("Status", "CreatedAt" DESC)` and `("CustomerId", "CreatedAt" DESC)` transformed expensive sequential scans across 300,000 rows into index scans, reducing query execution time from 64ms to 0.11ms.

**Q12: What is Keyset Pagination, and why is it superior to OFFSET/FETCH?**
> **A:** OFFSET pagination requires PostgreSQL to scan and discard all preceding rows ($O(N)$ complexity), causing latency to degrade significantly on deep pages. Keyset pagination uses a cursor `(CreatedAt, Id)` in the WHERE clause, allowing the database to jump directly to the target row in $O(1)$ time via index scan.

**Q13: Why did you use `AsNoTracking()` and DTO projections for read queries?**
> **A:** In EF Core, change tracking requires allocating identity tracking state in memory for every entity. For read-only list endpoints, `.AsNoTracking()` and projecting directly into DTOs (`.Select(...)`) eliminates change tracking overhead and executes lean SQL selecting only necessary columns.

---

### 5. Authentication, Authorization, & Security
**Q14: Where is the JWT stored in the React application, and what is the trade-off?**
> **A:** The JWT is stored exclusively in memory (React Context). This eliminates XSS vulnerabilities that exploit `localStorage`. The trade-off is that refreshing the browser clears authentication state, requiring a re-login. In an enterprise system, this is typically paired with an HTTP-only refresh cookie.

**Q15: How did you implement login rate limiting?**
> **A:** Using ASP.NET Core's built-in `RateLimiter` middleware with a fixed window of 5 requests per minute per IP address on `/api/auth/login`. Exceeded requests receive an immediate HTTP 429 Too Many Requests response.

**Q16: How do you prevent username enumeration attacks?**
> **A:** On login failure, the system returns a generic 401 response with "Email or password is incorrect", taking care not to distinguish between a non-existent email and a wrong password.

**Q17: What is RFC 7807 ProblemDetails?**
> **A:** ProblemDetails is a standardized JSON schema for HTTP API error responses containing `status`, `title`, `detail`, and custom extension properties (like `correlationId`). It provides consistent error structures across all microservices without exposing internal exception traces.

---

### 6. React 19, TypeScript, & Frontend
**Q18: What is TanStack Query (React Query), and why use it over raw `useEffect`?**
> **A:** TanStack Query handles server state: automatic caching, background refetching, deduplication of duplicate requests, and declarative loading/error states. `useEffect` requires manual state management, race condition handling, and cleanup logic.

**Q19: How do you handle form validation on the client?**
> **A:** We use `react-hook-form` paired with `zod` via `@hookform/resolvers/zod`. Zod schemas provide runtime validation and TypeScript compile-time type inference from a single source of truth.

**Q20: How are protected and admin routes enforced in React?**
> **A:** We created higher-order route components: `ProtectedRoute` verifies the user is authenticated (redirecting to `/login` otherwise), and `AdminRoute` verifies the JWT role claim is `Admin` (redirecting non-admins).

---

### 7. Testing Strategy
**Q21: Why use Testcontainers instead of in-memory EF Core for integration testing?**
> **A:** The EF Core InMemory provider does not enforce relational constraints, foreign keys, transactions, or database-specific features like PostgreSQL's `xmin` concurrency token. Testcontainers spins up a real, throwaway PostgreSQL Docker container for tests, guaranteeing production parity.

**Q22: Why did you choose NSubstitute over Moq?**
> **A:** Moq experienced community controversy and license concerns regarding telemetry tracking. NSubstitute is modern, clean, uses expressive Arrange-Act-Assert syntax (`client.ReserveAsync(default!).Returns(...)`), and has no commercial licensing restrictions.

**Q23: How do you test React components making API calls?**
> **A:** We use Vitest + React Testing Library + Mock Service Worker (MSW). MSW intercepts HTTP requests at the network layer rather than monkey-patching `window.fetch`. This means our component code runs unmodified and tests real user interactions.

---

### 8. DevOps & Production Readiness
**Q24: What is the purpose of multi-stage Docker builds?**
> **A:** The first stage uses the full .NET SDK image to restore, compile, and publish binaries. The final stage copies only the compiled output into a lean ASP.NET runtime image, reducing image size by over 70% and removing build tools from production containers for enhanced security.

**Q25: What is the role of the `X-Correlation-Id` header?**
> **A:** In a distributed system, a single user action may traverse Gateway -> Orders -> Inventory. The correlation ID middleware attaches an `X-Correlation-Id` to incoming requests (or generates one) and includes it in all Serilog log scopes and HTTP responses, allowing engineers to trace a request across all services using a single ID.
