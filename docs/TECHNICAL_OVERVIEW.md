# InvoiceDesk Technical Overview

## Overview
InvoiceDesk is a .NET 8 WPF desktop app for multi-company invoice management with EF Core (MySQL), MVVM, RESX localization (English/Bulgarian), and WebView2-based PDF export.

## Architecture
- Pattern: MVVM with CommunityToolkit.Mvvm.
- Data: MySQL via AppDbContext; migrations under Migrations.
- Services: business logic and orchestration under Services.
- Rendering: HTML-to-PDF templating in Rendering/InvoiceHtmlRenderer.cs.
- UI: WPF windows and views under Views, driven by ViewModels.
- Helpers: Localization and utilities under Helpers; resources in Resources.

## Configuration
Primary settings live in appsettings.json:
- ConnectionStrings:Default
- Culture (default UI culture)
- Pdf:OutputDirectory (defaults to Exports)
- Logging:FilePath and log levels
appsettings.json is copied to the output on build, so edits affect runtime.

## Build and Run
Prerequisites: .NET 8 SDK, MySQL 8, Microsoft Edge WebView2 Runtime (Evergreen).
- Build: dotnet build InvoiceDesk.sln
- Run: dotnet run --project InvoiceDesk/InvoiceDesk.csproj
VS Code tasks available: build, publish, watch.

## Database
- EF Core 8 with Pomelo MySQL provider.
- Migrations stored under Migrations; snapshot in AppDbContextModelSnapshot.cs.
- Seeding handled by AppDbInitializer.
- Typical workflow: dotnet ef migrations add <Name> then dotnet ef database update.

## Domain Model
Core entities in Models: Company, Customer, Invoice, InvoiceLine, VatType, InvoiceStatus, CountryOption, CultureOption.
Business rules live primarily in Services/InvoiceService.cs (draft creation, issuing, totals) and related services.

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
- Export pipeline and file output in Services/PdfExportService.cs; default output directory is Exports.

## Logging
- File logger writing to logs/app.log (Helpers/FileLogger.cs).
- Binding traces also routed to the same file.
- Levels configurable in appsettings.json.

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
