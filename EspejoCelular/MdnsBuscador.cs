using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace EspejoCelular;

/// Búsqueda mDNS propia (independiente de la de adb): pregunta en la red quién
/// ofrece «_adb-tls-connect._tcp.local» y devuelve nombre, IP actual y puerto.
/// Así el celular se encuentra por su nombre aunque el router le haya dado otra IP.
public static class MdnsBuscador
{
    const string Servicio = "_adb-tls-connect._tcp.local";
    static readonly IPEndPoint Grupo = new(IPAddress.Parse("224.0.0.251"), 5353);

    public static async Task<List<ServicioMdns>> Buscar(int milisegundos = 2500)
    {
        var hallados = new Dictionary<string, ServicioMdns>();
        var tareas = new List<Task>();
        foreach (var ip in IpsLocales())
            tareas.Add(Task.Run(() => BuscarDesde(ip, milisegundos, hallados)));
        await Task.WhenAll(tareas);
        lock (hallados) return hallados.Values.ToList();
    }

    static IEnumerable<IPAddress> IpsLocales()
    {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
            if (!nic.SupportsMulticast) continue;
            foreach (var u in nic.GetIPProperties().UnicastAddresses)
                if (u.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(u.Address)
                    && !u.Address.ToString().StartsWith("169.254."))
                    yield return u.Address;
        }
    }

    static void BuscarDesde(IPAddress local, int ms, Dictionary<string, ServicioMdns> hallados)
    {
        try
        {
            using var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            s.Bind(new IPEndPoint(local, 0));
            s.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, local.GetAddressBytes());
            s.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 255);
            var consulta = Consulta();
            var fin = DateTime.UtcNow.AddMilliseconds(ms);
            var proximoEnvio = DateTime.UtcNow;
            var buf = new byte[9000];
            while (DateTime.UtcNow < fin)
            {
                if (DateTime.UtcNow >= proximoEnvio)
                {
                    s.SendTo(consulta, Grupo);
                    proximoEnvio = DateTime.UtcNow.AddMilliseconds(800);
                }
                if (!s.Poll(200_000, SelectMode.SelectRead)) continue;
                EndPoint remoto = new IPEndPoint(IPAddress.Any, 0);
                int n = s.ReceiveFrom(buf, ref remoto);
                foreach (var r in Leer(buf, n, ((IPEndPoint)remoto).Address))
                    lock (hallados) hallados[r.Nombre] = r;
            }
        }
        catch { /* interfaz sin multicast o bloqueada: se ignora */ }
    }

    /// Pregunta PTR por el servicio, pidiendo respuesta directa (bit QU).
    static byte[] Consulta()
    {
        var m = new List<byte> { 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0 };
        foreach (var parte in Servicio.Split('.'))
        {
            var b = Encoding.ASCII.GetBytes(parte);
            m.Add((byte)b.Length); m.AddRange(b);
        }
        m.AddRange(new byte[] { 0, 0, 12, 0x80, 1 });
        return m.ToArray();
    }

    static IEnumerable<ServicioMdns> Leer(byte[] d, int n, IPAddress origen)
    {
        var res = new List<ServicioMdns>();
        try
        {
            if (n < 12) return res;
            int qd = d[4] << 8 | d[5], total = (d[6] << 8 | d[7]) + (d[8] << 8 | d[9]) + (d[10] << 8 | d[11]);
            int p = 12;
            for (int i = 0; i < qd; i++) { Nombre(d, ref p); p += 4; }
            var srv = new Dictionary<string, (int Puerto, string Destino)>();
            var aes = new Dictionary<string, IPAddress>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < total && p < n; i++)
            {
                var nombre = Nombre(d, ref p);
                int tipo = d[p] << 8 | d[p + 1];
                int largo = d[p + 8] << 8 | d[p + 9];
                int datos = p + 10;
                if (tipo == 33 && nombre.EndsWith(Servicio, StringComparison.OrdinalIgnoreCase)) // SRV
                {
                    int puerto = d[datos + 4] << 8 | d[datos + 5];
                    int q = datos + 6;
                    srv[nombre] = (puerto, Nombre(d, ref q));
                }
                else if (tipo == 1 && largo == 4) // A
                    aes[nombre] = new IPAddress(d.AsSpan(datos, 4));
                p = datos + largo;
            }
            foreach (var (nombre, (puerto, destino)) in srv)
            {
                var ip = aes.TryGetValue(destino, out var a) ? a : origen;
                var instancia = nombre[..(nombre.Length - Servicio.Length)].TrimEnd('.');
                res.Add(new ServicioMdns(instancia, "_adb-tls-connect._tcp", $"{ip}:{puerto}"));
            }
        }
        catch { }
        return res;
    }

    static string Nombre(byte[] d, ref int p)
    {
        var partes = new List<string>();
        int pos = p, saltos = 0;
        bool salto = false;
        while (true)
        {
            int len = d[pos];
            if (len == 0) { pos++; break; }
            if ((len & 0xC0) == 0xC0)
            {
                int destino = (len & 0x3F) << 8 | d[pos + 1];
                if (!salto) p = pos + 2;
                salto = true;
                pos = destino;
                if (++saltos > 20) break;
                continue;
            }
            partes.Add(Encoding.UTF8.GetString(d, pos + 1, len));
            pos += len + 1;
        }
        if (!salto) p = pos;
        return string.Join(".", partes);
    }
}
