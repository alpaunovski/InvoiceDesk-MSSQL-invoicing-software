; InvoiceDesk Inno Setup script
; Build the app first (Release):
;   dotnet publish ..\InvoiceDesk\InvoiceDesk.csproj -c Release -r win-x64 --self-contained false /p:PublishSingleFile=false
; Then run this script with Inno Setup Compiler.

#define MyAppName "InvoiceDesk"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "InvoiceDesk"
#define MyAppExeName "InvoiceDesk.exe"
; Absolute path to the publish output. Adjust if your checkout path differs.
#define MySourceDir "C:\\Users\\User\\github\\InvoiceDesk-MSSQL-invoicing-software\\InvoiceDesk\\bin\\Release\\net8.0-windows\\win-x64\\publish"
#define MyLicenseFile "C:\\Users\\User\\github\\InvoiceDesk-MSSQL-invoicing-software\\LICENSE"

[Setup]
AppId={{6D828B35-29F8-4EF2-96F5-02C754C0C6D9}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={commonpf64}\InvoiceDesk
DefaultGroupName=InvoiceDesk
DisableDirPage=no
DisableProgramGroupPage=yes
OutputBaseFilename=InvoiceDesk-setup
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
SetupLogging=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#MySourceDir}\\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#MyLicenseFile}"; DestDir: "{app}"; Flags: ignoreversion

[Dirs]
Name: "{localappdata}\\InvoiceDesk"; Flags: uninsalwaysuninstall
Name: "{localappdata}\\InvoiceDesk\\Exports"; Flags: uninsalwaysuninstall
Name: "{localappdata}\\InvoiceDesk\\Exports\\backups"; Flags: uninsalwaysuninstall
Name: "{localappdata}\\InvoiceDesk\\Exports\\signed"; Flags: uninsalwaysuninstall

[Icons]
Name: "{group}\\InvoiceDesk"; Filename: "{app}\\{#MyAppExeName}"
Name: "{commondesktop}\\InvoiceDesk"; Filename: "{app}\\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\\{#MyAppExeName}"; Description: "Launch InvoiceDesk"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup(): Boolean;
begin
  // Basic prerequisite reminder; returns true to proceed. For production, consider
  // adding runtime detection/download (WebView2 Evergreen + .NET Desktop Runtime 8+).
  MsgBox('Make sure Microsoft Edge WebView2 Runtime and .NET Desktop Runtime 8+ are installed before running InvoiceDesk.', mbInformation, MB_OK);
  Result := True;
end;
