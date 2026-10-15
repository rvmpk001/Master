# Windows UI Inspector MCP (solo lectura)

Servidor MCP local para GitHub Copilot en VS Code. Usa UI Automation a través de `pywinauto` para enumerar ventanas visibles e inspeccionar controles accesibles.

## Alcance de esta primera versión

Herramientas disponibles:

- `list_windows`: lista ventanas de nivel superior con título, handle, PID y metadatos básicos.
- `get_active_window`: identifica la ventana activa.
- `inspect_window`: devuelve el árbol UI Automation de una ventana por su handle.
- `save_window_inspection`: guarda un JSON nuevo dentro de `evidence/ui-inspection/`.

Esta versión no tiene herramientas para hacer clic, escribir, enviar formularios, cerrar ventanas, leer explícitamente el valor de campos de entrada ni acceder a SQL Server.

## Requisitos

- Windows 10/11, en el mismo equipo donde está abierta la aplicación heredada.
- Python 3.11 o posterior.
- VS Code con GitHub Copilot Chat y compatibilidad MCP.
- Acceso autorizado a la aplicación. Empieza preferiblemente en un entorno de pruebas.

## Instalación

1. Extrae primero el ZIP en una carpeta temporal. Después copia los archivos de dentro de `windows-ui-inspector-mcp/` a la raíz de tu repositorio existente. Si ya existen `.vscode/mcp.json` o `.github/skills/windows-legacy-discovery/SKILL.md`, combina sus contenidos manualmente; no los sobrescribas sin revisar.
2. Abre la carpeta raíz en VS Code.
3. Abre una terminal PowerShell en la raíz del proyecto.
4. Ejecuta:

   ```powershell
   Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
   .\install.ps1
   ```

   El cambio de política aplica solo a esa sesión de PowerShell.
5. Revisa `.vscode/mcp.json` y el código del servidor antes de confiar en él. Los servidores MCP locales ejecutan código con los permisos del usuario.
6. En VS Code, abre la paleta de comandos (`Ctrl+Shift+P`) y ejecuta `MCP: List Servers`. Inicia `windows-ui-inspector` si no se ha iniciado automáticamente y revisa su salida si aparece un error.
7. Abre GitHub Copilot Chat en modo agente. Pídele primero que ejecute `list_windows` y que no use ninguna herramienta distinta de lectura.

## Primer prompt de comprobación

> Usa las herramientas MCP de `windows-ui-inspector`. Ejecuta `list_windows` y dime qué ventanas visibles hay. No interactúes con ellas. Después identifica la ventana de la aplicación heredada usando su título/handle; no elijas una ventana por suposición. Cuando te confirme cuál es, ejecuta `inspect_window` con `max_depth=4` y `max_nodes=250`. No leas valores de campos, no hagas clic y no conectes a SQL Server.

## Cómo seleccionar la ventana

Cada ventana tiene un `handle` y un `process_id`. El usuario debe confirmar el título de la aplicación objetivo antes de inspeccionarla. Si hay más de una ventana parecida, no adivines: pregunta cuál es la correcta.

## Evidencias

`save_window_inspection` crea un JSON nuevo en `evidence/ui-inspection/`. Ese archivo puede contener nombres accesibles expuestos por la aplicación. Revísalo para evitar compartir nombres o datos sensibles. No almacenes credenciales, cadenas de conexión ni datos personales en Git.

## Solución de problemas

- **No inicia el servidor:** revisa `MCP: List Servers` → salida/logs; confirma que `install.ps1` terminó correctamente y que existe `tools/windows-ui-inspector/.venv/Scripts/python.exe`.
- **No aparecen ventanas:** confirma que la aplicación está abierta en el mismo escritorio/sesión de Windows, no dentro de una sesión remota distinta.
- **Acceso denegado o árbol incompleto:** comprueba que VS Code y la aplicación se ejecutan con niveles de permisos compatibles. No ejecutes VS Code como administrador salvo que tu política de TI lo autorice.
- **No coincide con Inspect.exe:** comprueba que Inspect.exe y `pywinauto` estén mirando la misma ventana y el modo UI Automation; algunos proveedores de accesibilidad exponen diferentes vistas.
- **Mucha salida:** reduce `max_depth` o `max_nodes`.

## Arquitectura

`server.py` usa MCP por stdio. La configuración de VS Code está en `.vscode/mcp.json`. La inspección utiliza `pywinauto` con el backend `uia`; documentación: <https://pywinauto.readthedocs.io/en/latest/getting_started.html>. Referencia oficial de configuración MCP de VS Code: <https://code.visualstudio.com/docs/agents/reference/mcp-configuration>.
