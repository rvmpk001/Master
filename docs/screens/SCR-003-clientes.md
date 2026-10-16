# SCR-003 — Administración de Clientes

## Origen y evidencia

- Ventana: `Clientes` (`FrmClientes`), formulario secundario de
  `WINFORMS CONTROL SHOWCASE - Enterprise Suite` (PID 19360).
- La ventana principal identificada es el Dashboard contenedor; durante esta
  inspección el formulario activo era Clientes. No se confunde con SCR-002.
- Árbol UIA principal, solo lectura, profundidad 8: 92 nodos, sin truncamiento:
  [`window-67400-20261015-152511.json`](../../evidence/ui-inspection/window-67400-20261015-152511.json).
- Árbol UIA del formulario Detalles del Cliente, solo lectura, profundidad 8:
  46 nodos, sin truncamiento:
  [`window-198538-20261015-152833.json`](../../evidence/ui-inspection/window-198538-20261015-152833.json).
- Ventana Clientes WinUI 3 generada, solo lectura, profundidad 4: 72 nodos, sin
  truncamiento:
  [`window-853678-20261015-154759.json`](../../evidence/ui-inspection/window-853678-20261015-154759.json).
- Destino WinUI 3: `src/Master.WinUI/CustomersView.xaml` y
  `src/Master.WinUI/CustomersView.xaml.cs`, alojada dentro del contenido de
  `DashboardWindow`.

La inspección no leyó valores de campos de entrada, no ejecutó patrones UIA y no
interactuó con la aplicación original. El árbol del formulario secundario de
handle 198516 alcanzó el límite de nodos por los elementos del listado; esa
captura truncada no se usa como inventario y no se distribuye. Se inspeccionó
aparte el contenedor Detalles del Cliente sin truncamiento.

## Controles observados

- Tabla/listado `dgvClientes`.
- Buscar (`txtBuscar`), filtro por ciudad (`cmbFiltroCiudad`), «Filtrar»,
  «Todos», «Exportar CSV» e «Importar CSV».
- Formulario: Documento, Nombres, Apellidos, Teléfono, Email, Ciudad, Límite
  Crédito, Fecha Nac/Con y Dirección.
- Preferencias: Laptops, Monitores, Componentes, Almacenamiento, Audio y Redes.
- Casillas Cliente Activo y Cliente VIP; opciones Persona Física y Persona
  Jurídica.
- Acciones Nuevo, Actualizar, Eliminar y Cancelar.

Los rótulos de campo, identificadores y controles son observados. El
comportamiento del programa heredado no se ejecutó ni se considera confirmado
solo por su etiqueta.

## Implementación

- La vista se abre desde Clientes en la navegación lateral o en la barra de
  acciones del Dashboard, reemplazando el contenido central sin crear otra
  ventana. «Volver al Dashboard» restaura el panel en la misma ventana.
- Las operaciones de lectura y escritura se habilitan solo en el flujo de Login
  autenticado. El Dashboard requiere una sesión autenticada y ya no expone una
  vista previa sin acceso a SQL Server.
- Nuevo, selección y edición, validación por campos obligatorios y longitudes
  del esquema, actualización, eliminación con confirmación y filtros sobre
  registros cargados están implementados.
- Las ciudades disponibles se derivan de los datos; no se codifica un catálogo.
- Importar CSV acepta el formato generado por la propia exportación. Valida las
  filas antes de importar y usa una única transacción; un duplicado o fallo
  cancela la importación completa. Exportar guarda los registros actualmente
  visibles.
- Preferencias se guardan como lista separada por punto y coma en
  `dbo.Clientes.Preferencias`.
- La migración aditiva autorizada agrega `EsVip` y `TipoPersona`. Los registros
  existentes permanecen con ambos valores `NULL` (desconocidos); no se asignan
  clasificaciones sin evidencia.
- VIP permite distinguir sí/no/desconocido; los radios permiten guardar
  Persona Física, Persona Jurídica o dejarla sin clasificar.
- La exportación CSV incluye VIP y tipo de persona. La importación admite tanto
  el formato nuevo como el formato anterior de 11 columnas.
- No se ejecutaron operaciones de escritura de clientes contra la base durante
  la verificación.

## Verificación de esta ejecución

- Inspección del origen: correcta; formulario secundario y grupo Detalles del
  Cliente guardados en evidencia.
- Compilación WinUI 3 Debug x64 con Visual Studio Community MSBuild 18:
  correcta; 0 advertencias y 0 errores en esta corrección.
- UI Automation comprobó los dos accesos a Clientes (menú lateral y barra
  superior), confirmó el título y el botón de regreso dentro del HWND del
  Dashboard, y verificó que el regreso restaura el panel sin crear una ventana
  adicional. Evidencias actuales:
  [`window-1771208-20261015-161903.json`](../../evidence/ui-inspection/window-1771208-20261015-161903.json)
  (Clientes, 32 nodos) y
  [`window-1771208-20261015-161919.json`](../../evidence/ui-inspection/window-1771208-20261015-161919.json)
  (Dashboard, 18 nodos); ambas sin truncamiento.
- 35 comprobaciones UI automatizadas en la aplicación generada: correctas.
  Incluyeron apertura y validación vacía del Login, navegación al Dashboard y
  Clientes, presencia de controles, edición local de campo, cambio de
  preferencia, entrada de búsqueda y bloqueo de persistencia en vista previa,
  ventana compacta de 740×580 y regreso conservando Dashboard y Login.
- El UIA de la implementación anterior de Clientes se conserva como evidencia
  histórica: 72 nodos sin truncamiento.
- Se detectó y corrigió una excepción `NullReferenceException` del diseño
  adaptable al iniciar el módulo. La compilación y las 35 comprobaciones UI se
  repitieron después de la corrección.
- Conexión SQL de solo lectura: correcta; se verificaron 20 clientes, 51
  productos y 40 ventas.
- Migración de esquema: aplicada tras autorización explícita y ejecutada de
  nuevo para comprobar idempotencia. Los 20 clientes siguen con VIP y tipo de
  persona sin clasificar.
- No se encontraron proyectos de pruebas .NET en el repositorio; la
  verificación funcional se realizó mediante UI Automation.

## Limitaciones pendientes

- No se comprobó el comportamiento original de los botones ni la semántica del
  formato CSV heredado; se documenta el formato de intercambio de esta
  implementación.
- Las operaciones SQL de escritura están implementadas, pero no se probaron
  con cambios reales para no alterar registros existentes.
- No se probó el flujo autenticado ni la visualización en vivo dentro de WinUI
  con credenciales en esta ejecución.
