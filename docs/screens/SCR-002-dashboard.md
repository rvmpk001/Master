# SCR-002 — Dashboard

## Origen y destino

- Origen: `WINFORMS CONTROL SHOWCASE - Enterprise Suite`, handle 329790,
  PID 9596. Árbol UIA de profundidad 8: 81 nodos, sin truncamiento.
- Evidencia del origen: `../../evidence/ui-inspection/window-329790-20261014-130659.json`.
- Destino: `src/Master.WinUI/DashboardWindow.xaml` y
  `src/Master.WinUI/DashboardWindow.xaml.cs`.
- Acceso: desde SCR-001 con «Abrir Dashboard (vista previa, sin autenticación)».
  La vista previa no verifica credenciales ni representa una sesión autenticada.

## Contenido observado y reproducido

- Navegación lateral para Dashboard, Clientes, Productos, Ventas, Reportes,
  Configuración, Utilidades, Gestión de Archivos y Controles Avanzados; incluye
  «Volver al Login».
- Barra superior con «Actualizar» y accesos a Clientes, Productos, Ventas y
  Reportes.
- Cuatro tarjetas KPI con los textos/valores accesibles observados: 20 clientes,
  51 productos, 40 facturados e ingresos de $70,634.27. Son valores estáticos,
  no datos en vivo.
- Progreso de meta mensual: 100% de $60,000.00 USD.
- Sección «Gráfica de Ventas». No se detectaron series de datos; la réplica
  muestra explícitamente que no hay datos para graficar.

## Comportamiento y límites

- «Actualizar» muestra que no hay conexión configurada y no altera los valores.
- Los módulos aún no migrados muestran un aviso en lugar de simular operaciones.
- «Volver al Login» cierra la ventana del Dashboard y conserva abierta la ventana
  de inicio de sesión.
- No se implementaron autenticación, acceso a SQL Server, persistencia ni
  integraciones externas.

## Verificación

- Captura de la aplicación WinUI 3 ejecutada, en ancho amplio:
  `../../evidence/ui/SCR-002-dashboard-winui-responsive.png`.
- Inspección de la pantalla WinUI 3: 61 nodos, sin truncamiento:
  `../../evidence/ui-inspection/window-2295156-20261014-132224.json`.
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

## Pendiente

- Conectar y verificar servicios de datos/autenticación solo cuando exista una
  configuración autorizada.
- Migrar las pantallas y comportamientos de los módulos restantes.
- Crear una gráfica real cuando haya evidencia y una fuente de datos disponible.
