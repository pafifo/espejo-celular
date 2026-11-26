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
        ClientSize = new Size(560, 600);
        BackColor = Color.White;

        var pasos = new Label
        {
            Text = "En el celular (conectado al mismo wifi que este PC):\n" +
                   "1. Ajustes › Opciones de desarrollador › Depuración inalámbrica: activar.\n" +
                   "2. Toca «Vincular dispositivo con código QR» y escanea este código.",
            Location = new Point(20, 16), Size = new Size(520, 70),
        };
        Controls.Add(pasos);

        var qr = new PictureBox
        {
            Location = new Point(150, 90), Size = new Size(260, 260),
            SizeMode = PictureBoxSizeMode.Zoom, Image = CrearQr($"WIFI:T:ADB;S:{nombreQr};P:{claveQr};;"),
        };
        Controls.Add(qr);

        var sep = new Label
        {
            Text = "¿Sin cámara? Toca «Vincular dispositivo con código de sincronización»\n" +
                   "y escribe aquí el código de 6 dígitos (la dirección es opcional):",
            Location = new Point(20, 364), Size = new Size(520, 46),
        };
        Controls.Add(sep);

        Controls.Add(new Label { Text = "Código", Location = new Point(20, 420), AutoSize = true });
        txtCodigo = new TextBox { Location = new Point(20, 444), Width = 120, MaxLength = 6 };
        Controls.Add(txtCodigo);

        Controls.Add(new Label { Text = "IP y puerto (opcional)", Location = new Point(155, 420), AutoSize = true });
        txtDireccion = new TextBox { Location = new Point(155, 444), Width = 220, PlaceholderText = "192.168.1.50:37099" };
        Controls.Add(txtDireccion);

        btnCodigo = new Button { Text = "Vincular", Location = new Point(390, 442), Size = new Size(150, 32) };
        btnCodigo.Click += async (_, _) => await VincularConCodigo();
        Controls.Add(btnCodigo);
        AcceptButton = btnCodigo;

        estado = new Label
        {
            Text = "Esperando que escanees el código…",
            Location = new Point(20, 495), Size = new Size(520, 50), ForeColor = Color.DimGray,
        };
        Controls.Add(estado);

        var cerrar = new Button { Text = "Cerrar", Location = new Point(440, 552), Size = new Size(100, 32), DialogResult = DialogResult.Cancel };
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
