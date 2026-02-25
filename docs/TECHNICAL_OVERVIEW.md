# InvoiceDesk Technical Overview

## Overview
InvoiceDesk is a .NET 8 WPF desktop app for multi-company invoice management with EF Core (SQL Server), MVVM, RESX localization (English/Bulgarian), and WebView2-based PDF export.

## Architecture
- Pattern: MVVM with CommunityToolkit.Mvvm; async commands and observable properties drive bindings.
- Data: SQL Server via AppDbContext/IDbContextFactory; migrations under Migrations; AppDbInitializer ensures database exists, applies migrations, backfills missing invoice numbers, seeds a default company.
- Services: business logic/orchestration under Services; ICompanyContext scopes queries per company for multi-tenant isolation.
- Rendering: HTML-to-PDF templating in Rendering/InvoiceHtmlRenderer.cs; WebView2 prints to PDF with deterministic formatting and dual-currency notes when enabled.
- UI: WPF windows/views under Views driven by ViewModels; culture changes propagate through LocalizedStrings helper and binding language updates.
- Helpers: Localization, currency dual-display, hashing, logging, VAT options under Helpers; resources in Resources.

## Key Services
- InvoiceService: draft creation/editing, issuing (transactional number allocation), totals calculation, immutability of issued invoices.
- PdfExportService: orchestrates WebView2 host, renders HTML, writes PDFs to disk and stores issued PDFs plus hashes in the database.
- PdfSigningService: applies KEP/QES signatures using certificates from the Windows store (smart cards/tokens); stores signed bytes + SHA-256.
- DatabaseBackupService: creates and restores compressed .bak files inside .zip archives; validates headers and uses compression when supported.
- InvoiceQueryService: filtering/searching invoices with company scoping.
- UserSettingsService/LanguageService: persists user-selected culture and preferences.

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
- Export pipeline and file output in Services/PdfExportService.cs; default output directory is exports, and issued PDFs are cached in the database with hashes/timestamps; PdfSigningService adds optional KEP/QES signatures.

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
