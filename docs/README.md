# Documentación de investigación

Esta documentación registra evidencia verificable sobre la aplicación heredada
antes de definir o construir una sustitución. No se presupone ningún
comportamiento que todavía no se haya observado.

## Estructura

- `inventory/`: contexto de la aplicación, herramientas, plan e inventario de
  pantallas.
- `decisions/`: preguntas abiertas y decisiones que requieren confirmación.
- `screens/`: ficha por pantalla (`SCR-XXX-nombre.md`) una vez que se observe.
- `evidence/`: capturas y volcados autorizados, sin credenciales ni datos
  personales innecesarios.
- `database/`: mapa, observaciones y migraciones incrementales autorizadas;
  validar permisos y alcance antes de conectar o modificar otra instancia.
- `specification/`: requisitos, reglas, criterios de aceptación, trazabilidad
  y brechas, después de reunir evidencia suficiente.

## Estado

La pantalla objetivo confirmada es el dashboard de “SISTEMA BLAPACI VER
3.2.0.0”, planta B-40. La versión WinUI reproduce la composición principal
observable en la captura: navegación de módulos, esquema visual de planta,
controles de producción, materiales, datos de remisión y tablas de ajuste de
humedad y absorción. La interfaz utiliza tema oscuro con contraste explícito,
abre maximizada y adapta sus paneles al tamaño de la ventana, usando
desplazamiento cuando el ancho disponible no permite mostrar todas las tablas.

Los datos visibles del tablero son de referencia y no están conectados al PLC.
La navegación y las acciones de datos consultan el perfil asignado al usuario
autenticado en `dbo.Perfiles` y sus permisos en `dbo.PerfilDetalle`, respetando
la jerarquía `Padre`. `Estatus` controla el acceso al módulo; Alta, Editar,
Baja, Visualizar y las demás acciones gobiernan las operaciones cuando la
opción tiene una acción definida en el perfil. Si no existe una acción definida,
se aplica el acceso del módulo. La opción (`Opcion`) identifica el permiso;
`Titulo` solo se usa como alternativa, pues un reporte y su módulo operativo
pueden compartir el mismo título. Las operaciones de fórmulas, proveedores,
clientes, camiones, pedidos, inventario, carga manual, seguridad y configuración
se integran con estas reglas. Las acciones de producción que requieren PLC no
se modifican. Las cuentas nuevas comienzan sin permisos y requieren asignación
explícita desde Configuración > Accesos. La conexión a SQL Server debe estar
configurada para usar estas funciones; no se validó contra una instancia SQL
real en este entorno.
