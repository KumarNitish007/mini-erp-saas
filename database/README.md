# Database scripts

These SQL scripts are the source of truth for the MiniERP schema (SQL Server 2019+ / Azure SQL).

| Folder | Applied | Contents |
|---|---|---|
| `migrations/` | once, in name order, recorded in `dbo.SchemaVersions` | tables, constraints, indexes |
| `procedures/` | every startup (`CREATE OR ALTER`) | invoice numbering and dashboard/report procedures |

## How they run

The API embeds this folder and applies it on startup with [DbUp](https://dbup.readthedocs.io/)
(`Database:MigrateOnStartup`, enabled by default). To run them by hand instead, execute the
`migrations` scripts in order, then every file in `procedures`, against an empty database you own.

## Adding a change

1. Never edit a migration that has already shipped. Add the next one, e.g. `0002_add_payments.sql`.
2. Procedures can be edited in place; they are re-applied on every deploy.
3. Keep the EF Core mappings in `src/backend/MiniErp.Infrastructure/Persistence/Configurations.cs` in sync;
   the integration tests run these scripts on a real SQL Server and will fail on a mismatch.

## Procedures

| Procedure | Purpose |
|---|---|
| `usp_Invoice_NextNumber` | Gap-free per-tenant, per-year invoice sequence (called inside the issue transaction) |
| `usp_Dashboard_Kpis` | Revenue this month, outstanding, overdue, drafts, low stock, customers |
| `usp_Report_MonthlySales` | Billing per month for the last N months, zero-filled |
| `usp_Report_LowStock` | Active products at or below their reorder level |
| `usp_Report_TopCustomers` | Customers ranked by billed value since a date |

No server names, credentials or data are stored here. The connection string is supplied per environment
(user-secrets, environment variables, `.env` for Docker, or Azure App Service settings).
