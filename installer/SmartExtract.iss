; SmartExtract Inno Setup Script
; Build: iscc SmartExtract.iss /DAppVersion=1.0.0
; Output: ..\dist\SmartExtractSetup.exe

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

#define MyAppName      "SmartExtract"
#define MyAppVersion   AppVersion
#define MyAppPublisher "SmartExtract"
#define MyAppURL       "https://github.com/mchwalek/SmartExtract"
#define MyAppExeName   "SmartExtract.exe"

[Setup]
AppId={{B4A5D3E2-7C91-4F8A-B2E6-5D9C1A3F8E4B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\dist
OutputBaseFilename=SmartExtractSetup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
LicenseFile=..\LICENSE
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "..\build\publish\SmartExtract.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\build\publish\SmartExtract.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\build\publish\SmartExtract.deps.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\build\publish\SmartExtract.runtimeconfig.json"; DestDir: "{app}"; Flags: ignoreversion

[Registry]
; SmartExtract config key (removed on uninstall)
Root: HKCU; Subkey: "Software\SmartExtract"; Flags: uninsdeletekey

; Context menu: .zip
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\SmartExtract"; ValueType: string; ValueName: ""; ValueData: "Smart Extract"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\SmartExtract"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\{#MyAppExeName}"",0"
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\SmartExtract\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""

; Context menu: .7z
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\SmartExtract"; ValueType: string; ValueName: ""; ValueData: "Smart Extract"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\SmartExtract"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\{#MyAppExeName}"",0"
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\SmartExtract\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""

; Context menu: .rar
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\SmartExtract"; ValueType: string; ValueName: ""; ValueData: "Smart Extract"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\SmartExtract"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\{#MyAppExeName}"",0"
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\SmartExtract\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""

; Context menu: .gz
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.gz\shell\SmartExtract"; ValueType: string; ValueName: ""; ValueData: "Smart Extract"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.gz\shell\SmartExtract"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\{#MyAppExeName}"",0"
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.gz\shell\SmartExtract\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""

; Context menu: .bz2
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.bz2\shell\SmartExtract"; ValueType: string; ValueName: ""; ValueData: "Smart Extract"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.bz2\shell\SmartExtract"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\{#MyAppExeName}"",0"
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.bz2\shell\SmartExtract\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""

; Context menu: .tar
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tar\shell\SmartExtract"; ValueType: string; ValueName: ""; ValueData: "Smart Extract"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tar\shell\SmartExtract"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\{#MyAppExeName}"",0"
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.tar\shell\SmartExtract\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""

[Code]
var
  SevenZipPage: TInputDirWizardPage;

{ Detect 7-Zip path using same priority as SevenZipLocator.cs:
  1. HKCU\Software\7-Zip -> Path64
  2. HKCU\Software\7-Zip -> Path
  3. Hardcoded fallback
  (PATH env search is not feasible in Pascal; handled at runtime by the app) }
function GetSevenZipDefaultPath(): String;
var
  Path: String;
begin
  if RegQueryStringValue(HKCU, 'Software\7-Zip', 'Path64', Path) and (Path <> '') then
  begin
    Result := Path;
    Exit;
  end;
  if RegQueryStringValue(HKCU, 'Software\7-Zip', 'Path', Path) and (Path <> '') then
  begin
    Result := Path;
    Exit;
  end;
  Result := 'C:\Program Files\7-Zip\';
end;

procedure InitializeWizard();
begin
  SevenZipPage := CreateInputDirPage(
    wpSelectDir,
    '7-Zip Location',
    'Where is 7-Zip installed?',
    'SmartExtract needs 7-Zip to inspect and extract archives. ' +
    'Confirm or correct the folder containing 7z.exe and 7zG.exe.',
    False,
    ''
  );
  SevenZipPage.Add('');
  SevenZipPage.Values[0] := GetSevenZipDefaultPath();
end;

{ Write the confirmed 7-Zip path to HKCU\Software\SmartExtract\SevenZipPath
  after files have been installed (ssPostInstall). }
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    RegWriteStringValue(HKCU, 'Software\SmartExtract', 'SevenZipPath', SevenZipPage.Values[0]);
end;

{ Check for .NET 10 Desktop Runtime via registry.
  Key: HKLM\SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App
  Subkeys are version strings like "10.0.0". }
function IsDotNet10Installed(): Boolean;
var
  Keys: TArrayOfString;
  I: Integer;
begin
  Result := False;
  if RegGetSubkeyNames(
    HKLM,
    'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App',
    Keys) then
  begin
    for I := 0 to GetArrayLength(Keys) - 1 do
      if Copy(Keys[I], 1, 3) = '10.' then
      begin
        Result := True;
        Break;
      end;
  end;
end;

{ Show a non-blocking warning if .NET 10 is absent. Installation continues. }
function InitializeSetup(): Boolean;
begin
  if not IsDotNet10Installed() then
    MsgBox(
      '.NET 10 Desktop Runtime was not detected on this machine.' + #13#10 +
      'SmartExtract requires it to run.' + #13#10 + #13#10 +
      'Download it from:' + #13#10 +
      'https://dotnet.microsoft.com/download/dotnet/10.0' + #13#10 + #13#10 +
      'Installation will continue regardless.',
      mbInformation,
      MB_OK
    );
  Result := True;
end;
