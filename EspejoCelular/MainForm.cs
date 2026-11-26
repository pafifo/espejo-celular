using System.Diagnostics;
using System.Text;

namespace EspejoCelular;

public class MainForm : Form
{
    readonly Config cfg = Config.Cargar();
    Adb? adb;
    Process? scrcpy;

    readonly Label lblEstado, lblDetalle;
    readonly Panel punto;
    readonly Button btnConectar, btnDetener, btnVincular, btnOlvidar;
    readonly CheckBox chkVisible, chkApagar, chkDespierto, chkAudio, chkAuto, chkGrabar;
    readonly ComboBox cmbCalidad;
    readonly TextBox txtRegistro;

    static readonly Color Verde = Color.FromArgb(22, 163, 74);
    static readonly Color Gris = Color.FromArgb(148, 163, 184);
    static readonly Color Ambar = Color.FromArgb(217, 119, 6);
    static readonly Color Rojo = Color.FromArgb(220, 38, 38);
    static readonly Color Acento = Color.FromArgb(37, 99, 235);

    public MainForm()
    {
        Text = "Espejo Celular";
        Font = new Font("Segoe UI", 10f);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(500, 664);
        BackColor = Color.FromArgb(248, 250, 252);
        try { Icon = new Icon(typeof(MainForm).Assembly.GetManifestResourceStream("EspejoCelular.icono.ico")!); } catch { }

        Controls.Add(new Label
        {
            Text = "Espejo Celular", Font = new Font("Segoe UI Semibold", 17f),
            Location = new Point(20, 14), AutoSize = true, ForeColor = Color.FromArgb(15, 23, 42),
        });
        Controls.Add(new Label
        {
            Text = "Tu celular en pantalla, manejado con el mouse y el teclado, por tu wifi.",
            Location = new Point(22, 52), Size = new Size(460, 22), ForeColor = Color.DimGray,
        });

        // Tarjeta de estado
        var tarjeta = new Panel { Location = new Point(20, 84), Size = new Size(460, 70), BackColor = Color.White };
        tarjeta.Paint += (_, e) => e.Graphics.DrawRectangle(new Pen(Color.FromArgb(226, 232, 240)), 0, 0, tarjeta.Width - 1, tarjeta.Height - 1);
        punto = new Panel { Location = new Point(16, 18), Size = new Size(14, 14), BackColor = Gris };
        punto.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.Clear(Color.White);
            e.Graphics.FillEllipse(new SolidBrush(punto.Tag as Color? ?? Gris), 0, 0, 13, 13);
        };
        lblEstado = new Label { Location = new Point(38, 12), Size = new Size(410, 26), Font = new Font("Segoe UI Semibold", 11f), Text = "Preparando…" };
        lblDetalle = new Label { Location = new Point(38, 38), Size = new Size(410, 24), ForeColor = Color.DimGray };
        tarjeta.Controls.AddRange(new Control[] { punto, lblEstado, lblDetalle });
        Controls.Add(tarjeta);

