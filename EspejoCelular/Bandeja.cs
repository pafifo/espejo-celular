using Microsoft.Win32;

namespace EspejoCelular;

/// Ícono junto al reloj: la X esconde la ventana y la app sigue ahí; con clic derecho
/// están las opciones y «Salir» para cerrarla del todo.
public partial class MainForm
{
    public static bool ArranqueOculto;   // abierta por Windows al iniciar (--inicio)
    NotifyIcon bandeja = null!;
    bool saliendo, iniciado, avisoBandejaDado;

    const string ClaveInicio = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValorInicio = "Espejo Celular";

    /// Abrir al iniciar Windows: valor en HKCU\...\Run (no necesita permisos de administrador).
    static bool InicioConWindows
    {
        get
        {
            try { using var k = Registry.CurrentUser.OpenSubKey(ClaveInicio); return k?.GetValue(ValorInicio) != null; }
            catch { return false; }
        }
        set
        {
            try
            {
                using var k = Registry.CurrentUser.CreateSubKey(ClaveInicio);
                if (value) k.SetValue(ValorInicio, $"\"{Environment.ProcessPath ?? Application.ExecutablePath}\" --inicio");
                else k.DeleteValue(ValorInicio, false);
            }
            catch { }
        }
    }

    /// Windows 11 esconde los íconos nuevos en la flechita ^. La primera vez se pide que quede
    /// visible junto al reloj; si el usuario después lo cambia en Configuración, se respeta.
    static async Task PedirIconoVisible()
    {
        await Task.Delay(3000); // Windows crea la entrada al registrar el ícono
        try
        {
            using var raiz = Registry.CurrentUser.OpenSubKey(@"Control Panel\NotifyIconSettings");
            if (raiz == null) return;
            foreach (var nombre in raiz.GetSubKeyNames())
            {
                using var k = raiz.OpenSubKey(nombre, true);
                if (k?.GetValue("ExecutablePath") is string ruta
                    && ruta.EndsWith(@"\EspejoCelular.exe", StringComparison.OrdinalIgnoreCase)
                    && k.GetValue("IsPromoted") == null)
                    k.SetValue("IsPromoted", 1, RegistryValueKind.DWord);
            }
        }
        catch { }
    }

    void Arrancar()
    {
        if (iniciado) return;
        iniciado = true;
        _ = Iniciar();
    }

    // Al abrirse con Windows la ventana no se muestra: queda solo el ícono junto al reloj.
    protected override void SetVisibleCore(bool value)
    {
        if (ArranqueOculto && value && !IsHandleCreated)
        {
            ArranqueOculto = false;
            CreateHandle();
            base.SetVisibleCore(false);
            BeginInvoke(Arrancar);
            return;
        }
        base.SetVisibleCore(value);
    }

    public void MostrarVentana()
    {
        if (IsDisposed) return;
        Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
        BringToFront();
        Arrancar();
    }

    void AlCerrar(object? s, FormClosingEventArgs e)
    {
        GuardarOpciones();
        // La X (o Alt+F4) solo esconde la ventana; el espejo y la grabación siguen.
        if (!saliendo && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            if (!avisoBandejaDado)
            {
                avisoBandejaDado = true;
                bandeja.ShowBalloonTip(5000, "Espejo Celular sigue abierto",
                    "Está junto al reloj (si no lo ves, en la flechita ^). Clic derecho en el ícono para ver las opciones o salir.", ToolTipIcon.Info);
            }
            return;
        }
        CerrarEspejo();
        bandeja.Visible = false;
        bandeja.Dispose();
    }

    void Salir()
    {
        saliendo = true;
        Close();
    }

