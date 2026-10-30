# Pizarra Workflows para Windows — Diseño

## Intención

Una aplicación de escritorio en español, instalada solo para Eric en Windows 11 x64, para escribir ideas y diagramar workflows de sistemas con la Wacom Intuos CTL-4100WL por Bluetooth. La app organiza tableros editables por carpetas de proyecto y guarda todo en el PC.

## Objetivos

- Biblioteca local con carpetas y varios tableros por proyecto.
- Lienzo desplazable y ampliable con post-its de texto, tinta o ambos.
- Formas de proceso rectangulares, decisiones en rombo y flechas conectadas que se mantienen al mover nodos.
- Archivos portables `.flowboard`, auto-guardado y exportación PNG/PDF.
- Mantener el driver oficial de Wacom y no requerir nube, cuenta, publicación ni cambios globales al driver.
- Confinar el cursor dentro de la app en modo tablero. Escape, pérdida de foco, minimizar, cerrar o desconectar el lápiz liberan el cursor; el mouse queda confinado durante ese modo.
- Instalar por usuario sin privilegios de administrador.

## Enfoque aprobado

Proyecto independiente en `C:\Antigravity\wacom-workboard`, C# y WPF sobre .NET 10 x64. WinPenKit se fija al commit `e5a7f61b78d64d6a77249b27bd76975af4d6cb4c`. Se usará `WpfStylusSession` con el controlador oficial de Wacom. La pantalla de la sesión muestra un monitor de 3440 × 1440.

Al abrir un tablero, la app se presenta en una ventana sin bordes a pantalla completa en el monitor primario. Esto hace que el área visible completa corresponda al tablero y evita depender de una región de pantalla fija al mover una ventana. El área de tinta se deriva de las coordenadas de pluma en píxeles físicos y la escala DPI de WPF. El cursor de Windows se limita al área de cliente cuando una tableta Wacom está presente y la vista del tablero tiene foco.

WinPenKit `CaptureRegion.Window(hwnd)` filtra puntos a los límites de una ventana; no reescala el área física de la tableta. Por eso la vista de tablero ocupa el monitor completo y recibe eventos de pluma a través de WPF/WinPenKit. Si luego se confirma una configuración con varios monitores, se añade selección de monitor y conversión según los límites del monitor elegido.

## Interfaz y datos

- Inicio: biblioteca de proyectos y tableros, crear/renombrar carpetas y tableros.
- Tablero: lienzo con herramientas de selección, post-it, proceso, decisión, flecha y borrador; zoom con rueda y paneo con botón central o Space+arrastre.
- Post-it: título y texto editable con teclado, más tinta vectorial local al post-it; el usuario escoge modo texto o tinta para evitar trazos accidentales.
- Workflow: nodos con etiquetas y flechas de un nodo a otro; conectores se vuelven a dibujar desde los bordes al moverlos.
- Datos: un archivo JSON versionado `.flowboard` por tablero, almacenado en una carpeta de proyecto dentro de `Documentos\Wacom Workboard`; guardado atómico y auto-guardado. Exportación raster PNG y PDF.
- Sin sincronización, OCR, colaboración, formatos editables externos ni contenido generado por IA.

## Entrada y recuperación

WinPenKit entrega eventos WPF con posición de escritorio en píxeles físicos, presión y trazos agrupados. Un convertidor toma el origen y la escala del elemento WPF para llevar puntos a coordenadas locales; el transformador del lienzo aplica zoom/paneo. `ClipCursor` se administra en una clase acotada: solo se activa tras detectar el digitizador Wacom, se actualiza ante cambios de tamaño, y se libera al perder activación, pulsar Escape, minimizar, cerrar, desconectar la tableta o ante error. Sin lápiz detectado, la app opera con mouse sin confinar el cursor.

## Criterios de aceptación

- La biblioteca crea y vuelve a abrir carpetas y tableros sin perder nodos, texto, tinta, formas ni flechas.
- La tinta responde a posición y presión sobre un tablero real CTL-4100WL conectado por Bluetooth.
- El cursor queda dentro de la app en modo tablero y vuelve al estado libre al disparar cada condición de escape.
- Al mover nodos, las flechas conservan sus endpoints; zoom/paneo no cambia las coordenadas persistidas.
- `.flowboard` reabre editable; PNG y PDF muestran el tablero completo.
- El instalador `.exe` instala y desinstala en el perfil actual de Windows sin elevar privilegios.

## Limitación de la sesión de implementación

La CTL-4100WL y el controlador Wacom no aparecen conectados/instalados en el PC de desarrollo actual. La app se compilará y sus componentes se probarán con datos sintéticos; la aprobación del soporte Bluetooth real requiere la prueba manual con el dispositivo.
