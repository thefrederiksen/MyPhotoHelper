[Setup]
; Basic App Information
AppName=MyPhotoHelper
AppVersion=1.2.6
AppPublisher=MyPhotoHelper Team
AppMutex=MyPhotoHelper-{{8F6F0AC4-B9A1-45fd-A8CF-72F04E6BDE8F}}
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
Name: "{group}\MyPhotoHelper"; Filename: "{app}\MyPhotoHelper.exe"; WorkingDir: "{app}"; IconFilename: "{app}\MyPhotoHelper.exe"; IconIndex: 0
Name: "{group}\{cm:UninstallProgram,MyPhotoHelper}"; Filename: "{uninstallexe}"

; Desktop shortcut (optional)  
Name: "{commondesktop}\MyPhotoHelper"; Filename: "{app}\MyPhotoHelper.exe"; WorkingDir: "{app}"; IconFilename: "{app}\MyPhotoHelper.exe"; IconIndex: 0; Tasks: desktopicon

[Registry]
; Windows startup entry (optional)
Root: HKCU; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "MyPhotoHelper"; ValueData: """{app}\MyPhotoHelper.exe"" --minimized"; Flags: uninsdeletevalue; Tasks: startupicon

[Run]
; Launch the application after installation
Filename: "{app}\MyPhotoHelper.exe"; Description: "{cm:LaunchProgram,MyPhotoHelper}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
// Check if MyPhotoHelper is running
function IsAppRunning(const FileName: string): Boolean;
var
  FSWbemLocator: Variant;
  FWMIService: Variant;
  FWbemObjectSet: Variant;
begin
  Result := False;
  try
    FSWbemLocator := CreateOleObject('WbemScripting.SWbemLocator');
    FWMIService := FSWbemLocator.ConnectServer('', 'root\CIMV2', '', '');
    FWbemObjectSet := FWMIService.ExecQuery(Format('SELECT Name FROM Win32_Process WHERE Name="%s"', [FileName]));
    Result := (FWbemObjectSet.Count > 0);
  except
    Result := False;
  end;
end;

// Custom messages for better UX
procedure InitializeWizard;
begin
  WizardForm.WelcomeLabel2.Caption := 'This will install MyPhotoHelper on your computer.' + #13#10#13#10 + 
    'MyPhotoHelper is an AI-powered photo organization and management application that helps you organize and manage your photo collection.' + #13#10#13#10 +
    'Click Next to continue, or Cancel to exit Setup.';
end;

// Check for running instances before installation
function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
  
  // Check if MyPhotoHelper is running
  if IsAppRunning('MyPhotoHelper.exe') then
  begin
    if MsgBox('MyPhotoHelper is currently running. Setup must close it to continue.' + #13#10#13#10 + 
              'Click OK to close MyPhotoHelper and continue with installation, or Cancel to exit Setup.', 
              mbConfirmation, MB_OKCANCEL) = IDOK then
    begin
      // Try to close the application gracefully
      ShellExec('', 'taskkill', '/IM MyPhotoHelper.exe', '', SW_HIDE, ewWaitUntilTerminated, ErrorCode);
      
      // Give it a moment to close
      Sleep(2000);
      
      // Check again
      if IsAppRunning('MyPhotoHelper.exe') then
      begin
        // Force close if still running
        ShellExec('', 'taskkill', '/F /IM MyPhotoHelper.exe', '', SW_HIDE, ewWaitUntilTerminated, ErrorCode);
        Sleep(1000);
      end;
    end
    else
    begin
      Result := False;
    end;
  end;
end;

// Show completion message
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // Installation completed successfully
  end;
end;