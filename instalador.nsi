; Instalador de Espejo Celular (incluye scrcpy). Se compila con construir.ps1
Unicode true
!include "MUI2.nsh"

!define APP "Espejo Celular"
!ifndef VERSION
  !define VERSION "1.0.0"
!endif
!define UNINST "Software\Microsoft\Windows\CurrentVersion\Uninstall\EspejoCelular"

Name "${APP}"
OutFile "distribucion\EspejoCelular-${VERSION}-instalador.exe"
InstallDir "$PROGRAMFILES64\${APP}"
InstallDirRegKey HKLM "${UNINST}" "InstallLocation"
RequestExecutionLevel admin
SetCompressor /SOLID lzma
BrandingText "${APP} ${VERSION}"

VIProductVersion "${VERSION}.0"
VIAddVersionKey "ProductName" "${APP}"
VIAddVersionKey "FileDescription" "Instalador de ${APP}"
VIAddVersionKey "FileVersion" "${VERSION}"
VIAddVersionKey "CompanyName" "ODML"
VIAddVersionKey "LegalCopyright" "ODML. Incluye scrcpy (Apache 2.0, Genymobile)"

!define MUI_ICON "EspejoCelular\icono.ico"
!define MUI_UNICON "EspejoCelular\icono.ico"
!define MUI_WELCOMEPAGE_TEXT "Este asistente instalará ${APP} ${VERSION}.$\r$\n$\r$\nVerás tu celular Android en la pantalla del PC y lo manejarás con el mouse y el teclado, conectado por tu propio wifi, sin internet ni cuentas.$\r$\n$\r$\nIncluye scrcpy, programa libre de Genymobile."
!define MUI_FINISHPAGE_RUN "$INSTDIR\EspejoCelular.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Abrir ${APP}"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "Spanish"

Section "Instalar"
  ; Cerrar procesos de una versión anterior
  nsExec::Exec 'taskkill /F /IM EspejoCelular.exe'
  nsExec::Exec '"$INSTDIR\scrcpy\adb.exe" kill-server'

  SetOutPath "$INSTDIR"
  File "dist\EspejoCelular.exe"
  File "LEEME.txt"
  SetOutPath "$INSTDIR\scrcpy"
  File /r "dist\scrcpy\*.*"

  ; Firewall: permitir que adb reciba los anuncios mDNS del celular (UDP 5353) para encontrarlo en la red
  nsExec::Exec 'netsh advfirewall firewall delete rule name="Espejo Celular (buscar celular)"'
  nsExec::Exec 'netsh advfirewall firewall add rule name="Espejo Celular (buscar celular)" dir=in action=allow program="$INSTDIR\scrcpy\adb.exe" protocol=UDP localport=5353 profile=any'

  ; Accesos directos para todos los usuarios
  SetShellVarContext all
  CreateShortcut "$SMPROGRAMS\${APP}.lnk" "$INSTDIR\EspejoCelular.exe"
  CreateShortcut "$DESKTOP\${APP}.lnk" "$INSTDIR\EspejoCelular.exe"

  WriteUninstaller "$INSTDIR\Desinstalar.exe"
  WriteRegStr HKLM "${UNINST}" "DisplayName" "${APP}"
  WriteRegStr HKLM "${UNINST}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKLM "${UNINST}" "Publisher" "ODML"
  WriteRegStr HKLM "${UNINST}" "DisplayIcon" "$INSTDIR\EspejoCelular.exe"
  WriteRegStr HKLM "${UNINST}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKLM "${UNINST}" "UninstallString" '"$INSTDIR\Desinstalar.exe"'
  WriteRegStr HKLM "${UNINST}" "QuietUninstallString" '"$INSTDIR\Desinstalar.exe" /S'
  WriteRegDWORD HKLM "${UNINST}" "NoModify" 1
  WriteRegDWORD HKLM "${UNINST}" "NoRepair" 1
SectionEnd

Section "Uninstall"
  nsExec::Exec 'taskkill /F /IM EspejoCelular.exe'
  nsExec::Exec 'taskkill /F /IM scrcpy.exe'
  nsExec::Exec '"$INSTDIR\scrcpy\adb.exe" kill-server'
  nsExec::Exec 'netsh advfirewall firewall delete rule name="Espejo Celular (buscar celular)"'
  SetShellVarContext all
  Delete "$SMPROGRAMS\${APP}.lnk"
  Delete "$DESKTOP\${APP}.lnk"
  RMDir /r "$INSTDIR\scrcpy"
  Delete "$INSTDIR\EspejoCelular.exe"
  Delete "$INSTDIR\LEEME.txt"
  Delete "$INSTDIR\Desinstalar.exe"
  RMDir "$INSTDIR"
  DeleteRegKey HKLM "${UNINST}"
SectionEnd