    void CrearBandeja()
    {
        var menu = new ContextMenuStrip { Font = new Font("Segoe UI", 10f), ShowCheckMargin = true, ShowImageMargin = false };

        var abrir = new ToolStripMenuItem("Abrir Espejo Celular") { Font = new Font("Segoe UI Semibold", 10f) };
        abrir.Click += (_, _) => MostrarVentana();
        abrir.ToolTipText = "Muestra la ventana principal de la app.";

        var conectar = new ToolStripMenuItem("Conectar y mostrar");
        conectar.Click += async (_, _) =>
        {
            if (scrcpy != null) CerrarEspejo();
            else if (btnConectar.Enabled) await ConectarYMostrar();
        };

        var captura = new ToolStripMenuItem("Copiar captura") { ToolTipText = "Foto de la pantalla del celular al portapapeles (y a Imágenes\\Espejo Celular)." };
        captura.Click += async (_, _) => await Capturar(false);
        var whatsapp = new ToolStripMenuItem("Enviar captura por WhatsApp") { ToolTipText = "Copia la captura y abre WhatsApp: elige el chat y pega con Ctrl+V." };
        whatsapp.Click += async (_, _) => await Capturar(true);
        var grabar = new ToolStripMenuItem("Grabar") { ToolTipText = "Graba la pantalla del celular con sonido, en Videos\\Espejo Celular." };
        grabar.Click += async (_, _) =>
        {
            try { if (grabadora.Activa) await DetenerGrabacion(); else await IniciarGrabacion(); }
            catch (Exception ex) { Registro("Error con la grabación: " + ex.Message); }
        };

        // Opciones: mismas casillas de la ventana (cambiar una cambia la otra).
        var opciones = new ToolStripMenuItem("Opciones");
        ToolStripMenuItem Opcion(CheckBox c, string tip)
        {
            var m = new ToolStripMenuItem(c.Text) { CheckOnClick = true, ToolTipText = tip };
            m.Click += (_, _) => c.Checked = m.Checked;
            m.Tag = c;
            opciones.DropDownItems.Add(m);
            return m;
        }
        Opcion(chkVisible, "La ventana del celular queda siempre encima de las otras.");
        Opcion(chkApagar, "La pantalla del celular se apaga; lo sigues viendo y usando en el PC.");
        Opcion(chkDespierto, "Mientras está el espejo abierto, el celular no se bloquea solo.");
        Opcion(chkAudio, "El sonido del celular sale por los parlantes del PC.");
        Opcion(chkGrabar, "Al abrir el espejo empieza a grabar solo.");
        Opcion(chkAuto, "Al abrir la app se conecta sola.");
        Opcion(chkFijo, "Puerto fijo 5555 para encontrarlo aunque cambie de IP.");
        Opcion(chkInicio, "Al prender el PC la app se abre sola y queda junto al reloj.");
        opciones.DropDownItems.Add(new ToolStripSeparator());
        var calidad = new ToolStripMenuItem("Calidad");
        for (int i = 0; i < cmbCalidad.Items.Count; i++)
        {
            int n = i;
            var m = new ToolStripMenuItem(cmbCalidad.Items[i]!.ToString());
            m.Click += (_, _) => cmbCalidad.SelectedIndex = n;
            calidad.DropDownItems.Add(m);
        }
        opciones.DropDownItems.Add(calidad);
        opciones.DropDown.ShowItemToolTips = true;

        var vincular = new ToolStripMenuItem("Vincular celular nuevo…");
        vincular.Click += async (_, _) => { MostrarVentana(); if (btnVincular.Enabled) await Vincular(); };

        var web = new ToolStripMenuItem("ojodigital.cl");
        web.Click += (_, _) => AbrirWeb(SitioWeb);
        var gh = new ToolStripMenuItem("Código y descargas en GitHub");
        gh.Click += (_, _) => AbrirWeb(Repositorio);

        var salir = new ToolStripMenuItem("Salir (cerrar del todo)") { ToolTipText = "Cierra el espejo, detiene la grabación y cierra la app." };
        salir.Click += (_, _) => Salir();

        menu.Items.AddRange(new ToolStripItem[]
        {
            abrir, new ToolStripSeparator(),
            conectar, captura, whatsapp, grabar, new ToolStripSeparator(),
            opciones, vincular, new ToolStripSeparator(),
            web, gh, new ToolStripSeparator(),
            new ToolStripMenuItem("v" + Version) { Enabled = false },
            salir,
        });
        menu.ShowItemToolTips = true;

        // Antes de mostrar el menú, reflejar el estado actual.
        menu.Opening += (_, _) =>
        {
            bool hayEspejo = scrcpy != null;
            conectar.Text = hayEspejo ? "Cerrar espejo" : "Conectar y mostrar";
            conectar.Enabled = hayEspejo || btnConectar.Enabled;
            captura.Enabled = whatsapp.Enabled = grabar.Enabled = barra != null;
            grabar.Text = grabadora.Activa ? "Detener grabación" : "Grabar";
            foreach (ToolStripItem it in opciones.DropDownItems)
                if (it is ToolStripMenuItem m && m.Tag is CheckBox c) m.Checked = c.Checked;
            for (int i = 0; i < calidad.DropDownItems.Count; i++)
                ((ToolStripMenuItem)calidad.DropDownItems[i]).Checked = i == cmbCalidad.SelectedIndex;
        };

        bandeja = new NotifyIcon
        {
            Icon = Icon ?? SystemIcons.Application,
            Text = "Espejo Celular",
            ContextMenuStrip = menu,
            Visible = true,
        };
        bandeja.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) MostrarVentana(); };
        _ = PedirIconoVisible();
        bandeja.BalloonTipClicked += (_, _) => MostrarVentana();
    }
}
