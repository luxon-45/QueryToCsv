; QueryToCsv Installer Script
#define MyAppName "QueryToCsv"
#define MyAppVersion "1.0.0"
#define MyAppExeName "QueryToCsv.exe"

[Setup]
AppId={{2756B9BF-C9B9-4C77-915D-1D10F9C31F50}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName={userpf}\{#MyAppName}
DisableProgramGroupPage=yes
ChangesEnvironment=yes
OutputDir=Installer
OutputBaseFilename=QueryToCsv-Setup-{#MyAppVersion}
Compression=lzma
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest

[Tasks]
Name: "addtopath"; Description: "Add to PATH environment variable"; GroupDescription: "Additional options:"

[Files]
; exe is always overwritten on update
Source: "QueryToCsv\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
; appsettings is user config, only copy on first install to preserve user settings
Source: "QueryToCsv\appsettings.json"; DestDir: "{app}"; Flags: onlyifdoesntexist

[Dirs]
; create queries and output folders
Name: "{app}\queries"
Name: "{app}\output"

[Registry]
Root: HKCU; Subkey: "Environment"; \
    ValueType: expandsz; ValueName: "Path"; ValueData: "{olddata};{app}"; \
    Check: NeedsAddPath('{app}'); Tasks: addtopath

[Code]
function NeedsAddPath(Param: string): Boolean;
var
  OrigPath: string;
begin
  if not RegQueryStringValue(HKEY_CURRENT_USER,
    'Environment',
    'Path', OrigPath)
  then begin
    Result := True;
    exit;
  end;
  Result := Pos(';' + Uppercase(Param) + ';', ';' + Uppercase(OrigPath) + ';') = 0;
end;

procedure RemovePath(Param: string);
var
  OrigPath: string;
  NewPath: string;
  SearchStr: string;
  P: Integer;
begin
  if not RegQueryStringValue(HKEY_CURRENT_USER,
    'Environment',
    'Path', OrigPath)
  then
    exit;

  SearchStr := ';' + Uppercase(Param);
  P := Pos(SearchStr, ';' + Uppercase(OrigPath));
  if P = 0 then
    exit;

  // Remove the entry (adjust P for the leading ';' we prepended)
  NewPath := Copy(OrigPath, 1, P - 1) + Copy(OrigPath, P + Length(Param) + 1, MaxInt);

  // Clean up leading/trailing semicolons
  if (Length(NewPath) > 0) and (NewPath[1] = ';') then
    NewPath := Copy(NewPath, 2, MaxInt);
  if (Length(NewPath) > 0) and (NewPath[Length(NewPath)] = ';') then
    NewPath := Copy(NewPath, 1, Length(NewPath) - 1);

  RegWriteStringValue(HKEY_CURRENT_USER,
    'Environment',
    'Path', NewPath);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    RemovePath(ExpandConstant('{app}'));
end;
