[Setup]
AppName=Fylo
AppVersion=1.0
AppPublisher=Fylo
DefaultDirName={autopf}\Fylo
DefaultGroupName=Fylo
OutputDir=.
OutputBaseFileName=Fylo-Setup
Compression=lzma2
SolidCompression=yes
UninstallDisplayIcon={app}\Fylo.exe
PrivilegesRequired=admin
DisableProgramGroupPage=yes

[Files]
Source: "bin\Release\net8.0-windows\win-x64\publish\Fylo.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "restore-default-explorer.ps1"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Fylo"; Filename: "{app}\Fylo.exe"; IconFilename: "{app}\Fylo.exe"
Name: "{group}\Uninstall Fylo"; Filename: "{uninstallexe}"
Name: "{group}\Restore default Explorer"; Filename: "powershell.exe"; Parameters: "-ExecutionPolicy Bypass -File ""{app}\restore-default-explorer.ps1"""
Name: "{autoprograms}\Fylo"; Filename: "{app}\Fylo.exe"; IconFilename: "{app}\Fylo.exe"

[Registry]
Root: HKCU; Subkey: "Software\Classes\Folder\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\Fylo.exe"" ""%1"""; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Folder\shell\open\command"; ValueType: string; ValueName: "DelegateExecute"; ValueData: ""

Root: HKCU; Subkey: "Software\Classes\Directory\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\Fylo.exe"" ""%1"""; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\shell\open\command"; ValueType: string; ValueName: "DelegateExecute"; ValueData: ""

Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\Fylo.exe"" ""%V"""; Flags: uninsdeletekey

Root: HKCU; Subkey: "Software\Classes\Drive\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\Fylo.exe"" ""%1"""; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Drive\shell\open\command"; ValueType: string; ValueName: "DelegateExecute"; ValueData: ""

[Run]
Filename: "taskkill"; Parameters: "/f /im explorer.exe"; Flags: runhidden skipifsilent
Filename: "explorer.exe"; Flags: runhidden skipifsilent
