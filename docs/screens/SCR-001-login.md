# SCR-001 — WinForms Control Showcase - Login

## Estado

- Estado: Objetivo confirmado por el usuario; inspección UI Automation
  confirmada; comportamiento funcional no probado.
- Identidad: ventana de demostración WinForms confirmada por el usuario como
  objetivo de esta migración visual.
- Fecha de observación: 2026-10-14 15:46:39 UTC.
- Ventana: handle `525482`; PID `8416`.
- Entorno: Desconocido.
- Método/evidencia: herramienta MCP `windows-ui-inspector.inspect_window`,
  backend pywinauto UIA; profundidad máxima 4, máximo 250 nodos; 18 nodos
  devueltos, sin truncamiento. Árbol completo guardado por
  `windows-ui-inspector.save_window_inspection` en
  [`evidence/ui-inspection/window-525482-20261014-094639.json`](../../evidence/ui-inspection/window-525482-20261014-094639.json).
  La evidencia anterior se conserva sin sobrescribir en
  [`evidence/ui/SCR-001-login.json`](../../evidence/ui/SCR-001-login.json) y
  [`evidence/ui-inspection/window-525482-20261014-090929.json`](../../evidence/ui-inspection/window-525482-20261014-090929.json).
- Seguridad: no se leyeron valores explícitos de campos de entrada; no se
  interactuó con controles.

## Propósito

- Confirmado: el título de ventana es “WinForms Control Showcase - Login”; la
  pantalla contiene controles cuyos nombres accesibles incluyen “Inicio de
  Sesión”, “Usuario”, “Contraseña” y “PIN de Seguridad (2FA)”.
- Inferido: podría ser una pantalla de inicio de sesión, por el título y las
  etiquetas observadas.
- Pendiente de verificar: propósito real, autenticación, campos obligatorios,
  controles relacionados con el PIN y comportamiento de cualquier acción.

## Jerarquía de controles

Los Automation IDs, nombres, tipos, clases, estados y rectángulos de esta tabla
fueron observados en el árbol UIA guardado. Los nombres accesibles vacíos y los
Automation IDs/clases no expuestos se representan como `(vacío)`. Los
rectángulos usan coordenadas de pantalla `izquierda, arriba, derecha, abajo`.
Se omiten los valores de entrada.

| ID | Padre | Nombre accesible | Tipo UIA | Automation ID | Clase | Visible | Habilitado | Rectángulo |
|---|---|---|---|---|---|---|---|---|
| SCR-001-CTL-001 | — | WinForms Control Showcase - Login | Window | `FrmLogin` | `WindowsForms10.Window.8.app.0.29e8405_r3_ad1` | Sí | Sí | `733,125,1330,793` |
| SCR-001-CTL-002 | CTL-001 | Showcase Corporativo de Controles WinForms | Pane | `pnlCard` | `WindowsForms10.Window.8.app.0.29e8405_r3_ad1` | Sí | Sí | `774,332,1289,757` |
| SCR-001-CTL-003 | CTL-002 | Mostrar ojo | CheckBox | `chkVerPassword` | `WindowsForms10.Button.app.0.29e8405_r3_ad1` | Sí | Sí | `1112,526,1236,553` |
| SCR-001-CTL-004 | CTL-002 | Cancelar | Button | `btnCancelar` | `WindowsForms10.Button.app.0.29e8405_r3_ad1` | Sí | Sí | `1049,676,1243,724` |
| SCR-001-CTL-005 | CTL-002 | Ingresar al Sistema | Button | `btnIngresar` | `WindowsForms10.Button.app.0.29e8405_r3_ad1` | Sí | Sí | `812,676,1024,724` |
| SCR-001-CTL-006 | CTL-002 | Recordar usuario | CheckBox | `chkRecordar` | `WindowsForms10.Button.app.0.29e8405_r3_ad1` | Sí | Sí | `812,626,983,655` |
| SCR-001-CTL-007 | CTL-002 | (vacío) | Edit | `mtxtPin` | `WindowsForms10.Edit.app.0.29e8405_r3_ad1` | Sí | Sí | `812,576,962,610` |
| SCR-001-CTL-008 | CTL-002 | PIN de Seguridad (2FA) | Text | `lblPin` | `WindowsForms10.Static.app.0.29e8405_r3_ad1` | Sí | Sí | `806,547,1011,572` |
| SCR-001-CTL-009 | CTL-002 | Contraseña | Edit | `txtPassword` | `WindowsForms10.Edit.app.0.29e8405_r3_ad1` | Sí | Sí | `812,482,1243,516` |
| SCR-001-CTL-010 | CTL-002 | Contraseña | Text | `lblPassword` | `WindowsForms10.Static.app.0.29e8405_r3_ad1` | Sí | Sí | `806,453,912,478` |
| SCR-001-CTL-011 | CTL-002 | Usuario | Edit | `txtUsuario` | `WindowsForms10.Edit.app.0.29e8405_r3_ad1` | Sí | Sí | `812,392,1243,426` |
| SCR-001-CTL-012 | CTL-002 | Usuario | Text | `lblUsuario` | `WindowsForms10.Static.app.0.29e8405_r3_ad1` | Sí | Sí | `806,363,882,388` |
| SCR-001-CTL-013 | CTL-001 | (vacío) | Pane | `pnlHeader` | `WindowsForms10.Window.8.app.0.29e8405_r3_ad1` | Sí | Sí | `744,170,1319,308` |
| SCR-001-CTL-014 | CTL-013 | Showcase Corporativo de Controles WinForms | Text | `lblSubtitulo` | `WindowsForms10.Static.app.0.29e8405_r3_ad1` | Sí | Sí | `875,242,1257,267` |
| SCR-001-CTL-015 | CTL-013 | Inicio de Sesión | Text | `lblTitulo` | `WindowsForms10.Static.app.0.29e8405_r3_ad1` | Sí | Sí | `873,200,1095,238` |
| SCR-001-CTL-016 | CTL-013 | (vacío) | Pane | `picLogo` | `WindowsForms10.Window.8.app.0.29e8405_r3_ad1` | Sí | Sí | `774,198,855,279` |
| SCR-001-CTL-017 | CTL-001 | (vacío) | TitleBar | (vacío) | (vacío) | Sí | Sí | `744,128,1319,170` |
| SCR-001-CTL-018 | CTL-017 | Cerrar | Button | (vacío) | (vacío) | Sí | Sí | `1268,126,1320,170` |

