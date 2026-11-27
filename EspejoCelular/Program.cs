namespace EspejoCelular;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // Una sola instancia: si ya está abierta (por ejemplo junto al reloj), se le pide que se muestre.
        using var mutex = new Mutex(true, "ODML.EspejoCelular", out bool primera);
        using var mostrar = new EventWaitHandle(false, EventResetMode.AutoReset, "ODML.EspejoCelular.Mostrar");
        if (!primera) { mostrar.Set(); return; }

        ApplicationConfiguration.Initialize();
        MainForm.ArranqueOculto = args.Any(a => a.Equals("--inicio", StringComparison.OrdinalIgnoreCase));
        var form = new MainForm();
        var hilo = new Thread(() =>
        {
            while (true)
            {
                mostrar.WaitOne();
                try { form.BeginInvoke(form.MostrarVentana); } catch { return; }
            }
        }) { IsBackground = true };
        hilo.Start();
        Application.Run(form);
    }
}
