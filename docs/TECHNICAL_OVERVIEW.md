# InvoiceDesk Technical Overview

## Overview
InvoiceDesk is a .NET 8 WPF desktop app for multi-company invoice management with EF Core (SQL Server), MVVM, RESX localization (English/Bulgarian), and WebView2-based PDF export.

## Architecture
- Pattern: MVVM with CommunityToolkit.Mvvm.
- Data: SQL Server via AppDbContext; migrations under Migrations; AppDbInitializer ensures database exists and seeds defaults.
- Services: business logic and orchestration under Services; queries scoped by ICompanyContext to enforce tenant isolation.
- Rendering: HTML-to-PDF templating in Rendering/InvoiceHtmlRenderer.cs.
- UI: WPF windows and views under Views, driven by ViewModels.
- Helpers: Localization and utilities under Helpers; resources in Resources.

## Configuration
Primary settings live in appsettings.json:
- ConnectionStrings:Default (SQL Server)
- Culture (default UI culture)
- Pdf:OutputDirectory (defaults to exports under workspace)
- Logging:FilePath and log levels
appsettings.json is copied to the output on build, so edits affect runtime.

## Build and Run
Prerequisites: .NET 8 SDK, SQL Server (LocalDB/Express/remote), Microsoft Edge WebView2 Runtime (Evergreen).
- Build: dotnet build InvoiceDesk.sln
- Run: dotnet run --project InvoiceDesk/InvoiceDesk.csproj
VS Code tasks available: build, publish, watch.

## Database
- EF Core 8 with SQL Server provider.
- Migrations stored under Migrations; snapshot in AppDbContextModelSnapshot.cs.
- AppDbInitializer creates the database if missing, applies migrations, repairs missing invoice numbers, and seeds a default company when empty.
- Typical workflow: dotnet ef migrations add <Name> then dotnet ef database update.

## Domain Model
Core entities in Models: Company, Customer, Invoice, InvoiceLine, VatType, InvoiceStatus, CountryOption, CultureOption.
Business rules live primarily in Services/InvoiceService.cs (draft creation, issuing, totals) and related services. Issued invoices are immutable and include stored PDFs/metadata.

## Localization
- RESX resources in Resources with en/bg; generators configured in InvoiceDesk.csproj.
- Helper: Helpers/LocalizedStrings.cs.
- Satellite languages limited to en and bg (SatelliteResourceLanguages set in the csproj).

## UI Layer
- Views/Windows under Views; composed by MainWindow and MainViewModel.
- Uses async commands and binding-friendly view models to load companies, customers, invoices, and apply culture changes.

## PDF Export
- HTML-to-PDF via WebView2.
- Template rendering in Rendering/InvoiceHtmlRenderer.cs.
- Export pipeline and file output in Services/PdfExportService.cs; default output directory is exports, and issued PDFs are cached in the database with hashes/timestamps.

## Logging
- File logger writing to logs/app.log (Helpers/FileLogger.cs), plus binding trace wiring in App.xaml.cs.
- Levels configurable in appsettings.json; fallback logging enabled during bootstrap to capture early failures.

## Notable Helpers
- Status localization converter: Helpers/StatusToLocalizedConverter.cs.
- Hashing and option helpers: Helpers/HashHelper.cs, Helpers/VatTypeOption.cs, Helpers/CountryOption.cs, Helpers/CultureOption.cs.

## Testing and Reliability
- No test suite present in the current snapshot.
- Recommend adding unit and integration tests around InvoiceService, PdfExportService, and database migrations; consider UI automation for critical flows (invoice issue/export, culture switching, company/customer CRUD).

## Deployment
- Publish via VS Code publish task (dotnet publish on the solution).
- Ensure appsettings connection strings, culture defaults, and logging paths are set per environment.
- Target machines must have the WebView2 Runtime installed.
