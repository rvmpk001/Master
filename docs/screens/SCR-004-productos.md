# SCR-004 — Catálogo de Productos

## Origen y evidencia

- Ventana contenedora: `WINFORMS CONTROL SHOWCASE - Enterprise Suite`,
  handle 67218, PID 11068. La ventana se mostró en primer plano por el usuario;
  no se restauró ni navegó mediante automatización.
- Pantalla: `Productos` (`FrmProductos`), handle 198348, dentro del panel
  `pnlContenedorHijos`.
- Árbol UIA del formulario, solo lectura, profundidad 4: 88 nodos, sin
  truncamiento:
  [`../../evidence/ui-inspection/window-198348-20261016-110215.json`](../../evidence/ui-inspection/window-198348-20261016-110215.json).
- Acciones CRUD en el contenedor `flpBotonesCrud`, profundidad 4: cinco nodos,
  sin truncamiento:
  [`../../evidence/ui-inspection/window-198454-20261016-110351.json`](../../evidence/ui-inspection/window-198454-20261016-110351.json).
- El árbol de `dgvProductos` incluye controles `Custom` de filas; no se
  interpretan sus nombres como valores leídos explícitamente de campos.

## Controles observados

- Catálogo `dgvProductos`.
- Filtros `txtBuscar` y `cmbFiltroCategoria`; botones «Filtrar» y «Todos».
- Grupo «Detalles del Producto»: Código, Nombre del Producto, Categoría,
  Precio Unitario, Stock Disponible, Descripción y Producto Activo.
- Imagen `picProducto`; acciones «Cargar Foto...» y «Quitar Foto».
- Acciones «Nuevo», «Actualizar», «Eliminar» y «Cancelar».

La inspección no ejecutó botones ni patrones UIA, y no leyó explícitamente
valores de campos de entrada. Por tanto, las operaciones originales no se
consideran demostradas solo por las etiquetas.

## Implementación WinUI 3

- Vista integrada: `src/Master.WinUI/ProductsView.xaml` y
  `src/Master.WinUI/ProductsView.xaml.cs`.
- Datos: `src/Master.WinUI/Data/Product.cs` y
  `src/Master.WinUI/Data/ProductRepository.cs`.
- Navegación: desde «Inventario / Productos» en el menú lateral o «Productos»
  en la barra del Dashboard; «Volver al Dashboard» vuelve al panel en la misma
  ventana. La pantalla sigue detrás del Login y requiere el flujo autenticado
  existente.
- El catálogo, las categorías y el guardado utilizan SQL Server configurado y
  parámetros SQL; no se ejecutan scripts de base de datos al iniciar.
- Buscar y filtrar por categoría opera sobre los registros cargados. Nuevo,
  selección, cancelar, validación, insertar, actualizar y eliminar con
  confirmación están conectados con `dbo.Productos`. La eliminación puede
  rechazarse si el producto está referenciado en ventas.
- Categoría, código, nombre, descripción, precio, stock, imagen y estado activo
  se corresponden con el esquema existente. Se validan los límites y
  restricciones definidos por ese esquema.
- Seleccionar una imagen local PNG, JPG, JPEG o BMP muestra una vista previa y
  la guarda como `ImagenBase64` al confirmar el producto; «Quitar Foto» limpia
  ese valor cuando se guarda. Esta selección local es una adaptación WinUI;
  no se verificó el comportamiento original del botón de imagen.
- El formulario permite desplazamiento y reorganiza la lista y el detalle en
  ventanas estrechas.

## Verificación y límites

- No se interactuó con controles de la aplicación heredada.
- Compilación WinUI 3 `Debug|x64` con Visual Studio Community MSBuild 18:
  correcta.
- Después del último ajuste de carga con reintento y protección contra cargas
  simultáneas, se recompiló el proyecto y se abrió la aplicación generada. El
  Login aceptó los datos de prueba proporcionados por el usuario y abrió el
  Dashboard; los datos de acceso no se conservaron.
- Login generado: se abrió; el envío vacío mostró «Datos requeridos». Después
  de que el usuario proporcionó datos de prueba, se observó el Dashboard. Los
  datos de acceso no se conservaron en evidencia ni en documentación.
- Productos: correcta; la consulta real de solo lectura cargó 51 filas y
  categorías desde la conexión SQL configurada.
- En la build final, la navegación del Dashboard a Productos presentó el
  contador «51 de 51». Volver al Dashboard y abrir Productos otra vez también
  terminó correctamente con «51 de 51», sin escribir ni eliminar registros.
- El camino de recuperación tras un fallo de carga no se probó forzando un
  error de SQL; solo se verificaron la compilación final, la carga exitosa y la
  navegación repetida.
- Filtrar con una búsqueda sin coincidencias devolvió `0 de 51`; «Todos»
  restauró `51 de 51`. La selección de una categoría filtró `5 de 51` y
  «Todos» volvió a restaurar los 51 productos.
- Nuevo permitió editar el borrador; se alternó «Producto Activo» y
  «Cancelar» limpió los valores y restauró el estado inicial. Guardar con
  código/nombre vacíos mostró la validación esperada sin intentar persistir.
- La navegación de regreso al Dashboard y la apertura de Productos desde la
  barra de acciones funcionaron en la misma ventana.
- Reflujo a `720×720`: título, filtros, lista, contador y formulario
  permanecieron accesibles; el catálogo conservó sus 51 filas.
- No se ejecutaron escrituras ni se confirmó una eliminación; no se alteraron
  datos de productos. Tampoco se probó selección ni limpieza de un archivo de
  imagen.
- Los filtros, CRUD y carga/limpieza de imagen son comportamientos de esta
  implementación basados en los controles observados y el esquema; no se
  afirma que reproduzcan la lógica interna original.
