# Compila Espejo Celular y arma el instalador con scrcpy incluido.
#   .\construir.ps1            -> app + instalador en .\distribucion
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$version = ([xml](Get-Content EspejoCelular\EspejoCelular.csproj)).Project.PropertyGroup.Version | Select-Object -First 1

if (-not (Test-Path "EspejoCelular\icono.ico")) { & .\crear_icono.ps1 }

# 1. App autónoma (no necesita .NET instalado en el PC de destino)
if (Test-Path dist) { Remove-Item dist -Recurse -Force }
dotnet publish EspejoCelular\EspejoCelular.csproj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=none -o dist | Out-Host
if ($LASTEXITCODE -ne 0) { throw "Falló la compilación" }

# 2. Copiar scrcpy (la versión más nueva instalada con winget)
$scrcpy = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\Genymobile.scrcpy*" -Recurse -Filter scrcpy.exe |
    Sort-Object { [version]($_.Directory.Name -replace '^.*-v', '') } -Descending | Select-Object -First 1
if (-not $scrcpy) { throw "No encontré scrcpy. Instálalo con: winget install Genymobile.scrcpy" }
"scrcpy: $($scrcpy.Directory.FullName)"
New-Item -ItemType Directory -Force dist\scrcpy | Out-Null
Get-ChildItem $scrcpy.Directory.FullName | Where-Object { $_.Name -notin 'scrcpy-console.bat','scrcpy-noconsole.vbs','open_a_terminal_here.bat' } |
    Copy-Item -Destination dist\scrcpy -Recurse
Copy-Item terceros\LICENCIA-scrcpy.txt dist\scrcpy\LICENSE.txt

# 3. Instalador NSIS
New-Item -ItemType Directory -Force distribucion | Out-Null
& "${env:ProgramFiles(x86)}\NSIS\makensis.exe" /V2 "/DVERSION=$version" instalador.nsi
if ($LASTEXITCODE -ne 0) { throw "Falló el instalador" }
$inst = Get-Item "distribucion\EspejoCelular-$version-instalador.exe"
"Listo: $($inst.FullName) ($([math]::Round($inst.Length/1MB,1)) MB)"
