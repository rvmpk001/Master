# Preguntas abiertas y decisiones

No se han tomado decisiones funcionales porque no se han probado reglas ni
acciones de la aplicación; la inspección realizada fue visual y de solo lectura.

## Identificación y alcance

- [ ] ¿Cuál es el nombre y la versión visibles de la aplicación?
- [ ] ¿Qué módulo, proceso o pregunta de negocio se investigará primero?
- [ ] ¿El entorno es desarrollo/pruebas y está autorizado para esta
      investigación?
- [ ] ¿Qué rol de prueba autorizado se utilizará? No registrar credenciales.

## Acceso y seguridad

- [ ] ¿Puede el usuario abrir la aplicación y dejar visible una pantalla
      inicial reconocible?
- [ ] ¿Los datos visibles contienen información personal o confidencial que
      deba ocultarse en evidencias?
- [ ] ¿Qué acciones, si alguna, se autorizan para una etapa posterior y en qué
      entorno seguro?
- [ ] ¿Existe un responsable que autorice cualquier futura consulta de solo
      lectura a SQL Server?

## Herramientas

- [ ] ¿La aplicación expone sus ventanas y controles mediante UI Automation?
- [ ] ¿Se necesita inspección manual o OCR para controles personalizados?
- [ ] ¿Se autorizará preparar una herramienta local de solo lectura si
      `Inspect.exe` no proporciona suficiente detalle?

## Decisiones diferidas

- No conectar a SQL Server hasta que exista autorización, entorno y alcance
  explícitos.
- No ejecutar acciones que puedan cambiar datos.
- No iniciar el desarrollo de la sustitución hasta contar con una
  especificación revisada y aprobada.
