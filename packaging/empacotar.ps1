<#
    Empacota o TLT para distribuicao.

    Gera dois artefatos em dist\:
      TLT-<versao>-portatil.zip     pasta compactada, roda sem instalar
      TLT-<versao>-instalador.exe   instalador (exige o Inno Setup)

    O ZIP e gerado sempre. O instalador so quando o Inno Setup esta presente, para
    que a ausencia dele nao impeca de produzir algo distribuivel.

    Uso:  powershell -ExecutionPolicy Bypass -File packaging\empacotar.ps1
#>

$ErrorActionPreference = 'Stop'

$raiz = Split-Path -Parent $PSScriptRoot
$publicado = Join-Path $raiz 'dist\TLT'
$destino = Join-Path $raiz 'dist'

# A versao vem do csproj: um lugar so, para o instalador, o ZIP e as propriedades do
# executavel nunca discordarem entre si.
$csproj = Join-Path $raiz 'src\Tlt.App\Tlt.App.csproj'
$versao = ([xml](Get-Content $csproj)).Project.PropertyGroup.Version | Where-Object { $_ }
if (-not $versao) { throw "nao consegui ler a versao de $csproj" }

Write-Host "TLT $versao" -ForegroundColor Cyan
Write-Host ''

# --- 1. publicacao ---
Write-Host '[1/3] publicando...' -NoNewline
if (Test-Path $publicado) { Remove-Item $publicado -Recurse -Force }

$log = & dotnet publish (Join-Path $raiz 'src\Tlt.App') -c Release -o $publicado --nologo -v quiet 2>&1
if ($LASTEXITCODE -ne 0) { Write-Host ''; Write-Host $log; throw 'falha ao publicar' }

$tamanho = [math]::Round((Get-ChildItem $publicado -Recurse -File | Measure-Object -Property Length -Sum).Sum / 1MB)
Write-Host " $tamanho MB"

# --- 2. pacote portatil ---
Write-Host '[2/3] compactando...' -NoNewline
$zip = Join-Path $destino "TLT-$versao-portatil.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path "$publicado\*" -DestinationPath $zip -CompressionLevel Optimal

$tamanhoZip = [math]::Round((Get-Item $zip).Length / 1MB)
Write-Host " $tamanhoZip MB"

# --- 3. instalador ---
Write-Host '[3/3] instalador...' -NoNewline

# O winget instala por usuario quando nao ha privilegio de administrador, e ai o
# Inno Setup vai para LocalAppData em vez de Program Files. Procurar so nos dois
# caminhos classicos faria o script reportar "nao instalado" com ele instalado.
$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    Write-Host ' pulado' -ForegroundColor Yellow
    Write-Host ''
    Write-Host '  O Inno Setup nao esta instalado. Para gerar o instalador:' -ForegroundColor Yellow
    Write-Host '    winget install JRSoftware.InnoSetup' -ForegroundColor Yellow
} else {
    $saida = & $iscc (Join-Path $PSScriptRoot 'tlt.iss') /Q 2>&1
    if ($LASTEXITCODE -ne 0) { Write-Host ''; Write-Host $saida; throw 'falha ao gerar o instalador' }

    $exe = Join-Path $destino "TLT-$versao-instalador.exe"
    $tamanhoExe = [math]::Round((Get-Item $exe).Length / 1MB)
    Write-Host " $tamanhoExe MB"
}

Write-Host ''
Write-Host 'pronto:' -ForegroundColor Green
Get-ChildItem $destino -File | ForEach-Object {
    Write-Host ("  {0,7} MB  {1}" -f [math]::Round($_.Length / 1MB), $_.Name)
}
