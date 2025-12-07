; TalkKeys Installer Script for Inno Setup 6
; A modern, polished installation experience
; Download Inno Setup from: https://jrsoftware.org/isinfo.php

#define MyAppName "TalkKeys"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "TalkKeys"
#define MyAppURL "https://github.com/yourusername/talkkeys"
#define MyAppExeName "TalkKeys.exe"
#define MyAppDescription "Voice to Text - Speak naturally, type instantly"

[Setup]
; Unique application identifier
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
AppComments={#MyAppDescription}
VersionInfoDescription={#MyAppDescription}
VersionInfoVersion={#MyAppVersion}

; Installation directories
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}

; Output settings
OutputDir=output
OutputBaseFilename=TalkKeys-Setup-{#MyAppVersion}
SetupIconFile=..\HotkeyPaster\icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

; Compression - maximum
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes

; Privileges - per-user installation by default
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

; Modern UI settings
WizardStyle=modern
WizardSizePercent=100
WizardResizable=no

; Custom wizard images (dark theme)
WizardImageFile=WizardImage.bmp
WizardSmallImageFile=WizardSmallImage.bmp
WizardImageStretch=no

; Pages configuration
DisableWelcomePage=no
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyPage=no
DisableFinishedPage=no

; Minimum Windows version (Windows 10 1809+)
MinVersion=10.0.17763

; Uninstaller
CreateUninstallRegKey=yes
Uninstallable=yes

; Misc
CloseApplications=yes
RestartApplications=no
ShowLanguageDialog=no
AllowNoIcons=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Messages]
; Custom welcome message
WelcomeLabel1=Welcome to TalkKeys
WelcomeLabel2=Voice to Text made simple.%n%nSpeak naturally into your microphone and TalkKeys will instantly transcribe and paste your words wherever you're typing.%n%nThis will install TalkKeys {#MyAppVersion} on your computer.
; Custom ready message
ReadyLabel1=Ready to Install
ReadyLabel2a=Click Install to begin. TalkKeys will be ready in seconds.
ReadyLabel2b=Click Install to begin. TalkKeys will be ready in seconds.
; Finished message
FinishedHeadingLabel=Setup Complete!
FinishedLabelNoIcons=TalkKeys has been installed successfully.%n%nPress your hotkey (default: Ctrl+Shift+Space) to start recording.
FinishedLabel=TalkKeys has been installed successfully.%n%nPress your hotkey (default: Ctrl+Shift+Space) to start recording.
ClickFinish=Click Finish to launch TalkKeys and start using voice-to-text.

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked
Name: "startwithwindows"; Description: "Launch TalkKeys when Windows starts"; GroupDescription: "Startup:"; Flags: checkedonce

[Files]
; Main application files
Source: "..\HotkeyPaster\bin\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Comment: "{#MyAppDescription}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; Comment: "{#MyAppDescription}"

[Registry]
; Startup entry
Root: HKCU; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "TalkKeys"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: startwithwindows

[Run]
; Launch after installation with a nice message
Filename: "{app}\{#MyAppExeName}"; Description: "Launch TalkKeys now"; Flags: nowait postinstall skipifsilent shellexec

[UninstallRun]
; Gracefully stop the application
Filename: "taskkill"; Parameters: "/F /IM {#MyAppExeName}"; Flags: runhidden; RunOnceId: "StopTalkKeys"

[UninstallDelete]
; Clean up logs (keep settings for reinstall)
Type: filesandordirs; Name: "{userappdata}\TalkKeys\logs"

[Code]
var
  DotNetMissing: Boolean;

// Check if .NET 8.0 Desktop Runtime is installed
function IsDotNet8DesktopRuntimeInstalled(): Boolean;
var
  Output: AnsiString;
  ResultCode: Integer;
  TempFile: String;
begin
  Result := False;
  TempFile := ExpandConstant('{tmp}\dotnet_check.txt');

  // Run dotnet --list-runtimes and capture output
  if Exec('cmd.exe', '/c dotnet --list-runtimes > "' + TempFile + '" 2>&1', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    if ResultCode = 0 then
    begin
      if LoadStringFromFile(TempFile, Output) then
      begin
        // Check for Microsoft.WindowsDesktop.App 8.x
        Result := (Pos('Microsoft.WindowsDesktop.App 8.', Output) > 0);
      end;
    end;
  end;

  // Cleanup
  DeleteFile(TempFile);
end;

// Initialize setup - check prerequisites
function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
  DotNetMissing := not IsDotNet8DesktopRuntimeInstalled();

  if DotNetMissing then
  begin
    case MsgBox('TalkKeys requires the .NET 8.0 Desktop Runtime to run.' + #13#10 + #13#10 +
                'Would you like to download and install it now?' + #13#10 + #13#10 +
                'Click Yes to open the download page.' + #13#10 +
                'Click No to cancel installation.',
                mbConfirmation, MB_YESNO or MB_DEFBUTTON1) of
      IDYES:
        begin
          ShellExec('open',
            'https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-8.0.11-windows-x64-installer',
            '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
          MsgBox('Please install the .NET 8.0 Desktop Runtime, then run this installer again.',
                 mbInformation, MB_OK);
          Result := False;
        end;
      IDNO:
        Result := False;
    end;
  end;
end;

// Custom colors for the wizard
procedure InitializeWizard();
begin
  // Set wizard window properties for a cleaner look
  WizardForm.Color := clWhite;

  // Style the main panel
  WizardForm.MainPanel.Color := $00F9FAFB;  // Light gray like the app

  // Style the inner page
  WizardForm.InnerPage.Color := clWhite;

  // Make Next/Finish button more prominent
  WizardForm.NextButton.Font.Style := [fsBold];

  // Style the finish page message
  WizardForm.FinishedLabel.Font.Size := 9;
end;

// Update status during installation
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // Installation complete
    Log('TalkKeys installation completed successfully');
  end;
end;

// Cleanup on uninstall
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    // Ask if user wants to remove settings
    if MsgBox('Do you want to remove your TalkKeys settings and API keys?' + #13#10 + #13#10 +
              'Click Yes to remove all data.' + #13#10 +
              'Click No to keep your settings for future reinstallation.',
              mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
    begin
      DelTree(ExpandConstant('{userappdata}\TalkKeys'), True, True, True);
      Log('User data removed');
    end;
  end;
end;
