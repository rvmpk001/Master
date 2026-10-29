# Driver Unitronics

`Unitronics.ComDriver.dll` (versión x86 1.0.0.90) y
`Unitronics.ComDriver.Security.dll` (versión 1.0.2.0) se copiaron de la
aplicación BLAPACI original instalada, con autorización del usuario.

Estos ensamblados se usan desde `Master.PlcProbe`, un proceso auxiliar x86 para
.NET Framework 4.8. El proceso conserva el modo XML que utiliza la aplicación
original, hace únicamente la lectura MI configurada y cierra el canal después
de cada comprobación. La aplicación WinUI inicia este auxiliar y presenta el
resultado; no carga directamente el driver en .NET moderno.

Mantén ambos archivos juntos y compila toda la solución para x86. No sustituyas
el driver ni cambies el modo XML sin validar la comunicación serial real. El
equipo donde se ejecute la aplicación debe tener disponible .NET Framework 4.8.
