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
- `database/`: mapa, observaciones de solo lectura e incógnitas; no conectar
  hasta contar con autorización y alcance definidos.
- `specification/`: requisitos, reglas, criterios de aceptación, trazabilidad
  y brechas, después de reunir evidencia suficiente.

## Estado

El usuario confirmó como objetivo la ventana WinForms
“WinForms Control Showcase - Login”; se inspeccionó y guardó el árbol UIA
completo. El entorno, la lógica funcional y el comportamiento de sus acciones
siguen sin verificarse. Consulta el [catálogo de pantallas](inventory/screen-catalog.md)
y las [preguntas abiertas](decisions/open-questions.md).
