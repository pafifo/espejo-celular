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
    public string? Serie { get; set; }        // ro.serialno: identifica al celular aunque cambie IP, MAC o nombre
    public bool PuertoFijo { get; set; } = true; // respaldo por el puerto 5555 (sobrevive a cambios de IP)

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

    /// true solo cuando el usuario pulsó «Olvidar celular».
    [System.Text.Json.Serialization.JsonIgnore]
    public bool Olvidado { get; set; }

    public void Guardar()
    {
        try
        {
            Directory.CreateDirectory(Carpeta);
            // Nunca borrar el celular guardado por accidente (otra ventana, lectura fallida…):
            // si aquí no hay celular y no se pidió olvidarlo, se conserva el que está en disco.
            if (!Olvidado && Modelo == null && NombreMdns == null && UltimaDireccion == null && Serie == null && File.Exists(Archivo))
            {
                var enDisco = JsonSerializer.Deserialize<Config>(File.ReadAllText(Archivo));
                if (enDisco != null)
                {
                    Modelo = enDisco.Modelo;
                    NombreMdns = enDisco.NombreMdns;
                    UltimaDireccion = enDisco.UltimaDireccion;
                    Serie = enDisco.Serie;
                }
            }
            File.WriteAllText(Archivo, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
