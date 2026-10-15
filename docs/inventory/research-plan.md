# Plan inicial de investigación

## Estado

- Fase: 0 — Preparación.
- Fecha: 2026-10-14.
- Aplicación objetivo y entorno: Pendientes de identificar.
- Regla de ejecución: detenerse antes de acciones con posibles efectos
  secundarios; SQL Server queda fuera de alcance por ahora.

## Objetivo

Recopilar una especificación funcional basada en observaciones reproducibles de
la aplicación heredada, para que posteriormente pueda revisarse antes de
reconstruirla en WinUI 3 con C# y .NET. Este plan no define ni implementa la
aplicación nueva.

## Preparación completada

- Revisado el contenido del repositorio y las instrucciones locales.
- Encontrado el texto del skill `windows-legacy-discovery` en
  `skills/windows-legacy-discovery/SKILL.md`; el cargador de skills de esta
  sesión no lo ofrece, por lo que se siguieron sus instrucciones directamente.
- Comprobadas las herramientas locales disponibles, sin abrir ni manipular la
  aplicación objetivo.
- Preparada esta estructura inicial de documentación.

## Fases propuestas

1. **Identificación y acceso seguro:** confirmar nombre/versión, objetivo de
   investigación, rol autorizado y si el entorno es de pruebas o desarrollo.
   Evitar producción si el alcance no está claro.
2. **Inventario de ventanas y navegación:** con la aplicación abierta en una
   pantalla reconocible, capturar el estado inicial, título, controles
   accesibles y rutas observadas. Asignar IDs estables `SCR-XXX` y
   `SCR-XXX-CTL-XXX`. No asumir que un botón funciona.
3. **Observación de pantallas:** documentar valores iniciales, etiquetas,
   estados, mensajes y controles. Vincular cada afirmación con evidencia y
   distinguir confirmado, inferido y pendiente.
4. **Flujos seguros:** después de confirmar el entorno y autorización, observar
   una acción reversible por vez. No guardar, borrar, importar, exportar ni
   enviar formularios que puedan cambiar datos sin autorización explícita.
5. **Reglas, persistencia e integraciones:** probar hipótesis en un entorno
   aprobado y con datos ficticios. SQL queda excluido hasta disponer de
   autorización explícita, conexión aprobada y alcance de solo lectura.
6. **Especificación y revisión:** consolidar requisitos y casos de aceptación
   trazables a evidencia; resolver brechas críticas y solicitar aprobación
   antes de iniciar la modernización.

## Registro de evidencia

Usar IDs `OBS-XXX` para hallazgos, `ACT-XXX` para acciones, `CALC-XXX` para
pruebas de caja negra, `REQ-XXX` para requisitos, `BR-XXX` para reglas y
`TC-XXX` para casos de prueba. Cada registro debe incluir método, pasos
reproducibles, resultado observado, evidencia, límites y siguiente paso. No
guardar secretos ni datos personales innecesarios.

## Estructura documental prevista

- `docs/inventory/application-overview.md`: identidad y contexto.
- `docs/inventory/tooling-capabilities.md`: comprobaciones de herramientas y
  limitaciones.
- `docs/inventory/screen-catalog.md`: catálogo de pantallas observadas.
- `docs/decisions/open-questions.md`: datos pendientes y confirmaciones.
- `docs/screens/SCR-XXX-nombre.md`: ficha por pantalla observada.
- `docs/evidence/`: capturas y volcados permitidos, referenciados desde los
  hallazgos.
- `docs/database/`: mapa y observaciones de solo lectura, solo cuando se
  autorice esa fase.
- `docs/specification/`: especificación funcional, reglas, criterios,
  trazabilidad y brechas después de recopilar evidencia.

## Siguiente paso mínimo

Identificar la aplicación (nombre y versión visibles), confirmar que se
investigará en un entorno de pruebas/desarrollo autorizado y dejar abierta una
pantalla inicial reconocible. En ese primer paso solo se observará la ventana;
no se pulsarán acciones ni se conectará a SQL Server.
