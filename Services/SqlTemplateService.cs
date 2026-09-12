namespace SQLDatabaseScriptGenerator.Services;

public class SqlTemplateService : ISqlTemplateService
{
    private static readonly Dictionary<string, string> Templates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["schema_customers"] = @"-- Table Schema: Customers & Orders
CREATE TABLE Customers (
    CustomerID INT IDENTITY(1,1) PRIMARY KEY,
    FirstName NVARCHAR(50) NOT NULL,
    LastName NVARCHAR(50) NOT NULL,
    Email NVARCHAR(100) UNIQUE NOT NULL,
    PhoneNumber VARCHAR(20) NULL,
    CreatedAt DATETIME2 DEFAULT GETUTCDATE()
);

CREATE TABLE Orders (
    OrderID INT IDENTITY(1001,1) PRIMARY KEY,
    CustomerID INT NOT NULL FOREIGN KEY REFERENCES Customers(CustomerID),
    OrderDate DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    TotalAmount DECIMAL(18, 2) NOT NULL CHECK (TotalAmount >= 0),
    Status VARCHAR(20) NOT NULL DEFAULT 'Pending'
);",

        ["buggy_sql"] = @"-- Buggy SQL Query with Syntax & Logical Errors
SELEC CustomerID, FirstName LastName, SUM(TotalAmount
FROM Customers
INNER JOI Orders ON Customers.CustomerID = Orders.CustID
WHER Status = 'Completed' AND OrderDate >= '2026-01-01'
GROUP Customers.CustomerID
HAVING SUM(TotalAmount) > 500
ORDER BY CreatedDate DESC",

        ["optimize_query"] = @"-- Unoptimized Query Needing Tuning & Indexes
SELECT c.CustomerID, c.FirstName, c.LastName, o.OrderID, o.OrderDate, o.TotalAmount
FROM Customers c
LEFT JOIN Orders o ON c.CustomerID = o.CustomerID
WHERE YEAR(o.OrderDate) = 2026
  AND o.Status LIKE '%Completed%'
  AND (SELECT COUNT(*) FROM Orders o2 WHERE o2.CustomerID = c.CustomerID) > 5
ORDER BY o.TotalAmount DESC;",

        ["stored_proc_input"] = @"-- Input Table: Products & Inventory
CREATE TABLE Products (
    ProductID INT IDENTITY(1,1) PRIMARY KEY,
    SKU NVARCHAR(50) NOT NULL UNIQUE,
    ProductName NVARCHAR(150) NOT NULL,
    Category NVARCHAR(50) NOT NULL,
    UnitPrice DECIMAL(18,2) NOT NULL,
    StockQuantity INT NOT NULL DEFAULT 0,
    IsActive BIT NOT NULL DEFAULT 1,
    ModifiedDate DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);"
    };

    public Dictionary<string, string> GetAllTemplates() => Templates;

    public string? GetTemplateByKey(string key)
    {
        return Templates.TryGetValue(key, out var template) ? template : null;
    }
}
