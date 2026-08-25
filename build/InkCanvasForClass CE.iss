; 脚本由 Inno Setup 脚本向导生成。
; 有关创建 Inno Setup 脚本文件的详细信息，请参阅帮助文档！

#define MyAppName "InkCanvasForClass CE"
#define MyAppVersion "1.7.18.1"
#define MyAppPublisher "CJK_mkp"
#define MyAppURL "https://inkcanvasforclass.github.io"
#define MyAppExeName "InkCanvasForClass.exe"
#define MyAppAssocName MyAppName + ""
#define MyAppAssocExt ".exe"
#define MyAppAssocKey StringChange(MyAppAssocName, " ", "") + MyAppAssocExt

[Setup]
; 注意：AppId 的值唯一标识此应用程序。不要在其他应用程序的安装程序中使用相同的 AppId 值。
AppId={{CA801226-FD02-4C78-BCF8-753B38E70CB3}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}

; ✅ 核心修改：使用自定义函数获取当前交互式用户的 LocalAppData，确保管理员/普通用户路径一致
DefaultDirName={code:GetCurrentUserLocalAppData}\{#MyAppName}

UninstallDisplayIcon={app}\{#MyAppExeName}
ChangesAssociations=yes
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=LICENSE
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=.
OutputBaseFilename=InkCanvasForClass CE Setup
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "chinesesimp"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Languages\EnglishBritish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "dotnet6"; Description: "下载并安装 .NET Runtime 6 (运行本程序所需)"; GroupDescription: "运行时组件:"; Flags: unchecked

[Files]
Source: "release\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Registry]
Root: HKA; Subkey: "Software\Classes\{#MyAppAssocExt}\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppAssocKey}"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\{#MyAppAssocKey}"; ValueType: string; ValueName: ""; ValueData: "{#MyAppAssocName}"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\{#MyAppAssocKey}\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"
Root: HKA; Subkey: "Software\Classes\{#MyAppAssocKey}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:ProgramOnTheWeb,{#MyAppName}}"; Filename: "{#MyAppURL}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
// ✅ 核心函数：始终返回当前交互式用户的 LocalAppData 路径
// 无论是否以管理员权限运行，都绑定到启动安装程序的原始用户
function GetCurrentUserLocalAppData(Param: String): String;
var
  UserAppData: String;
begin
  // 1. 优先读取 LOCALAPPDATA 环境变量
  UserAppData := GetEnv('LOCALAPPDATA');
  
  // 2. UAC 提权后环境变量可能指向 systemprofile，需通过注册表修正
  if (UserAppData = '') or (Pos('systemprofile', Lowercase(UserAppData)) > 0) then
  begin
    if RegQueryStringValue(HKEY_CURRENT_USER, 
        'Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders', 
        'Local AppData', UserAppData) then
    begin
      // ExpandConstantEx 展开 %USERPROFILE% 等未解析的环境变量
      // 参数 False, False 确保在非安装上下文中正确解析
      UserAppData := ExpandConstantEx(UserAppData, False, False);
    end;
  end;
  
  // 3. 最终兜底：通过 USERPROFILE 拼接
  if (UserAppData = '') or (Pos('systemprofile', Lowercase(UserAppData)) > 0) then
    UserAppData := GetEnv('USERPROFILE') + '\AppData\Local';
    
  Result := UserAppData;
end;

var
  DownloadPage: TDownloadWizardPage;

function GetDotNet6DownloadUrl: String;
begin
  if IsWin64 then
    Result := 'https://builds.dotnet.microsoft.com/dotnet/Runtime/6.0.36/dotnet-runtime-6.0.36-win-x64.exe'
  else
    Result := 'https://builds.dotnet.microsoft.com/dotnet/Runtime/6.0.36/dotnet-runtime-6.0.36-win-x86.exe';
end;

function GetDotNet6InstallerName: String;
begin
  if IsWin64 then
    Result := 'dotnet-runtime-6.0.36-win-x64.exe'
  else
    Result := 'dotnet-runtime-6.0.36-win-x86.exe';
end;

procedure InitializeWizard;
begin
  DownloadPage := CreateDownloadPage(SetupMessage(msgWizardPreparing), SetupMessage(msgPreparingDesc), nil);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  Error: String;
begin
  if CurPageID = wpReady then
  begin
    if IsTaskSelected('dotnet6') then
    begin
      WizardForm.StatusLabel.Caption := '正在下载 .NET Runtime 6...';
      WizardForm.StatusLabel.Visible := True;
      DownloadPage.Clear;
      DownloadPage.Add(
        GetDotNet6DownloadUrl,
        GetDotNet6InstallerName, '');
      DownloadPage.Show;
      try
        try
          DownloadPage.Download;
        except
          if DownloadPage.AbortedByUser then
            Log('Aborted by user.')
          else
          begin
            Error := Format('%s: %s', [DownloadPage.LastBaseNameOrUrl, GetExceptionMessage]);
            SuppressibleMsgBox(AddPeriod(Error), mbCriticalError, MB_OK, IDOK);
          end;
          Result := False;
          Exit;
        end;
      finally
        DownloadPage.Hide;
        WizardForm.StatusLabel.Visible := False;
      end;
    end;
  end;
  Result := True;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
  DotNetInstallerPath: String;
begin
  if CurStep = ssPostInstall then
  begin
    if IsTaskSelected('dotnet6') then
    begin
      DotNetInstallerPath := ExpandConstant(Format('{tmp}\%s', [GetDotNet6InstallerName]));
      if FileExists(DotNetInstallerPath) then
      begin
        WizardForm.StatusLabel.Caption := '正在安装 .NET Runtime 6...';
        WizardForm.StatusLabel.Visible := True;
        Log('Installing .NET Runtime 6...');
        Exec(DotNetInstallerPath, '/install /quiet /norestart', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
        Log(Format('Installation completed with code: %d', [ResultCode]));
        WizardForm.StatusLabel.Visible := False;
        DeleteFile(DotNetInstallerPath);
      end;
    end;
  end;
end;
