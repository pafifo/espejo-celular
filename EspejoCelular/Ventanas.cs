namespace EspejoCelular;

static class Ventanas
{
    /// Centra un diálogo sobre su dueño y lo mantiene dentro de la pantalla de ese dueño
    /// (FormStartPosition.CenterParent falla con varios monitores de distinta escala).
    public static void CentrarSobreDueno(Form f)
    {
        f.Load += (_, _) =>
        {
            var dueno = f.Owner;
            if (dueno == null) return;
            var area = Screen.FromControl(dueno).WorkingArea;
            int x = dueno.Left + (dueno.Width - f.Width) / 2;
            int y = dueno.Top + (dueno.Height - f.Height) / 2;
            x = Math.Clamp(x, area.Left, Math.Max(area.Left, area.Right - f.Width));
            y = Math.Clamp(y, area.Top, Math.Max(area.Top, area.Bottom - f.Height));
            f.StartPosition = FormStartPosition.Manual;
            f.Location = new Point(x, y);
        };
    }
}
