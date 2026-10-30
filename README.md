<p align="center">
  <img src="https://img.shields.io/badge/version-0.3.0-blue?style=for-the-badge" alt="Versión">
  <img src="https://img.shields.io/badge/license-MIT-brightgreen?style=for-the-badge" alt="Licencia">
  <img src="https://img.shields.io/badge/estado-beta-orange?style=for-the-badge" alt="Estado">
  <img src="https://img.shields.io/badge/.NET-10-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET 10">
  <img src="https://img.shields.io/badge/WPF-Windows%2011%20x64-0078D4?style=for-the-badge&logo=windows&logoColor=white" alt="WPF · Windows 11">
  <img src="https://img.shields.io/badge/Wacom-WinTab-111111?style=for-the-badge" alt="Wacom WinTab">
  <img src="https://img.shields.io/badge/pruebas-61%20✓-brightgreen?style=for-the-badge" alt="Pruebas">
</p>

<h1 align="center">
  ✍️ DexWacom
</h1>

<h3 align="center">
  Pizarra de notas y diagramas de flujo para tabletas Wacom sin pantalla<br>
  <sub>🔥 Toda la superficie de la tableta corresponde a la ventana de la app: el lápiz nunca se sale</sub>
</h3>

<p align="center">
  <b>Creado por</b> <a href="https://dexapanama.com"><b>DEXA Panamá</b></a> · Eric Salas<br>
  <sub>🇵🇦 DEXA Panamá — Desarrollo a medida potenciado con IA</sub>
</p>

---

## 🤔 ¿Qué es DexWacom?

DexWacom es una aplicación de escritorio para Windows pensada para **escribir notas a mano y bocetear diagramas de flujo** con una tableta Wacom sin pantalla, como la **Wacom Intuos CTL-4100WL**. Funciona como una pizarra personal: organizas tus ideas en **proyectos** y **tableros**, escribes con el lápiz, armas diagramas con bloques y flechas, y todo se guarda solo en tu PC.

El problema que resuelve: con una tableta sin pantalla, el driver reparte la superficie de la tableta entre **todos** tus monitores. Si tienes la app en media pantalla o en otro monitor, cada movimiento del lápiz se dispara fuera de la ventana y la tinta no cae donde miras. DexWacom le pide al driver Wacom, mediante **WinTab**, que **toda la tableta corresponda solo a la ventana de la app**, la siga cuando la mueves o redimensionas y **retenga el cursor dentro** mientras el lápiz está cerca de la tableta.

Está hecha para uso diario real: monitores con escalas distintas (por ejemplo, un 4K al 150 % junto a un ultrawide al 100 %), ventana encajada a media pantalla, Bluetooth o cable, y sin ninguna ventana emergente que interrumpa el trazo.

| Métrica | Valor |
|:--------|:------|
| 🖊️ Área de la tableta usada por la app | **100 % → ventana de DexWacom** |
| 🖥️ Monitores con distinta escala | **Soportados (DPI PerMonitorV2)** |
| 🧪 Pruebas automáticas | **61 en verde** |
| 🔒 Conexiones de red | **Ninguna, 100 % local** |
| 🗑️ Eliminación de tableros | **A la Papelera de reciclaje** |

---

## ✨ Características

### 🖊️ Lápiz y tableta

| Característica | Estado |
|:---------------|:------:|
| 🎯 Toda la tableta mapeada a la ventana de la app (también a media pantalla) | ✅ |
| 🧲 El cursor no sale de la ventana mientras el lápiz está cerca de la tableta | ✅ |
| 🖥️ Monitores con distinta escala de Windows (4K al 150 % + otro al 100 %) | ✅ |
| 💧 Presión del lápiz en el grosor del trazo | ✅ |
| ✋ Moverte por el lienzo con el lápiz (herramienta Mano o Espacio mantenido) | ✅ |
| 🖱️ El ratón sigue funcionando para todo | ✅ |
| ⎋ `Esc` suelta el lápiz de la ventana; un botón naranja lo vuelve a vincular | ✅ |
| 📶 Funciona por Bluetooth o por cable con el driver oficial de Wacom | ✅ |

