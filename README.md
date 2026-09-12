# SQL Database Script Generator

[![.NET Core Build & Test](https://github.com/pra2011shant/SQL-Database-Script-Generator/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/pra2011shant/SQL-Database-Script-Generator/actions/workflows/build-and-test.yml)
[![Target Framework](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

An advanced, high-performance ASP.NET Core MVC application built with **.NET 8** for generating, parsing, analyzing, and validating Microsoft SQL Server (T-SQL) database scripts.

---

## 🌟 Key Features

- **Deep T-SQL Parsing & Validation**: Utilizes `Microsoft.SqlServer.TransactSql.ScriptDom` for abstract syntax tree (AST) generation, syntax error pinpointing (line and column diagnostics), and compliance validation.
- **Automated SQL Formatting & Beautification**: Consistent keyword casing (uppercase), structured indentation, and statement separation.
- **Batch & Statement Metrics**: Analyzes script complexity by counting total SQL batches and statements.
- **Clean Architecture Design**: Decoupled folder and layer architecture (Models, Services, Controllers, Views) for scalability and testability.
- **CI/CD Automation**: Continuous integration pipeline with GitHub Actions verifying every push and pull request.

---

## 🏗️ Architecture & Project Structure

The project follows Clean Architecture principles:

```
SQLDatabaseScriptGenerator/
│
├── .github/
│   └── workflows/
│       └── build-and-test.yml    # CI/CD Workflow for GitHub Actions
│
├── Controllers/
│   └── HomeController.cs         # MVC controller managing user interactions
│
├── Models/
│   ├── ErrorViewModel.cs         # Error handling view model
│   ├── SqlParseError.cs          # Syntax error diagnostic details (line, col, message)
│   └── SqlValidationResult.cs    # Result object containing AST metrics and errors
│
├── Services/
│   ├── ISqlParserService.cs      # Contract for SQL parsing, validation, and formatting
│   └── SqlParserService.cs       # ScriptDom parser and generator implementation
│
├── Views/
│   ├── Home/                     # Razor views for script generation and validation UI
│   └── Shared/                   # Layouts and partials
│
├── wwwroot/                      # Static assets (CSS, JavaScript, Libraries)
├── Program.cs                    # Application bootstrapping & DI configuration
├── SQLDatabaseScriptGenerator.csproj
└── README.md
```

---

## 🧰 Tech Stack & Dependencies

| Technology / Package | Version | Purpose |
| :--- | :--- | :--- |
| **.NET** | `8.0` | Modern LTS framework runtime |
| **ASP.NET Core MVC** | `8.0` | Web architecture pattern |
| **Microsoft.SqlServer.TransactSql.ScriptDom** | `180.107.0` | In-depth SQL parsing, AST navigation, and script generation |
| **Newtonsoft.Json** | `13.0.4` | JSON payload serialization and configuration management |

---

## 🚀 Getting Started

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Visual Studio 2022 (v17.8+)](https://visualstudio.microsoft.com/) or [Visual Studio Code](https://code.visualstudio.com/) with C# Dev Kit

### Installation & Run

1. **Clone the repository:**
   ```bash
   git clone https://github.com/pra2011shant/SQL-Database-Script-Generator.git
   cd SQL-Database-Script-Generator
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

## 🔄 CI/CD Pipeline

The project includes an automated GitHub Actions workflow defined in `.github/workflows/build-and-test.yml` that:
- Runs automatically on every `push` and `pull_request` to `main` / `master`
- Restores all dependencies with .NET 8.0 SDK
- Compiles the solution in `Release` mode
- Executes unit and integration tests

---

## 📄 License

This project is licensed under the MIT License.
