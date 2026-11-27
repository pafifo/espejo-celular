using System.Diagnostics;
using System.Text;

namespace EspejoCelular;

public partial class MainForm : Form
{
    readonly Config cfg = Config.Cargar();
    Adb? adb;
    Process? scrcpy;

    readonly Label lblEstado, lblDetalle;
    readonly Panel punto;
    readonly Button btnConectar, btnDetener, btnVincular, btnOlvidar;
    readonly CheckBox chkVisible, chkApagar, chkDespierto, chkAudio, chkAuto, chkGrabar, chkFijo, chkInicio;
    readonly ComboBox cmbCalidad;
    readonly TextBox txtRegistro;

    static readonly Color Verde = Color.FromArgb(22, 163, 74);
    static readonly Color Gris = Color.FromArgb(148, 163, 184);
    static readonly Color Ambar = Color.FromArgb(217, 119, 6);
    static readonly Color Rojo = Color.FromArgb(220, 38, 38);
    // Línea gráfica Ojo Digital MediaLab
    static readonly Color Celeste = Color.FromArgb(0, 156, 222);   // #009cde
    static readonly Color Oscuro = Color.FromArgb(0, 119, 168);    // #0077a8
    static readonly Color Texto = Color.FromArgb(29, 35, 43);      // #1d232b
    static readonly Color Gris2 = Color.FromArgb(107, 116, 128);   // #6b7480
    static readonly Color Acento = Oscuro;

    const string SitioWeb = "https://ojodigital.cl";
    const string Repositorio = "https://github.com/pafifo/espejo-celular";
    static string Version => Application.ProductVersion.Split('+')[0];

    static void AbrirWeb(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
    }

