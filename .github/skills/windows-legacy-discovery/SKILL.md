---
name: windows-legacy-discovery
description: Inspecciona ventanas Windows heredadas en modo de solo lectura, guarda evidencias, genera una aplicación WinUI 3 funcional con C# y .NET, implementa los comportamientos verificables, adapta la interfaz a diferentes resoluciones, compila, ejecuta pruebas y corrige errores automáticamente.
---

# Windows Legacy UI Migration

## Objetivo

Inspeccionar una ventana Windows heredada y generar una aplicación WinUI 3 con C# y .NET que reproduzca los controles, los comportamientos observables y las funcionalidades que puedan determinarse con evidencia suficiente.

La prioridad es la funcionalidad real, no la reproducción visual exacta.

La interfaz generada debe ser clara, moderna, accesible y adaptable a diferentes resoluciones.

Flujo obligatorio:

**Detectar → Inspeccionar → Guardar evidencia → Analizar controles y comportamientos → Generar interfaz y lógica → Compilar → Ejecutar → Probar → Corregir → Informar resultados.**

Trabaja de forma autónoma. Continúa las etapas posibles sin solicitar nuevos prompts al usuario. No declares terminada una aplicación únicamente porque sus controles sean visibles o el proyecto compile.

## 1. Detectar la aplicación

1. Comprueba las herramientas MCP disponibles y sus firmas reales.
2. Ejecuta `list_windows` si está disponible.
3. Identifica las ventanas candidatas mediante título, handle, PID y metadatos disponibles.
4. No asumas que la ventana activa es la aplicación objetivo.
5. Si existe una configuración previa inequívoca de la aplicación objetivo, úsala y comprueba que sigue siendo válida.
6. Si existen varias candidatas y no es posible distinguirlas con evidencia suficiente, presenta las opciones y formula una única pregunta.
7. No inspecciones ni migres una ventana de demostración como si fuera la aplicación heredada real.

La ventana `WinForms Control Showcase - Login` es una aplicación de demostración previamente observada. No se considera la aplicación objetivo salvo confirmación expresa.

## 2. Inspeccionar la ventana

1. Verifica que la ventana seleccionada siga disponible.
2. Ejecuta `inspect_window` con `max_depth=4` y `max_nodes=250`, respetando la firma real.
3. Extrae las propiedades que estén disponibles:
   - Nombre y tipo de control.
   - Texto y nombre accesible.
   - `AutomationId`.
   - Clase y tecnología.
   - Jerarquía y contenedor.
   - Visibilidad y estado habilitado.
   - Posición y dimensiones.
   - Patrones UI Automation, únicamente si existe una operación de consulta segura.
4. No leas explícitamente valores de campos de entrada ni extraigas contraseñas o datos personales innecesarios.
5. No hagas clic, no escribas, no envíes formularios ni ejecutes patrones de interacción sobre la aplicación original.
6. Si la inspección es incompleta, registra la limitación e intenta una alternativa de lectura disponible.
7. No inventes controles, propiedades ni comportamientos que no estén respaldados por la evidencia.

## 3. Guardar evidencia

Utiliza `save_window_inspection` si está disponible y su firma permite guardar el árbol inspeccionado.

1. Guarda la evidencia en la carpeta de evidencia del proyecto, respetando la estructura existente.
2. Si no hay una convención previa, utiliza:
   - `evidence/ui/SCR-001.json`
   - `docs/screens/SCR-001.md`
   - `docs/screens/README.md`
3. Conserva el árbol de inspección completo que proporcione la herramienta.
4. Utiliza la ruta real devuelta por el MCP. No presupongas nombres ni ubicaciones.
5. Verifica que los archivos existan y que el JSON sea válido.
6. Revisa los datos sensibles antes de guardar.
7. No sobrescribas evidencias anteriores sin comprobar que corresponden a la misma pantalla y ejecución.

Si una operación no está disponible, continúa con las tareas independientes que sí puedan realizarse y registra la limitación.

## 4. Analizar los controles y comportamientos

Antes de generar código, analiza la evidencia y construye internamente un inventario de controles.

Para cada control relevante identifica:

- Identificador y tipo.
- Etiqueta visible.
- Estado inicial observable.
- Acción que aparentemente representa.
- Comportamiento respaldado por evidencia.
- Resultado esperado, si puede determinarse.
- Dependencias conocidas.
- Incertidumbres y limitaciones.