### 📊 Diagramas de flujo

| Bloque | Forma | Uso |
|:-------|:-----:|:----|
| ⬭ Inicio/Fin | Óvalo | Dónde empieza y termina el proceso |
| ▭ Proceso | Rectángulo | Un paso o actividad |
| ◇ Decisión | Rombo | Una pregunta con dos salidas (sí / no) |
| ▱ Entrada/Salida | Paralelogramo | Datos que entran o salen |
| 📝 Nota | Post-it | Escribir a mano dentro de la nota |
| → Flecha | Línea con punta | Une dos bloques; sale del borde de cada uno |

### 🗂️ Biblioteca y edición

- 📁 **Proyectos y tableros con nombre automático** ("Proyecto 1", "Tablero 1"…): se crean al instante, sin preguntar nada.
- ✏️ **Panel lateral** para renombrar o eliminar el proyecto o tablero seleccionado.
- 🗑️ **Eliminar pide un segundo toque** y manda a la **Papelera de reciclaje** de Windows, así que se puede recuperar.
- ↶ **Deshacer / Rehacer** con botones y con `Ctrl+Z` / `Ctrl+Y`.
- ⌫ **Goma**: borra los trazos que toca el lápiz, también dentro de las notas. Cada pasada es un solo paso de deshacer.
- 💾 **Guardado automático** con escritura atómica: un corte de luz no deja un tablero a medias.
- 🖼️ **Exportar** el tablero a **PNG** o **PDF**.
- 🔍 **Zoom y vista**: acercar, alejar y volver al inicio (⌂).
- 🩺 **Diagnóstico** integrado con registro detallado de la entrada del lápiz.

---

## 🧭 Cómo se usa

### 🧰 Barra de herramientas

| Grupo | Botón | Qué hace |
|:------|:------|:---------|
| ✏️ Lápiz | ✎ Dibujar | Escribe y dibuja con presión. Dentro de una nota, la tinta queda en la nota |
| ✏️ Lápiz | ⌫ Goma | Pasa el lápiz sobre un trazo para borrarlo |
| ✏️ Lápiz | ↖ Mover | Toca un bloque y arrástralo |
| ✏️ Lápiz | → Flecha | Toca el bloque de origen (queda resaltado) y luego el de destino |
| ✏️ Lápiz | ✋ Mano | Arrastra para subir, bajar o moverte por el lienzo |
| 🔍 Vista | − / ＋ | Alejar o acercar |
| 🔍 Vista | ⌂ Inicio | Volver al principio del tablero al 100 % |
| 📊 Diagrama | ⬭ ▭ ◇ ▱ ＋ Nota | Crea el bloque en el centro y pasa a ↖ Mover para colocarlo |
| ✂️ Editar | ↶ Deshacer · ↷ Rehacer | Deshace o rehace la última acción |
| ✂️ Editar | Aa Texto | Cambia el texto del bloque seleccionado (dentro de la misma ventana) |
| ✂️ Editar | 🗑 Quitar bloque | Quita el bloque seleccionado y sus flechas |
| ⚙️ Sistema | Vincular Wacom · Diagnóstico · ⛶ · PNG · PDF | Vinculación, pruebas, pantalla completa y exportación |

### ⌨️ Atajos

| Atajo | Acción |
|:------|:-------|
| `Ctrl + Z` / `Ctrl + Y` | ↶ Deshacer / ↷ Rehacer |
| `Ctrl + S` | 💾 Guardar ahora |
| `Supr` | 🗑️ Quitar el bloque seleccionado |
| `Espacio` (mantener) | ✋ El lápiz mueve el lienzo mientras la tecla está pulsada |
| Rueda del ratón | ↕️ Subir y bajar el lienzo |
| `Shift` + rueda | ↔️ Mover el lienzo a los lados |
| `Ctrl` + rueda | 🔍 Zoom |
| `Esc` | ⎋ Soltar el lápiz de la ventana (en un cuadro de texto, solo cancela lo escrito) |

