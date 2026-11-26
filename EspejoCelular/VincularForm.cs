using QRCoder;

namespace EspejoCelular;

/// Vincula un celular nuevo por wifi: escaneando un QR o con el código de 6 dígitos.
public class VincularForm : Form
{
    readonly Adb adb;
    readonly CancellationTokenSource cts = new();
    readonly string nombreQr = "espejo-" + Aleatorio(6);
    readonly string claveQr = Aleatorio(10);
    readonly Label estado;
    readonly TextBox txtCodigo, txtDireccion;
    readonly Button btnCodigo;
    bool trabajando;

    /// Dirección ip:puerto con la que quedó conectado y nombre mDNS del celular.
    public string? Serial { get; private set; }
    public string? NombreMdns { get; private set; }

    public VincularForm(Adb adb, Icon? icono)
    {
        this.adb = adb;
        Text = "Vincular celular por wifi";
        Icon = icono;
        Font = new Font("Segoe UI", 10f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.Manual;
        Ventanas.CentrarSobreDueno(this);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(560, 640);
        BackColor = Color.White;

        var negrita = new Font("Segoe UI Semibold", 10.5f);
        Controls.Add(new Label
        {
            Text = "Paso 1 · En el celular (mismo wifi que este PC)", Font = negrita,
            Location = new Point(20, 14), AutoSize = true,
        });
        Controls.Add(new Label
        {
            Text = "Abre Opciones de desarrollador › «Depuración inalámbrica», actívala y entra\n" +
                   "a ella tocando su nombre.  (Xiaomi: Ajustes › Ajustes adicionales › Opciones\n" +
                   "de desarrollador)",
            Location = new Point(20, 38), Size = new Size(520, 62),
        });
        Controls.Add(new Label
        {
            Text = "Paso 2 · Elige UNA de estas dos formas", Font = negrita,
            Location = new Point(20, 106), AutoSize = true,
        });

        var pestanas = new TabControl { Location = new Point(20, 132), Size = new Size(520, 400) };

        // --- Pestaña QR ---
        var tabQr = new TabPage("Con código QR (recomendado)") { BackColor = Color.White };
        tabQr.Controls.Add(new Label
        {
            Text = "En el celular toca «Vincular dispositivo con código QR» y apunta la\n" +
                   "cámara a este código. No tienes que escribir nada aquí.",
            Location = new Point(12, 10), Size = new Size(490, 46),
        });
        tabQr.Controls.Add(new PictureBox
        {
            Location = new Point(125, 62), Size = new Size(260, 260),
            SizeMode = PictureBoxSizeMode.Zoom, Image = CrearQr($"WIFI:T:ADB;S:{nombreQr};P:{claveQr};;"),
        });
        tabQr.Controls.Add(new Label
        {
            Text = "Cuando lo escanee, se conecta solo.",
            Location = new Point(12, 330), Size = new Size(490, 24), ForeColor = Color.DimGray,
        });
        pestanas.TabPages.Add(tabQr);

        // --- Pestaña código ---
        var tabCodigo = new TabPage("Con código de 6 números") { BackColor = Color.White };
        tabCodigo.Controls.Add(new Label
        {
            Text = "1. En el celular toca «Vincular dispositivo con código de vinculación».\n" +
                   "    Aparecen un código de 6 números y una dirección IP con puerto.\n" +
                   "2. Deja esa ventana abierta en el celular.\n" +
                   "3. Escribe aquí el código de 6 números y pulsa «Vincular».",
            Location = new Point(12, 10), Size = new Size(490, 90),
        });
        tabCodigo.Controls.Add(new Label { Text = "Código de 6 números", Location = new Point(12, 112), AutoSize = true });
        txtCodigo = new TextBox
        {
            Location = new Point(12, 136), Width = 200, MaxLength = 6,
            Font = new Font("Segoe UI", 16f), TextAlign = HorizontalAlignment.Center,
        };
        tabCodigo.Controls.Add(txtCodigo);
        btnCodigo = new Button { Text = "Vincular", Location = new Point(230, 136), Size = new Size(150, 40) };
        btnCodigo.Click += async (_, _) => await VincularConCodigo();
        tabCodigo.Controls.Add(btnCodigo);

        tabCodigo.Controls.Add(new Label
        {
            Text = "Solo si después de unos segundos dice que no encuentra el celular,\n" +
                   "copia también la «Dirección IP y puerto» que muestra esa ventana:",
            Location = new Point(12, 200), Size = new Size(490, 46), ForeColor = Color.DimGray,
        });
        txtDireccion = new TextBox { Location = new Point(12, 250), Width = 240, PlaceholderText = "ej. 192.168.1.50:37099" };
        tabCodigo.Controls.Add(txtDireccion);
        pestanas.TabPages.Add(tabCodigo);

        pestanas.SelectedIndexChanged += (_, _) =>
        {
            if (trabajando) return;
            Estado(pestanas.SelectedTab == tabQr ? "Esperando que escanees el código QR…" : "Escribe el código de 6 números que muestra el celular.");
            if (pestanas.SelectedTab == tabCodigo) txtCodigo.Focus();
        };
        Controls.Add(pestanas);
        AcceptButton = btnCodigo;

        estado = new Label
        {
            Text = "Esperando que escanees el código QR…",
            Location = new Point(20, 540), Size = new Size(520, 48), ForeColor = Color.DimGray,
        };
        Controls.Add(estado);

        var cerrar = new Button { Text = "Cerrar", Location = new Point(440, 594), Size = new Size(100, 32), DialogResult = DialogResult.Cancel };
        Controls.Add(cerrar);
        CancelButton = cerrar;

        Shown += async (_, _) => await EsperarQr();
        FormClosed += (_, _) => cts.Cancel();
    }

    static string Aleatorio(int n)
    {
        const string letras = "abcdefghjkmnpqrstuvwxyz23456789";
        return new string(Enumerable.Range(0, n)
            .Select(_ => letras[System.Security.Cryptography.RandomNumberGenerator.GetInt32(letras.Length)]).ToArray());
    }

    static Image CrearQr(string texto)
    {
        using var gen = new QRCodeGenerator();
        using var datos = gen.CreateQrCode(texto, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(datos).GetGraphic(10);
        return Image.FromStream(new MemoryStream(png));
    }

    void Estado(string texto, Color? color = null)
    {
        if (IsDisposed) return;
        estado.Text = texto;
        estado.ForeColor = color ?? Color.DimGray;
    }

    /// Espera a que el celular publique el servicio de emparejamiento con el nombre del QR.
    async Task EsperarQr()
    {
        while (!cts.IsCancellationRequested)
        {
            if (!trabajando)
            {
                var servicios = await adb.ServiciosMdns();
                var s = servicios.FirstOrDefault(x => x.Tipo.StartsWith("_adb-tls-pairing") && x.Nombre == nombreQr);
                if (s != null && !trabajando)
                {
                    trabajando = true;
                    Estado("Celular encontrado, vinculando…");
                    if (await adb.Emparejar(s.Direccion, claveQr)) { await Terminar(s.Ip); return; }
                    Estado("No se pudo vincular. Vuelve a abrir el QR en el celular.", Color.Firebrick);
                    trabajando = false;
                }
            }
            try { await Task.Delay(1200, cts.Token); } catch { return; }
        }
    }

    async Task VincularConCodigo()
    {
        var codigo = txtCodigo.Text.Trim();
        if (codigo.Length != 6 || !codigo.All(char.IsDigit))
        {
            Estado("El código tiene 6 dígitos.", Color.Firebrick);
            return;
        }
        if (trabajando) return;
        trabajando = true;
        btnCodigo.Enabled = false;
        try
        {
            var direccion = txtDireccion.Text.Trim();
            if (direccion == "")
            {
                Estado("Buscando el celular en la red…");
                for (int i = 0; i < 8 && direccion == "" && !cts.IsCancellationRequested; i++)
                {
                    var s = (await adb.ServiciosMdns())
                        .FirstOrDefault(x => x.Tipo.StartsWith("_adb-tls-pairing") && x.Nombre != nombreQr);
                    if (s != null) direccion = s.Direccion;
                    else await Task.Delay(1000);
                }
                if (direccion == "")
                {
                    Estado("No lo encontré. Escribe la IP y el puerto que muestra la ventana del código.", Color.Firebrick);
                    return;
                }
            }
            Estado("Vinculando con " + direccion + "…");
            if (await adb.Emparejar(direccion, codigo))
            {
                var ip = direccion.Contains(':') ? direccion[..direccion.LastIndexOf(':')] : direccion;
                await Terminar(ip);
            }
            else Estado("No se pudo vincular. Revisa el código (cambia cada vez que abres la ventana).", Color.Firebrick);
        }
        finally
        {
            trabajando = false;
            if (!IsDisposed) btnCodigo.Enabled = true;
        }
    }

    /// Tras vincular, busca el puerto de conexión (distinto al de vinculación) y conecta.
    async Task Terminar(string ip)
    {
        Estado("Vinculado. Conectando…", Color.SeaGreen);
        for (int i = 0; i < 15 && !cts.IsCancellationRequested; i++)
        {
            // adb a veces se conecta solo al vincular.
            var dev = (await adb.Dispositivos()).FirstOrDefault(d => d.Listo && d.Serial.StartsWith(ip + ":"));
            var servicio = (await adb.ServiciosMdns())
                .FirstOrDefault(x => x.Tipo.StartsWith("_adb-tls-connect") && x.Ip == ip);
            if (dev == null && servicio != null && await adb.Conectar(servicio.Direccion))
                dev = new Dispositivo(servicio.Direccion, "device", "");
            if (dev != null)
            {
                Serial = dev.Serial;
                NombreMdns = servicio?.Nombre;
                DialogResult = DialogResult.OK;
                Close();
                return;
            }
            await Task.Delay(1000);
        }
        Estado("Quedó vinculado, pero no pude conectar. Cierra esta ventana y pulsa «Conectar».", Color.DarkOrange);
    }
}
