using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text;

namespace EspejoCelular;

/// Barra pequeña que va pegada arriba de la ventana del espejo (la de scrcpy) y la sigue
/// cuando se mueve. scrcpy no deja poner botones dentro de su ventana, así que va encima.
public class BarraEspejo : Form
{
    IntPtr espejo;
    public event Action<string>? Diag;
    readonly uint pid;
    readonly WinEventDelegate alMoverse, alFrente;
    IntPtr gancho1, gancho2;
    readonly System.Windows.Forms.Timer respaldo = new() { Interval = 400 };

    public readonly BotonBarra Captura, Grabar, WhatsApp;
    public readonly ToolTip Tip = new() { AutoPopDelay = 10000, InitialDelay = 300, ReshowDelay = 150 };

    static readonly Color Borde = Color.FromArgb(195, 202, 212); // #c3cad4

    public BarraEspejo(IntPtr hwndEspejo, bool encima)
    {
        espejo = hwndEspejo;
        GetWindowThreadProcessId(espejo, out pid);
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.White;
        TopMost = encima;
        DoubleBuffered = true;
        Text = "Espejo Celular · barra";

        Captura = new BotonBarra("", "Copiar captura", Color.FromArgb(0, 119, 168), Color.White);
        Grabar = new BotonBarra("", "Grabar", Color.White, Color.FromArgb(29, 35, 43)) { Icono2 = Color.FromArgb(220, 38, 38), Contorno = Borde };
        WhatsApp = new BotonBarra("", "WhatsApp", Color.FromArgb(21, 128, 61), Color.White);
        Controls.AddRange(new Control[] { Captura, Grabar, WhatsApp });
        foreach (var b in new[] { Captura, Grabar, WhatsApp }) b.SizeChanged += (_, _) => Acomodar();

        Tip.SetToolTip(Captura, "Saca una foto de la pantalla del celular y la copia al portapapeles (pégala con Ctrl+V). También queda guardada en Imágenes\\Espejo Celular.");
        Tip.SetToolTip(Grabar, "Empieza o detiene la grabación en video de la pantalla del celular, con sonido. Queda en Videos\\Espejo Celular.");
        Tip.SetToolTip(WhatsApp, "Saca una captura, la copia y abre WhatsApp: elige el chat y pega con Ctrl+V.");

        alMoverse = (_, evento, hwnd, idObjeto, _, _, _) =>
        {
            if (hwnd == espejo && idObjeto == 0) BeginInvoke(Ubicar);
        };
        // Cuando se hace clic en el espejo (o en otra ventana), volver a poner la barra encima del espejo.
        alFrente = (_, evento, hwnd, idObjeto, _, _, _) =>
        {
            if (evento == 0x0003 || evento == 0x0016 || evento == 0x0017) BeginInvoke(Ubicar);
        };
        respaldo.Tick += (_, _) => Ubicar();
        Acomodar();
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x80; // WS_EX_TOOLWINDOW: no sale en Alt+Tab
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        // La ventana del espejo pasa a ser "dueña" de la barra: la barra queda siempre
        // encima de ella y se esconde sola cuando se minimiza.
        SetWindowLongPtr(Handle, -8, espejo);
        gancho1 = SetWinEventHook(0x800B, 0x800B, IntPtr.Zero, alMoverse, pid, 0, 0); // LOCATIONCHANGE
        gancho2 = SetWinEventHook(0x0003, 0x0017, IntPtr.Zero, alFrente, 0, 0, 0); // FOREGROUND … MINIMIZEEND (todas las apps)
        respaldo.Start();
        Ubicar();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        Diag?.Invoke("Barra: ventana destruida (cerrando=" + (closing ? "sí" : "no") + ").");
        base.OnHandleDestroyed(e);
    }

    bool closing;
    protected override void OnFormClosing(FormClosingEventArgs e) { closing = true; Diag?.Invoke("Barra: cierre " + e.CloseReason); base.OnFormClosing(e); }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        respaldo.Stop();
        if (gancho1 != IntPtr.Zero) UnhookWinEvent(gancho1);
        if (gancho2 != IntPtr.Zero) UnhookWinEvent(gancho2);
        base.OnFormClosed(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var p = new Pen(Borde);
        e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
    }

    int Px(int v) => (int)Math.Round(v * DeviceDpi / 96.0);

    bool vertical;   // en columna, al costado del espejo, cuando no cabe arriba

