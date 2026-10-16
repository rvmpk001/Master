# SQL Server

## Estado actual

- La aplicación WinUI lee su conexión desde `src/Master.WinUI/appsettings.json`.
- La configuración local verificada es `.\SQLEXPRESS` / `DemoShowcaseDB` y usa autenticación integrada de Windows. La base no existía al inspeccionarla; se creó después de la autorización explícita del usuario.
- La identidad de Windows que ejecuta la aplicación necesita permisos de lectura sobre `dbo.Usuarios`, `dbo.Clientes`, `dbo.Productos` y `dbo.Ventas`. No se crean usuarios ni se conceden permisos desde la aplicación.
- El código no crea bases, tablas ni ejecuta scripts al iniciar.
- La conexión de solo lectura se verificó: 20 clientes, 51 productos y 40 ventas.
- Los KPIs cuentan todas las filas de clientes, productos y ventas y suman `Ventas.Total` sin filtrar `Activo` ni `Estado`. La serie mensual se agrupa por `Ventas.FechaVenta` durante los últimos 12 meses.

## Script inicial

`Scripts/Database.sql` es un script de instalación inicial, no una migración recurrente. Crea `DemoShowcaseDB`, siete tablas y sus objetos de integridad, y agrega datos de demostración. Se ejecutó una vez después de la autorización explícita del usuario; el archivo original no se modificó.

El script no elimina objetos ni modifica permisos, pero sí crea la base/tablas e inserta registros. Incluye usuarios de demostración con una contraseña predeterminada compartida y un hash SHA-256 fijo; no usar esas cuentas en producción. También incorpora datos de clientes de muestra. La aplicación solo admite el formato SHA-256 heredado que describe el script; dicho formato no tiene sal ni coste de derivación y debe reemplazarse por almacenamiento de contraseñas moderno antes de un despliegue real. La columna `PasswordHash` permite guardar un formato más largo, pero cambiar la estrategia requerirá una decisión y migración aprobadas.

Las comprobaciones `IF NOT EXISTS` hacen que algunas operaciones de creación/siembra se omitan si encuentran el objeto o registro marcador, pero no comparan ni actualizan columnas, restricciones o índices existentes. Si una siembra quedó incompleta, los lotes que dependen de identificadores identity fijos pueden fallar o referenciar registros distintos. Por ello no ejecutar el archivo sobre una base existente como mecanismo de actualización.

## Plan antes de instalar

La base `DemoShowcaseDB` no existía en `.\SQLEXPRESS` durante la revisión. Tras aprobación, el instalador creó la base y se verificaron las siete tablas, sus columnas, restricciones y los registros sembrados. El conteo real de `Productos` es 51, aunque el comentario y el mensaje final del script dicen 50.

La instancia consultada no tenía una base preexistente que comparar. Una futura instalación en otra instancia requiere revalidar el estado, aprobar los datos de demostración y usar una identidad con permiso para crear la base y el esquema. Una base preexistente requiere primero una comparación de solo lectura y después un script incremental revisado y autorizado; nunca se debe reutilizar automáticamente el instalador.

La ejecución por `sqlcmd` también dejó al menos un texto acentuado con codificación incorrecta: en el producto `CMP-003`, el carácter que debería ser `á` se almacenó como U+00C3 (`Ã`). No se repararon datos. Antes de usar los registros de demostración, hay que revisar el resto de las cadenas sembradas y aprobar una corrección incremental separada; volver a ejecutar el instalador no las corrige.

## Migración de clientes

`Scripts/Migrations/20261015_AddCustomerProfileFields.sql` agrega `EsVip BIT NULL` y `TipoPersona NVARCHAR(20) NULL` y una restricción que admite `Fisica` o `Juridica`. Es incremental e idempotente, exige ejecutarse dentro de `DemoShowcaseDB` y no asigna valores a los registros existentes: esos datos históricos permanecen desconocidos (`NULL`) hasta que se clasifiquen desde la interfaz.

La migración se ejecutó después de la autorización explícita del usuario y se volvió a ejecutar para comprobar su idempotencia. No insertó, actualizó ni eliminó registros de clientes. El código de la aplicación no ejecuta migraciones automáticamente.

## Autenticación y cifrado

La cadena local usa `Integrated Security=True`: SQL Server autoriza a la cuenta de Windows que ejecuta el proceso, que es distinta de los usuarios de aplicación de `dbo.Usuarios`. `TrustServerCertificate=True` se deja configurado para la instancia local cuyo certificado no está en la cadena de confianza del equipo. Para otro entorno, instale/configure un certificado confiable y desactive esa opción.

El esquema no tiene campos ni tabla para PIN/2FA ni una meta mensual. La gráfica no inventa una meta: consulta los importes existentes de `Ventas` por mes. No hay una columna de moneda, por lo que el Dashboard no atribuye una divisa a los importes.
