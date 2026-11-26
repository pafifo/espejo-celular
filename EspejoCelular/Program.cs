namespace EspejoCelular;

static class Program
{
    [STAThread]
    static void Main()
    {
        // Una sola instancia abierta a la vez.
        using var mutex = new Mutex(true, "ODML.EspejoCelular", out bool primera);
        if (!primera) return;

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
