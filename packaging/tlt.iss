; Instalador do TLT (Transleitor).
;
; Gera um .exe de instalacao a partir da pasta publicada em dist\TLT.
; Compilar com:  ISCC.exe packaging\tlt.iss
;
; Duas decisoes que valem explicacao:
;
; 1. Instala em LocalAppData e nao em Program Files. Isso dispensa privilegio de
;    administrador, e o TLT e aplicativo de usuario: exigir UAC de quem so quer
;    legendar uma reuniao perde instalacoes sem ganhar nada em troca.
;
; 2. Os modelos NAO vao no instalador. Sao cerca de 500 MB que o app baixa na
;    primeira execucao e guarda em cache. Embuti-los triplicaria o download para
;    todo mundo, inclusive para quem so quer experimentar.

#define Nome "TLT"
#define NomeCompleto "TLT — Transleitor"
#define Versao "0.1.0"
#define Autor "Rafael dos Santos"
#define Executavel "TLT.exe"

[Setup]
AppId={{BF98D467-75AC-4B31-8577-923E5E7E659E}
AppName={#NomeCompleto}
AppVersion={#Versao}
AppVerName={#NomeCompleto} {#Versao}
AppPublisher={#Autor}
VersionInfoVersion={#Versao}

DefaultDirName={localappdata}\Programs\{#Nome}
DefaultGroupName={#Nome}
DisableProgramGroupPage=yes
DisableDirPage=no

; Sem privilegio de administrador: ver a nota no topo.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

OutputDir=..\dist
OutputBaseFilename=TLT-{#Versao}-instalador
SetupIconFile=..\src\Tlt.App\tlt.ico
UninstallDisplayIcon={app}\{#Executavel}
UninstallDisplayName={#NomeCompleto}

Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern

; O app usa API do Windows 10 build 19041 (captura por processo e ocultacao em
; compartilhamento de tela). Instalar em versao anterior daria erro em execucao,
; entao e melhor barrar aqui.
MinVersion=10.0.19041

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na área de trabalho"; GroupDescription: "Atalhos:"
Name: "iniciarcomwindows"; Description: "Iniciar o TLT junto com o Windows"; GroupDescription: "Inicialização:"; Flags: unchecked

[Files]
Source: "..\dist\TLT\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#NomeCompleto}"; Filename: "{app}\{#Executavel}"
Name: "{group}\Desinstalar o {#Nome}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#Nome}"; Filename: "{app}\{#Executavel}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; \
    ValueName: "{#Nome}"; ValueData: """{app}\{#Executavel}"""; \
    Flags: uninsdeletevalue; Tasks: iniciarcomwindows

[Run]
Filename: "{app}\{#Executavel}"; Description: "Abrir o {#Nome} agora"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Preferencias do overlay: posicao e tamanho da janela.
Type: filesandordirs; Name: "{userappdata}\{#Nome}"

[Code]
// Os modelos baixados ocupam cerca de 500 MB e ficam fora da pasta do programa.
// Apagar sem perguntar puniria quem esta apenas reinstalando ou atualizando, que
// teria de baixar tudo de novo.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Modelos: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    Modelos := ExpandConstant('{localappdata}\TLT\models');
    if not DirExists(Modelos) then
      Exit;

    // Em desinstalacao silenciosa nao ha ninguem para responder. Perguntar assim
    // mesmo trava o processo esperando um clique que nunca vem — descoberto na
    // pratica, testando o ciclo completo do instalador.
    //
    // O padrao no silencio e PRESERVAR: apagar 500 MB sem confirmacao seria a pior
    // das duas escolhas possiveis.
    if UninstallSilent then
      Exit;

    if MsgBox('Remover também os modelos de reconhecimento e tradução baixados?' + #13#10 +
              'São cerca de 500 MB. Responda Não se pretende reinstalar o TLT.',
              mbConfirmation, MB_YESNO) = IDYES then
      DelTree(Modelos, True, True, True);
  end;
end;