> 💡 **Tip Wacom:** en Wacom Center puedes asignar `Espacio` a un ExpressKey de la tableta y `Ctrl+Z` a otro. Así te mueves por el lienzo y deshaces sin soltar el lápiz.

---

## ⚙️ Cómo funciona por dentro

Esta es la parte que hace que el lápiz "viva" dentro de la ventana. Todo se calculó y comprobó con registros reales de una CTL-4100WL.

1. 🎯 **Contexto WinTab de sistema** (`CXO_SYSTEM | CXO_MESSAGES`), creado a partir del contexto por defecto del driver (`WTI_DEFSYSCTX`). Su rectángulo de cursor (`lcSys*`) y su rectángulo de salida de paquetes (`lcOut*`) se ajustan al **área cliente de la ventana en píxeles físicos**. El driver mueve el cursor del sistema solo dentro de ese rectángulo.
2. 📐 **Área cliente real**: se obtiene con `GetClientRect` + `ClientToScreen`, sin la barra de título ni los bordes invisibles de Windows 11. Se recalcula al mover, redimensionar, maximizar, encajar o cambiar de monitor (`WM_MOVE`, `WM_SIZE`, `WM_DPICHANGED`).
3. 🖥️ **DPI por monitor (PerMonitorV2)** declarado en `app.manifest`. Sin esto, en un monitor con otra escala Windows virtualiza las coordenadas y la tinta cae desplazada.
4. 🔝 **`WTOverlap`** mantiene el contexto de la app encima, para que el driver aplique su rectángulo.
5. ↕️ **Eje Y comprobado en vivo**: el driver de la CTL-4100WL fija todos los paquetes en un borde si la extensión de salida es negativa. Por eso los paquetes se piden con extensión positiva y la app voltea la Y por su cuenta. Las primeras lecturas en el aire se comparan con el cursor del sistema para confirmar la orientación (`PacketYOrientation`).
6. 🎨 **Conversión exacta al lienzo**: de píxeles de escritorio a coordenadas del lienzo sin redondear a píxel entero (el mismo enfoque que `WpfCoordinates` de WinPenKit), para trazos suaves.
7. 🧲 **Encierro del cursor** con `ClipCursor` mientras el lápiz está en proximidad (`WT_PROXIMITY`). Se libera al alejar el lápiz, al cambiar de ventana, al minimizar o con `Esc`, así el ratón queda libre cuando no usas la tableta.
8. 🧵 **Una sola fuente de tinta**: en cuanto WinTab entrega paquetes, la entrada de Windows Ink y los clics de ratón que Windows genera desde el lápiz (firma `0xFF515700` en `GetMessageExtraInfo`) se ignoran para dibujar. Si otra fuente dejó un contacto "atascado", la punta de la tableta lo cancela y toma el control.
9. 🪟 **Sin ventanas emergentes**: crear, renombrar, eliminar y editar texto ocurren dentro de la ventana principal. Un diálogo aparte le quitaría la activación y soltaría el lápiz.

---

## 🚀 Inicio Rápido

### 1️⃣ Requisitos Previos

- 🪟 **Windows 11 x64**
- 🖊️ **Tableta Wacom** con el **driver oficial** instalado y corriendo (probado con **Intuos CTL-4100WL**)
- 🧰 **.NET SDK 10 x64** (solo para compilar)
- 📦 **Inno Setup 6** (solo para generar el instalador)

```bash
winget install Microsoft.DotNet.SDK.10
```

```bash
winget install JRSoftware.InnoSetup
```

### 2️⃣ Clonar

El repositorio incluye **WinPenKit** como submódulo de referencia, así que clona con `--recursive`:

```bash
git clone --recursive https://github.com/esalassystech-cyber/DexWacom.git
```

### 3️⃣ Compilar y probar

```bash
dotnet test PizarraWorkflows.sln -c Release
```

### 4️⃣ Generar el instalador

```bash
powershell -ExecutionPolicy Bypass -File scripts/publish.ps1
```

Genera `dist\Pizarra-Workflows-Diagnostics-0.3.0.exe`: autocontenido (no necesita .NET instalado), por usuario y sin permisos de administrador. Para actualizar, ejecútalo encima de la instalación existente; conserva tus proyectos.

