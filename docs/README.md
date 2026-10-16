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

La migración conserva el Login, conecta el Dashboard a KPI y ventas mensuales
de SQL Server y añade Clientes y Productos como vistas dentro de la misma
ventana, conectadas al esquema existente. Se verificaron el flujo autenticado,
la lectura/filtros de Productos y la navegación; no se probaron escrituras de
negocio. Consulta el [catálogo de pantallas](inventory/screen-catalog.md),
las [fichas de pantallas](screens/README.md) y las
[preguntas abiertas](decisions/open-questions.md).