Distingue explícitamente entre:

**Confirmado:** comportamiento respaldado por evidencia, documentación disponible o pruebas reproducibles.

**Inferido:** comportamiento probable, pero no demostrado. No debe presentarse como funcionalidad original confirmada.

**Pendiente:** comportamiento que no puede determinarse con los recursos disponibles.

No interpretes el nombre de un control como prueba suficiente de su comportamiento. Por ejemplo, un botón denominado `Login` no demuestra cómo funciona la autenticación, qué credenciales acepta ni qué servicio utiliza.

No es necesario generar una matriz extensa como documento independiente. Este inventario sirve para guiar la implementación y las pruebas.

## 5. Localizar e inspeccionar el proyecto

1. Identifica la raíz real del workspace.
2. Localiza el proyecto WinUI 3 existente.
3. Inspecciona los archivos XAML, C#, configuración, recursos y dependencias pertinentes.
4. Identifica el punto de entrada, las ventanas, las páginas, los modelos y los servicios existentes.
5. Comprueba las versiones de .NET y Windows App SDK utilizadas por el proyecto.
6. Conserva la arquitectura existente y reutiliza componentes cuando sea apropiado.
7. No reemplaces archivos completos cuando baste con cambios localizados.
8. No elimines funcionalidades existentes que no estén relacionadas con la migración.

Si no existe un proyecto WinUI 3, comprueba si el entorno dispone de los recursos necesarios para crearlo. No sustituyas silenciosamente el objetivo por una aplicación web, HTML, React o una maqueta estática.

Si no es posible crear el proyecto por falta de SDK, dependencias o herramientas, informa el bloqueo y no declares que la aplicación fue generada correctamente.

## 6. Generar la interfaz WinUI 3

Genera o actualiza los archivos XAML y C# necesarios.

### 6.1. Diseño

- Utiliza controles WinUI 3 apropiados para el propósito de cada elemento.
- Conserva textos, etiquetas, identificadores y tipos de control originales cuando sea posible.
- No es obligatorio reproducir exactamente colores, dimensiones, alineación ni distribución.
- Utiliza una distribución moderna, consistente, legible y profesional.
- Muestra etiquetas descriptivas para todos los campos y controles relevantes.
- Mantén visibles las acciones principales.
- Incluye estados visuales coherentes para controles habilitados, deshabilitados, seleccionados y con errores.
- No omitas controles importantes únicamente para simplificar el diseño.

### 6.2. Diseño adaptable

La interfaz debe funcionar en distintas dimensiones de ventana.

- Utiliza `Grid`, `StackPanel`, `RelativePanel` u otros mecanismos adecuados de composición.
- Aprovecha `Grid` con tamaños flexibles, `Auto` y `*` cuando corresponda.
- Evita dimensiones fijas innecesarias que provoquen recortes.
- Utiliza `VisualStateManager`, estados adaptativos u otros mecanismos apropiados cuando sea necesario cambiar la distribución.
- Reorganiza formularios y secciones según el espacio disponible.
- Permite desplazamiento cuando el contenido no quepa en el área visible.
- Evita que los botones, etiquetas o campos importantes queden fuera de la ventana.
- Comprueba que el redimensionamiento no inutilice los controles.
- Conserva una experiencia adecuada para ventanas pequeñas y grandes.

No utilices CSS, Flexbox ni Grid web como sustitutos de los mecanismos nativos de WinUI 3.

## 7. Implementar la funcionalidad real

**Esta fase es obligatoria. No basta con generar XAML que dibuje los controles.**

Cada control interactivo debe tener un comportamiento implementado, respaldado por evidencia o claramente identificado como una adaptación necesaria.

### 7.1. Botones

Para cada botón:

1. Identifica su propósito mediante la evidencia disponible.
2. Conecta su evento `Click`, un comando mediante `ICommand` o el mecanismo apropiado para la arquitectura existente.
3. Implementa la acción correspondiente.
4. Gestiona el estado de carga, éxito o error cuando corresponda.
5. Comprueba que la acción produzca un resultado observable.
6. Evita manejadores vacíos o acciones que solo cambien el aspecto visual sin cumplir el propósito del control.

No dejes botones habilitados que parezcan operativos si no existe ninguna acción implementada.

Si no se conoce la lógica original, no inventes una operación de negocio. Documenta la limitación y aplica únicamente un comportamiento alternativo cuando sea seguro, útil y esté claramente identificado como tal.

