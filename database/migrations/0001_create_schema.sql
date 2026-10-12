/* =====================================================================
   MiniERP - 0001 Create schema
   Multi-tenant: every business table carries TenantId, and every query
   (EF Core global filter + stored procedures) is scoped by it.
   Applied once by DbUp (journal table: dbo.SchemaVersions).
   ===================================================================== */

CREATE TABLE dbo.Tenants
(
    Id            UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Tenants PRIMARY KEY,
    Name          NVARCHAR(200)    NOT NULL,
    Slug          NVARCHAR(80)     NOT NULL,
    Gstin         NVARCHAR(15)     NULL,
    StateCode     CHAR(2)          NOT NULL,
    Address       NVARCHAR(500)    NULL,
    CreatedAtUtc  DATETIME2(3)     NOT NULL CONSTRAINT DF_Tenants_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_Tenants_Slug UNIQUE (Slug)
);

CREATE TABLE dbo.Users
(
    Id            UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
    TenantId      UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Users_Tenants REFERENCES dbo.Tenants (Id),
    Email         NVARCHAR(256)    NOT NULL,
    FullName      NVARCHAR(150)    NOT NULL,
    PasswordHash  NVARCHAR(500)    NOT NULL,
    Role          NVARCHAR(20)     NOT NULL CONSTRAINT CK_Users_Role CHECK (Role IN (N'Owner', N'Admin', N'Staff')),
    IsActive      BIT              NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT (1),
    CreatedAtUtc  DATETIME2(3)     NOT NULL CONSTRAINT DF_Users_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc  DATETIME2(3)     NULL,
    CONSTRAINT UQ_Users_Email UNIQUE (Email)
);
CREATE INDEX IX_Users_TenantId ON dbo.Users (TenantId);

CREATE TABLE dbo.Products
(
    Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Products PRIMARY KEY,
    TenantId       UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Products_Tenants REFERENCES dbo.Tenants (Id),
    Sku            NVARCHAR(50)     NOT NULL,
    Name           NVARCHAR(200)    NOT NULL,
    Unit           NVARCHAR(20)     NOT NULL,
    HsnCode        NVARCHAR(10)     NULL,
    UnitPrice      DECIMAL(18, 2)   NOT NULL CONSTRAINT CK_Products_UnitPrice CHECK (UnitPrice >= 0),
    GstRate        DECIMAL(5, 2)    NOT NULL CONSTRAINT CK_Products_GstRate CHECK (GstRate IN (0, 0.25, 3, 5, 12, 18, 28)),
    StockQuantity  DECIMAL(18, 3)   NOT NULL CONSTRAINT CK_Products_Stock CHECK (StockQuantity >= 0),
    ReorderLevel   DECIMAL(18, 3)   NOT NULL CONSTRAINT DF_Products_ReorderLevel DEFAULT (0),
    IsActive       BIT              NOT NULL CONSTRAINT DF_Products_IsActive DEFAULT (1),
    CreatedAtUtc   DATETIME2(3)     NOT NULL CONSTRAINT DF_Products_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc   DATETIME2(3)     NULL,
    RowVersion     ROWVERSION       NOT NULL,
    CONSTRAINT UQ_Products_Tenant_Sku UNIQUE (TenantId, Sku)
);
CREATE INDEX IX_Products_Tenant_Name ON dbo.Products (TenantId, Name) INCLUDE (Sku, UnitPrice, StockQuantity, IsActive);

CREATE TABLE dbo.Customers
(
    Id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Customers PRIMARY KEY,
    TenantId        UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Customers_Tenants REFERENCES dbo.Tenants (Id),
    Name            NVARCHAR(200)    NOT NULL,
    Email           NVARCHAR(256)    NULL,
    Phone           NVARCHAR(20)     NULL,
    Gstin           NVARCHAR(15)     NULL,
    StateCode       CHAR(2)          NOT NULL,
    BillingAddress  NVARCHAR(500)    NULL,
    IsActive        BIT              NOT NULL CONSTRAINT DF_Customers_IsActive DEFAULT (1),
    CreatedAtUtc    DATETIME2(3)     NOT NULL CONSTRAINT DF_Customers_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc    DATETIME2(3)     NULL
);
CREATE INDEX IX_Customers_Tenant_Name ON dbo.Customers (TenantId, Name);

