using System.Diagnostics;
using System.Text;

namespace EspejoCelular;

public record ServicioMdns(string Nombre, string Tipo, string Direccion)
{
    public string Ip => Direccion.Contains(':') ? Direccion[..Direccion.LastIndexOf(':')] : Direccion;
}

public record Dispositivo(string Serial, string Estado, string Modelo)
{
    public bool Listo => Estado == "device";
}

/// Envoltorio de adb.exe (el que viene con scrcpy). Todo corre oculto, sin consola.
public class Adb
{
    public string ScrcpyExe { get; }
    public string AdbExe { get; }

    public Adb(string scrcpyExe)
    {
        ScrcpyExe = scrcpyExe;
        AdbExe = Path.Combine(Path.GetDirectoryName(scrcpyExe)!, "adb.exe");
    }

    /// Busca scrcpy.exe: ruta guardada, junto a la app, PATH y carpeta de winget.
    public static string? BuscarScrcpy(string? guardada)
    {
        bool Valido(string? p) => !string.IsNullOrEmpty(p) && File.Exists(p)
            && File.Exists(Path.Combine(Path.GetDirectoryName(p)!, "adb.exe"));

        // Primero el scrcpy que viene con el instalador.
        var local = Path.Combine(AppContext.BaseDirectory, "scrcpy", "scrcpy.exe");
        if (Valido(local)) return local;

        if (Valido(guardada)) return guardada;

        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
        {
            try
            {
                var p = Path.Combine(dir.Trim(), "scrcpy.exe");
                if (Valido(p)) return p;
            }
            catch { }
        }

        var winget = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "WinGet", "Packages");
        if (Directory.Exists(winget))
        {
            var hallado = Directory.GetDirectories(winget, "Genymobile.scrcpy*")
                .SelectMany(d => Directory.GetFiles(d, "scrcpy.exe", SearchOption.AllDirectories))
                .OrderByDescending(f => f)
                .FirstOrDefault(Valido);
            if (hallado != null) return hallado;
        }
        return null;
    }

    /// Inicia el servidor de adb sin capturar salida (si se captura, el servidor
    /// hereda la tubería y las llamadas se cuelgan).
    public async Task IniciarServidor()
    {
        var psi = new ProcessStartInfo(AdbExe, "start-server")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        using var p = Process.Start(psi)!;
        await Task.Run(() => p.WaitForExit(15000));
    }

    public async Task<(int Codigo, string Salida)> Ejecutar(string args, int timeoutMs = 15000)
    {
        var psi = new ProcessStartInfo(AdbExe, args)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        var sb = new StringBuilder();
        using var p = new Process { StartInfo = psi };
        p.OutputDataReceived += (_, e) => { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); };
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();

        using var cts = new CancellationTokenSource(timeoutMs);
        try { await p.WaitForExitAsync(cts.Token); }
        catch (OperationCanceledException)
        {
            try { p.Kill(true); } catch { }
            return (-1, "Tiempo de espera agotado: adb " + args);
        }
        lock (sb) return (p.ExitCode, sb.ToString().Trim());
    }

    public async Task<List<ServicioMdns>> ServiciosMdns()
    {
        var (_, salida) = await Ejecutar("mdns services", 8000);
        var lista = new List<ServicioMdns>();
        foreach (var linea in salida.Split('\n'))
        {
            var partes = linea.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (partes.Length >= 3 && partes[1].StartsWith("_adb"))
            {
                var tipo = partes[1].TrimEnd('.');
                lista.Add(new ServicioMdns(partes[0], tipo, partes[2]));
            }
        }
        return lista;
    }

    public async Task<List<Dispositivo>> Dispositivos()
    {
        var (_, salida) = await Ejecutar("devices -l", 8000);
        var lista = new List<Dispositivo>();
        foreach (var linea in salida.Split('\n').Skip(1))
        {
            var partes = linea.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (partes.Length < 2) continue;
            var modelo = partes.FirstOrDefault(x => x.StartsWith("model:"))?[6..].Replace('_', ' ') ?? "";
            lista.Add(new Dispositivo(partes[0], partes[1], modelo));
        }
        return lista;
    }

    public async Task<bool> Emparejar(string direccion, string codigo)
    {
        var (_, salida) = await Ejecutar($"pair {direccion} {codigo}", 20000);
        return salida.Contains("Successfully paired", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<bool> Conectar(string direccion)
    {
        var (_, salida) = await Ejecutar($"connect {direccion}", 12000);
        return salida.Contains("connected to", StringComparison.OrdinalIgnoreCase)
            && !salida.Contains("cannot", StringComparison.OrdinalIgnoreCase)
            && !salida.Contains("failed", StringComparison.OrdinalIgnoreCase);
    }

    public Task Desconectar(string serial) => Ejecutar($"disconnect {serial}", 8000);

    /// Deja al celular escuchando también en el puerto fijo 5555 (dura hasta que se reinicia).
    public Task PuertoFijo(string serial) => Ejecutar($"-s {serial} tcpip 5555", 10000);

    public static async Task<bool> PuertoAbierto(string ip, int puerto, int ms = 2000)
    {
        using var c = new System.Net.Sockets.TcpClient();
        try { return await c.ConnectAsync(ip, puerto).WaitAsync(TimeSpan.FromMilliseconds(ms)).ContinueWith(t => !t.IsFaulted && c.Connected); }
        catch { return false; }
    }

    /// IPs de la red local (cada /24 donde está el PC) que tienen abierto el puerto pedido.
    public static async Task<List<string>> BuscarPuertoEnRed(int puerto)
    {
        var redes = new HashSet<string>();
        foreach (var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType is not (System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211
                or System.Net.NetworkInformation.NetworkInterfaceType.Ethernet)) continue;
            var props = nic.GetIPProperties();
            if (props.GatewayAddresses.Count == 0) continue; // solo redes reales (no VPN ni Hyper-V)
            foreach (var u in props.UnicastAddresses)
                if (u.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && u.PrefixLength >= 16)
                {
                    var b = u.Address.GetAddressBytes();
                    redes.Add($"{b[0]}.{b[1]}.{b[2]}.");
                }
        }
        var abiertas = new System.Collections.Concurrent.ConcurrentBag<string>();
        var tareas = redes.SelectMany(r => Enumerable.Range(1, 254).Select(async i =>
        {
            var ip = r + i;
            if (await PuertoAbierto(ip, puerto, 1500)) abiertas.Add(ip);
        }));
        await Task.WhenAll(tareas);
        return abiertas.ToList();
    }

    /// Captura de pantalla del celular en resolución completa (PNG), sin pasar por la ventana.
    public async Task<byte[]?> Captura(string serial)
    {
        var psi = new ProcessStartInfo(AdbExe, $"-s {serial} exec-out screencap -p")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var p = Process.Start(psi)!;
        using var ms = new MemoryStream();
        using var cts = new CancellationTokenSource(15000);
        try
        {
            var err = p.StandardError.ReadToEndAsync();
            await p.StandardOutput.BaseStream.CopyToAsync(ms, cts.Token);
            await p.WaitForExitAsync(cts.Token);
        }
        catch { try { p.Kill(true); } catch { } return null; }
        var b = ms.ToArray();
        // Un PNG válido empieza con 89 50 4E 47
        return b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 ? b : null;
    }

    public async Task<string> Propiedad(string serial, string prop)
    {
        var (_, salida) = await Ejecutar($"-s {serial} shell getprop {prop}", 8000);
        return salida.Trim();
    }
}
