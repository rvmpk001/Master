# SCR-005 — Facturación / Ventas

## Origen y evidencia

- Ventana principal: `WINFORMS CONTROL SHOWCASE - Enterprise Suite`,
  handle `132676`, PID `15656`.
- El usuario seleccionó Facturación / Ventas y la dejó visible en el panel
  `pnlContenedorHijos`; formulario observado: `Ventas` (`FrmVentas`),
  handle `263878`.
- Inspección UI Automation de solo lectura, profundidad 4: 62 nodos, sin
  truncamiento:
  [`../../evidence/ui-inspection/window-263878-20261017-084431.json`](../../evidence/ui-inspection/window-263878-20261017-084431.json).
- La inspección no hizo clic, no escribió, no leyó valores de campos ni
  ejecutó operaciones. Las filas del detalle exponen encabezados pero no se
  documentan valores de celdas.
- No hay una herramienta de captura de pantalla del escritorio disponible en
  esta sesión; no se generó una imagen de la aplicación heredada.

## Controles observados

- Cabecera: `txtNumeroFactura`, `dtpFechaVenta`, `cmbCliente` (Cliente
  Receptor, obligatorio) y `cmbMetodoPago`.
- Agregado de artículos: `cmbProducto`, `nudCantidad`,
  `lblStockInfo`, `btnAgregarItem` y `btnQuitarItem`.
- Detalle `dgvDetalle`: cinco columnas accesibles
  (`CodigoProducto`, `NombreProducto`, `Cantidad`, `PrecioUnitario`,
  `Subtotal`); UIA expuso dos filas estructurales sin registrar sus valores.
- Totales visibles: Subtotal, ITBIS/IVA 18% y Total.
- Observaciones (`txtObservaciones`) y acciones `btnNuevaVenta`,
  `btnGenerarTicket` («Ver / Imprimir Ticket») y
  `btnRegistrarVenta` («Completar factura»).
- Conteo UIA del formulario: 10 botones, 4 ComboBox (incluido el selector de
  fecha expuesto por UIA), 2 Spinner, 3 Edit, 1 DataGrid, 5 encabezados y
  10 elementos de celda. La inspección no permite confirmar la lógica de los
  botones solo por sus nombres.

## Integración WinUI 3

- Vista integrada en la ventana y el área de contenido existentes:
  [`../../src/Master.WinUI/SalesView.xaml`](../../src/Master.WinUI/SalesView.xaml)
  y
  [`../../src/Master.WinUI/SalesView.xaml.cs`](../../src/Master.WinUI/SalesView.xaml.cs).
- Acceso por «Facturación / Ventas» en el menú lateral o «Ventas» en la barra
  del Dashboard; «Volver al Dashboard» retorna en la misma ventana. No se
  crea una ventana ni un Login nuevos.
- La vista sigue el patrón existente de `UserControl` más repositorio y
  comparte el `AuthenticatedUser` autenticado. El Login y sus reglas de
  autenticación no se modificaron.
- Campos, detalle, totales y acciones se reorganizan según el ancho de la
  ventana; el contenido puede desplazarse verticalmente.

## Funcionalidad y fundamento

- `dbo.Ventas`, `dbo.DetalleVenta`, `dbo.Configuracion` y sus columnas se
  revisaron en `Database/Scripts/Database.sql`; el script crea y siembra la
  base inicial y **no fue ejecutado ni modificado** en esta tarea.
- Lectura: clientes activos, productos activos, métodos de pago ya presentes
  en ventas, tasa fiscal y moneda desde la configuración SQL existente.
  No se codifica una lista de pagos ni una tasa de reserva; si falta
  configuración, la pantalla informa el error.
- El usuario confirmó que completar una venta debe insertar el encabezado y
  sus detalles **sin modificar `dbo.Productos.Stock`**. El repositorio usa
  una transacción serializable, valida cliente/productos/configuración y
  revierte implícitamente si falla un comando antes del commit.
- La tasa fiscal se lee de `Sistema_ImpuestoPorcentaje`; se redondea a dos
  decimales. Se genera un comprobante `FAC-{año}-{secuencia}` usando la
  convención de muestra del script inicial. La lógica original de cálculo y
  numeración no pudo verificarse; se documenta como adaptación.
- Agregar/Quitar modifica el borrador local; las cantidades deben ser enteros
  positivos y el cliente, método de pago y detalle se validan antes de
  completar. El guardado solicita confirmación y escribe solo las tablas de
  venta y detalle.
- «Ver / Imprimir Ticket» muestra una vista previa de una factura ya
  registrada. La impresión física no está implementada.
- No se afirma que el texto de cabecera sobre atomicidad demuestre por sí solo
  el comportamiento original. No se actualizan existencias porque el usuario
  lo excluyó expresamente.

## Clasificación de hallazgos

- **Confirmado:** ventana y formulario visibles; jerarquía dentro de
  `pnlContenedorHijos`; controles, columnas accesibles y etiquetas descritas
  arriba; tablas y configuración presentes en el script existente; política
  solicitada para no modificar existencias.
- **Inferido / adaptación WinUI:** que «Nueva Venta» limpia el borrador,
  agregar/quitar actualiza el detalle, el formulario requiere un método de
  pago, el comprobante sigue el prefijo `FAC-{año}-`, y el encabezado más las
  líneas se guardan en una transacción. También son decisiones de esta
  implementación limitar selectores a clientes/productos activos, derivar
  métodos de pago de datos existentes, usar una vista previa local del ticket
  y solicitar confirmación antes de insertar.
- **Pendiente de validar:** las acciones, validaciones y reglas exactas del
  programa heredado; su comportamiento de impresión; escritura en un entorno
  de prueba autorizado; navegación y reflujo de la vista generada en
  ejecución. No se confirmó si la aplicación heredada altera existencias.

## Verificación y limitaciones

- Evidencia de origen: **Correcta**; árbol UIA guardado y JSON validado
  (62 nodos, sin truncamiento).
- Compilación `Debug|x64` del proyecto WinUI 3 con Visual Studio Community
  MSBuild 18: **Correcta**, sin errores reportados. Una primera compilación
  detectó una referencia inválida a `Grid.Foreground`; se corrigió y se
  compiló de nuevo.
- Aplicación generada: **Correcta**, abrió el Login existente. UI Automation
  pulsó «Ingresar» con credenciales incompletas y observó «Datos requeridos»;
  no se introdujeron credenciales ni se llamó al servicio de autenticación.
- No se ejecutaron pruebas que inserten ventas ni se ejecutó el script SQL.
  La escritura de negocio en una base existente queda pendiente de una prueba
  autorizada con datos aislados.
- Las interacciones de controles, navegación en ejecución y reflujo visual
  de la nueva vista no se verificaron con UI Automation en esta ejecución:
  llegar a ella requiere autenticación, y no se proporcionaron credenciales
  de prueba. No se añadió una ruta de acceso alternativa.
- No se implementó integración con impresoras. Tampoco se confirmó el
  comportamiento heredado de «Nueva Venta», «Quitar Ítem», la persistencia,
  la numeración o la actualización de inventario; la política de no modificar
  existencias sí fue indicada explícitamente por el usuario para esta
  implementación.