CREATE TABLE dbo.InvoiceSequences
(
    TenantId    UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_InvoiceSequences_Tenants REFERENCES dbo.Tenants (Id),
    [Year]      INT              NOT NULL,
    LastNumber  INT              NOT NULL,
    CONSTRAINT PK_InvoiceSequences PRIMARY KEY (TenantId, [Year])
);

CREATE TABLE dbo.Invoices
(
    Id            UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Invoices PRIMARY KEY,
    TenantId      UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Invoices_Tenants REFERENCES dbo.Tenants (Id),
    Number        NVARCHAR(30)     NULL,  -- assigned when issued, so drafts never burn numbers
    CustomerId    UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Invoices_Customers REFERENCES dbo.Customers (Id),
    InvoiceDate   DATE             NOT NULL,
    DueDate       DATE             NOT NULL,
    Status        NVARCHAR(20)     NOT NULL CONSTRAINT CK_Invoices_Status CHECK (Status IN (N'Draft', N'Issued', N'Paid', N'Cancelled')),
    IsInterState  BIT              NOT NULL,
    SubTotal      DECIMAL(18, 2)   NOT NULL,
    CgstAmount    DECIMAL(18, 2)   NOT NULL,
    SgstAmount    DECIMAL(18, 2)   NOT NULL,
    IgstAmount    DECIMAL(18, 2)   NOT NULL,
    GrandTotal    DECIMAL(18, 2)   NOT NULL,
    Notes         NVARCHAR(1000)   NULL,
    IssuedAtUtc   DATETIME2(3)     NULL,
    PaidAtUtc     DATETIME2(3)     NULL,
    CreatedAtUtc  DATETIME2(3)     NOT NULL CONSTRAINT DF_Invoices_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc  DATETIME2(3)     NULL,
    RowVersion    ROWVERSION       NOT NULL,
    CONSTRAINT CK_Invoices_DueDate CHECK (DueDate >= InvoiceDate)
);
CREATE UNIQUE INDEX UX_Invoices_Tenant_Number ON dbo.Invoices (TenantId, Number) WHERE Number IS NOT NULL;
CREATE INDEX IX_Invoices_Tenant_Status_Date ON dbo.Invoices (TenantId, Status, InvoiceDate) INCLUDE (CustomerId, GrandTotal, DueDate);
CREATE INDEX IX_Invoices_CustomerId ON dbo.Invoices (CustomerId);

CREATE TABLE dbo.InvoiceLines
(
    Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_InvoiceLines PRIMARY KEY,
    InvoiceId      UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_InvoiceLines_Invoices REFERENCES dbo.Invoices (Id) ON DELETE CASCADE,
    ProductId      UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_InvoiceLines_Products REFERENCES dbo.Products (Id),
    Description    NVARCHAR(300)    NOT NULL,
    Quantity       DECIMAL(18, 3)   NOT NULL CONSTRAINT CK_InvoiceLines_Quantity CHECK (Quantity > 0),
    UnitPrice      DECIMAL(18, 2)   NOT NULL,
    GstRate        DECIMAL(5, 2)    NOT NULL,
    TaxableAmount  DECIMAL(18, 2)   NOT NULL,
    TaxAmount      DECIMAL(18, 2)   NOT NULL,
    LineTotal      DECIMAL(18, 2)   NOT NULL,
    SortOrder      INT              NOT NULL
);
CREATE INDEX IX_InvoiceLines_InvoiceId ON dbo.InvoiceLines (InvoiceId);
CREATE INDEX IX_InvoiceLines_ProductId ON dbo.InvoiceLines (ProductId);

CREATE TABLE dbo.StockMovements
(
    Id            UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_StockMovements PRIMARY KEY,
    TenantId      UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_StockMovements_Tenants REFERENCES dbo.Tenants (Id),
    ProductId     UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_StockMovements_Products REFERENCES dbo.Products (Id),
    Quantity      DECIMAL(18, 3)   NOT NULL,  -- signed: +in / -out
    Type          NVARCHAR(20)     NOT NULL CONSTRAINT CK_StockMovements_Type CHECK (Type IN (N'Opening', N'Purchase', N'Sale', N'SaleReversal', N'Adjustment')),
    Reference     NVARCHAR(100)    NULL,
    CreatedAtUtc  DATETIME2(3)     NOT NULL CONSTRAINT DF_StockMovements_CreatedAtUtc DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_StockMovements_Tenant_Product ON dbo.StockMovements (TenantId, ProductId, CreatedAtUtc);