    public MainForm()
    {
        Text = "Espejo Celular";
        Font = new Font("Segoe UI", 10f);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(500, 690);
        BackColor = Color.FromArgb(244, 246, 249); // #f4f6f9
        try { Icon = new Icon(typeof(MainForm).Assembly.GetManifestResourceStream("EspejoCelular.icono.ico")!); } catch { }

        // Cabecera de la marca: blanca, logo Ojo Digital, nombre, versión y línea celeste.
        var cabecera = new Panel { Location = new Point(0, 0), Size = new Size(500, 96), BackColor = Color.White };
        cabecera.Paint += (_, e) => { using var b = new SolidBrush(Celeste); e.Graphics.FillRectangle(b, 0, 93, cabecera.Width, 3); };
        var logo = new PictureBox { Location = new Point(18, 12), Size = new Size(98, 68), SizeMode = PictureBoxSizeMode.Zoom, Cursor = Cursors.Hand };
        try { logo.Image = Image.FromStream(typeof(MainForm).Assembly.GetManifestResourceStream("EspejoCelular.logo.png")!); } catch { }
        logo.Click += (_, _) => AbrirWeb(SitioWeb);
        cabecera.Controls.Add(logo);
        cabecera.Controls.Add(new Label
        {
            Text = "Espejo Celular", Font = new Font("Segoe UI Semibold", 17f),
            Location = new Point(128, 14), AutoSize = true, ForeColor = Texto,
        });
        var lblVersion = new Label
        {
            Text = "v" + Version, Font = new Font("Segoe UI", 9f), ForeColor = Gris2,
            AutoSize = true, Location = new Point(128, 0),
        };
        cabecera.Controls.Add(lblVersion);
        cabecera.Controls.Add(new Label
        {
            Text = "Tu celular en pantalla, manejado con el mouse\ny el teclado, por tu wifi.",
            Location = new Point(130, 50), Size = new Size(360, 40), ForeColor = Gris2, Font = new Font("Segoe UI", 9f),
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

        var guia = new LinkLabel { Text = "¿Primera vez? Cómo preparar el celular", Location = new Point(20, 268), AutoSize = true, LinkColor = Oscuro };
        guia.LinkClicked += (_, _) => { using var g = new GuiaForm(Icon); g.ShowDialog(this); };
        Controls.Add(guia);

        // Opciones
        var grupo = new GroupBox { Text = "Opciones", Location = new Point(20, 296), Size = new Size(460, 266) };
        chkVisible = Casilla(grupo, "Ventana siempre visible (encima de las demás)", 26, cfg.SiempreVisible);
        chkApagar = Casilla(grupo, "Apagar la pantalla del celular mientras lo uso aquí", 52, cfg.ApagarPantalla);
        chkDespierto = Casilla(grupo, "No dejar que el celular se bloquee", 78, cfg.MantenerDespierto);
        chkAudio = Casilla(grupo, "Escuchar el sonido del celular en el PC", 104, cfg.Audio);
        chkGrabar = Casilla(grupo, "Empezar a grabar apenas se abra el espejo", 130, cfg.Grabar);
        chkAuto = Casilla(grupo, "Conectar apenas abro la app", 156, cfg.ConectarAlAbrir);
        chkFijo = Casilla(grupo, "Encontrarlo siempre aunque cambie de IP (puerto fijo 5555)", 182, cfg.PuertoFijo);
        chkInicio = new CheckBox { Text = "Abrir al iniciar Windows (queda junto al reloj)", Location = new Point(14, 208), AutoSize = true, Checked = InicioConWindows };
        chkInicio.CheckedChanged += (_, _) => InicioConWindows = chkInicio.Checked;
        grupo.Controls.Add(chkInicio);
        grupo.Controls.Add(new Label { Text = "Calidad:", Location = new Point(14, 236), AutoSize = true });
        cmbCalidad = new ComboBox { Location = new Point(84, 232), Width = 360, DropDownStyle = ComboBoxStyle.DropDownList };
        cmbCalidad.Items.AddRange(new object[] { "Alta (nítida, wifi bueno)", "Media (recomendada)", "Ahorro (wifi lento)" });
        cmbCalidad.SelectedIndex = Math.Clamp(cfg.Calidad, 0, 2);
        cmbCalidad.SelectedIndexChanged += (_, _) => GuardarOpciones();
        grupo.Controls.Add(cmbCalidad);
        Controls.Add(grupo);

        Controls.Add(new Label
        {
            Text = "Clic = tocar · clic derecho = atrás · rueda = deslizar · Alt+H = inicio",
            Location = new Point(20, 568), Size = new Size(460, 22), ForeColor = Color.DimGray, Font = new Font("Segoe UI", 8.5f),
        });

        txtRegistro = new TextBox
        {
            Location = new Point(20, 594), Size = new Size(460, 104), Multiline = true, ReadOnly = true,
            ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 8.5f), BackColor = Color.White,
        };
        Controls.Add(txtRegistro);

        // Todo lo anterior baja para dejar sitio a la cabecera.
        foreach (Control c in Controls) c.Top += 24;
        Controls.Add(cabecera);
        // La versión va al lado del nombre.
        var nombre = cabecera.Controls[1];
        lblVersion.Location = new Point(nombre.Right + 4, nombre.Top + 12);

        // Pie con enlaces
        var web = new LinkLabel { Text = "ojodigital.cl", Location = new Point(20, 730), AutoSize = true, LinkColor = Oscuro };
        web.LinkClicked += (_, _) => AbrirWeb(SitioWeb);
        var gh = new LinkLabel { Text = "Código y descargas en GitHub", Location = new Point(116, 730), AutoSize = true, LinkColor = Oscuro };
        gh.LinkClicked += (_, _) => AbrirWeb(Repositorio);
        var hecho = new Label { Text = "Ojo Digital MediaLab", Location = new Point(330, 730), Size = new Size(150, 20), TextAlign = ContentAlignment.TopRight, ForeColor = Gris2 };
        Controls.AddRange(new Control[] { web, gh, hecho });
        ClientSize = new Size(500, 760);

        // Ayudas al pasar el mouse
        var tip = new ToolTip { AutoPopDelay = 12000, InitialDelay = 400, ReshowDelay = 200 };
        tip.SetToolTip(logo, "Ojo Digital MediaLab · abre ojodigital.cl");
        tip.SetToolTip(lblVersion, "Versión instalada de Espejo Celular");
        tip.SetToolTip(btnConectar, "Busca tu celular en el wifi y abre su pantalla en una ventana del PC.");
        tip.SetToolTip(btnDetener, "Cierra la ventana del celular. El celular queda vinculado.");
        tip.SetToolTip(btnVincular, "Primera vez o celular nuevo: lo vincula escaneando un código QR o con un código de 6 números.");
        tip.SetToolTip(btnOlvidar, "Borra el celular guardado en esta app. Para usarlo de nuevo hay que vincularlo otra vez.");
        tip.SetToolTip(guia, "Guía paso a paso para activar el modo desarrollador y la depuración inalámbrica.");
        tip.SetToolTip(chkVisible, "La ventana del celular queda siempre encima de las otras ventanas.");
        tip.SetToolTip(chkApagar, "La pantalla del celular se apaga para ahorrar batería; lo sigues viendo y usando en el PC.");
        tip.SetToolTip(chkDespierto, "Mientras está el espejo abierto, el celular no se bloquea solo.");
        tip.SetToolTip(chkAudio, "El sonido del celular (videos, música, notificaciones) sale por los parlantes del PC. Las llamadas no.");
        tip.SetToolTip(chkGrabar, "Al abrir el espejo empieza a grabar solo. También puedes grabar cuando quieras con el botón «Grabar» de la barra sobre el espejo. Los videos quedan en Videos\\Espejo Celular.");
        tip.SetToolTip(chkInicio, "Al prender el PC, Espejo Celular se abre solo y queda como ícono junto al reloj, sin mostrar la ventana.");
        tip.SetToolTip(chkAuto, "Al abrir la app se conecta sola, sin pulsar «Conectar y mostrar».");
        tip.SetToolTip(chkFijo, "Deja el celular escuchando en el puerto 5555 para encontrarlo aunque cambie de IP o Android apague la depuración inalámbrica. Se pierde al reiniciar el celular.");
        tip.SetToolTip(cmbCalidad, "Alta: más nítida, necesita buen wifi. Ahorro: para wifi lento o con cortes.");
        tip.SetToolTip(txtRegistro, "Registro de lo que hace la app. Sirve para ver por qué no conecta.");
        tip.SetToolTip(web, "Abre ojodigital.cl");
        tip.SetToolTip(gh, "Abre la página del proyecto en GitHub: código, novedades y nuevas versiones.");

        Shown += (_, _) => Arrancar();
        FormClosing += AlCerrar;
        CrearBandeja();
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
        cfg.PuertoFijo = chkFijo.Checked;
        cfg.Calidad = cmbCalidad.SelectedIndex;
        cfg.Guardar();
    }

    void Registro(string texto)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(() => Registro(texto)); return; }
        txtRegistro.AppendText($"{DateTime.Now:HH:mm:ss}  {texto}\r\n");
        // Copia en disco para revisar fallas: %APPDATA%\EspejoCelular\registro.txt (máx. ~1 MB)
        try
        {
            var arch = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EspejoCelular", "registro.txt");
            if (File.Exists(arch) && new FileInfo(arch).Length > 1_000_000) File.Delete(arch);
            File.AppendAllText(arch, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  v{Version}  {texto}\r\n");
        }
        catch { }
    }

