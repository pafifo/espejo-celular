namespace EspejoCelular;

/// Atajos de teclado y mouse para manejar el celular desde el PC, y qué hacer si algo falla.
/// (Las teclas son las de scrcpy; «Alt» es la tecla Alt de la izquierda.)
public class AyudaForm : Form
{
    static readonly Color Texto = Color.FromArgb(29, 35, 43);
    static readonly Color Oscuro = Color.FromArgb(0, 119, 168);
    static readonly Color Gris = Color.FromArgb(90, 100, 112);

    public AyudaForm(Icon? icono)
    {
        Text = "Espejo Celular · Atajos y ayuda";
        Icon = icono;
        Font = new Font("Segoe UI", 10.5f);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(760, 720);
        MinimumSize = new Size(520, 400);
        BackColor = Color.White;

        var caja = new RichTextBox
        {
            Location = new Point(16, 12), Size = new Size(728, 648), ReadOnly = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            BorderStyle = BorderStyle.None, BackColor = Color.White, ForeColor = Texto, Font = Font,
            ScrollBars = RichTextBoxScrollBars.Vertical, DetectUrls = false, TabStop = false,
        };
        caja.SelectionTabs = new[] { 250 };
        Controls.Add(caja);

        var negrita = new Font("Segoe UI Semibold", 10.5f);
        var titulo = new Font("Segoe UI Semibold", 12.5f);

        void Titulo(string t)
        {
            if (caja.TextLength > 0) caja.AppendText("\n");
            caja.SelectionFont = titulo; caja.SelectionColor = Oscuro;
            caja.AppendText(t + "\n");
        }
        void Fila(string tecla, string que)
        {
            caja.SelectionFont = negrita; caja.SelectionColor = Texto;
            caja.AppendText(tecla + "\t");
            caja.SelectionFont = Font; caja.SelectionColor = Texto;
            caja.AppendText(que + "\n");
        }
        void Nota(string t)
        {
            caja.SelectionFont = Font; caja.SelectionColor = Gris;
            caja.AppendText(t + "\n");
        }

        Titulo("Con el mouse, en la ventana del celular");
        Fila("Clic", "Tocar");
        Fila("Clic y arrastrar", "Deslizar");
        Fila("Rueda del mouse", "Subir y bajar");
        Fila("Clic derecho", "Atrás (y prende la pantalla si está apagada)");
        Fila("Clic en la rueda", "Inicio");
        Fila("Ctrl + clic y arrastrar", "Pellizcar para hacer zoom");
        Fila("Doble clic en el borde negro", "Ajustar la ventana al celular");
        Nota("Puedes escribir con el teclado del PC en cualquier campo del celular.");

        Titulo("Teclas (Alt es la tecla Alt de la izquierda)");
        Fila("Alt + H", "Inicio");
        Fila("Alt + B  o  Alt + Retroceso", "Atrás");
        Fila("Alt + S", "Apps recientes");
        Fila("Alt + N", "Bajar las notificaciones (dos veces: ajustes rápidos)");
        Fila("Alt + Mayús + N", "Subir las notificaciones");
        Fila("Alt + ↑  /  Alt + ↓", "Subir o bajar el volumen");
        Fila("Alt + P", "Botón de encendido (bloquear)");
        Fila("Alt + O", "Apagar la pantalla del celular; lo sigues viendo aquí");
        Fila("Alt + Mayús + O", "Volver a prender la pantalla del celular");
        Fila("Alt + R", "Girar la pantalla del celular");
        Fila("Alt + ←  /  Alt + →", "Girar solo la ventana del PC");
        Fila("Alt + F", "Pantalla completa (otra vez para salir)");
        Fila("Alt + G", "Tamaño real, píxel por píxel");
        Fila("Alt + W", "Ajustar la ventana y quitar los bordes negros");

        Titulo("Copiar y pegar entre el celular y el PC");
        Fila("Ctrl + C  o  Alt + C", "Copia en el celular; queda también en el PC");
        Fila("Ctrl + V  o  Alt + V", "Pega en el celular lo que copiaste en el PC");
        Fila("Alt + Mayús + V", "Escribe el texto letra por letra, para apps que no dejan pegar");
        Nota("Lo que copias en el celular como siempre (mantener apretado › Copiar) también\nllega al PC. Se comparte solo texto: para imágenes usa «Copiar captura» o\n«Mandar archivo».");

        Titulo("Mandar archivos al celular");
        Nota("Usa el botón «Mandar archivo» de la barra, o arrastra archivos desde el PC y\nsuéltalos sobre la ventana del celular o sobre la barra.");
        Fila("Fotos, PDF, música…", "Quedan en la carpeta Descargas del celular");
        Fila("Archivos .apk", "Se instalan como app");

        Titulo("La barra junto al celular");
        Fila("Copiar captura", "Foto de la pantalla al portapapeles (Imágenes\\Espejo Celular)");
        Fila("Grabar", "Video con sonido; se detiene con el mismo botón (Videos\\Espejo Celular)");
        Fila("WhatsApp", "Copia la captura y abre WhatsApp: elige el chat y pega con Ctrl+V");
        Fila("Mandar archivo", "Del PC a Descargas del celular");

        Titulo("Junto al reloj");
        Nota("La X de la ventana principal solo la esconde: la app queda como ícono junto al\nreloj (o en la flechita ^). Clic en el ícono para abrirla; clic derecho para ver\ntodas las opciones o «Salir» para cerrarla del todo.");

        Titulo("Si algo falla");
        Fila("No encuentra el celular", "Si lo reiniciaste, vuelve a activar «Depuración inalámbrica»");
        Fila("Se ve pero no responde", "Xiaomi/Redmi/POCO: activa «Depuración USB (ajustes de seguridad)»");
        Fila("No pega el texto", "Prueba con Alt + Mayús + V");
        Fila("No graba", "Algunos celulares no permiten grabar y mostrar a la vez; baja la calidad");
        Nota("El registro, abajo en la ventana principal, muestra qué está pasando.");

        caja.SelectionStart = 0;
        caja.ScrollToCaret();

        var ok = new Button
        {
            Text = "Cerrar", Size = new Size(120, 36), Location = new Point(624, 672),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right, DialogResult = DialogResult.OK,
        };
        ok.Click += (_, _) => Close();
        Controls.Add(ok);
        AcceptButton = CancelButton = ok;
    }
}
