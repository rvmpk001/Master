# Aplicación heredada — ficha inicial

- Estado: Aplicación objetivo confirmada por el usuario para la migración visual
- Nombre visible de la ventana objetivo: WinForms Control Showcase - Login
- Versión:
- Entorno autorizado: Desarrollo / Pruebas / Producción / Desconocido
- Ejecutable (si se conoce, sin credenciales ni parámetros secretos):
- Usuario/rol de prueba (sin contraseñas):
- Fecha de primera observación: 2026-10-14
- Evidencias: [`evidence/ui-inspection/window-525482-20261014-094639.json`](../../evidence/ui-inspection/window-525482-20261014-094639.json)
- Preguntas pendientes: Versión, entorno autorizado, propósito funcional y comportamiento de controles.

## Notas
El usuario confirmó explícitamente que esta ventana de demostración es el
objetivo de esta migración. La inspección UIA del handle 525482 (PID 8416)
devolvió 18 nodos sin truncamiento. No se confirmó ninguna regla funcional: la
presencia de un control solo demuestra que fue expuesto por la interfaz de
accesibilidad. Entorno, versión, propósito, reglas, comportamiento y
persistencia siguen pendientes de verificar. No se leyeron valores explícitos
de campos ni se ejecutaron acciones.
