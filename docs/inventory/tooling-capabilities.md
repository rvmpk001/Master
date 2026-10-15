# Herramientas de inspección disponibles

## Comprobación inicial

- Fecha: 2026-10-14.
- Sistema: Windows.
- Alcance: detección local de herramientas e inspección de la ventana objetivo,
  confirmada por el usuario para esta migración visual.

## Disponibles

| Capacidad | Estado observado | Límite actual |
|---|---|---|
| Enumerar procesos y títulos de ventana | `Get-Process` disponible en PowerShell | No enumera controles ni garantiza que una ventana sea la aplicación correcta. |
| Inspección y guardado UI Automation MCP | `windows-ui-inspector` disponible; inspección y árbol guardado para handle 525482/PID 8416 | El árbol entrega 18 nodos, pero no informa patrones UIA admitidos. |
| Consulta de patrones UI Automation | `UIAutomationClient` y `UIAutomationTypes` cargables; `GetSupportedPatterns()` consultado sin ejecutar patrones | La vista .NET recorrió 16 nodos; no expuso `TitleBar` ni el botón `Cerrar` que sí aparecen en el árbol MCP. |
| Inspección interactiva de UI Automation | `Inspect.exe` localizado en el Windows SDK | No está en `PATH`; no se ha probado para esta ventana. |
| Navegador integrado | Disponible en la sesión | Sirve para páginas web, no para controlar ventanas Win32 externas. |

## No encontrados en la comprobación

- `Get-UIAWindow` o módulo PowerShell de UI Automation.
- `WinAppDriver.exe`.
- `AccessibilityInsights.exe`.
- `UIAVerify.exe`.
- `Spyxx.exe`.
- `uiautomatorviewer.bat`.

No se comprobó la instalación de OCR ni otros paquetes Python; su
disponibilidad se considera **desconocida**, no ausente. No hay una consulta de
patrones UIA expuesta por las herramientas MCP disponibles; para esta ventana
fue posible consultar esa capacidad localmente con la API .NET, sin invocar
patrones ni leer valores de controles.

## Cobertura necesaria para una inspección automatizada

1. Identificar de forma inequívoca la ventana y su proceso.
2. Consultar el árbol UI Automation: AutomationId, nombre accesible, tipo,
   estado habilitado/solo lectura y rectángulo cuando estén expuestos. Leer
   valores requiere una autorización y un alcance separados.
3. Capturar una imagen de referencia y relacionarla con la pantalla y los
   hallazgos.
4. Determinar qué patrones UI Automation ofrece cada control (por ejemplo,
   Invoke, Toggle, Value, Selection o Grid) mediante `GetSupportedPatterns()`;
   esta llamada de consulta no ejecuta los patrones.
5. Automatizar acciones solo con autorización y comprobación de entorno,
   precondiciones y posibles efectos. No usar coordenadas como sustituto de
   identificar el control.
6. Registrar errores de automatización y recurrir a inspección manual si la
   aplicación no expone controles accesibles.

## Próxima comprobación segura

Si se necesita caracterizar los patrones de `TitleBar`/`Cerrar` u otros nodos
ausentes de la vista .NET, probar `Inspect.exe` en la misma ventana. No ejecutar
acciones de control.
