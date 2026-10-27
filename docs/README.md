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

Los datos visibles son de referencia y no están conectados al PLC, a la base
de datos ni al sistema heredado. Las acciones de producción y guardado están
deshabilitadas hasta contar con una integración y autorización verificables.
Los demás módulos muestran que su migración está pendiente. La compilación y
el arranque se verifican en esta etapa; las operaciones reales de planta no se
han probado.
