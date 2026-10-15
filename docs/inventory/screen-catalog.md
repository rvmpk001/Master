# Catálogo de pantallas

## Estado

- Estado: La ventana y su uso como objetivo de esta migración fueron
  confirmados por el usuario; inspección UIA guardada.
- Fecha: 2026-10-14.

| ID | Nombre visible | Cómo se accede | Evidencia | Estado |
|---|---|---|---|---|
| SCR-001 | WinForms Control Showcase - Login | Ventana WinForms visible; handle 525482; PID 8416 | Origen: [`evidence/ui-inspection/window-525482-20261014-094639.json`](../../evidence/ui-inspection/window-525482-20261014-094639.json), 18 nodos. WinUI visible tras volver del Dashboard: [`evidence/ui-inspection/window-2164260-20261014-132257.json`](../../evidence/ui-inspection/window-2164260-20261014-132257.json), 32 nodos. | Implementado; cinco pruebas funcionales del Login pasaron en la ejecución anterior; ventana visible verificada tras volver del Dashboard |
| SCR-002 | WINFORMS CONTROL SHOWCASE - Enterprise Suite | Botón de vista previa en SCR-001; la navegación no implica autenticación | Origen: [`evidence/ui-inspection/window-329790-20261014-130659.json`](../../evidence/ui-inspection/window-329790-20261014-130659.json), 81 nodos. WinUI: [`evidence/ui-inspection/window-2295156-20261014-132224.json`](../../evidence/ui-inspection/window-2295156-20261014-132224.json), 61 nodos. Captura: [`evidence/ui/SCR-002-dashboard-winui-responsive.png`](../../evidence/ui/SCR-002-dashboard-winui-responsive.png) | Implementado como vista previa; layout ancho/estrecho y retorno al Login verificados; servicios y módulos pendientes |

## Convenciones

- Asignar `SCR-001`, `SCR-002`, etc. en el orden de observación.
- Crear una ficha `docs/screens/SCR-XXX-nombre.md` por pantalla.
- Registrar por separado controles (`SCR-XXX-CTL-XXX`) y acciones (`ACT-XXX`).
- No incluir pantallas inferidas como observadas; marcar claramente hipótesis y
  rutas pendientes.