### 7.2. CheckBox y ToggleSwitch

Para cada checkbox o interruptor:

1. Asegura que el usuario pueda cambiar su estado.
2. Gestiona correctamente los eventos `Checked`, `Unchecked` o el enlace de datos correspondiente.
3. Mantén el estado sincronizado con el modelo de la interfaz cuando corresponda.
4. Propaga el cambio a los controles o comportamientos dependientes cuando exista evidencia suficiente.
5. Respeta los estados iniciales y las reglas observables.
6. Prueba tanto la activación como la desactivación.

Un checkbox no se considera completamente funcional solo porque visualmente cambie de marcado a desmarcado. También debe implementarse su efecto asociado cuando dicho efecto sea conocido.

### 7.3. Campos de entrada

Los campos `TextBox`, `PasswordBox`, `NumberBox` y equivalentes deben:

- Permitir introducir y modificar valores.
- Conservar correctamente su estado.
- Mostrar etiquetas y ayudas apropiadas.
- Aplicar las validaciones conocidas.
- Mostrar mensajes de error comprensibles.
- Respetar los requisitos de seguridad y privacidad.

No registres contraseñas ni valores sensibles en logs o evidencias.

### 7.4. ComboBox, ListView y selección

- Permite desplegar y seleccionar opciones.
- Gestiona correctamente el elemento seleccionado.
- Actualiza el estado correspondiente.
- Implementa los efectos asociados cuando se conozcan.
- Respeta los estados habilitado y deshabilitado observados.

Si no se conocen las opciones originales, no inventes un catálogo y lo presentes como si fuera el real.

### 7.5. Navegación, formularios y otros controles

Implementa el comportamiento apropiado de los controles adicionales: navegación, pestañas, menús, botones de opción, comandos, tablas y acciones contextuales.

Utiliza eventos, comandos y enlaces de datos coherentes con la arquitectura existente.

No generes interacciones ficticias únicamente para que una demostración parezca funcionar.

### 7.6. Validaciones y mensajes

Cuando exista evidencia suficiente:

- Valida los campos obligatorios.
- Valida los formatos y rangos conocidos.
- Muestra mensajes de error apropiados.
- Impide las acciones inválidas cuando corresponda.
- Conserva los valores introducidos cuando sea apropiado.
- Gestiona correctamente los errores de las operaciones.

No inventes reglas de negocio desconocidas.

## 8. Servicios externos y reglas de negocio

No inventes ni simules silenciosamente autenticación, autorizaciones, consultas SQL, persistencia, llamadas a API ni operaciones del sistema original.

1. Inspecciona el código y los recursos disponibles para determinar si ya existe una implementación reutilizable.
2. Si la lógica existente puede utilizarse de forma segura, intégrala respetando su arquitectura.
3. No conectes la aplicación a bases de datos ni a servicios externos sin una configuración existente y una autorización apropiada.
4. Si falta un backend, no declares que una operación real se completó.
5. Si una funcionalidad requiere un servicio inaccesible, registra la dependencia y el resultado de la prueba.
6. Si una alternativa local puede implementarse sin falsear el comportamiento original, identifica claramente esa diferencia.
7. Nunca muestres una autenticación como exitosa sin una comprobación real del resultado.

La ausencia de un servicio externo no debe impedir implementar y probar las partes independientes de la interfaz.

## 9. Compilar y ejecutar

Después de implementar los cambios:

1. Identifica el proyecto y la configuración de compilación adecuados.
2. Comprueba la disponibilidad del SDK y las dependencias necesarias.
3. Ejecuta la compilación utilizando las herramientas reales disponibles.
4. Analiza los errores y advertencias relevantes.
5. Corrige los errores solucionables.
6. Repite la compilación hasta obtener éxito o alcanzar un bloqueo real.
7. Ejecuta la aplicación generada si el entorno lo permite de forma segura.
8. Comprueba que la ventana se abre y que los controles se representan correctamente.

Utiliza `dotnet build` cuando sea compatible con el proyecto y el entorno. Si requiere otro procedimiento, identifica el apropiado en función de los archivos y las herramientas disponibles.

No afirmes que el proyecto compila o se ejecuta si no has obtenido evidencia de ello.

## 10. Pruebas funcionales obligatorias

**La compilación no sustituye las pruebas funcionales.**

Después de ejecutar la aplicación generada, prueba los controles utilizando mecanismos reales disponibles en el entorno.

