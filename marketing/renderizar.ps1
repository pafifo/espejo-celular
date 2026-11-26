# Convierte imagen1.html e imagen2.html en PNG de 1200x1200 con Chrome sin ventana.
Set-Location $PSScriptRoot
Add-Type -AssemblyName System.Drawing
$ico = Resolve-Path "..\EspejoCelular\icono.ico"
$png = Join-Path (Split-Path $ico) "icono-256.png"
if (-not (Test-Path $png)) {
    $i = [System.Drawing.Icon]::new($ico.Path, 256, 256); $i.ToBitmap().Save($png, [System.Drawing.Imaging.ImageFormat]::Png)
}
$chrome = "C:\Program Files\Google\Chrome\Application\chrome.exe"
foreach ($n in 1, 2) {
    $html = (Resolve-Path "imagen$n.html").Path -replace '\\', '/'
    $salida = Join-Path $PSScriptRoot "espejo-celular-$n.png"
    & $chrome --headless=new --disable-gpu --hide-scrollbars --force-device-scale-factor=1 `
        --window-size=1200,1200 --screenshot="$salida" --user-data-dir="$env:TEMP\chrome-render-espejo" "file:///$html" 2>$null | Out-Null
    "Lista: $salida"
}
