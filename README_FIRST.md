# Empieza aquí

1. Extrae el ZIP en una carpeta temporal y copia sus archivos a la raíz del repositorio existente. Si ya existe `.vscode/mcp.json` o `.github/skills/windows-legacy-discovery/SKILL.md`, combina los contenidos manualmente y no sobrescribas sin revisar.
2. En PowerShell, desde la raíz: `Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass` y después `./install.ps1`.
3. Confirma que la aplicación antigua esté abierta en el mismo escritorio de Windows.
4. En VS Code, ejecuta `MCP: List Servers` y revisa el servidor `windows-ui-inspector`.
5. En Copilot Agent, usa el prompt incluido en `README.md`; empieza solo con `list_windows`.

No te conectes todavía a SQL Server ni permitas herramientas de interacción. El primer éxito esperado es que Copilot devuelva un listado de ventanas con títulos y handles, y luego el árbol UIA de la ventana que confirmes.