> ⚠️ El instalador no está firmado, así que Windows SmartScreen puede mostrar un aviso la primera vez.

---

## 🗂️ Dónde guarda tus cosas

| Qué | Dónde |
|:----|:------|
| 📁 Proyectos y tableros | `Documentos\Wacom Workboard\<Proyecto>\<Tablero>.flowboard` |
| 🩺 Registros de diagnóstico | `%LOCALAPPDATA%\PizarraWorkflows\diagnostics\` |
| 💻 Programa instalado | `%LOCALAPPDATA%\Programs\Wacom Workboard\` (o la carpeta que elijas) |

- 📄 Cada tablero es un archivo **JSON** (`.flowboard`, esquema v2) con bloques, flechas, trazos libres y trazos dentro de las notas. Los tableros del esquema v1 se migran solos al guardarlos.
- 💾 El guardado escribe a un archivo temporal y lo reemplaza de forma atómica.

---

## 🩺 Diagnóstico

- 🧪 **Botón Diagnóstico**: abre una zona de prueba separada del tablero. Registra cada paquete de la tableta (posición, cursor del sistema, área de destino, escala DPI, presión, botones, orientación del eje Y) en `wacom-*.jsonl`.
- 📋 **Registro del tablero** (`tablero-*.jsonl`): cada punto que llega al lienzo con su fuente (WinTab, Windows Ink, ratón), su posición y el motivo si se rechaza. También anota cuándo se suspende o reanuda la vinculación.
- 🔐 Los registros **no contienen el contenido de tus tableros** y nunca salen de tu PC.

---

## 🛠️ Stack Tecnológico

| Tecnología | Propósito |
|:-----------|:----------|
| 🟣 .NET 10 / C# | Lenguaje y runtime |
| 🪟 WPF | Interfaz y dibujo del lienzo |
| 🖊️ WinTab (`Wintab32.dll`) | Datos del lápiz y mapeo tableta → ventana |
| 🧩 Win32 (`user32`, `shell32`) | Área cliente, `ClipCursor`, cursor y Papelera de reciclaje |
| 📄 System.Text.Json | Formato `.flowboard` |
| 🧪 xUnit | Pruebas automáticas |
| 📦 Inno Setup 6 | Instalador por usuario |
| 📚 WinPenKit (submódulo) | Referencia técnica de entrada de lápiz en Windows |

---

## 🏗️ Estructura del Proyecto

```text
DexWacom/
├── src/
│   ├── Pizarra.Core/              # 🧠 Lógica sin interfaz: documento, mapeo WinTab, goma, biblioteca
│   │   ├── BoardDocument.cs       #    Modelo del tablero (bloques, flechas, trazos)
│   │   ├── BoardRepository.cs     #    Guardado atómico, validación y migración
│   │   ├── BoardInkOperations.cs  #    Añadir trazos y goma
│   │   ├── BoardLibrary.cs        #    Nombres automáticos y rutas únicas
│   │   ├── WindowSystemMapping.cs #    Rectángulos WinTab
│   │   └── WinTabMessageRouter.cs #    Mensajes WinTab, mapeo y orientación del eje Y
│   └── Pizarra.App/               # 🪟 Aplicación WPF
│       ├── MainWindow.xaml(.cs)   #    Barra, biblioteca, panel lateral, editor en ventana
│       ├── BoardSurface.cs        #    Lienzo: dibujo, herramientas, vista, deshacer
│       ├── WintabWindowSession.cs #    Contexto WinTab ligado a la ventana
│       ├── NativeWindowBounds.cs  #    Área cliente y conversión en píxeles físicos
│       ├── PenMouseMessages.cs    #    Detecta clics de ratón generados por el lápiz
│       ├── RecycleBin.cs          #    Eliminar a la Papelera de reciclaje
│       ├── WacomDiagnosticsWindow.cs
│       └── app.manifest           #    DPI PerMonitorV2
├── tests/
│   ├── Pizarra.Tests/             # 🧪 Pruebas del núcleo
│   └── Pizarra.App.Tests/         # 🧪 Pruebas del lienzo y la ventana (WPF)
├── installer/PizarraWorkflows.iss # 📦 Script de Inno Setup
├── scripts/
│   ├── publish.ps1                # 🚀 Publica y genera el instalador
│   └── generate-icon.ps1          # 🎨 Genera el icono multirresolución
├── assets/                        # 🖼️ Icono (.svg y .ico)
├── docs/                          # 📝 Diseño y plan original
└── vendor/WinPenKit/              # 📚 Submódulo de referencia
```

> ℹ️ Internamente el código y el instalador todavía usan el nombre anterior del proyecto, **Pizarra Workflows**.

---

## 📋 Scripts / Referencia CLI

| Comando | Descripción |
|:--------|:------------|
| `dotnet build PizarraWorkflows.sln -c Release` | 🔨 Compila la solución |
| `dotnet test PizarraWorkflows.sln -c Release` | 🧪 Ejecuta las 61 pruebas |
| `scripts/publish.ps1` | 🚀 Publica la app autocontenida y genera el instalador con Inno Setup |
| `scripts/generate-icon.ps1` | 🎨 Regenera `assets/pizarra-workflows.ico` desde el diseño vectorial |

---

## 🔒 Seguridad y Privacidad

- ✅ **100 % local**: no hay conexiones de red, cuentas ni telemetría.
- ✅ **Sin credenciales** en el código ni en la configuración.
- ✅ **Eliminar va a la Papelera de reciclaje**, nunca borra de forma definitiva.
- ✅ **Guardado atómico**: un fallo a mitad de escritura no corrompe el tablero.
- ✅ **Validación al abrir**: tableros dañados o con datos fuera de rango se rechazan sin cerrar la app.
- ✅ **Driver intacto**: no modifica la configuración del driver Wacom ni la conexión Bluetooth; el mapeo existe solo mientras DexWacom está activa.

---

## 🧱 Limitaciones Conocidas

- 🖊️ Probado a fondo con la **Wacom Intuos CTL-4100WL**. Otros modelos Wacom deberían funcionar con el mismo driver; otras marcas pueden comportarse distinto.
- 💾 Exportar a PNG o PDF abre el diálogo de Windows para elegir dónde guardar; mientras está abierto, el lápiz queda libre.
- 🔘 Los botones laterales del lápiz los gestiona el driver (por defecto, clic derecho). Configúralos en Wacom Center.

---

## 🙏 Créditos

- 📚 [WinPenKit](https://github.com/TheSevenPens/WinPenKit) de TheSevenPens, por su documentación sobre WinTab, DPI y coordenadas de lápiz en Windows, que sirvió de referencia técnica.

---

## 📝 Historial de Cambios

| Versión | Cambios |
|:--------|:--------|
| 🟢 **0.3.0** | Diagramas de flujo (Inicio/Fin, Proceso, Decisión, Entrada/Salida, flechas con punta), goma, deshacer y rehacer con botones, mano y desplazamiento con rueda, zoom y ⌂ Inicio, biblioteca con nombres automáticos, panel para renombrar y eliminar (a la Papelera), sin ventanas emergentes, `Esc` seguro en cuadros de texto, eje Y comprobado en vivo, registro del tablero |
| 🔵 **0.2.3** | DPI PerMonitorV2, área cliente real en píxeles físicos, `WTOverlap`, una sola fuente de tinta, encierro del cursor con `ClipCursor`, diagnóstico con posición del cursor |
| ⚪ **0.2.2** | Primera versión con vinculación WinTab a la ventana y diagnóstico |

---

## 📄 Licencia

```text
MIT License — Copyright (c) 2026 DEXA Panamá (Eric Salas)
```

Ver [LICENSE](LICENSE) para el texto completo.

---

<p align="center">
  <b>🇵🇦 Hecho con ❤️ por DEXA Panamá</b><br>
  <sub><a href="https://dexapanama.com">DEXA Panamá</a> · <a href="mailto:eric.salas@dexapanama.com">eric.salas@dexapanama.com</a></sub>
</p>
