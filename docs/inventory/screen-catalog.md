# Catálogo de pantallas

## Estado

- Estado: La ventana y su uso como objetivo de esta migración fueron
  confirmados por el usuario; inspección UIA guardada.
- Fecha: 2026-10-14.

| ID | Nombre visible | Cómo se accede | Evidencia | Estado |
|---|---|---|---|---|
| SCR-001 | WinForms Control Showcase - Login | Ventana WinForms visible; handle 525482; PID 8416 | Origen: [`evidence/ui-inspection/window-525482-20261014-094639.json`](../../evidence/ui-inspection/window-525482-20261014-094639.json), 18 nodos. WinUI visible tras volver del Dashboard: [`evidence/ui-inspection/window-2164260-20261014-132257.json`](../../evidence/ui-inspection/window-2164260-20261014-132257.json), 32 nodos. | Implementado con autenticación SQL; credenciales no probadas; usuario opcional recordado localmente, sin guardar contraseña |
| SCR-002 | WINFORMS CONTROL SHOWCASE - Enterprise Suite | Login SQL exitoso; no hay acceso sin autenticación | Origen: [`evidence/ui-inspection/window-329790-20261014-130659.json`](../../evidence/ui-inspection/window-329790-20261014-130659.json), 81 nodos. WinUI: [`evidence/ui-inspection/window-2295156-20261014-132224.json`](../../evidence/ui-inspection/window-2295156-20261014-132224.json), 61 nodos. Captura anterior: [`evidence/ui/SCR-002-dashboard-winui-responsive.png`](../../evidence/ui/SCR-002-dashboard-winui-responsive.png) | KPI, gráfica y Productos integrados; autenticación y lectura de 51 productos verificadas; escrituras pendientes; otros módulos no migrados |
| SCR-003 | Clientes (`FrmClientes`), formulario secundario de Enterprise Suite | Clientes en el menú lateral o en la barra superior del Dashboard WinUI 3; se muestra en la misma ventana | Origen: [`evidence/ui-inspection/window-67400-20261015-152511.json`](../../evidence/ui-inspection/window-67400-20261015-152511.json), 92 nodos. Detalles: [`evidence/ui-inspection/window-198538-20261015-152833.json`](../../evidence/ui-inspection/window-198538-20261015-152833.json), 46 nodos. WinUI integrado: [`evidence/ui-inspection/window-1771208-20261015-161903.json`](../../evidence/ui-inspection/window-1771208-20261015-161903.json), 32 nodos. | Persistencia y CSV conectados; navegación integrada verificada; no se ejecutaron escrituras de negocio |
| SCR-004 | Productos (`FrmProductos`), formulario secundario de Enterprise Suite | «Inventario / Productos» en el menú lateral o «Productos» en la barra del Dashboard WinUI 3; misma ventana | Origen: [`evidence/ui-inspection/window-198348-20261016-110215.json`](../../evidence/ui-inspection/window-198348-20261016-110215.json), 88 nodos. Botones CRUD: [`evidence/ui-inspection/window-198454-20261016-110351.json`](../../evidence/ui-inspection/window-198454-20261016-110351.json), 5 nodos. | Integrado con `dbo.Productos`; compilado; lectura, filtros, borrador, validación, navegación y reflujo verificados; escrituras pendientes |
| SCR-005 | Ventas (`FrmVentas`), formulario secundario de Enterprise Suite | «Facturación / Ventas» en el menú lateral o «Ventas» en la barra del Dashboard WinUI 3; misma ventana | Origen: [`evidence/ui-inspection/window-263878-20261017-084431.json`](../../evidence/ui-inspection/window-263878-20261017-084431.json), 62 nodos, sin truncamiento. | Integrado con `dbo.Ventas` y `dbo.DetalleVenta`; compilado; escritura transaccional implementada, sin escritura real de prueba; no modifica `dbo.Productos.Stock` |

## Convenciones

- Asignar `SCR-001`, `SCR-002`, etc. en el orden de observación.
- Crear una ficha `docs/screens/SCR-XXX-nombre.md` por pantalla.
- Registrar por separado controles (`SCR-XXX-CTL-XXX`) y acciones (`ACT-XXX`).
- No incluir pantallas inferidas como observadas; marcar claramente hipótesis y
  rutas pendientes.
- Las interacciones de SCR-005 no se ejecutaron en la aplicación heredada;
  la adaptación y sus limitaciones se detallan en
  [`../screens/SCR-005-ventas.md`](../screens/SCR-005-ventas.md).
