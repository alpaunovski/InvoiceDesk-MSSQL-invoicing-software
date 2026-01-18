# InvoiceDesk (WPF, .NET 8)

Multi-company invoice manager built with WPF, EF Core (SQL Server), WebView2 PDF export, MVVM, and RESX localization (English/Bulgarian).

## Disclaimer

Course project; not production-hardened. No warranty. Educational use only.

## Stack & Key Bits
- .NET 8 WPF, MVVM via CommunityToolkit.Mvvm
- EF Core 8 (SQL Server) with IDbContextFactory
- WebView2 for HTML→PDF (PrintToPdfAsync)
- RESX localization: Strings.resx (en) and Strings.bg.resx (bg); SatelliteResourceLanguages limited to en;bg

## Prerequisites
- .NET 8 SDK
- SQL Server instance reachable with create/alter rights (LocalDB/Express/remote ok)
- Microsoft Edge WebView2 Runtime (Evergreen)

## Configure
1. Copy/update [InvoiceDesk/appsettings.json](InvoiceDesk/appsettings.json): set ConnectionStrings:Default.
2. Ensure the SQL login can create/alter the target database; the app will create it if missing.
3. Optional: change Logging:FilePath (defaults to logs/app.log under workspace) and Pdf:OutputDirectory (defaults to exports under workspace).

## Database
```
cd InvoiceDesk
dotnet ef migrations add InitialCreate
dotnet ef database update
```
- Uses migrations (no EnsureCreated). AppDbInitializer creates the database if absent and seeds one default Company when empty.

## Build / Clean / Run
```
cd InvoiceDesk
dotnet build InvoiceDesk.sln
dotnet clean InvoiceDesk.sln   # to clear bin/obj
dotnet run --project InvoiceDesk
```

## Runtime Behavior
- Multi-company isolation: services use `ICompanyContext` to scope queries.
- Invoice issuing is transactional; numbering per company; issued invoices/PDFs are immutable.
- PDF exports go to InvoiceDesk/exports (relative to workspace) and are also stored in DB as bytes/metadata.
- User culture preference is persisted; UI binds `DataGrid.Language` to the selected culture to avoid validation issues when switching languages.

## Troubleshooting
- Missing WebView2: install Evergreen runtime (x64/ARM as appropriate).
- Binding issues: binding trace is enabled to logs/app.log; check for BindingExpression entries.
- SQL Server permissions: ensure the configured user can create/alter the database and target catalog exists or is creatable.

## Notes for Development
- Logs: `logs/app.log` (file logger + binding trace).
- PDF renderer: [InvoiceDesk/Rendering/InvoiceHtmlRenderer.cs](InvoiceDesk/Rendering/InvoiceHtmlRenderer.cs) (embed logo as data URL; invariant formatting).
- PDF pipeline: [InvoiceDesk/Services/PdfExportService.cs](InvoiceDesk/Services/PdfExportService.cs) (WebView2 headless host, timeouts, diagnostics).
- Domain rules: [InvoiceDesk/Services/InvoiceService.cs](InvoiceDesk/Services/InvoiceService.cs) (draft creation, issuing, totals).
- Localization helper: [InvoiceDesk/Helpers/LocalizedStrings.cs](InvoiceDesk/Helpers/LocalizedStrings.cs) and RESX files in `InvoiceDesk/Resources`.

## Feature Highlights
- Invoice list/search, draft editor, issue/export actions
- Company and customer management windows
- Per-line VAT type selection (domestic, intra-EU reverse charge, export, exempt)
- Runtime culture switching with persisted preference
