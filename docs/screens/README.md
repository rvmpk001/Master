# Inventario de pantallas

- [SCR-001 — Inicio de sesión](./SCR-001-login.md): formulario WinForms observado; Login conectado a autenticación SQL, sin prueba con credenciales.
- [SCR-002 — Dashboard](./SCR-002-dashboard.md): panel WinForms observado; KPI y serie mensual desde SQL tras autenticación.
- [SCR-003 — Administración de Clientes](./SCR-003-clientes.md): vista integrada en la ventana Dashboard, con consulta, CRUD y CSV conectados al esquema SQL; no se probaron escrituras reales.
- [SCR-004 — Catálogo de Productos](./SCR-004-productos.md): vista integrada en el Dashboard; lectura, filtros, borrador, validación y navegación verificados; escrituras pendientes de prueba.

Evidencia:

- Origen WinForms: [`../../evidence/ui-inspection/window-525482-20261014-094639.json`](../../evidence/ui-inspection/window-525482-20261014-094639.json)
- Réplica WinUI 3: [`../../evidence/ui-inspection/window-2950106-20261014-125904.json`](../../evidence/ui-inspection/window-2950106-20261014-125904.json)
- Captura WinUI 3: [`../../evidence/ui/SCR-001-login-winui-final-build.png`](../../evidence/ui/SCR-001-login-winui-final-build.png)
- Dashboard WinForms: [`../../evidence/ui-inspection/window-329790-20261014-130659.json`](../../evidence/ui-inspection/window-329790-20261014-130659.json)
- Dashboard WinForms, inspección de solo lectura del 2026-10-16 (46 nodos):
  [`../../evidence/ui-inspection/window-67218-20261016-105121.json`](../../evidence/ui-inspection/window-67218-20261016-105121.json)
- Productos WinForms (`FrmProductos`), inspección de solo lectura del
  2026-10-16 (88 nodos):
  [`../../evidence/ui-inspection/window-198348-20261016-110215.json`](../../evidence/ui-inspection/window-198348-20261016-110215.json)
- Acciones CRUD de Productos (5 nodos):
  [`../../evidence/ui-inspection/window-198454-20261016-110351.json`](../../evidence/ui-inspection/window-198454-20261016-110351.json)
- Dashboard WinUI 3: [`../../evidence/ui-inspection/window-2295156-20261014-132224.json`](../../evidence/ui-inspection/window-2295156-20261014-132224.json)
- Clientes en el árbol Enterprise Suite: [`../../evidence/ui-inspection/window-67400-20261015-152511.json`](../../evidence/ui-inspection/window-67400-20261015-152511.json)
- Formulario Detalles del Cliente: [`../../evidence/ui-inspection/window-198538-20261015-152833.json`](../../evidence/ui-inspection/window-198538-20261015-152833.json)
- WinUI 3 Clientes generada: [`../../evidence/ui-inspection/window-853678-20261015-154759.json`](../../evidence/ui-inspection/window-853678-20261015-154759.json)
- Clientes integrados en Dashboard: [`../../evidence/ui-inspection/window-1771208-20261015-161903.json`](../../evidence/ui-inspection/window-1771208-20261015-161903.json)
- Dashboard tras volver desde Clientes: [`../../evidence/ui-inspection/window-1771208-20261015-161919.json`](../../evidence/ui-inspection/window-1771208-20261015-161919.json)
- Login visible tras volver desde el Dashboard: [`../../evidence/ui-inspection/window-2164260-20261014-132257.json`](../../evidence/ui-inspection/window-2164260-20261014-132257.json)
- Captura real del Dashboard WinUI 3 en ejecución: [`../../evidence/ui/SCR-002-dashboard-winui-responsive.png`](../../evidence/ui/SCR-002-dashboard-winui-responsive.png)
