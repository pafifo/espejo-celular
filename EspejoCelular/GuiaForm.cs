namespace EspejoCelular;

/// Explica cómo preparar el celular la primera vez (una sola vez por celular).
public class GuiaForm : Form
{
    public GuiaForm(Icon? icono)
    {
        Text = "Cómo preparar el celular (una sola vez)";
        Icon = icono;
        Font = new Font("Segoe UI", 10f);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.Manual;
        Ventanas.CentrarSobreDueno(this);
        ClientSize = new Size(600, 600);
        BackColor = Color.White;

        var texto = new RichTextBox
        {
            Location = new Point(16, 16), Size = new Size(568, 520), ReadOnly = true,
            BorderStyle = BorderStyle.None, BackColor = Color.White, Font = Font,
            Text =
"Necesitas Android 11 o más nuevo, y el celular y el PC en el mismo wifi.\n" +
"Esto se hace una sola vez por celular y toma un par de minutos.\n\n" +
"1. ACTIVAR EL MODO DESARROLLADOR\n" +
"   Ajustes › Acerca del teléfono › toca 7 veces «Número de compilación».\n" +
"   • Xiaomi / Redmi / POCO: toca 7 veces «Versión de HyperOS» (o «Versión de MIUI»).\n" +
"   • Samsung: Acerca del teléfono › Información de software › «Número de compilación».\n" +
"   • Motorola: Acerca del teléfono › «Número de compilación».\n" +
"   Te pedirá el PIN y dirá «Ahora eres desarrollador».\n\n" +
"2. ACTIVAR LA DEPURACIÓN INALÁMBRICA\n" +
"   Entra a Opciones de desarrollador:\n" +
"   • La mayoría: Ajustes › Sistema › Opciones de desarrollador.\n" +
"   • Xiaomi: Ajustes › Ajustes adicionales › Opciones de desarrollador.\n" +
"   • Samsung: al final de Ajustes.\n" +
"   Activa «Depuración inalámbrica» y acepta «Permitir siempre en esta red».\n\n" +
"3. SOLO XIAOMI / REDMI / POCO\n" +
"   En Opciones de desarrollador activa también «Depuración USB (ajustes de seguridad)».\n" +
"   Sin esto la pantalla se ve, pero no responde al mouse.\n\n" +
"4. VINCULAR CON ESTE PC\n" +
"   En el PC pulsa «Vincular celular nuevo…». En el celular, dentro de Depuración\n" +
"   inalámbrica, toca «Vincular dispositivo con código QR» y escanea el código.\n\n" +
"DESPUÉS, CADA VEZ\n" +
"   Solo pulsa «Conectar y mostrar». Si reiniciaste el celular, vuelve a activar\n" +
"   «Depuración inalámbrica» (truco: en Opciones de desarrollador › «Mosaicos de\n" +
"   configuración rápida» agrega el botón a la cortina de notificaciones).\n\n" +
"SEGURIDAD\n" +
"   Todo viaja solo por tu wifi, sin internet ni cuentas. Solo los PC que vinculaste\n" +
"   pueden conectarse. Si quieres, apaga la Depuración inalámbrica cuando no la uses.",
        };
        Controls.Add(texto);

        var ok = new Button { Text = "Entendido", Location = new Point(464, 552), Size = new Size(120, 34), DialogResult = DialogResult.OK };
        Controls.Add(ok);
        AcceptButton = CancelButton = ok;
    }
}
