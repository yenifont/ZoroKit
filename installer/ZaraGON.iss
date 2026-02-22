; ZaraGON Installer Script - Inno Setup 6+
; Full PC installation (C:\ZaraGON) with admin privileges
; Turkish UI, proper Add/Remove Programs registration

#define MyAppName "ZaraGON"
#define MyAppVersion "1.0.6"
#define MyAppPublisher "ZaraGON"
#define MyAppURL "https://github.com/ZaraGON"
#define MyAppExeName "ZaraGON.exe"
#define MyAppDescription "Apache, PHP ve MariaDB yerel gelistirme ortami yoneticisi"

[Setup]
AppId={{E8F3A2B1-4C5D-6E7F-8A9B-0C1D2E3F4A5B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
AppComments={#MyAppDescription}

; C:\ installation - requires admin
DefaultDirName=C:\{#MyAppName}
DefaultGroupName={#MyAppName}
PrivilegesRequired=admin

; Output
OutputDir=Output
OutputBaseFilename=ZaraGON-Setup-{#MyAppVersion}

; Icons & branding
SetupIconFile=..\src\ZaraGON.UI\Resources\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName} {#MyAppVersion}
WizardStyle=modern
WizardImageFile=wizard.bmp
WizardSmallImageFile=wizard-small.bmp

; Compression
Compression=lzma2/ultra64
SolidCompression=yes

; Architecture
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Wizard pages - show all useful pages
DisableWelcomePage=no
DisableDirPage=no
DisableProgramGroupPage=yes
DisableReadyPage=no
DisableFinishedPage=no

; Update behavior
CloseApplications=force
RestartApplications=no

; Version info embedded in setup exe
VersionInfoVersion=1.0.6.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} Kurulum
VersionInfoTextVersion={#MyAppVersion}
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[Messages]
; Custom Turkish welcome text
WelcomeLabel1={#MyAppName} Kurulumuna Hosgeldiniz
WelcomeLabel2=Bu sihirbaz {#MyAppName} {#MyAppVersion} surumunu bilgisayariniza kuracaktir.%n%n{#MyAppName}, Apache, PHP ve MariaDB ile yerel gelistirme ortami yoneticisidir.%n%nTum gerekli bilesenler dahildir, ek bir yukleme gerekmez.%n%nDevam etmek icin Ileri'ye tiklayin.
FinishedHeadingLabel={#MyAppName} Kurulumu Tamamlandi!
FinishedLabel={#MyAppName} basariyla kuruldu.%n%nMasaustu ve Baslat menusunde kisayollar olusturuldu. Kaldirmak icin: Ayarlar > Uygulamalar > {#MyAppName} > Kaldir veya Baslat menusundeki "ZaraGON Kaldir" kisayolunu kullanin.%n%nGorev cubuguna sabitlemek icin uygulamayi calistirip simgeye sag tiklayin > Gorev cubuguna sabitle.
SelectDirLabel3={#MyAppName} asagidaki klasore kurulacaktir.
SelectDirBrowseLabel=Devam etmek icin Ileri'ye tiklayin. Farkli bir klasor secmek icin Gozat'a tiklayin.
ReadyLabel1=Kuruluma Hazir
ReadyLabel2a={#MyAppName} kurulmaya hazir. Baslamak icin Kur'a tiklayin.
SelectTasksLabel2=Yapmak istediginiz ek gorevleri secin:
ClickFinish=Kurulumu tamamlamak icin Bitir'e tiklayin.

[Tasks]
Name: "desktopicon"; Description: "Masaustune kisayol olustur"; GroupDescription: "Ek gorevler:"; Flags: checked
Name: "startmenuicon"; Description: "Baslat menusune ekle (ZaraGON Kaldir kisayolu dahil)"; GroupDescription: "Ek gorevler:"; Flags: checked
Name: "startupicon"; Description: "Windows ile birlikte baslat"; GroupDescription: "Ek gorevler:"; Flags: unchecked

[Files]
; VC++ Redistributable (bundled — installed silently during setup, like XAMPP/Laragon)
Source: "deps\vc_redist.x64.exe"; DestDir: "{tmp}"; Flags: ignoreversion skipifsourcedoesntexist deleteafterinstall
; Main application (self-contained single-file publish output)
Source: "..\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
; Additional files if present (DLLs, configs, resources)
Source: "..\publish\*.dll"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\publish\*.json"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\publish\Resources\*"; DestDir: "{app}\Resources"; Flags: ignoreversion skipifsourcedoesntexist recursesubdirs createallsubdirs

[Icons]
; Start Menu shortcuts
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Comment: "{#MyAppDescription}"; Tasks: startmenuicon
Name: "{group}\{#MyAppName} Kaldir"; Filename: "{uninstallexe}"; Tasks: startmenuicon
; Desktop shortcut
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; Comment: "{#MyAppDescription}"; Tasks: desktopicon
; Windows Startup
Name: "{commonstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: startupicon

[Run]
; Launch app after install (runs as invoking user, not admin)
Filename: "{app}\{#MyAppExeName}"; Description: "{#MyAppName} uygulamasini baslat"; Flags: nowait postinstall skipifsilent shellexec

[UninstallRun]
; Kill ALL services, PHP processes and app before uninstall
Filename: "taskkill"; Parameters: "/IM {#MyAppExeName} /F"; RunOnceId: "KillApp"; Flags: runhidden waituntilterminated
Filename: "taskkill"; Parameters: "/IM httpd.exe /F"; RunOnceId: "KillApache"; Flags: runhidden waituntilterminated
Filename: "taskkill"; Parameters: "/IM php.exe /F"; RunOnceId: "KillPHP"; Flags: runhidden waituntilterminated
Filename: "taskkill"; Parameters: "/IM php-cgi.exe /F"; RunOnceId: "KillPHPCGI"; Flags: runhidden waituntilterminated
Filename: "taskkill"; Parameters: "/IM mysqld.exe /F"; RunOnceId: "KillMySQL"; Flags: runhidden waituntilterminated
Filename: "taskkill"; Parameters: "/IM mariadbd.exe /F"; RunOnceId: "KillMariaDB"; Flags: runhidden waituntilterminated

[UninstallDelete]
; Delete EVERYTHING in install directory — belt-and-suspenders with [Code] below
Type: filesandordirs; Name: "{app}\bin"
Type: filesandordirs; Name: "{app}\config"
Type: filesandordirs; Name: "{app}\www"
Type: filesandordirs; Name: "{app}\apps"
Type: filesandordirs; Name: "{app}\logs"
Type: filesandordirs; Name: "{app}\temp"
Type: filesandordirs; Name: "{app}\backups"
Type: filesandordirs; Name: "{app}\mariadb"
Type: filesandordirs; Name: "{app}\Resources"
Type: files; Name: "{app}\*"
; Remove startup shortcut if exists
Type: files; Name: "{commonstartup}\{#MyAppName}.lnk"

[Code]
var
  ResultCode: Integer;

// Kill all ZaraGON-related processes
procedure KillAllProcesses;
begin
  Exec('taskkill', '/IM {#MyAppExeName} /F', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('taskkill', '/IM httpd.exe /F', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('taskkill', '/IM php.exe /F', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('taskkill', '/IM php-cgi.exe /F', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('taskkill', '/IM mysqld.exe /F', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('taskkill', '/IM mariadbd.exe /F', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  // Wait for handles to release
  Sleep(1500);
end;

// Remove ZaraGON-managed block from hosts file (between START/END markers)
procedure CleanHostsFile;
var
  HostsPath, Line: String;
  Lines: TArrayOfString;
  CleanContent: String;
  I: Integer;
  InBlock: Boolean;
begin
  HostsPath := ExpandConstant('{sys}\drivers\etc\hosts');
  if not FileExists(HostsPath) then Exit;
  if not LoadStringsFromFile(HostsPath, Lines) then Exit;

  CleanContent := '';
  InBlock := False;
  for I := 0 to GetArrayLength(Lines) - 1 do
  begin
    Line := Lines[I];
    // Detect start of ZaraGON block
    if Pos('# --- ZaraGON START ---', Line) > 0 then
    begin
      InBlock := True;
      Continue;
    end;
    // Detect end of ZaraGON block
    if Pos('# --- ZaraGON END ---', Line) > 0 then
    begin
      InBlock := False;
      Continue;
    end;
    // Skip lines inside the block
    if InBlock then Continue;

    if CleanContent <> '' then
      CleanContent := CleanContent + #13#10;
    CleanContent := CleanContent + Line;
  end;
  SaveStringToFile(HostsPath, CleanContent, False);
end;

// Install VC++ Redistributable silently — handle exit codes ourselves (no restart prompt)
procedure InstallVcRedist;
var
  VcPath: String;
  VcResult: Integer;
begin
  VcPath := ExpandConstant('{tmp}\vc_redist.x64.exe');
  if not FileExists(VcPath) then Exit;

  WizardForm.StatusLabel.Caption := 'Visual C++ Runtime yukleniyor...';
  Exec(VcPath, '/install /quiet /norestart', '', SW_HIDE, ewWaitUntilTerminated, VcResult);
  // 0 = success, 3010 = success (reboot suggested but NOT needed), 1638 = newer already installed
  // All are fine — no restart required since ZaraGON hasn't started yet
end;

// Override: never ask for restart — VC++ works immediately for new processes
function NeedRestart(): Boolean;
begin
  Result := False;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // Install VC++ after files are copied, before app launches
    InstallVcRedist;
  end;
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
  KillAllProcesses;
end;

function InitializeUninstall(): Boolean;
begin
  Result := True;
  // Kill everything before uninstall begins
  KillAllProcesses;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  AppDir: String;
begin
  if CurUninstallStep = usUninstall then
  begin
    // Kill again right before file deletion (in case something respawned)
    KillAllProcesses;
    // Clean hosts file
    CleanHostsFile;
  end;

  if CurUninstallStep = usPostUninstall then
  begin
    AppDir := ExpandConstant('{app}');
    // Primary: Inno Setup DelTree
    DelTree(AppDir, True, True, True);
    // Fallback: cmd rmdir if anything survived (locked files etc.)
    if DirExists(AppDir) then
      Exec('cmd.exe', '/C rmdir /S /Q "' + AppDir + '"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    // Last resort: schedule deletion on next reboot if still present
    if DirExists(AppDir) then
      Exec('cmd.exe', '/C rd /S /Q "' + AppDir + '" 2>nul & if exist "' + AppDir + '" (echo @rd /S /Q "' + AppDir + '" > "%TEMP%\zaragon_cleanup.cmd" & reg add "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce" /v ZaraGONCleanup /t REG_SZ /d "%TEMP%\zaragon_cleanup.cmd" /f)', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;