## Propiedades disponibles

La inspección devolvió, para los nodos, tipo UIA, nombre accesible, Automation
ID, nombre de clase, framework, profundidad, hijos, estado habilitado, estado
visible, foco de teclado y rectángulo. Para la ventana raíz también devolvió
handle y PID. Los nodos reportados estaban habilitados y visibles; ninguno
reportó foco de teclado. Los controles expusieron framework `WinForm`; también
se devolvieron nombres de clase.

## Patrones UI Automation consultados

Se consultaron únicamente identificadores de patrones soportados mediante
`AutomationElement.GetSupportedPatterns()` en UI Automation de .NET; no se
obtuvieron objetos de patrón ni se ejecutó ningún patrón. Esta consulta recorrió
16 nodos en la vista Raw UIA y confirmó el handle `525482` y PID `8416`.

| ID de control | Patrones reportados |
|---|---|
| CTL-001 (`FrmLogin`) | `WindowPattern`, `TransformPattern` |
| CTL-002 (`pnlCard`) | Ninguno reportado |
| CTL-003 (`chkVerPassword`) | `InvokePattern`, `TogglePattern` |
| CTL-004 (`btnCancelar`) | `InvokePattern` |
| CTL-005 (`btnIngresar`) | `InvokePattern` |
| CTL-006 (`chkRecordar`) | `InvokePattern`, `TogglePattern` |
| CTL-007 (`mtxtPin`) | `ValuePattern`, `TextPattern` |
| CTL-008 (`lblPin`) | Ninguno reportado |
| CTL-009 (`txtPassword`) | `ValuePattern`, `TextPattern` |
| CTL-010 (`lblPassword`) | Ninguno reportado |
| CTL-011 (`txtUsuario`) | `ValuePattern`, `TextPattern` |
| CTL-012 (`lblUsuario`) | Ninguno reportado |
| CTL-013 (`pnlHeader`) | Ninguno reportado |
| CTL-014 (`lblSubtitulo`) | Ninguno reportado |
| CTL-015 (`lblTitulo`) | Ninguno reportado |
| CTL-016 (`picLogo`) | Ninguno reportado |
| CTL-017 (`TitleBar`) | No consultable en la vista .NET usada |
| CTL-018 (`Cerrar`) | No consultable en la vista .NET usada |

Estos identificadores solo indican los patrones reportados como compatibles; no
demuestran el comportamiento resultante de invocarlos. La herramienta MCP
`inspect_window`/`save_window_inspection` no incluye patrones soportados en su
árbol.

## Acciones visibles, no ejecutadas

| Control | Acción aparente por etiqueta | Resultado observado |
|---|---|---|
| CTL-004 | Cancelar | No ejecutada; efecto desconocido. |
| CTL-005 | Ingresar al Sistema | No ejecutada; efecto desconocido. |
| CTL-018 | Cerrar | No ejecutada. |
| CTL-003 | Mostrar ojo | No ejecutada; efecto desconocido. |
| CTL-006 | Recordar usuario | No ejecutada; efecto desconocido. |

