# SCR-002 — Dashboard

## Origen y destino

- Origen: `WINFORMS CONTROL SHOWCASE - Enterprise Suite`, handle 329790,
  PID 9596. Árbol UIA de profundidad 8: 81 nodos, sin truncamiento.
- Evidencia del origen: `../../evidence/ui-inspection/window-329790-20261014-130659.json`.
- Inspección de esta ejecución: ventana WinForms `WINFORMS CONTROL SHOWCASE -
  Enterprise Suite`, handle 67218, PID 11068; profundidad 4, 46 nodos, sin
  truncamiento. Evidencia de solo lectura:
  `../../evidence/ui-inspection/window-67218-20261016-105121.json`.
- Destino: `src/Master.WinUI/DashboardWindow.xaml` y
  `src/Master.WinUI/DashboardWindow.xaml.cs`.
- Acceso: desde SCR-001 después de validar el usuario y la contraseña contra
  SQL Server. No existe una ruta de vista previa sin autenticación.
- La ventana inicia maximizada.

## Contenido observado y reproducido

- Navegación lateral para Dashboard, Clientes, Productos, Ventas, Reportes,
  Configuración, Utilidades, Gestión de Archivos y Controles Avanzados; incluye
  «Volver al Login».
- Barra superior con «Actualizar» y accesos a Clientes, Productos, Ventas y
  Reportes.
- La ventana original mostró 20 clientes, 51 productos, 40 ventas e ingresos
  de `$70,634.27`; son valores de la evidencia heredada, no valores codificados
  en la réplica.
- La réplica consulta los KPI en SQL Server. La consulta de solo lectura en
  `DemoShowcaseDB` devolvió 20 clientes, 51 productos, 40 ventas y `71,200.81`
  como suma de `Ventas.Total`.
- La meta mensual de `$60,000 USD` observada en el origen no existe en el
  esquema y no se simula.
- La gráfica consulta `Ventas.FechaVenta` y `Ventas.Total` agrupados por mes
  durante los últimos 12 meses. La base no define moneda.

## Comportamiento y límites

- «Actualizar» vuelve a consultar SQL Server y refresca KPI y gráfica. Si falla
  la conexión, el estado diferencia datos previos de un error inicial.
- SCR-003 Clientes, SCR-004 Productos y SCR-005 Ventas están alojadas en el
  área de contenido del Dashboard. Los accesos del menú lateral y de la barra
  superior cambian la vista dentro de la misma ventana; «Volver al Dashboard»
  restaura el panel. Los módulos restantes aún no migrados muestran un aviso
  en lugar de simular operaciones.
- «Volver al Login» cierra la ventana del Dashboard y abre una nueva ventana de
  inicio de sesión; tras autenticar correctamente, el Login anterior se cierra
  al abrirse el Dashboard.
- La ventana requiere un `AuthenticatedUser`; sus consultas de Dashboard y
  Clientes y Productos usan la conexión SQL configurada.

## Verificación

- Captura de la aplicación WinUI 3 ejecutada, en ancho amplio:
  `../../evidence/ui/SCR-002-dashboard-winui-responsive.png`.
- Inspección de la pantalla WinUI 3: 61 nodos, sin truncamiento:
  `../../evidence/ui-inspection/window-2295156-20261014-132224.json`.
- La inspección actual del origen confirma la navegación lateral, las acciones
  de Dashboard y módulos, los indicadores de estado y la barra de menús; no se
  ejecutaron acciones en la aplicación original.
- Reflujo estrecho a 540×720: los cuatro encabezados KPI, la meta y la gráfica
  se encontraron al recorrer el `ScrollViewer` (posiciones de desplazamiento
  observadas: 0%, 48.7%, 97.4% y 100%).
- Reflujo ancho: las etiquetas de Clientes/Productos quedaron en la misma fila y
  Ventas/Ingresos en la siguiente; la captura muestra las cuatro tarjetas.
- Navegación de regreso: correcta; tras seleccionar «Volver al Login» se observó
  la ventana `WinForms Control Showcase - Login` visible. Evidencia:
  `../../evidence/ui-inspection/window-2164260-20261014-132257.json`.
- Compilación actual: correcta con Visual Studio Community MSBuild 18, en
  `Debug|x64`; se generó `src/Master.WinUI/bin/x64/Debug/net8.0-windows10.0.19041.0/Master.WinUI.dll`.
  La primera tentativa con `dotnet build` no pudo cargar la tarea PRI desde el
  SDK; se repitió con el MSBuild de Visual Studio que sí contiene las tareas
  WinUI necesarias.
- Las cinco pruebas funcionales previas del Login y las pruebas previas del
  aviso de actualización y de módulos no migrados pasaron en la ejecución
  anterior. En esta continuación se comprobó además que el Login siguiera
  visible después de volver desde el Dashboard.
- En esta ejecución, UI Automation activó Clientes primero desde la barra
  superior y después desde el menú lateral; en ambos casos encontró el título
  de Clientes dentro del mismo HWND del Dashboard. «Volver al Dashboard»
  restauró su contenido en ese HWND. La enumeración final mostró únicamente
  las ventanas existentes Login y Dashboard para el proceso de la aplicación.
- Evidencia UIA actualizada de la vista Clientes integrada (32 nodos, sin
  truncamiento): `../../evidence/ui-inspection/window-1771208-20261015-161903.json`.
- Evidencia UIA del Dashboard después de volver (18 nodos, sin truncamiento):
  `../../evidence/ui-inspection/window-1771208-20261015-161919.json`.
- La compilación de esta corrección: correcta con Visual Studio Community
  MSBuild 18, `Debug|x64`, 0 advertencias y 0 errores.
- En esta ejecución, Visual Studio Community MSBuild 18 compiló
  `Master.WinUI.csproj` correctamente en `Debug|x64`. `dotnet build` no pudo
  cargar `ExpandPriContent` porque falta `Microsoft.Build.Packaging.Pri.Tasks`
  en el SDK local; se usó el MSBuild de Visual Studio, que sí dispone de la
  tarea WinUI requerida.
- Se inició el ejecutable generado; la validación de Login vacío mostró
  «Datos requeridos» sin llamar al servicio. Después de que el usuario
  proporcionó datos de prueba, el proceso abrió el Dashboard y la vista
  Productos leyó 51 filas desde SQL Server.
- El arranque maximizado de ambas ventanas y la ausencia del acceso de vista
  previa se verifican en la ejecución actual.
- Consulta real de solo lectura: correcta; devolvió 12 filas mensuales, los
  KPI indicados arriba y el catálogo de 51 productos. El flujo Login →
  Dashboard quedó verificado en la ejecución actual.

## Pendiente

- Migrar las pantallas y comportamientos de los módulos restantes.
- La meta mensual requiere una fuente y una regla de negocio aprobadas.
- No se verificó visualmente la gráfica con una sesión autenticada.