        btnConectar = new Button
        {
            Text = "Conectar y mostrar", Location = new Point(20, 168), Size = new Size(300, 48),
            BackColor = Acento, ForeColor = Color.White, FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 11f), Cursor = Cursors.Hand,
        };
        btnConectar.FlatAppearance.BorderSize = 0;
        btnConectar.Click += async (_, _) => await ConectarYMostrar();
        Controls.Add(btnConectar);

        btnDetener = new Button { Text = "Cerrar espejo", Location = new Point(330, 168), Size = new Size(150, 48), Enabled = false };
        btnDetener.Click += (_, _) => CerrarEspejo();
        Controls.Add(btnDetener);

        btnVincular = new Button { Text = "Vincular celular nuevo…", Location = new Point(20, 226), Size = new Size(300, 34) };
        btnVincular.Click += async (_, _) => await Vincular();
        Controls.Add(btnVincular);

        btnOlvidar = new Button { Text = "Olvidar celular", Location = new Point(330, 226), Size = new Size(150, 34) };
        btnOlvidar.Click += (_, _) => Olvidar();
        Controls.Add(btnOlvidar);

        var guia = new LinkLabel { Text = "¿Primera vez? Cómo preparar el celular", Location = new Point(20, 268), AutoSize = true };
        guia.LinkClicked += (_, _) => { using var g = new GuiaForm(Icon); g.ShowDialog(this); };
        Controls.Add(guia);

        // Opciones
        var grupo = new GroupBox { Text = "Opciones", Location = new Point(20, 296), Size = new Size(460, 214) };
        chkVisible = Casilla(grupo, "Ventana siempre visible (encima de las demás)", 26, cfg.SiempreVisible);
        chkApagar = Casilla(grupo, "Apagar la pantalla del celular mientras lo uso aquí", 52, cfg.ApagarPantalla);
        chkDespierto = Casilla(grupo, "No dejar que el celular se bloquee", 78, cfg.MantenerDespierto);
        chkAudio = Casilla(grupo, "Escuchar el sonido del celular en el PC", 104, cfg.Audio);
        chkGrabar = Casilla(grupo, "Grabar la sesión en video (carpeta Videos)", 130, cfg.Grabar);
        chkAuto = Casilla(grupo, "Conectar apenas abro la app", 156, cfg.ConectarAlAbrir);
        grupo.Controls.Add(new Label { Text = "Calidad:", Location = new Point(14, 184), AutoSize = true });
        cmbCalidad = new ComboBox { Location = new Point(84, 180), Width = 360, DropDownStyle = ComboBoxStyle.DropDownList };
        cmbCalidad.Items.AddRange(new object[] { "Alta (nítida, wifi bueno)", "Media (recomendada)", "Ahorro (wifi lento)" });
        cmbCalidad.SelectedIndex = Math.Clamp(cfg.Calidad, 0, 2);
        cmbCalidad.SelectedIndexChanged += (_, _) => GuardarOpciones();
        grupo.Controls.Add(cmbCalidad);
        Controls.Add(grupo);

        Controls.Add(new Label
        {
            Text = "Clic = tocar · clic derecho = atrás · rueda = deslizar · Alt+H = inicio",
            Location = new Point(20, 516), Size = new Size(460, 22), ForeColor = Color.DimGray, Font = new Font("Segoe UI", 8.5f),
        });

        txtRegistro = new TextBox
        {
            Location = new Point(20, 542), Size = new Size(460, 104), Multiline = true, ReadOnly = true,
            ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 8.5f), BackColor = Color.White,
        };
        Controls.Add(txtRegistro);

        Shown += async (_, _) => await Iniciar();
        FormClosing += (_, _) => { GuardarOpciones(); CerrarEspejo(); };
    }

    CheckBox Casilla(GroupBox g, string texto, int y, bool valor)
    {
        var c = new CheckBox { Text = texto, Location = new Point(14, y), AutoSize = true, Checked = valor };
        c.CheckedChanged += (_, _) => GuardarOpciones();
        g.Controls.Add(c);
        return c;
    }

    void GuardarOpciones()
    {
        cfg.SiempreVisible = chkVisible.Checked;
        cfg.ApagarPantalla = chkApagar.Checked;
        cfg.MantenerDespierto = chkDespierto.Checked;
        cfg.Audio = chkAudio.Checked;
        cfg.Grabar = chkGrabar.Checked;
        cfg.ConectarAlAbrir = chkAuto.Checked;
        cfg.Calidad = cmbCalidad.SelectedIndex;
        cfg.Guardar();
    }

    void Registro(string texto)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(() => Registro(texto)); return; }
        txtRegistro.AppendText($"{DateTime.Now:HH:mm:ss}  {texto}\r\n");
    }

    void Estado(string titulo, string detalle, Color color)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(() => Estado(titulo, detalle, color)); return; }
        lblEstado.Text = titulo;
        lblDetalle.Text = detalle;
        punto.Tag = color;
        punto.Invalidate();
    }

    void Ocupado(bool si)
    {
        btnConectar.Enabled = !si && scrcpy == null;
        btnVincular.Enabled = !si;
        btnOlvidar.Enabled = !si;
        UseWaitCursor = si;
    }

    string NombreCelular => string.IsNullOrEmpty(cfg.Modelo) ? "tu celular" : cfg.Modelo!;

    async Task Iniciar()
    {
        var ruta = Adb.BuscarScrcpy(cfg.ScrcpyPath);
        if (ruta == null)
        {
            Estado("No encontré scrcpy", "Instálalo con: winget install Genymobile.scrcpy", Rojo);
            Registro("scrcpy.exe no está en el PATH ni en la carpeta de winget.");
            btnConectar.Enabled = btnVincular.Enabled = false;
            return;
        }
        cfg.ScrcpyPath = ruta;
        cfg.Guardar();
        adb = new Adb(ruta);
        Registro("Motor: " + ruta);

        Ocupado(true);
        await adb.IniciarServidor();
        Ocupado(false);

        if (string.IsNullOrEmpty(cfg.NombreMdns) && string.IsNullOrEmpty(cfg.UltimaDireccion))
            Estado("Ningún celular vinculado", "Pulsa «Vincular celular nuevo…» la primera vez.", Gris);
        else
            Estado(NombreCelular, "Listo para conectar" + (cfg.UltimaDireccion != null ? $" · última vez en {cfg.UltimaDireccion}" : ""), Gris);

        if (cfg.ConectarAlAbrir && (cfg.NombreMdns != null || cfg.UltimaDireccion != null))
            await ConectarYMostrar();
    }

    async Task Vincular()
    {
        if (adb == null) return;
        using var f = new VincularForm(adb, Icon);
        if (f.ShowDialog(this) == DialogResult.OK && f.Serial != null)
        {
            cfg.Modelo = cfg.UltimaDireccion = null; // celular nuevo: reemplaza al anterior
            cfg.NombreMdns = f.NombreMdns;
            await Recordar(f.Serial);
            Registro("Celular vinculado: " + NombreCelular);
            Estado(NombreCelular, "Vinculado y conectado. Pulsa «Conectar y mostrar».", Verde);
        }
    }

    void Olvidar()
    {
        cfg.Modelo = cfg.NombreMdns = cfg.UltimaDireccion = null;
        cfg.Olvidado = true;
        cfg.Guardar();
        cfg.Olvidado = false;
        Estado("Ningún celular vinculado", "Pulsa «Vincular celular nuevo…».", Gris);
        Registro("Celular olvidado en esta app (en el celular puedes quitar este PC en Depuración inalámbrica).");
    }

    /// Guarda modelo y dirección del celular conectado.
    async Task Recordar(string serial)
    {
        if (adb == null) return;
        var nombre = await adb.Propiedad(serial, "ro.product.marketname");
        if (string.IsNullOrWhiteSpace(nombre)) nombre = await adb.Propiedad(serial, "ro.product.model");
        if (!string.IsNullOrWhiteSpace(nombre) && !nombre.Contains("error")) cfg.Modelo = nombre;
        if (serial.Contains(':')) cfg.UltimaDireccion = serial;
        cfg.Guardar();
    }

    bool EsElRecordado(string serial)
    {
        if (cfg.UltimaDireccion != null && serial == cfg.UltimaDireccion) return true;
        if (cfg.NombreMdns != null && serial.StartsWith(cfg.NombreMdns)) return true;
        if (cfg.UltimaDireccion != null && serial.Contains(':'))
        {
            var ip = cfg.UltimaDireccion[..cfg.UltimaDireccion.LastIndexOf(':')];
            if (serial.StartsWith(ip + ":")) return true;
        }
        return false;
    }

    /// Encuentra el celular: ya conectado, anunciado por mDNS en la red, o en la última dirección.
    async Task<string?> EncontrarCelular()
    {
        if (adb == null) return null;
        var hayRecordado = cfg.NombreMdns != null || cfg.UltimaDireccion != null;

        var listos = (await adb.Dispositivos()).Where(d => d.Listo).ToList();
        var ya = listos.FirstOrDefault(d => EsElRecordado(d.Serial));
        if (ya != null) return ya.Serial;
        if (!hayRecordado && listos.Count == 1) return listos[0].Serial; // ej. conectado por cable

        Estado(NombreCelular, "Buscando en la red wifi…", Ambar);
        for (int intento = 0; intento < 4; intento++)
        {
            var servicios = (await adb.ServiciosMdns()).Where(s => s.Tipo.StartsWith("_adb-tls-connect")).ToList();
            var s = servicios.FirstOrDefault(x => x.Nombre == cfg.NombreMdns)
                    ?? (hayRecordado ? null : servicios.FirstOrDefault());
            if (s != null)
            {
                Registro($"Encontrado en la red: {s.Direccion}");
                if (await adb.Conectar(s.Direccion))
                {
                    cfg.NombreMdns ??= s.Nombre;
                    return s.Direccion;
                }
            }
            await Task.Delay(1200);
        }

        if (cfg.UltimaDireccion != null)
        {
            Registro("Probando la última dirección conocida: " + cfg.UltimaDireccion);
            if (await adb.Conectar(cfg.UltimaDireccion)) return cfg.UltimaDireccion;
        }
        return null;
    }

    async Task ConectarYMostrar()
    {
        if (adb == null || scrcpy != null) return;
        Ocupado(true);
        try
        {
            Estado(NombreCelular, "Conectando…", Ambar);
            var serial = await EncontrarCelular();
            if (serial == null)
            {
                Estado("No encontré " + NombreCelular,
                    "Revisa: mismo wifi y «Depuración inalámbrica» activada.", Rojo);
                Registro("Sin conexión. Si el celular se reinició, vuelve a activar Depuración inalámbrica." +
                         " Si cambiaste de celular o de red, usa «Vincular celular nuevo…».");
                return;
            }

            // Esperar a que quede autorizado
            for (int i = 0; i < 10; i++)
            {
                var d = (await adb.Dispositivos()).FirstOrDefault(x => x.Serial == serial);
                if (d?.Listo == true) break;
                if (d?.Estado == "unauthorized") Estado(NombreCelular, "Acepta el aviso «¿Permitir depuración?» en el celular.", Ambar);
                await Task.Delay(1000);
            }

            await Recordar(serial);
            AbrirEspejo(serial);
        }
        finally { Ocupado(false); }
    }

    string ArgumentosScrcpy(string serial)
    {
        var a = new List<string> { "-s", serial, "--window-title", $"\"{NombreCelular}\"" };
        if (cfg.SiempreVisible) a.Add("--always-on-top");
        if (cfg.ApagarPantalla) a.Add("--turn-screen-off");
        if (cfg.MantenerDespierto) a.Add("--stay-awake");
        if (!cfg.Audio) a.Add("--no-audio");
        a.AddRange(cfg.Calidad switch
        {
            0 => new[] { "--max-size", "1920", "--video-bit-rate", "16M", "--max-fps", "60" },
            2 => new[] { "--max-size", "1024", "--video-bit-rate", "3M", "--max-fps", "30" },
            _ => new[] { "--max-size", "1600", "--video-bit-rate", "8M", "--max-fps", "60" },
        });
        if (cfg.Grabar)
        {
            var carpeta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Espejo Celular");
            Directory.CreateDirectory(carpeta);
            a.Add("--record");
            a.Add($"\"{Path.Combine(carpeta, $"Celular {DateTime.Now:yyyy-MM-dd HH.mm.ss}.mp4")}\"");
        }
        return string.Join(" ", a);
    }

    void AbrirEspejo(string serial)
    {
        if (adb == null) return;
        var args = ArgumentosScrcpy(serial);
        var psi = new ProcessStartInfo(adb.ScrcpyExe, args)
        {
            UseShellExecute = false,
            CreateNoWindow = true,              // sin consola negra
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = Path.GetDirectoryName(adb.ScrcpyExe)!,
        };
        var errores = new StringBuilder();
        var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
        void Linea(string? l)
        {
            if (string.IsNullOrWhiteSpace(l)) return;
            if (l.StartsWith("ERROR") || l.StartsWith("WARN")) lock (errores) errores.AppendLine(l);
            Registro(l.Trim());
        }
        p.OutputDataReceived += (_, e) => Linea(e.Data);
        p.ErrorDataReceived += (_, e) => Linea(e.Data);
        p.Exited += (_, _) => BeginInvoke(() =>
        {
            var codigo = p.ExitCode;
            scrcpy = null;
            btnDetener.Enabled = false;
            btnConectar.Enabled = true;
            string err; lock (errores) err = errores.ToString();
            if (codigo == 0 || codigo == 2)
                Estado(NombreCelular, "Espejo cerrado.", Gris);
            else
            {
                Estado("Se cortó la conexión", "Detalle en el registro de abajo.", Rojo);
                if (err.Contains("INJECT_EVENTS") || err.Contains("injecting"))
                    Registro("Se ve pero no responde al mouse: en Xiaomi/Redmi activa «Depuración USB (ajustes de seguridad)» en Opciones de desarrollador.");
            }
            p.Dispose();
        });

        Registro("Abriendo espejo: scrcpy " + args);
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        scrcpy = p;
        btnDetener.Enabled = true;
        btnConectar.Enabled = false;
        Estado(NombreCelular, "Conectado por wifi · " + serial, Verde);
    }

    void CerrarEspejo()
    {
        try { if (scrcpy is { HasExited: false }) scrcpy.Kill(true); } catch { }
    }
}