## Datos y persistencia

- No se leyeron los valores de `Edit` ni el contenido de campos de entrada.
- No se probó ni se infiere persistencia.
- No se estableció conexión a SQL Server.

## Réplica funcional WinUI 3

- Proyecto: [`src/Master.WinUI/Master.WinUI.csproj`](../../src/Master.WinUI/Master.WinUI.csproj), .NET 8 y Windows App SDK 1.7.
- Interfaz y comportamiento: [`MainWindow.xaml`](../../src/Master.WinUI/MainWindow.xaml) y [`MainWindow.xaml.cs`](../../src/Master.WinUI/MainWindow.xaml.cs).
- Se conservan las etiquetas, los Automation IDs y los controles principales. El usuario y el PIN usan `TextBox`; la contraseña alterna entre `PasswordBox` y texto visible, sincronizando su contenido en memoria.
- El formulario usa columnas flexibles, un ancho máximo, controles que se estiran y `ScrollViewer` vertical. La inspección confirma que los controles caben horizontalmente a 360 DIP, el panel queda centrado en una ventana ancha y el desplazamiento permite alcanzar ambos botones.
- `Cancelar` es una adaptación local: borra contraseña y PIN y, si `Recordar usuario` está marcado, conserva el usuario solo en la ventana actual; si está desmarcado, borra también el usuario. No se escribe información a disco.
- `Ingresar al Sistema` muestra un aviso explícito de que no hay servicio de autenticación y que no se validaron ni enviaron datos. No se inventaron autenticación ni reglas de validación.
- Evidencia UIA de origen reutilizada: [`evidence/ui-inspection/window-525482-20261014-094639.json`](../../evidence/ui-inspection/window-525482-20261014-094639.json), JSON válido, 18 nodos, sin truncamiento. La ventana original no figuraba en las ventanas visibles durante esta ejecución y su PID anterior ya no estaba activo; no se inició ni se manipuló. La evidencia corresponde a la inspección anterior y no a una nueva captura del origen.
- Evidencia WinUI ejecutada después de la compilación final: [`evidence/ui-inspection/window-2950106-20261014-125904.json`](../../evidence/ui-inspection/window-2950106-20261014-125904.json), JSON válido, 15 nodos, sin truncamiento; [`evidence/ui/SCR-001-login-winui-final-build.png`](../../evidence/ui/SCR-001-login-winui-final-build.png), captura real en estado inicial, `630×900` píxeles.
- Compilación: correcta con Visual Studio MSBuild 18, `x64/Debug`, sin errores.

## Pruebas de la réplica

| Prueba | Estado | Resultado observado |
|---|---|---|
| Abrir la ventana WinUI generada | Correcta | Proceso activo y ventana respondiendo. |
| Escribir y editar usuario y PIN de prueba | Correcta | Los campos conservaron los cambios. |
| Mostrar, ocultar y volver a mostrar la contraseña de prueba | Correcta | Se conservó al alternar; el contenido no se incluyó en logs ni evidencias. |
| Activar y desactivar `Recordar usuario` | Correcta | El estado del checkbox cambió. |
| Ingresar | Correcta (aviso local) | Se mostró que no existe backend; no se informó autenticación exitosa ni se enviaron datos. |
| Cancelar con `Recordar usuario` marcado | Correcta | Se borraron contraseña y PIN, y se conservó el usuario en la ventana. |
| Cancelar con `Recordar usuario` desmarcado | Correcta | Se borraron los campos, incluido el usuario. |
| Redimensionar estrecho y ancho | Correcta | Sin desbordamiento horizontal; a tamaño estrecho se accedió a las acciones mediante desplazamiento; el panel quedó centrado en ancho grande. |
| Autenticación real / integración con backend | Bloqueada | No hay servicio ni configuración disponibles; no se intentó conectar a sistemas externos. |
| Comportamiento de la ventana original | No ejecutada | No se interactuó con la aplicación WinForms ni se conocían sus manejadores. |

## Limitaciones

- No se pudo obtener una inspección en vivo nueva del origen porque su ventana no estaba abierta. Los resultados y la réplica se basan en la evidencia UIA previamente guardada.
- El árbol UIA no contiene la imagen original del logotipo ni colores de píxel; `WCS` continúa como marca tipográfica aproximada.
- La autenticación, la persistencia tras cerrar la ventana y las reglas de validación del PIN siguen sin determinarse. La selección de recordar usuario solo afecta el borrado local en la ventana actual.