    void Estado(string titulo, string detalle, Color color)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(() => Estado(titulo, detalle, color)); return; }
        lblEstado.Text = titulo;
        lblDetalle.Text = detalle;
        if (bandeja != null) { var s = "Espejo Celular · " + titulo; bandeja.Text = s.Length > 63 ? s[..63] : s; }
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
            Estado(NombreCelular, "Listo para conectar. Lo busco por su nombre en la red.", Gris);

        if (cfg.ConectarAlAbrir && (cfg.NombreMdns != null || cfg.UltimaDireccion != null))
            await ConectarYMostrar();
    }

    async Task Vincular()
    {
        if (adb == null) return;
        using var f = new VincularForm(adb, Icon);
        if (f.ShowDialog(this) == DialogResult.OK && f.Serial != null)
        {
            cfg.Modelo = cfg.UltimaDireccion = cfg.Serie = null; // celular nuevo: reemplaza al anterior
            cfg.NombreMdns = f.NombreMdns;
            await Recordar(f.Serial);
            Registro("Celular vinculado: " + NombreCelular);
            Estado(NombreCelular, "Vinculado y conectado. Pulsa «Conectar y mostrar».", Verde);
        }
    }

    void Olvidar()
    {
        cfg.Modelo = cfg.NombreMdns = cfg.UltimaDireccion = cfg.Serie = null;
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
        var serie = await adb.Propiedad(serial, "ro.serialno");
        if (!string.IsNullOrWhiteSpace(serie) && !serie.Contains("error") && !serie.Contains(' ')) cfg.Serie = serie;
        if (serial.Contains(':')) cfg.UltimaDireccion = serial;
        // Si adb lo conectó por su nombre mDNS («adb-XXXX-yyyy._adb-tls-connect._tcp»), se guarda el nombre.
        if (serial.StartsWith("adb-") && serial.Contains("._adb")) cfg.NombreMdns = serial[..serial.IndexOf("._adb")];
        cfg.Guardar();
    }

    /// Parte fija del nombre mDNS: «adb-<número de serie>-». El final cambia a veces
    /// (al reactivar la depuración), la serie no.
    string? PrefijoNombre
    {
        get
        {
            var n = cfg.NombreMdns;
            if (string.IsNullOrEmpty(n)) return null;
            int i = n.LastIndexOf('-');
            return n.StartsWith("adb-") && i > 4 ? n[..(i + 1)] : n;
        }
    }

    /// Número de serie guardado o, si falta (vinculado con una versión anterior), el que trae el nombre «adb-<serie>-xxxx».
    string? SerieEsperada
    {
        get
        {
            if (!string.IsNullOrEmpty(cfg.Serie)) return cfg.Serie;
            var n = cfg.NombreMdns;
            if (n == null || !n.StartsWith("adb-")) return null;
            int i = n.LastIndexOf('-');
            return i > 4 ? n[4..i] : null;
        }
    }

    bool EsMiNombre(string nombre) => PrefijoNombre is { } p && nombre.StartsWith(p, StringComparison.OrdinalIgnoreCase);

    /// Solo lo reconoce por nombre: una IP vieja puede ser hoy otro equipo.
    bool EsElRecordado(string serial) =>
        EsMiNombre(serial) || (PrefijoNombre == null && cfg.UltimaDireccion != null && serial == cfg.UltimaDireccion);

    /// Encuentra el celular por su nombre en la red (mDNS), aunque haya cambiado de IP o de puerto.
    async Task<string?> EncontrarCelular()
    {
        if (adb == null) return null;
        var hayRecordado = cfg.NombreMdns != null || cfg.UltimaDireccion != null || cfg.Serie != null;

        var todos = await adb.Dispositivos();
        var listos = todos.Where(d => d.Listo).ToList();
        var ya = listos.FirstOrDefault(d => EsElRecordado(d.Serial));
        if (ya != null) return ya.Serial;
        if (!hayRecordado && listos.Count == 1) return listos[0].Serial; // ej. conectado por cable

        // Conexiones muertas a direcciones viejas: se sueltan para no quedar pegado a ellas.
        foreach (var d in todos.Where(d => !d.Listo && d.Serial.Contains(':')))
            await adb.Desconectar(d.Serial);

        // Conectado con otra dirección (ej. puerto fijo): se reconoce por su número de serie.
        if (SerieEsperada != null)
            foreach (var d in listos)
                if (await adb.Propiedad(d.Serial, "ro.serialno") == SerieEsperada) return d.Serial;

        // 1) Rápido: la última IP en el puerto fijo.
        var ipVieja = cfg.UltimaDireccion is { } u && u.Contains(':') ? u[..u.LastIndexOf(':')] : null;
        // El wifi del celular en ahorro de energía tarda en despertar: hasta 3 intentos.
        if (cfg.PuertoFijo && ipVieja != null)
            for (int i = 0; i < 3; i++)
            {
                if (await Adb.PuertoAbierto(ipVieja, 5555) && await ConectarSiEsMio(ipVieja + ":5555")) return ipVieja + ":5555";
                await Task.Delay(500);
            }

        // 2) Por su nombre en la red (mDNS).
        Estado(NombreCelular, "Buscando por su nombre en la red wifi…", Ambar);
        var probadas = new HashSet<string>();
        var limite = DateTime.Now.AddSeconds(cfg.PuertoFijo && SerieEsperada != null ? 8 : 20);
        while (DateTime.Now < limite)
        {
            // Dos buscadores en paralelo: el de adb y uno propio.
            var tAdb = adb.ServiciosMdns();
            var tPropio = MdnsBuscador.Buscar(2500);
            var servicios = (await tAdb).Concat(await tPropio)
                .Where(s => s.Tipo.StartsWith("_adb-tls-connect")).ToList();

            // adb a veces se conecta solo al verlo anunciado.
            var auto = (await adb.Dispositivos()).FirstOrDefault(d => d.Listo && EsElRecordado(d.Serial));
            if (auto != null) return auto.Serial;

            var candidatos = servicios.Where(x => EsMiNombre(x.Nombre)).ToList();
            if (!hayRecordado) candidatos = servicios;
            foreach (var s in candidatos.Where(c => probadas.Add(c.Direccion)))
            {
                Registro($"Encontrado por nombre ({s.Nombre}) en {s.Direccion}");
                if (await adb.Conectar(s.Direccion))
                {
                    cfg.NombreMdns = s.Nombre; // por si cambió el final del nombre
                    return s.Direccion;
                }
            }
            if (servicios.Count > 0 && candidatos.Count == 0)
                Registro("En la red hay otro celular con depuración, pero no es " + NombreCelular + ".");
            await Task.Delay(500);
        }

        // 3) Recorrer la red buscando el puerto fijo y confirmar por número de serie (aunque cambió de IP).
        if (cfg.PuertoFijo && SerieEsperada != null)
        {
            Estado(NombreCelular, "Recorriendo la red wifi…", Ambar);
            foreach (var ip in await Adb.BuscarPuertoEnRed(5555))
                if (await ConectarSiEsMio(ip + ":5555"))
                {
                    Registro($"Encontrado por su número de serie en {ip}");
                    return ip + ":5555";
                }
        }

        if (cfg.UltimaDireccion != null)
        {
            Registro("Probando la última dirección conocida: " + cfg.UltimaDireccion);
            if (await adb.Conectar(cfg.UltimaDireccion)) return cfg.UltimaDireccion;
        }
        return null;
    }

    /// Conecta a la dirección y la deja solo si es el celular recordado (mismo número de serie).
    async Task<bool> ConectarSiEsMio(string direccion)
    {
        if (adb == null || !await adb.Conectar(direccion)) return false;
        for (int i = 0; i < 5; i++)
        {
            var serie = await adb.Propiedad(direccion, "ro.serialno");
            if (serie == SerieEsperada) return true;
            if (!string.IsNullOrEmpty(serie) && !serie.Contains("error") && !serie.Contains("unauthorized")) break;
            await Task.Delay(800);
        }
        await adb.Desconectar(direccion);
        return false;
    }

    /// Deja el celular escuchando en el puerto fijo 5555 para encontrarlo la próxima vez
    /// aunque Android apague la depuración inalámbrica o cambie la IP. Devuelve el serial a usar.
    async Task<string> AsegurarPuertoFijo(string serial)
    {
        if (adb == null || !cfg.PuertoFijo || serial.EndsWith(":5555")) return serial;
        var ip = serial.Contains(':') && !serial.StartsWith("adb-") ? serial[..serial.LastIndexOf(':')] : null;
        if (ip == null)
        {
            var ruta = await adb.Ejecutar($"-s {serial} shell ip -4 -o addr show wlan0", 8000);
            var m = System.Text.RegularExpressions.Regex.Match(ruta.Salida, @"inet (\d+\.\d+\.\d+\.\d+)");
            if (!m.Success) return serial;
            ip = m.Groups[1].Value;
        }
        if (!await Adb.PuertoAbierto(ip, 5555))
        {
            Registro("Activando el puerto fijo 5555 del celular (para encontrarlo siempre)…");
            await adb.PuertoFijo(serial);
            await Task.Delay(2500);
        }
        for (int i = 0; i < 5; i++)
        {
            if (await adb.Conectar(ip + ":5555"))
            {
                var d = (await adb.Dispositivos()).FirstOrDefault(x => x.Serial == ip + ":5555");
                if (d?.Listo == true) return ip + ":5555";
            }
            await Task.Delay(1000);
        }
        // No resultó: volver a la conexión original.
        if (serial.Contains(':')) await adb.Conectar(serial);
        return serial;
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
                MessageBox.Show(this,
                    $"No encontré {NombreCelular} en el wifi.\n\n" +
                    "Lo más común es que el celular se haya reiniciado o apagado. En ese caso:\n\n" +
                    "  1. En el celular abre Ajustes › Opciones de desarrollador.\n" +
                    "  2. Activa «Depuración inalámbrica».\n" +
                    "  3. Aquí pulsa otra vez «Conectar y mostrar».\n\n" +
                    "Después de conectar puedes volver a apagar la Depuración inalámbrica: " +
                    "la app lo seguirá encontrando hasta el próximo reinicio del celular.\n\n" +
                    "Si no es eso, revisa que el celular y el PC estén en el mismo wifi. " +
                    "Si cambiaste de celular, usa «Vincular celular nuevo…».",
                    "No encontré el celular", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            serial = await AsegurarPuertoFijo(serial);
            if (serial.Contains(':') && !serial.StartsWith("adb-")) { cfg.UltimaDireccion = serial; cfg.Guardar(); }
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
        // La grabación va aparte (botón «Grabar» de la barra), para poder empezarla y pararla cuando uno quiera.
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
        p.Exited += (_, _) => BeginInvoke(async () =>
        {
            var codigo = p.ExitCode;
            scrcpy = null;
            CerrarBarra();
            if (grabadora.Activa) await DetenerGrabacion();
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
        serialEspejo = serial;
        btnDetener.Enabled = true;
        btnConectar.Enabled = false;
        Estado(NombreCelular, "Conectado por wifi · " + serial, Verde);
        MontarBarra(p);
    }

    void CerrarEspejo()
    {
        CerrarBarra();
        if (grabadora.Activa) grabadora.DetenerYa();
        try { if (scrcpy is { HasExited: false }) scrcpy.Kill(true); } catch { }
    }

    // ---- Barra de captura / grabación / WhatsApp sobre la ventana del espejo ----

    BarraEspejo? barra;
    string? serialEspejo;
    readonly Grabadora grabadora = new();
    readonly System.Windows.Forms.Timer relojGrabacion = new() { Interval = 1000 };

    async void MontarBarra(Process p)
    {
        // La ventana de scrcpy aparece unos segundos después de iniciar el proceso.
        IntPtr hwnd = IntPtr.Zero;
        for (int i = 0; i < 100 && hwnd == IntPtr.Zero; i++)
        {
            await Task.Delay(200);
            if (scrcpy != p || p.HasExited) return;
            hwnd = BarraEspejo.VentanaDe(p.Id);
        }
        if (hwnd == IntPtr.Zero) return;

        CerrarBarra();
        barra = new BarraEspejo(hwnd, cfg.SiempreVisible);
        barra.Diag += s => BeginInvoke(() => Registro(s));
        Registro("Barra lista sobre el espejo.");
        barra.Captura.Click += async (_, _) => await Capturar(false);
        barra.WhatsApp.Click += async (_, _) => await Capturar(true);
        grabadora.Linea -= LineaGrabacion;
        grabadora.Linea += LineaGrabacion;
        barra.Grabar.Click += async (_, _) =>
        {
            try
            {
                if (!barra.Grabar.Enabled) return;
                if (grabadora.Activa) await DetenerGrabacion(); else await IniciarGrabacion();
            }
            catch (Exception ex) { Registro("Error con la grabación: " + ex.Message); }
        };
        barra.Show();
        PintarGrabacion();
        if (cfg.Grabar && !grabadora.Activa) await IniciarGrabacion();
    }

    void LineaGrabacion(string l)
    {
        try { BeginInvoke(() => Registro(l)); } catch { }
    }

    void CerrarBarra()
    {
        var b = barra;
        if (b != null) Registro("Cerrando la barra.");
        barra = null;
        try { if (b is { IsDisposed: false }) b.Close(); } catch { }
    }

    static string CarpetaCapturas
    {
        get
        {
            var c = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Espejo Celular");
            Directory.CreateDirectory(c);
            return c;
        }
    }

    async Task Capturar(bool paraWhatsApp)
    {
        if (adb == null || serialEspejo == null || barra == null) return;
        var boton = paraWhatsApp ? barra.WhatsApp : barra.Captura;
        if (!boton.Enabled) return;
        boton.Enabled = false;
        var textoOriginal = boton.Texto;
        boton.Texto = "Capturando…";
        try
        {
            var png = await adb.Captura(serialEspejo);
            if (png == null)
            {
                Registro("No se pudo sacar la captura del celular.");
                boton.Texto = textoOriginal;
                barra?.Avisar(boton, "No se pudo, reintenta");
                return;
            }
            var archivo = Path.Combine(CarpetaCapturas, $"Captura {DateTime.Now:yyyy-MM-dd HH.mm.ss}.png");
            await File.WriteAllBytesAsync(archivo, png);

            using (var ms = new MemoryStream(png))
            using (var img = new Bitmap(ms))
            {
                var datos = new DataObject();
                datos.SetImage(new Bitmap(img));
                datos.SetData("PNG", false, new MemoryStream(png));
                Clipboard.SetDataObject(datos, true, 5, 100);
            }
            Registro("Captura copiada y guardada en " + archivo);
            boton.Texto = textoOriginal;

            if (!paraWhatsApp)
            {
                barra?.Avisar(boton, "Copiada ✓");
                return;
            }
            AbrirWhatsApp();
            barra?.Avisar(boton, "Elige el chat y pega con Ctrl+V", 6000);
        }
        catch (Exception ex)
        {
            Registro("Error al capturar: " + ex.Message);
            boton.Texto = textoOriginal;
        }
        finally { if (!boton.IsDisposed) boton.Enabled = true; }
    }

    static void AbrirWhatsApp()
    {
        // WhatsApp de escritorio si está instalado; si no, WhatsApp Web en el navegador.
        try { Process.Start(new ProcessStartInfo("whatsapp:") { UseShellExecute = true }); return; } catch { }
        AbrirWeb("https://web.whatsapp.com");
    }

    async Task IniciarGrabacion()
    {
        if (adb == null || serialEspejo == null || barra == null) return;
        barra.Grabar.Enabled = false;
        barra.Grabar.Texto = "Iniciando…";
        bool ok = await grabadora.Iniciar(adb.ScrcpyExe, serialEspejo, cfg.Calidad, cfg.Audio);
        if (barra == null) { if (ok) await grabadora.Detener(); return; }
        barra.Grabar.Enabled = true;
        if (!ok)
        {
            Registro("Este celular no permitió grabar mientras se ve el espejo.");
            PintarGrabacion();
            barra.Avisar(barra.Grabar, "No se pudo grabar", 4000);
            return;
        }
        Registro("Grabando en " + grabadora.Archivo);
        relojGrabacion.Tick -= TicGrabacion;
        relojGrabacion.Tick += TicGrabacion;
        relojGrabacion.Start();
        PintarGrabacion();
    }

    void TicGrabacion(object? s, EventArgs e) => PintarGrabacion();

    async Task DetenerGrabacion()
    {
        relojGrabacion.Stop();
        if (barra != null) { barra.Grabar.Enabled = false; barra.Grabar.Texto = "Guardando…"; }
        var archivo = await grabadora.Detener();
        if (barra != null) { barra.Grabar.Enabled = true; PintarGrabacion(); }
        if (archivo == null) { Registro("La grabación no quedó guardada."); return; }
        Registro("Video guardado: " + archivo);
        if (barra != null) barra.Avisar(barra.Grabar, "Video guardado ✓", 3000);
        // Mostrar el video recién grabado en el Explorador.
        try { Process.Start("explorer.exe", $"/select,\"{archivo}\""); } catch { }
    }

    void PintarGrabacion()
    {
        if (barra == null) return;
        var b = barra.Grabar;
        if (grabadora.Activa)
        {
            var t = DateTime.Now - grabadora.Inicio;
            b.Fondo = Rojo; b.Tinta = Color.White; b.Icono2 = Color.White; b.Contorno = null;
            b.CambiarIcono("");
            b.Texto = $"Detener {(int)t.TotalMinutes:00}:{t.Seconds:00}";
            barra.Tip.SetToolTip(b, "Grabando. Clic para detener y guardar el video en Videos\\Espejo Celular.");
        }
        else
        {
            b.Fondo = Color.White; b.Tinta = Texto; b.Icono2 = Rojo; b.Contorno = Color.FromArgb(195, 202, 212);
            b.CambiarIcono("");
            b.Texto = "Grabar";
            barra.Tip.SetToolTip(b, "Empieza a grabar en video la pantalla del celular, con sonido. Queda en Videos\\Espejo Celular.");
        }
    }
}