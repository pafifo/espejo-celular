# Dibuja el ícono de Espejo Celular (celular azul con onda wifi) y lo guarda como .ico multi-tamaño.
Add-Type -AssemblyName System.Drawing
$salida = Join-Path $PSScriptRoot "EspejoCelular\icono.ico"
$tamanos = 16, 24, 32, 48, 64, 128, 256
$pngs = @()
foreach ($t in $tamanos) {
    $bmp = [System.Drawing.Bitmap]::new([int]$t, [int]$t)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)
    $k = $t / 256.0
    function New-Redondeado($x, $y, $w, $h, $r) {
        $p = New-Object System.Drawing.Drawing2D.GraphicsPath
        $p.AddArc($x, $y, $r, $r, 180, 90); $p.AddArc($x + $w - $r, $y, $r, $r, 270, 90)
        $p.AddArc($x + $w - $r, $y + $h - $r, $r, $r, 0, 90); $p.AddArc($x, $y + $h - $r, $r, $r, 90, 90)
        $p.CloseFigure(); return $p
    }
    # Cuerpo del celular
    $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(37, 99, 235))), (New-Redondeado (60*$k) (12*$k) (136*$k) (232*$k) (40*$k)))
    # Pantalla
    $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(219, 234, 254))), (New-Redondeado (76*$k) (36*$k) (104*$k) (176*$k) (18*$k)))
    # Onda wifi en la pantalla
    $pen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(37, 99, 235), [single][Math]::Max(1.5, 13*$k))
    $pen.StartCap = 'Round'; $pen.EndCap = 'Round'
    $g.DrawArc($pen, 90*$k, 82*$k, 76*$k, 76*$k, 225, 90)
    $g.DrawArc($pen, 108*$k, 104*$k, 40*$k, 40*$k, 225, 90)
    $g.FillEllipse((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(37, 99, 235))), 118*$k, 136*$k, 20*$k, 20*$k)
    # Botón inferior
    $g.FillEllipse([System.Drawing.Brushes]::White, 118*$k, 220*$k, 20*$k, 14*$k)
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += , $ms.ToArray()
    $bmp.Dispose()
}
$fs = [System.IO.File]::Create($salida)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$tamanos.Count)
$offset = 6 + 16 * $tamanos.Count
for ($i = 0; $i -lt $tamanos.Count; $i++) {
    $t = $tamanos[$i]; $d = if ($t -ge 256) { 0 } else { $t }
    $bw.Write([byte]$d); $bw.Write([byte]$d); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([UInt16]1); $bw.Write([UInt16]32); $bw.Write([UInt32]$pngs[$i].Length); $bw.Write([UInt32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $bw.Write($p) }
$bw.Close()
"Icono: $salida"
