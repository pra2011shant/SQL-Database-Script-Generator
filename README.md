# SQL Database Script Generator 🚀

[![.NET Core Build & Test](https://github.com/pra2011shant/SQL-Database-Script-Generator/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/pra2011shant/SQL-Database-Script-Generator/actions/workflows/build-and-test.yml)
[![Target Framework](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![GitHub Repository](https://img.shields.io/badge/GitHub-SQL--Database--Script--Generator-181717?logo=github)](https://github.com/pra2011shant/SQL-Database-Script-Generator)

An enterprise-grade, high-performance ASP.NET Core MVC application built with **.NET 8** for parsing, analyzing, optimizing, transpiling, and generating Microsoft SQL Server (T-SQL) database scripts with AI-powered assistance, visual diffing, and dynamic ER diagrams.

---

## 🌟 Key Features

### 1. 🔍 Deep T-SQL AST Parsing & Diagnostics
- Uses `Microsoft.SqlServer.TransactSql.ScriptDom (TSql160)` to parse SQL scripts into an Abstract Syntax Tree (AST).
- Detects exact syntax errors with line and column precision before execution.
- Generates clean, standardized, beautified T-SQL with configurable keyword casing and indentation.

### 2. ⚡ Groq Cloud AI (Llama 3.3 70B) & Deterministic Fallback
- Lightning-fast cloud AI generation via **Groq LPU** running **`llama-3.3-70b-versatile`**.
- Native **multilingual support**: understands prompts in Hindi, Hinglish, Roman Hindi, English, and converts conversational requests directly into production-grade T-SQL.
- **100% Offline Fallback Mechanism**: If no API key is provided or the connection is offline, the system automatically uses deterministic ScriptDom AST rules to optimize, fix errors, and generate stored procedures without interruption.

### 3. ⚖️ Monaco Side-by-Side Diff Viewer
- Integrated `monaco.editor.createDiffEditor` providing side-by-side visual diffing between input queries and generated/optimized SQL.
- Highlights additions (green) and deletions (red) in real-time.

### 4. 📊 Dynamic Visual Schema & ER Diagrams
- Automatically inspects table definitions and columns to generate **Mermaid.js Entity Relationship Diagrams (`erDiagram`)**.
- Visualizes primary keys, data types, and entity relationships dynamically.

### 5. 🏢 Corporate SQL Standards Manager (Lightweight RAG Engine)
- Configure and persist company-wide SQL standards (e.g., custom schema prefixes like `dbo.`, mandatory audit columns like `CreatedAt`/`IsDeleted`, indexing policies, and keyword casing).
- Injects organizational standards into AI prompts and AST generation pipelines to ensure compliance.

### 6. 🔄 Multi-Database SQL Transpiler
- Instantly transpile T-SQL scripts into other popular database dialects:
  - **PostgreSQL** (`PL/pgSQL`, `TIMESTAMP`, `BOOLEAN`, `SERIAL`)
  - **MySQL** (`AUTO_INCREMENT`, `DATETIME`, backtick escaping)
  - **Oracle** (`PL/SQL`, `NUMBER`, `VARCHAR2`, `SYSDATE`)

### 7. 🔌 Live Database Read-Only Schema Inspector
- Connect safely to live SQL Server instances using `Microsoft.Data.SqlClient`.
- Inspect existing tables and extract clean DDL definitions directly into Monaco Editor with a single click.

### 8. 🛡️ Clean & Responsive Architecture
- **Zero-Hardcoding Policy**: All stored procedures, CRUD operations, indexes, and mock data queries are generated dynamically from parsed column metadata.
- **Non-Blocking UI**: Asynchronous AJAX processing, 150ms input debouncing, request cancellation via `AbortController`, and client-side template caching.

---

## 🏗️ Architecture & Project Structure

```
SQLDatabaseScriptGenerator/
│
├── .github/
│   └── workflows/
│       └── build-and-test.yml          # GitHub Actions CI/CD pipeline
│
├── Controllers/
│   └── HomeController.cs               # Central API & MVC controller for processing requests
│
├── Models/
│   ├── AiSettings.cs                   # Groq AI settings model
│   ├── CorporateSqlStandards.cs        # Corporate SQL policy and standards model
│   ├── LiveDbInspectRequest.cs         # Connection and inspection request models
│   ├── ScriptRequestModel.cs           # User payload for SQL actions and generator modes
│   ├── ScriptResponseModel.cs          # Structured JSON response (SQL, diff, ER, transpilations)
│   ├── SqlParseError.cs                # Syntax error diagnostic details (line, col, message)
│   ├── SqlValidationResult.cs          # AST metrics, table metadata, and validation info
│   └── TableMetadata.cs                # Dynamic table columns, PKs, and data types
│
├── Services/
│   ├── AiConfigurationService.cs       # Thread-safe Groq settings manager
│   ├── GroqService.cs                  # Groq Cloud API HTTP client (Llama 3.3 70B)
│   ├── IAiCompletionService.cs         # AI completion contract
│   ├── ILiveDatabaseInspectorService.cs# Live SQL Server read-only inspection contract
│   ├── LiveDatabaseInspectorService.cs # Live database schema reader implementation
│   ├── ISchemaVisualizerService.cs     # Mermaid ER diagram generation contract
│   ├── SchemaVisualizerService.cs      # Mermaid.js ER diagram builder
│   ├── ISqlEngineService.cs            # Master orchestrator for analysis and generation
│   ├── SqlEngineService.cs             # Workflow engine connecting AST, AI, Transpiler, and ER
│   ├── ISqlParserService.cs            # T-SQL ScriptDom parsing & formatting contract
│   ├── SqlParserService.cs             # AST parsing, table extraction, and fallback generator
│   ├── ISqlStandardsService.cs         # Corporate standards RAG engine contract
│   ├── SqlStandardsService.cs          # Thread-safe in-memory standards manager
│   ├── ISqlTranspilerService.cs        # Multi-database dialect transpiler contract
│   └── SqlTranspilerService.cs         # T-SQL to Postgres/MySQL/Oracle converter
│
├── Views/
│   ├── Home/
│   │   └── Index.cshtml                # Main interactive dashboard with Monaco editor and tabs
│   └── Shared/
│       ├── _Layout.cshtml              # Master layout with modals (Live DB, Corporate Policy, Groq)
│       └── Error.cshtml                # Friendly error view
│
├── wwwroot/
│   ├── css/
│   │   └── site.css                    # Modern UI styles, dark mode, and responsive layout
│   └── js/
│       └── sql-generator.js            # Non-blocking client-side orchestration, Monaco & Mermaid
│
├── appsettings.json                    # Configuration (Groq Cloud defaults)
├── Program.cs                          # Application bootstrapping & Dependency Injection
└── SQLDatabaseScriptGenerator.csproj   # Project dependencies and targets
```

---

## 🧰 Tech Stack & Dependencies

| Technology / Package | Version | Purpose |
| :--- | :--- | :--- |
| **.NET** | `8.0 (LTS)` | High-performance C# runtime |
| **ASP.NET Core MVC** | `8.0` | Clean web architecture |
| **Groq Cloud API** | `llama-3.3-70b` | Ultra-fast natural language to SQL AI inference |
| **Microsoft.SqlServer.TransactSql.ScriptDom** | `180.107.0` | AST parsing, validation, and deterministic T-SQL generation |
| **Microsoft.Data.SqlClient** | `7.0.3` | Safe read-only live SQL Server schema inspection |
| **Newtonsoft.Json** | `13.0.4` | JSON payload serialization |
| **Monaco Editor** | `0.45.0 (CDN)` | Full-featured code editor with syntax highlighting & Diff Viewer |
| **Mermaid.js** | `10.8.0 (CDN)` | Dynamic client-side Entity Relationship (ER) diagrams |
| **Bootstrap** | `5.3.0` | Responsive layout and dark modern UI components |

---

## 🚀 Getting Started

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Visual Studio 2022 (v17.8+)](https://visualstudio.microsoft.com/) or [Visual Studio Code](https://code.visualstudio.com/)
- *(Optional)* Free Groq API Key from [console.groq.com/keys](https://console.groq.com/keys)

### Installation & Run

1. **Clone the repository:**
   ```bash
   git clone https://github.com/pra2011shant/SQL-Database-Script-Generator.git
   cd SQLDatabaseScriptGenerator/SQLDatabaseScriptGenerator
   ```

2. **Restore dependencies:**
   ```bash
   dotnet restore
   ```

3. **Build the solution:**
   ```bash
   dotnet build
   ```

4. **Run the application:**
   ```bash
   dotnet run
   ```
   Open your browser and navigate to `https://localhost:5001` or `http://localhost:5000`.

---

## ⚡ Groq Cloud AI Setup (Optional)

To enable AI-powered SQL generation and multilingual parsing:
1. Get a free API key from [Groq Console](https://console.groq.com/keys).
2. Click the **AI: Groq** button in the top navigation bar of the application.
3. Paste your Groq API key and click **Save Groq Key**.
4. You can now write prompts in Hindi, Hinglish, or English.
5. If no API key is provided, the application automatically uses deterministic AST offline generation.

---

## 🔄 CI/CD Pipeline

The project includes an automated GitHub Actions workflow defined in `.github/workflows/build-and-test.yml` that:
- Triggers on every `push` and `pull_request` to `main` and `master`.
- Sets up .NET 8.0 SDK on Ubuntu runner.
- Restores, builds, and verifies all project assemblies in `Release` configuration.

---

## 📄 License

This project is licensed under the MIT License.
