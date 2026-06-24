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

function FindSevenZipFromPath(): String;
var
  PathEnv: String;
  Dir: String;
  Sep: Integer;
begin
  Result := '';
  PathEnv := GetEnv('PATH');

  while PathEnv <> '' do
  begin
    Sep := Pos(';', PathEnv);
    if Sep > 0 then
    begin
      Dir := Copy(PathEnv, 1, Sep - 1);
      Delete(PathEnv, 1, Sep);
    end
    else
    begin
      Dir := PathEnv;
      PathEnv := '';
    end;

    Dir := Trim(Dir);
    if (Dir <> '') and FileExists(AddBackslash(Dir) + '7z.exe') then
    begin
      Result := AddBackslash(Dir);
      Exit;
    end;
  end;
end;

{ Detect 7-Zip path using same priority as SevenZipLocator.cs:
  1. HKCU\Software\7-Zip -> Path64
  2. HKCU\Software\7-Zip -> Path
  3. PATH env search for 7z.exe
  4. Hardcoded fallback }
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
  Path := FindSevenZipFromPath();
  if Path <> '' then
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


