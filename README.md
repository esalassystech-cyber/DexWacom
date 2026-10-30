# Pizarra Workflows 0.2.2 · diagnóstico

Actualización local para Windows 11 x64. Conserva la conexión Bluetooth del controlador oficial Wacom y actualiza la instalación existente; no crea otra aplicación ni mueve la biblioteca de `Documentos\Wacom Workboard`.

## Entrada y herramientas

- Al abrir la app, WinTab intenta vincular el cursor y las coordenadas de tinta por separado a toda la ventana activa, incluida la barra de herramientas. Lee de vuelta los valores aceptados y actualiza la escala al mover o redimensionar la ventana.
- **Dibujar** inicia como modo normal. La punta dibuja tinta clara sobre el fondo azul y negra dentro de un post-it. Levantar la punta termina el trazo; el lienzo y las notas recortan la tinta a sus bordes.
- **Seleccionar** permite mover post-its, procesos y decisiones. **Conectar** enlaza dos bloques.
- Pulsa un botón lateral del lápiz para crear un post-it vacío bajo la punta. Cada pulsación crea uno. También puedes usar **＋ Nota** con el mouse.
- Los botones **Proceso**, **Decisión**, **Editar** y **Borrar** son las acciones explícitas. Ya no hay atajos de una sola letra que creen objetos mientras escribes.
- **Vincular Wacom** activa la asignación de ventana. **Esc** la suspende y libera el cursor; el mismo botón la reactiva. La pantalla completa es opcional.
- La entrada WPF y mouse se mantiene disponible aunque WinTab esté abierto; las fuentes se arbitran por trazo para evitar tinta duplicada. Si WinTab no entrega paquetes válidos, el estado sigue indicando que está esperando y conserva la entrada básica.
- **Diagnóstico** abre una zona vacía separada del tablero. Registra fuente, coordenadas, presión, botones y activación de ventana, sin guardar contenido de tus proyectos. El registro JSONL queda en `%LOCALAPPDATA%\PizarraWorkflows\diagnostics` y el botón **Abrir carpeta del registro** permite adjuntarlo para investigar.
- WinTab valida el identificador del contexto, descarta mensajes viejos/de otro contexto y consulta los metadatos del cursor para identificar el botón de presión. La conexión Bluetooth y el controlador oficial no se modifican.
- `Ctrl+Z` deshace, `Ctrl+Y` rehace y `Ctrl+S` guarda. Rueda para zoom; botón central o derecho para desplazarse.

Los tableros anteriores `.flowboard` se abren y migran al nuevo formato al guardarlos. La instalación mantiene el mismo `AppId`, la ruta `%LOCALAPPDATA%\Programs\Wacom Workboard` y los accesos directos. Los proyectos y notas permanecen en Documentos.

## Actualizar

Ejecuta `dist\Pizarra-Workflows-Diagnostics-0.2.2.exe` sobre la instalación actual; no la desinstales primero. El instalador usa el mismo `AppId`, carpeta e icono, y conserva los archivos `.flowboard` de Documentos. Es autocontenido, por usuario y no requiere cuenta ni elevación. No está firmado y puede mostrar un aviso de SmartScreen.

Para generar el actualizador se requiere .NET SDK 10 x64 e Inno Setup 6:

```powershell
./scripts/publish.ps1
```

## Icono

`assets\pizarra-workflows.svg` documenta el arte vectorial. `scripts\generate-icon.ps1` dibuja su equivalente con WPF y genera un `.ico` multirresolución integrado en la ventana, ejecutable, accesos directos e instalador.

## Verificación

```powershell
dotnet test PizarraWorkflows.sln -c Release
dotnet build PizarraWorkflows.sln -c Release
```

Las pruebas cubren el enrutamiento de mensajes WinTab, contextos equivocados, rangos de coordenadas independientes, entrada WPF/mouse con contexto abierto, selección, trazos separados y coordenadas dentro de notas. La CTL-4100WL no está conectada a este entorno. La integración Bluetooth solo se considerará aceptada tras revisar el registro y probarla físicamente en el PC donde está la tableta.
