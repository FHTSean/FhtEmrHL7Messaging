; Script generated by the Inno Setup Script Wizard.
; SEE THE DOCUMENTATION FOR DETAILS ON CREATING INNO SETUP SCRIPT FILES!

#define MyAppName "FHT Message Service"
#define MyAppVersion "1.2"
#define MyAppPublisher "University of Melbourne"
#define MyAppURL "https://unimelb.edu.au"
#define MyAppFolder "C:\fht"

[Setup]
; NOTE: The value of AppId uniquely identifies this application. Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId=9176c70e-0bab-4cd7-8cb4-3c8e5a7d7811
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={code:MyConst}\fht\FHTMessageService


DisableDirPage=yes
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputBaseFilename=FHTMessageServiceSetup
SetupIconFile=FhtMessageService.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin


[Code]
function GetHKLM: Integer;
begin
  if IsWin64 then
    Result := HKLM64
  else
    Result := HKLM32;
end;

function MyConst(Value: String): String;
var
  myPath: String;
begin
  Result := ExpandConstant('{#MyAppFolder}');
  if RegQueryStringValue(GetHKLM, 'SOFTWARE\fht',
     'installationDir', myPath) then
    begin
      Result := myPath;
    end;

end;

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "C:\FHTdeploy\FHTMessageService\FHTMessageService.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\FHTdeploy\FHTMessageService\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[run]
Filename: {sys}\sc.exe; Parameters: "create FHTMessageService displayname= ""FHT Message Service"" start= delayed-auto binPath= ""{app}\FHTMessageService.exe""" ; Flags: runhidden
Filename: {sys}\sc.exe; Parameters: "description FHTMessageService ""FHT to EMR message service""" ; Flags: runhidden
Filename: {sys}\sc.exe; Parameters: "failure FHTMessageService actions= restart/180000/restart/180000/restart/180000 reset= 86400" ; Flags: runhidden
Filename: {sys}\sc.exe; Parameters: "start FHTMessageService" ; Flags: runhidden

[UninstallRun]
Filename: {sys}\sc.exe; Parameters: "stop FHTMessageService" ; Flags: runhidden
Filename: {sys}\sc.exe; Parameters: "delete FHTMessageService" ; Flags: runhidden
