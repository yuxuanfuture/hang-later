#ifndef SourceDir
  #define SourceDir "publish\\win-x64"
#endif

[Setup]
AppId={{12C5E1F4-0328-4C2A-8EA8-A7B401A63A51}
AppName=Hang Later
AppVersion=0.10.0
AppPublisher=Yuxuan Fu
AppPublisherURL=https://github.com/yuxuanfuture/hang-later
DefaultDirName={localappdata}\Programs\Hang Later
DefaultGroupName=Hang Later
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\build\releases
OutputBaseFilename=HangLater-Windows-x64-Setup
SetupIconFile=HangLater.Windows\Resources\HangLater.ico
UninstallDisplayIcon={app}\HangLater.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Hang Later"; Filename: "{app}\HangLater.exe"
Name: "{userdesktop}\Hang Later"; Filename: "{app}\HangLater.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Run]
Filename: "{app}\HangLater.exe"; Description: "Launch Hang Later"; Flags: nowait postinstall skipifsilent