    Size Tamaño(bool enColumna)
    {
        int pad = Px(5), sep = Px(5);
        var bs = Controls.Cast<Control>().ToList();
        if (enColumna)
            return new Size(Controls.Cast<BotonBarra>().Max(b => b.AnchoNatural) + pad * 2, bs.Sum(b => b.Height) + sep * (bs.Count - 1) + pad * 2);
        return new Size(bs.Sum(b => b.Width) + sep * (bs.Count - 1) + pad * 2, bs.Max(b => b.Height) + pad * 2);
    }

    void Acomodar() => Acomodar(true);

    void Acomodar(bool reubicar)
    {
        int pad = Px(5), sep = Px(5), x = pad, y = pad;
        int ancho = Controls.Cast<BotonBarra>().Max(b => b.AnchoNatural);
        foreach (BotonBarra b in Controls)
        {
            b.AnchoMinimo = vertical ? ancho : 0;
            b.Location = new Point(x, y);
            if (vertical) y += b.Height + sep; else x += b.Width + sep;
        }
        ClientSize = Tamaño(vertical);
        if (reubicar && IsHandleCreated) Ubicar();
    }
    /// Pone la barra centrada justo arriba de la ventana del espejo
    /// (o dentro de su borde superior si no cabe arriba).
    public void Ubicar()
    {
        if (IsDisposed) return;
        if (!IsWindow(espejo))
        {
            // scrcpy a veces recrea su ventana: buscar la nueva del mismo proceso.
            var nueva = VentanaDe((int)pid);
            Diag?.Invoke($"La ventana del espejo cambió ({espejo} → {nueva}).");
            if (nueva == IntPtr.Zero) { Hide(); return; }
            espejo = nueva;
            SetWindowLongPtr(Handle, -8, espejo);
        }
        if (IsIconic(espejo) || !IsWindowVisible(espejo)) { if (Visible) Hide(); return; }

        if (DwmGetWindowAttribute(espejo, 9, out RECT r, Marshal.SizeOf<RECT>()) != 0) GetWindowRect(espejo, out r);
        var area = Screen.FromHandle(espejo).WorkingArea;
        int sep = Px(2), x, y;
        // Nunca encima de la imagen del celular (taparía la hora y la señal):
        // arriba si cabe; si no, al costado derecho; si no, al izquierdo; si no, abajo.
        var h = Tamaño(false);
        var v = Tamaño(true);
        bool columna;
        if (r.Top - h.Height - sep >= area.Top) { columna = false; x = r.Left + (r.Right - r.Left - h.Width) / 2; y = r.Top - h.Height - sep; }
        else if (r.Right + sep + v.Width <= area.Right) { columna = true; x = r.Right + sep; y = r.Top; }
        else if (r.Left - sep - v.Width >= area.Left) { columna = true; x = r.Left - sep - v.Width; y = r.Top; }
        else if (r.Bottom + sep + h.Height <= area.Bottom) { columna = false; x = r.Left + (r.Right - r.Left - h.Width) / 2; y = r.Bottom + sep; }
        else { columna = true; x = r.Right - v.Width; y = r.Bottom - v.Height; } // pantalla completa: esquina de abajo
        if (columna != vertical) { vertical = columna; Acomodar(false); }
        x = Math.Clamp(x, area.Left, Math.Max(area.Left, area.Right - Width));
        y = Math.Clamp(y, area.Top, Math.Max(area.Top, area.Bottom - Height));        if (!Visible) Show();
        // Justo encima del espejo en el orden de ventanas: detrás de la ventana que esté
        // sobre el espejo (o arriba de todo si el espejo es la primera).
        var sobre = GetWindow(espejo, 3); // GW_HWNDPREV
        if (sobre == Handle)
            SetWindowPos(Handle, IntPtr.Zero, x, y, 0, 0, 0x0001 | 0x0004 | 0x0010); // ya está bien: solo mover
        else
        {
            bool espejoArriba = (GetWindowLongPtr(espejo, -20).ToInt64() & 0x8) != 0; // WS_EX_TOPMOST
            var tras = sobre != IntPtr.Zero ? sobre : (espejoArriba ? new IntPtr(-1) : IntPtr.Zero);
            SetWindowPos(Handle, tras, x, y, 0, 0, 0x0001 | 0x0010); // NOSIZE|NOACTIVATE
            if (GetWindow(espejo, 3) != Handle) // si el sistema no lo dejó, al frente de todo
                SetWindowPos(Handle, espejoArriba ? new IntPtr(-1) : IntPtr.Zero, x, y, 0, 0, 0x0001 | 0x0010);
        }
    }

