using System.Text.Json;

namespace EspejoCelular;

/// Preferencias y celular recordado. Se guardan en %APPDATA%\EspejoCelular\config.json
public class Config
{
    public string? ScrcpyPath { get; set; }

    // Celular recordado
    public string? Modelo { get; set; }
    public string? NombreMdns { get; set; }   // ej. adb-XXXXXXXX-AbCdEf
    public string? UltimaDireccion { get; set; } // ej. 192.168.1.50:41235

    // Opciones de la ventana
    public bool SiempreVisible { get; set; } = true;
    public bool ApagarPantalla { get; set; } = false;
    public bool MantenerDespierto { get; set; } = true;
    public bool Audio { get; set; } = true;
    public bool ConectarAlAbrir { get; set; } = false;
    public bool Grabar { get; set; } = false;
    public int Calidad { get; set; } = 1; // 0 alta, 1 media, 2 ahorro

    static string Carpeta => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EspejoCelular");
    static string Archivo => Path.Combine(Carpeta, "config.json");

    public static Config Cargar()
    {
        try
        {
            if (File.Exists(Archivo))
                return JsonSerializer.Deserialize<Config>(File.ReadAllText(Archivo)) ?? new Config();
        }
        catch { }
        return new Config();
    }

    public void Guardar()
    {
        try
        {
            Directory.CreateDirectory(Carpeta);
            File.WriteAllText(Archivo, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
