# Security & OWASP Top 10 Mapping

This document details how ShipTrack addresses common security concerns and identifies honest architectural trade-offs.

## 1. OWASP Top 10 Mapping

### A01: Broken Access Control
- **Implementation:**
  - Role-based authorization policies (`Authenticated`, `AdminOnly`) configured in each service.
  - Ownership validation on sensitive endpoints: `/api/orders/{id}` and `/api/orders/{id}/cancel` ensure a Customer can only read or cancel their own orders unless the caller has the `Admin` role.
  - Gateway hides internal inventory endpoints: `/api/inventory/*` (reserve and release) are blocked at the YARP gateway and require the `X-Internal-Key` secret header.

### A02: Cryptographic Failures
- **Implementation:**
  - Passwords are never stored in plain text. Hashed using ASP.NET Core `PasswordHasher<AppUser>` implementing PBKDF2 with HMAC-SHA256 and salted per user.
  - JWTs are signed with HMAC-SHA256 using a symmetric key of at least 32 characters configured via environment variables.

### A03: Injection
- **Implementation:**
  - Entity Framework Core with parameterised queries is used exclusively. No raw string-concatenated SQL queries exist in the codebase.
  - Input validation is applied before reaching domain logic using FluentValidation.

### A04: Insecure Design
- **Implementation:**
  - State machine enforces valid order lifecycle transitions (`Confirmed` -> `Packed` -> `Shipped` -> `Delivered`).
  - Optimistic concurrency control (`xmin`) prevents race conditions and overselling in inventory reservations.

### A05: Security Misconfiguration
- **Implementation:**
  - Centralised `GlobalExceptionHandler` returning standard RFC 7807 `ProblemDetails`. Internal stack traces are never sent to clients.
  - CORS policies strictly restrict origins to the frontend host (`FRONTEND_ORIGIN`).
  - Docker containers run as unprivileged non-root users (`appuser`).

### A06: Vulnerable and Outdated Components
- **Implementation:**
  - Central Package Management (`Directory.Packages.props`) maintains single versions of all dependencies across all projects.

### A07: Identification and Authentication Failures
- **Implementation:**
  - Login endpoint is rate-limited using ASP.NET Core's built-in RateLimiter (5 attempts per minute per IP) to mitigate brute-force attacks.
  - Generic error messages ("Email or password is incorrect") are returned on login failure to prevent username enumeration.

### A08: Software and Data Integrity Failures
- **Implementation:**
  - CI workflows run automated unit, integration, and frontend tests prior to image build.

### A09: Security Logging and Monitoring Failures
- **Implementation:**
  - Every HTTP request receives an `X-Correlation-Id` passed through the gateway to internal services and recorded in log contexts.
  - Sensitive details (passwords, JWT secrets) are omitted from log payloads.

### A10: Server-Side Request Forgery (SSRF)
- **Implementation:**
  - Microservices only communicate with explicit internal URLs configured via typed options and Docker DNS.

---

## 2. Known Trade-Offs & Gaps
1. **In-Memory JWT Storage:** The React client stores the JWT in memory (React Context). Refreshing the page clears the token, requiring the user to re-authenticate. While this avoids XSS extraction from `localStorage`, in a high-scale production app a secure HTTP-only refresh cookie flow with token rotation would be implemented.
2. **Symmetric JWT Key:** Backend microservices share a symmetric signing key. In an enterprise organization with independent teams, asymmetric keys (RS256 / ECDSA) with a public JWKS endpoint on Identity.Api would allow services to verify tokens without sharing the private signing key.