Prioriza las herramientas de automatización de UI compatibles con Windows y WinUI 3. También puedes utilizar pruebas automatizadas existentes o incorporar pruebas adecuadas cuando el proyecto lo permita.

No supongas que el MCP de inspección original puede interactuar con la aplicación generada. Verifica sus capacidades antes de utilizarlo.

### 10.1. Pruebas de controles

Comprueba:

- Que los checkbox se marquen y desmarquen.
- Que los campos permitan escribir, modificar y conservar valores.
- Que los ComboBox permitan seleccionar opciones.
- Que los botones ejecuten sus acciones.
- Que las validaciones produzcan los resultados esperados.
- Que los mensajes de error y confirmación aparezcan cuando corresponda.
- Que los controles dependientes respondan correctamente a los cambios de estado.
- Que no existan errores de ejecución que impidan utilizar la interfaz.

### 10.2. Pruebas de la ventana

Comprueba:

- Apertura y presentación de la ventana.
- Visibilidad de etiquetas y controles.
- Ausencia de recortes importantes.
- Redimensionamiento de la ventana.
- Distribución adecuada con distintas dimensiones.
- Accesibilidad de las acciones principales.
- Comportamiento de desplazamiento cuando sea necesario.

### 10.3. Pruebas de integración

Prueba las integraciones disponibles solo cuando existan recursos y configuración apropiados.

Si una acción depende de un backend no disponible, marca la prueba como bloqueada o pendiente. No la marques como exitosa basándote en una respuesta simulada.

### 10.4. Evidencia de las pruebas

Registra el resultado de cada prueba con uno de estos estados:

- **Correcta:** se ejecutó la prueba y se observó el resultado esperado.
- **Fallida:** se ejecutó la prueba y el resultado no fue el esperado.
- **Bloqueada:** no se pudo ejecutar por falta de herramientas, configuración o dependencias.
- **No ejecutada:** no se realizó la prueba.

No confundas una prueba bloqueada con una prueba correcta.

## 11. Corregir automáticamente los errores

Si una prueba falla:

1. Examina el error y localiza su causa probable.
2. Inspecciona el código relacionado.
3. Corrige el problema cuando sea posible.
4. Vuelve a compilar si se modificó código que lo requiera.
5. Repite la prueba fallida.
6. Ejecuta nuevamente las pruebas relacionadas para detectar regresiones.

Continúa hasta que las pruebas disponibles pasen o exista un bloqueo real.

No ocultes errores, no elimines pruebas para obtener un resultado favorable y no declares éxito por haber intentado una corrección.

Si no hay herramientas para interactuar con la aplicación generada, realiza las comprobaciones estáticas disponibles y registra explícitamente que la funcionalidad interactiva no pudo verificarse.

## 12. Seguridad

- La aplicación Windows original se inspecciona en modo de solo lectura.
- No hagas clic, escribas ni envíes formularios en la aplicación original.
- No ejecutes `Invoke`, `Toggle`, `Select` ni patrones equivalentes sobre la aplicación original.
- No cierres ventanas ni modifiques datos originales.
- No te conectes a SQL Server ni a otros servicios externos sin autorización y configuración apropiadas.
- No extraigas ni registres credenciales o datos personales innecesarios.
- No inventes herramientas, parámetros, archivos ni resultados.
- La ejecución y las pruebas interactivas se realizan exclusivamente sobre la aplicación generada y los entornos de prueba autorizados.

## 13. Herramientas y ejecución desde VS Code

### Herramientas MCP de inspección

Utiliza las herramientas MCP disponibles con sus firmas reales:

- `list_windows(visible_only=true)`: enumerar ventanas.
- `get_active_window()`: consultar la ventana activa.
- `inspect_window(window_handle, max_depth=4, max_nodes=250)`: obtener el árbol UI Automation.
- `save_window_inspection(window_handle, max_depth=4, max_nodes=250)`: guardar la inspección en JSON.

Verifica los nombres y parámetros reales antes de ejecutar las operaciones.

El MCP de inspección actualmente identificado no ofrece por sí mismo operaciones explícitas para crear archivos C#/XAML, ejecutar compilaciones, interactuar con controles o comparar visualmente interfaces.

### Operaciones en el workspace

Utiliza las herramientas de archivos, terminal y automatización de VS Code que estén realmente disponibles.

