[Setup]
; Basic App Information
AppName=MyPhotoHelper
AppVersion=1.1.0
AppPublisher=MyPhotoHelper Team
AppPublisherURL=https://github.com/thefrederiksen/MyPhotoHelper
AppSupportURL=https://github.com/thefrederiksen/MyPhotoHelper/issues
AppUpdatesURL=https://github.com/thefrederiksen/MyPhotoHelper/releases
DefaultDirName={localappdata}\MyPhotoHelper
DefaultGroupName=MyPhotoHelper
AllowNoIcons=yes
OutputBaseFilename=MyPhotoHelper-Setup
OutputDir=Output
Compression=lzma
SolidCompression=yes
WizardStyle=modern

; Security - No admin privileges required
PrivilegesRequired=lowest
DisableProgramGroupPage=yes

; Modern installer look
WizardImageFile=
WizardSmallImageFile=

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupicon"; Description: "Start automatically with Windows"; GroupDescription: "Startup Options:"; Flags: unchecked

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; Start Menu shortcuts (always created)
Name: "{group}\MyPhotoHelper"; Filename: "{app}\MyPhotoHelper.exe"; WorkingDir: "{app}"
Name: "{group}\{cm:UninstallProgram,MyPhotoHelper}"; Filename: "{uninstallexe}"

; Desktop shortcut (optional)
Name: "{commondesktop}\MyPhotoHelper"; Filename: "{app}\MyPhotoHelper.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Registry]
; Windows startup entry (optional)
Root: HKCU; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "MyPhotoHelper"; ValueData: """{app}\MyPhotoHelper.exe"" --minimized"; Flags: uninsdeletevalue; Tasks: startupicon

[Run]
; Launch the application after installation
Filename: "{app}\MyPhotoHelper.exe"; Description: "{cm:LaunchProgram,MyPhotoHelper}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
// Custom messages for better UX
procedure InitializeWizard;
begin
  WizardForm.WelcomeLabel2.Caption := 'This will install MyPhotoHelper on your computer.' + #13#10#13#10 + 
    'MyPhotoHelper is an AI-powered photo organization and management application that helps you organize and manage your photo collection.' + #13#10#13#10 +
    'Click Next to continue, or Cancel to exit Setup.';
end;

// Show completion message
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // Installation completed successfully
  end;
end;