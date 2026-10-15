$ErrorActionPreference = 'Stop'
$ToolDir = Join-Path $PSScriptRoot 'tools\windows-ui-inspector'
$VenvDir = Join-Path $ToolDir '.venv'

Write-Host 'Windows UI Inspector MCP - instalación' -ForegroundColor Cyan
Write-Host 'Este script instala dependencias locales; no abre ni modifica la aplicación objetivo.'

$PyLauncher = Get-Command py -ErrorAction SilentlyContinue
$PythonCmd = Get-Command python -ErrorAction SilentlyContinue
if ($PyLauncher) {
    & py -3 -m venv $VenvDir
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo crear el entorno virtual. Instala Python 3.11 o superior y vuelve a ejecutar.' }
} elseif ($PythonCmd) {
    & python -m venv $VenvDir
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo crear el entorno virtual. Instala Python 3.11 o superior y vuelve a ejecutar.' }
} else {
    throw 'No se encontró Python. Instala Python 3.11 o superior desde https://www.python.org/downloads/windows/ y vuelve a ejecutar.'
}

$VenvPython = Join-Path $VenvDir 'Scripts\python.exe'
& $VenvPython -c "import sys; assert sys.version_info >= (3, 11), 'Se requiere Python 3.11 o superior'; print(sys.version)"
if ($LASTEXITCODE -ne 0) { throw 'La versión de Python del entorno virtual no cumple el requisito 3.11+.' }
& $VenvPython -m pip install --upgrade pip
if ($LASTEXITCODE -ne 0) { throw 'Falló la actualización de pip.' }
& $VenvPython -m pip install -r (Join-Path $ToolDir 'requirements.txt')
if ($LASTEXITCODE -ne 0) { throw 'Falló la instalación de dependencias.' }

Write-Host ''
Write-Host 'Instalación terminada.' -ForegroundColor Green
Write-Host 'Siguiente: abre la carpeta raíz en VS Code y ejecuta MCP: List Servers para iniciar windows-ui-inspector.'
Write-Host 'Mantén la aplicación objetivo abierta en una pantalla reconocible.'