1. Identifica la raíz del workspace.
2. Lee los archivos pertinentes antes de modificarlos.
3. Guarda la evidencia mediante el MCP y registra su ruta real.
4. Modifica el código existente o crea los archivos necesarios.
5. Ejecuta las compilaciones y pruebas disponibles.
6. Examina los resultados reales del terminal.
7. Comprueba que los archivos se hayan guardado correctamente.

No asumas que una herramienta está disponible por el mero hecho de ejecutarse desde VS Code.

Si falta una herramienta de automatización UI, comprueba si existe alguna alternativa instalada y compatible antes de declarar que no es posible realizar las pruebas.

No instales dependencias, modifiques configuraciones del sistema ni realices operaciones potencialmente destructivas sin una justificación apropiada.

## 14. Criterios de aceptación

La migración se considera satisfactoria únicamente cuando se hayan evaluado todos los criterios aplicables:

1. La evidencia de origen está guardada y validada.
2. Los controles relevantes están representados en la interfaz generada.
3. Los controles interactivos tienen eventos, comandos o enlaces de datos apropiados.
4. Las acciones conocidas tienen implementaciones reales.
5. Las validaciones conocidas se han implementado.
6. La aplicación compila correctamente, si el entorno permite compilarla.
7. La aplicación se ejecuta correctamente, si el entorno permite ejecutarla.
8. Las pruebas funcionales ejecutadas producen los resultados esperados.
9. La interfaz es legible, conserva sus etiquetas y se adapta a distintas dimensiones.
10. Las limitaciones y funcionalidades no verificadas están identificadas explícitamente.

La ausencia de herramientas no debe provocar que se declare una etapa completada sin evidencia. Informa el estado real y continúa con las etapas independientes posibles.

No declares la migración completamente verificada si existen pruebas funcionales esenciales pendientes.

## 15. Criterio de finalización y reporte

Completa todas las etapas posibles sin solicitar nuevos prompts entre ellas.

Al terminar, informa únicamente:

- Aplicación y ventana de origen.
- Proyecto y tecnología de destino.
- Archivos creados o modificados y sus rutas reales.
- Número y tipos de controles detectados y generados, si se conocen.
- Funcionalidades implementadas.
- Resultado de la compilación y ejecución.
- Pruebas funcionales realizadas y su estado.
- Errores corregidos y pendientes.
- Limitaciones que requieran herramientas, configuración o intervención humana.

No generes documentación extensa, matrices de trazabilidad ni análisis independientes de reglas de negocio salvo que sean necesarios para resolver una incertidumbre.

Pregunta al usuario solo cuando una ambigüedad o un bloqueo real impida continuar con seguridad.

**Regla fundamental:** una interfaz visible no equivale a una aplicación funcional. El trabajo debe incluir la implementación de los comportamientos conocidos, la ejecución de pruebas reales y el reporte honesto de todo lo que no haya podido verificarse.

### Gestión del script SQL inicial

La ubicación predeterminada del script inicial de base de datos es `Database/Scripts/Database.sql`, salvo que el proyecto ya tenga otra convención.

Antes de usar el script:

1. Leer su contenido completo.
2. Identificar el nombre de la base de datos y los objetos que crea.
3. Detectar instrucciones destructivas, datos iniciales, credenciales, cambios de permisos y dependencias.
4. Determinar si el script es idempotente o si su ejecución repetida puede producir errores o alterar datos.
5. Comprobar si la base objetivo existe y comparar el esquema antes de ejecutar cambios.
6. Documentar los riesgos y preparar un plan de ejecución.

Reglas de ejecución:

- El script original es una referencia de esquema y una posible instalación inicial; no es una orden automática de ejecución.
- No ejecutarlo al iniciar la aplicación.
- No ejecutarlo automáticamente cada vez que se invoque el skill.
- No asumir que el esquema está vacío.
- No ejecutar instrucciones de creación, modificación o eliminación sobre una base existente sin autorización.
- No modificar el archivo original silenciosamente.
- Si se necesitan cambios, generar un script incremental separado y revisar su impacto.
- Conservar evidencia de los cambios aplicados y distinguir entre scripts analizados, generados y ejecutados.
- Documentar el procedimiento de instalación y los requisitos de permisos en `Database/README.md`.

La creación inicial de una base nueva y las actualizaciones de una base existente deben ser procesos diferentes. No reutilizar un script de instalación inicial como mecanismo de migración recurrente.