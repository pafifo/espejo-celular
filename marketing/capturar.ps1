# Captura una ventana visible de la app por su título (recorta desde la pantalla, resolución real).
param([string]$Titulo, [string]$Salida)
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System; using System.Runtime.InteropServices; using System.Text;
public static class Cap {
  public delegate bool CB(IntPtr h, IntPtr p);
  [DllImport("user32.dll")] static extern bool EnumWindows(CB cb, IntPtr p);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
  [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int a, out R r, int s);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  public struct R { public int L,T,Ri,B; }
  public static IntPtr Buscar(string t) { IntPtr res = IntPtr.Zero; EnumWindows((h,p)=>{ if (!IsWindowVisible(h)) return true; var s=new StringBuilder(256); GetWindowText(h,s,256); if (s.ToString()==t) { res=h; return false; } return true; }, IntPtr.Zero); return res; }
  public static R Rect(IntPtr h) { R r; DwmGetWindowAttribute(h, 9, out r, 16); return r; }
}
'@
$h = [Cap]::Buscar($Titulo)
if ($h -eq [IntPtr]::Zero) { throw "No encontré la ventana '$Titulo'" }
[Cap]::SetForegroundWindow($h) | Out-Null; Start-Sleep -Milliseconds 400
$r = [Cap]::Rect($h)
$bmp = [System.Drawing.Bitmap]::new([int]($r.Ri - $r.L), [int]($r.B - $r.T))
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r.L, $r.T, 0, 0, $bmp.Size)
$bmp.Save($Salida, [System.Drawing.Imaging.ImageFormat]::Png)
"$Salida  $($bmp.Width)x$($bmp.Height)"
