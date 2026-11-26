# Espejo Celular

App de Windows para ver un celular Android en pantalla y manejarlo con el mouse y el teclado del PC, por el wifi de la casa (sin internet, sin cuentas). Usa [scrcpy](https://github.com/Genymobile/scrcpy) (Apache 2.0) como motor y le pone encima una interfaz propia en español:

- Vinculación por QR o código de 6 dígitos (depuración inalámbrica de Android 11+).
- Recuerda el celular y lo encuentra solo en la red (mDNS), aunque cambie el puerto.
- Opciones: siempre visible, apagar pantalla del celular, no bloquear, audio, grabar, calidad.
- Guía paso a paso para activar el modo desarrollador (incluye Xiaomi, Samsung y Motorola).

## Compilar

Requiere .NET 8 SDK, NSIS y scrcpy instalado con `winget install Genymobile.scrcpy`.

```powershell
.\construir.ps1
```

Deja `dist\` (app autónoma + scrcpy) y `distribucion\EspejoCelular-<versión>-instalador.exe`.

El instalador pide administrador para instalar en Archivos de programa y abrir en el firewall el puerto UDP 5353 de adb (para encontrar el celular por mDNS).

## Estructura

- `EspejoCelular\` código C# WinForms (`MainForm`, `VincularForm`, `GuiaForm`, `Adb`, `Config`).
- `instalador.nsi` script NSIS.
- `crear_icono.ps1` genera `icono.ico`.
- `terceros\LICENCIA-scrcpy.txt` licencia de scrcpy que se copia en la instalación.
