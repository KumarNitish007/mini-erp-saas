# Design decisions

Short notes on the choices that shape this codebase and the alternatives I turned down.

## 1. The schema lives in SQL scripts, applied with DbUp

**Decision:** `database/migrations/*.sql` (run once, journaled) and `database/procedures/*.sql` (`CREATE OR ALTER`, re-applied every deploy) are the single source of truth. They are embedded in the Infrastructure assembly and applied at startup by `DatabaseMigrator`. EF Core maps to the tables but never generates them.

**Why:** In ERP teams the database is shared with DBAs, reports and integrations. Reviewable, hand-written DDL gives exact control over constraints (`CHECK`, filtered unique index on invoice number), indexes and stored procedures, and the same scripts can be run by hand on any server. EF migrations would add a second, generated definition of the schema that drifts from the stored procedures.

**Trade-off:** Entity mappings must be kept in line with the scripts manually; the integration tests catch any mismatch because they run the real scripts on a real SQL Server.

## 2. EF Core for writes, Dapper + stored procedures for reads

**Decision:** Commands (create product, issue invoice...) go through EF Core aggregates with change tracking. Dashboard and reporting queries are stored procedures called with Dapper.

**Why:** Writes benefit from the domain model enforcing invariants, plus optimistic concurrency (`ROWVERSION`). Reads for dashboards are aggregate-heavy, and set-based T-SQL is faster, easier to tune with execution plans and returns exactly the shape the UI needs in one round trip.

## 3. Tenant isolation in one place

**Decision:** Every tenant-owned entity implements `ITenantOwned`. `AppDbContext`:

- adds a **global query filter** `TenantId == CurrentTenantId` to each of them. When nobody is signed in the tenant is `Guid.Empty`, so a forgotten `[Authorize]` returns nothing rather than everything.
- on save, **stamps** `TenantId` on new rows and **throws** if a row is added for another tenant or a `TenantId` is modified.

Stored procedures take `@TenantId` explicitly. The single intentional bypass (`IgnoreQueryFilters`) is login, where the tenant isn't known yet.

**Alternatives considered:** a database per tenant (strong isolation, but heavy for a small-business SaaS), or SQL Server Row-Level Security (a good next step as defence in depth; the `TenantId` column design is already compatible).

## 4. Gap-free invoice numbers

**Decision:** Drafts have no number. `IssueAsync` runs in one transaction that calls `dbo.usp_Invoice_NextNumber` (an `UPDLOCK, HOLDLOCK` read-increment on `InvoiceSequences`), deducts stock, writes the stock ledger and commits.

**Why:** Indian GST rules expect consecutive invoice numbers per financial series. Because number allocation shares the transaction, a failed issue (for example insufficient stock) rolls the counter back too, so no number is burned. There's an integration test for exactly that. Concurrent issuers queue on the key lock instead of colliding.

## 5. GST maths in the domain, mirrored in the UI

**Decision:** `GstCalculator` rounds each line to paise (half away from zero). For intra-state supply the tax is split as `CGST = round(tax / 2)` and `SGST = tax - CGST`. The Angular editor has a tiny mirror (`core/gst.ts`) for the live preview, but the server always recalculates and stores the final figures.

**Why:** It guarantees `CGST + SGST == total tax` to the paisa and makes the browser preview match the PDF.

## 6. Plain application services instead of MediatR / CQRS libraries

**Decision:** One service class per feature (`InvoiceService`, `ProductService`...), injected into thin controllers.

**Why:** For this size the indirection of a mediator adds ceremony without benefit. The services are still easy to test, and the boundaries (validation, transactions, mapping) are explicit. If cross-cutting pipelines grow (auditing, caching), introducing a mediator is a mechanical change.

## 7. Secrets and configuration

- No connection strings, passwords or keys are committed. `appsettings.json` has empty placeholders; the Development file points at the per-developer LocalDB instance only.
- `Jwt:Key` is required at startup (validated, at least 32 characters). Development alone falls back to a random key per process so the app runs with zero setup.
- Docker reads secrets from a git-ignored `.env` (see `.env.example`); Azure takes them as `@secure()` Bicep parameters.

## 8. Testing strategy

- **Unit tests** cover the domain (invoice lifecycle, stock, GST rounding), validators and Indian number formatting: fast, with no I/O.
- **Integration tests** boot the real API with `WebApplicationFactory` against a real SQL Server (Testcontainers in CI). That exercises the DbUp scripts, stored procedures, EF mappings, auth policies and tenant filters together. An in-memory provider was rejected because it cannot run T-SQL or enforce constraints.