    /// Cambia el texto del botón un momento (por ejemplo «Copiada») y luego vuelve.
    public async void Avisar(BotonBarra b, string texto, int ms = 2200)
    {
        var antes = b.Texto;
        b.Texto = texto;
        await Task.Delay(ms);
        if (!b.IsDisposed && b.Texto == texto) b.Texto = antes;
    }

    // ---- Win32 ----
    delegate void WinEventDelegate(IntPtr hook, uint evento, IntPtr hwnd, int idObjeto, int idHijo, uint hilo, uint tiempo);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    [DllImport("user32.dll")] static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr mod, WinEventDelegate fn, uint pid, uint hilo, uint flags);
    [DllImport("user32.dll")] static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr h, ref POINT p);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr despues, int x, int y, int w, int alto, uint flags);
    [DllImport("user32.dll")] static extern IntPtr SetWindowLongPtr(IntPtr h, int indice, IntPtr valor);
    [DllImport("user32.dll")] static extern IntPtr GetWindowLongPtr(IntPtr h, int indice);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, uint cmd);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int atributo, out RECT r, int tam);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc fn, IntPtr l);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern int GetWindowTextLength(IntPtr h);

    /// Ventana principal visible de un proceso (la de scrcpy tarda un poco en aparecer).
    public static IntPtr VentanaDe(int idProceso)
    {
        IntPtr hallada = IntPtr.Zero;
        EnumWindows((h, _) =>
        {
            GetWindowThreadProcessId(h, out var p);
            if (p == idProceso && IsWindowVisible(h) && GetWindowTextLength(h) > 0) { hallada = h; return false; }
            return true;
        }, IntPtr.Zero);
        return hallada;
    }
}

/// Botón plano con ícono (Segoe Fluent Icons / MDL2) y texto, con los colores de la marca.
public class BotonBarra : Control
{
    string texto;
    readonly string icono;
    bool encima;
    public Color Fondo, Tinta;
    public Color? Icono2, Contorno;
    static readonly string FuenteIconos =
        FontFamily.Families.Any(f => f.Name == "Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";
    readonly Font fIcono = new(FuenteIconos, 11f);

    public BotonBarra(string icono, string texto, Color fondo, Color tinta)
    {
        this.icono = icono; this.texto = texto; Fondo = fondo; Tinta = tinta;
        Font = new Font("Segoe UI Semibold", 10.5f);
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.Selectable, false);
        Medir();
    }

    public string Texto { get => texto; set { texto = value; Medir(); Invalidate(); } }
    public string IconoActual { get; set; } = "";
    int anchoMinimo;
    public int AnchoNatural { get; private set; }
    public int AnchoMinimo { get => anchoMinimo; set { if (anchoMinimo != value) { anchoMinimo = value; Medir(); } } }

    int Px(int v) => (int)Math.Round(v * DeviceDpi / 96.0);

    void Medir()
    {
        var t = TextRenderer.MeasureText(texto, Font);
        var i = TextRenderer.MeasureText(IconoActual.Length > 0 ? IconoActual : icono, fIcono);
        AnchoNatural = Px(12) + i.Width + Px(2) + t.Width + Px(10);
        Size = new Size(Math.Max(AnchoMinimo, AnchoNatural), Math.Max(t.Height, i.Height) + Px(12));
    }

    public void CambiarIcono(string nuevo) { IconoActual = nuevo; Medir(); Invalidate(); }

    protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); Medir(); }
    protected override void OnMouseEnter(EventArgs e) { encima = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { encima = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Color.White);
        var fondo = Fondo;
        if (encima && Enabled) fondo = ControlPaint.Dark(fondo, 0.08f);
        if (Fondo == Color.White && encima) fondo = Color.FromArgb(236, 240, 245);
        using (var camino = Redondeado(new Rectangle(0, 0, Width - 1, Height - 1), Px(4)))
        {
            using var b = new SolidBrush(fondo);
            g.FillPath(b, camino);
            if (Contorno is Color c) { using var p = new Pen(c); g.DrawPath(p, camino); }
        }
        var tinta = Enabled ? Tinta : Color.FromArgb(150, Tinta);
        var ic = IconoActual.Length > 0 ? IconoActual : icono;
        var tamIcono = TextRenderer.MeasureText(ic, fIcono);
        var rIcono = new Rectangle(Px(8), 0, tamIcono.Width, Height);
        TextRenderer.DrawText(g, ic, fIcono, rIcono, Icono2 ?? tinta, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
        var rTexto = new Rectangle(rIcono.Right + Px(2), 0, Width - rIcono.Right - Px(2), Height);
        TextRenderer.DrawText(g, texto, Font, rTexto, tinta, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
    }

    static GraphicsPath Redondeado(Rectangle r, int radio)
    {
        var p = new GraphicsPath();
        int d = radio * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}

/// Graba la pantalla con una segunda conexión de scrcpy sin ventana, así el espejo
/// sigue abierto mientras se graba y se puede empezar o parar cuando uno quiera.
public class Grabadora
{
    Process? proceso;
    public string? Archivo { get; private set; }
    public DateTime Inicio { get; private set; }
    public bool Activa => proceso is { HasExited: false };
    public event Action<string>? Linea;

    static string Carpeta
    {
        get
        {
            var c = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Espejo Celular");
            Directory.CreateDirectory(c);
            return c;
        }
    }

    /// Prueba varias combinaciones: muchos celulares no permiten dos codificadores de video
    /// por hardware a la vez, así que primero usa el de software.
    public async Task<bool> Iniciar(string scrcpyExe, string serial, int calidad, bool audio)
    {
        if (Activa) return true;
        var tam = calidad switch { 0 => "1920", 2 => "1024", _ => "1600" };
        var bits = calidad switch { 0 => "12M", 2 => "3M", _ => "8M" };
        var intentos = new List<string>();
        if (audio) intentos.Add("--video-encoder=c2.android.avc.encoder --audio-source=playback --audio-dup --audio-codec=aac");
        intentos.Add("--video-encoder=c2.android.avc.encoder --no-audio");
        if (audio) intentos.Add("--audio-source=playback --audio-dup --audio-codec=aac");
        intentos.Add("--no-audio");

        foreach (var extra in intentos)
        {
            Archivo = Path.Combine(Carpeta, $"Celular {DateTime.Now:yyyy-MM-dd HH.mm.ss}.mp4");
            var args = $"-s {serial} --no-window --no-control --max-size {tam} --video-bit-rate {bits} --max-fps 30 {extra} --record \"{Archivo}\"";
            Linea?.Invoke("Grabando: scrcpy " + args);
            var psi = new ProcessStartInfo(scrcpyExe, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = Path.GetDirectoryName(scrcpyExe)!,
            };
            var p = new Process { StartInfo = psi };
            bool fallo = false;
            void L(string? l)
            {
                if (string.IsNullOrWhiteSpace(l)) return;
                if (l.Contains("ERROR")) fallo = true;
                Linea?.Invoke("[grabación] " + l.Trim());
            }
            p.OutputDataReceived += (_, e) => L(e.Data);
            p.ErrorDataReceived += (_, e) => L(e.Data);
            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            // Si el codificador no sirve, scrcpy se cae en los primeros segundos.
            for (int i = 0; i < 40 && !p.HasExited && !fallo; i++) await Task.Delay(100);
            if (!p.HasExited && !fallo)
            {
                proceso = p;
                Inicio = DateTime.Now;
                return true;
            }
            try { if (!p.HasExited) p.Kill(true); } catch { }
            p.Dispose();
            try { if (File.Exists(Archivo)) File.Delete(Archivo); } catch { }
        }
        Archivo = null;
        return false;
    }

    /// Detiene la grabación como si se apretara Ctrl+C, para que el video quede bien cerrado.
    public async Task<string?> Detener()
    {
        var p = proceso;
        proceso = null;
        if (p == null) return null;
        await Task.Run(() =>
        {
            if (!p.HasExited && !CtrlC(p))
                try { p.Kill(true); } catch { }
            p.WaitForExit(3000);
        }).ConfigureAwait(false);
        p.Dispose();
        return Archivo != null && File.Exists(Archivo) ? Archivo : null;
    }

    public void DetenerYa() => Detener().GetAwaiter().GetResult();

    static readonly object candado = new();

    static bool CtrlC(Process p)
    {
        lock (candado)
        {
            if (!AttachConsole((uint)p.Id)) return false;
            SetConsoleCtrlHandler(IntPtr.Zero, true); // que el Ctrl+C no cierre esta app
            try
            {
                GenerateConsoleCtrlEvent(0, 0);
                return p.WaitForExit(10000);
            }
            finally
            {
                FreeConsole();
                SetConsoleCtrlHandler(IntPtr.Zero, false);
            }
        }
    }

    [DllImport("kernel32.dll")] static extern bool AttachConsole(uint pid);
    [DllImport("kernel32.dll")] static extern bool FreeConsole();
    [DllImport("kernel32.dll")] static extern bool SetConsoleCtrlHandler(IntPtr fn, bool agregar);
    [DllImport("kernel32.dll")] static extern bool GenerateConsoleCtrlEvent(uint evento, uint grupo);
}
