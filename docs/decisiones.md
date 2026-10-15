# Registro de decisiones

Formato: una entrada por decisión, la más reciente arriba. Si una decisión se
revoca, no se borra: se añade una nueva entrada que la sustituye y se marca la
antigua como **Sustituida por D-XXX**.

| Estado | Significado |
| --- | --- |
| Aceptada | Vigente |
| Propuesta | Pendiente de OK de Javier |
| Sustituida | Ya no vigente (ver la que la reemplaza) |

---

## Pendientes de decidir
| Tema | Notas |
| --- | --- |
| Tamaño de tile en píxeles | 16 o 32 px; se cierra en la semana 1. Los metros por tile ya están decididos (D-008) |
| Resolución base | p. ej. 640×360 con escalado entero (Pixel Perfect Camera) |
| Direcciones de animación | 4 u 8 |
| Paleta | ~32 colores cerrada |
| ~~Versión exacta de Unity~~ | Cerrado: 6000.3.26f1 (D-016) |
| Nombre definitivo | Comprobar marca, Steam y redes |

---

## D-017 · 15/10/2026 · Paquetes de Unity alineados con el editor 6000.3.26f1
- **Estado:** Aceptada (OK de Javier el 15/10/2026: "sube collab-proxy e Input System a versiones compatibles").
- **Decisión:** `com.unity.collab-proxy` pasa de 2.6.0 a **2.13.6** y `com.unity.inputsystem` de 1.12.0 a **1.20.0**, las versiones que el propio editor trae embebidas (`Editor\Data\Resources\PackageManager\Editor\`). Son las que la plantilla Universal 2D debía usar, pero con las anteriores el compilador de Unity daba errores (`ObjectInfo` ambiguo, `BuildTarget.ReservedCFE`).
- **Motivo:** el editor compila sin errores con las versiones que ya conoce; no depender de descargas externas ni de versiones que no se han probado con 6000.3.26f1.
- **Cómo revisar:** al subir de versión de editor (D-016) o de LTS, comparar de nuevo con los `.tgz` embebidos.

## D-016 · 15/10/2026 · Versión de Unity: 6000.3.26f1 (LTS)
- **Estado:** Aceptada (OK de Javier el 15/10/2026: "instala la última 6000.3.x LTS").
- **Decisión:** motor **Unity 6000.3.26f1** (última 6000.3.x LTS que ofrece el Hub), con IL2CPP y Documentation. Proyecto `CepaCero/` creado a partir de la plantilla Universal 2D del propio editor.
- **Motivo:** LTS con soporte hasta diciembre de 2027 (ver §1 de `docs/setup-unity.md`). 6.6 es más nueva pero no LTS. 6.0 LTS terminaba su soporte en octubre de 2026.
- **Coste:** Unity Hub intentó descargar por su cuenta 6000.6.6f1, que no es la versión del proyecto. No afecta al repo; hay que confirmar que la descarga quedó cancelada en el Hub (pestaña Downloads).

## D-015 · 15/10/2026 · Hambre y sed duras, configurables
- **Estado:** Aceptada (OK de Javier el 15/10/2026: opción C y ajuste de partida).
- **Decisión:** cada jugador tiene barras de **hambre y sed** (0-100). Ajuste de partida `Survival = Off | Soft | Hard`, **Hard por defecto**.
  - Ritmo: la comida dura **3 días** de juego y el agua **2** (con días de 15 min, unos 45 y 30 min reales). Lata y botella rellenan 40.
  - Barras vacías (Soft y Hard): sin comida la vida máxima baja al 75 %; sin agua la stamina se recupera a la mitad.
  - Solo Hard: sin comida pierdes 1 de vida cada 10 s y sin agua 1 cada 5 s, hasta caer (y solo, morir).
  - Caídos y muertos no gastan barras; al reaparecer vuelven llenas. Avisos al 25 % y al vaciarse.
  - Red: el anfitrión las calcula; su dueño las recibe en el mensaje `Vitals` (2 bytes, solo cuando cambia un 1 %). Protocolo v2, guardado v4 (lee v3 con las barras llenas).
- **Motivo:** dar peso al saqueo de día y a las tablas de botín de comida; el ajuste mantiene la partida accesible (pilar 4) para quien no lo quiera.
- **Alternativas:** A (sin barras) y B (castigan sin matar) quedan como `Off` y `Soft`. Propuesta completa en `docs/propuesta-hambre-sed.md`.

## D-014 · 15/10/2026 · Código de Unity en un paquete local (`com.cepacero.unity`)
- **Estado:** Aceptada (OK de Javier el 15/10/2026).
- **Decisión:** el código que usa Unity (vistas, entrada, después FishNet) vive en `packages/com.cepacero.unity`, junto al núcleo; `CepaCero/Assets/_Project/` queda para contenido (escenas, prefabs, sprites, audio, ScriptableObjects). Incluye ya un **prototipo de depuración** (`PrototypeGame`) que juega la simulación con cuadrados.
- **Motivo:** Unity Hub no crea proyectos en carpetas con archivos, así que el código no podía esperar dentro de `CepaCero/`; además separa código revisable de assets binarios y permite un asmdef claro (`CepaCero.Unity` → `CepaCero.Core`, `Unity.InputSystem`).
- **Alternativa:** mover los scripts a `Assets/_Project/Scripts/` con sus asmdefs (es solo mover archivos).

## D-013 · 15/10/2026 · Guardado sin zombis y zombis que no saben dónde estás
- **Estado:** Aceptada
- **Decisión:** las partidas guardadas no incluyen los zombis vivos; cargar de noche reinicia la horda de esa noche. Los zombis colocados de día empiezan sin saber dónde están los jugadores (vista y oído); la horda nocturna sí lo sabe.
- **Motivo:** guardados pequeños y robustos; el sigilo de día da sentido al ruido y a la luz (pilares 3 y 4).

## D-012 · 15/10/2026 · Objetos, inventario, botín y crafteo
- **Estado:** Aceptada (las 5 recomendaciones de `docs/propuesta-inventario.md`)
- **Decisión:** inventario por **huecos** (20 + 5 de acceso rápido; lo grande ocupa más); el botín **no reaparece** por defecto (opción de partida); **fuego amigo desactivado** por defecto (opción); al caer, un compañero puede **levantarte** (30 s para morir desangrado); al morir, la mochila se queda en el suelo y se reaparece al amanecer en el fuerte; **pistola** con poca munición en el MVP. Objetos, recetas y tablas de botín son datos en `docs/` (como el bestiario).
- **Motivo:** accesible pero con decisiones (pilar 4); la escasez y el ruido del arma de fuego dan tensión; levantar al compañero refuerza el cooperativo (pilar 1).
- **Añadido por Claude, confirmado por Javier el 15/10/2026:** (1) si cae todo el grupo a la vez, todos reaparecen al amanecer sin mochila; la opción `PermaDeath` hace que la partida termine. (2) **Recolección:** romper bloques da materiales (árbol → 4 de madera, muro de madera → 2, muro de metal → 2 de chatarra…, `Items/BlockDrops.cs`); lo que rompen los zombis no da nada. (3) Analizar una muestra (usarla) la consume y desbloquea sus recetas para el grupo. (4) Levantar a alguien: mantener la acción 3 s a menos de 1,5 tiles; vuelve con 30 de vida.

## D-011 · 15/10/2026 · CI con GitHub Actions
- **Estado:** Aceptada
- **Decisión:** `.github/workflows/checks.yml` ejecuta `scripts/check.sh` (tests de Python y del núcleo C#, datos generados al día) y simbench en cada push y PR, sin licencia de Unity. El informe de simbench se guarda como artefacto.
- **Motivo:** detectar regresiones (incluidas las de rendimiento) sin abrir Unity. Coste: minutos de GitHub Actions dentro del plan gratuito; checkout sin LFS para no gastar ancho de banda.
- **Más adelante:** builds de Unity (GameCI) y subida a Steam (SteamPipe) cuando haya proyecto; requieren licencia de Unity como secreto del repo y se decidirán aparte.

## D-010 · 15/10/2026 · Simulación sin motor en un paquete UPM local
- **Estado:** Aceptada (OK de Javier el 15/10/2026).
- **Decisión:** la simulación (mundo de bloques, mapa, flow fields, zombis, reglas) vive en `packages/com.cepacero.core/` como C# sin `UnityEngine` (`noEngineReferences`), C# 9 / .NET Standard 2.1. Unity la referencia con `file:` en `Packages/manifest.json`. Se testea con `dotnet test` (`tests/`) y con el Test Runner de Unity. Unity solo hace vista, entrada y transporte de red. Detalle en `docs/arquitectura.md`.
- **Motivo:** el estado del juego queda en datos con colas de cambios, que es justo lo que viaja por la red. Tests rápidos en local y en CI sin licencia de Unity. Rendimiento con arrays para cientos de zombis. Independiente de FishNet y de la plataforma.
- **Coste:** una capa de adaptadores en Unity (Tilemap ↔ BlockWorld, mensajes FishNet ↔ bytes del núcleo) y tipos propios (`Int2`, `Float2`) en lugar de `Vector2`.

## D-009 · 15/10/2026 · Dedicación: sin límite fijo de horas
- **Estado:** Aceptada
- **Decisión:** Javier dedica "las horas que hagan falta". El plan mantiene hitos semanales; si un hito acaba antes, se adelanta el siguiente, nunca se amplía el alcance del MVP.
- **Motivo:** el riesgo pasa de "falta de tiempo" a "crecimiento del alcance" y agotamiento, compatibilizando con el trabajo. Se revisa el ritmo real al terminar la semana 2.

## D-008 · 15/10/2026 · Formato de salida de osm2tiles (`cepacero.map` v1)
- **Estado:** Aceptada
- **Decisión:** JSON con cabecera (bbox, fecha de los datos OSM, atribución, `meters_per_tile`, tamaño), leyenda, dos capas planas fila a fila con la fila 0 al sur (`ground` = suelo, `blocks` = bloques del mundo) y una lista `buildings` con la categoría de cada edificio (sin nombres ni marcas). PNG de previsualización aparte. Por defecto, **2 m por tile**, configurable. Leyendas como listas (índice = id) y puertas como array plano, para que Unity lo lea con `JsonUtility` sin paquetes extra. Especificación en `tools/osm2tiles/README.md`.
- **Motivo:** las dos capas encajan con el modelo del juego (Tilemap visual + rejilla de bloques rompibles). JSON se lee en Unity sin dependencias. La fila 0 al sur coincide con el eje Y de Unity. Las categorías alimentan el botín.
- **Licencia:** el JSON es una base de datos derivada de OSM: se distribuye bajo **ODbL 1.0** con atribución "© OpenStreetMap contributors".

## D-007 · 15/10/2026 · Red: FishNet + FishySteamworks sobre Steamworks.NET
- **Estado:** Aceptada
- **Decisión:** librería de red **FishNet** (versión gratuita, no Pro). Transporte Steam **FishySteamworks** con **Steamworks.NET** (MIT). Para desarrollo local, el transporte **Tugboat** de FishNet (dos instancias en el mismo PC). AppID de pruebas 480 hasta tener AppID propio.
- **Motivo:** predicción en el cliente integrada (movimiento inmediato con latencia), pool de objetos, visibilidad por zonas y serialización compacta: encajan con 100+ zombis y un mundo por chunks. Licencia gratuita y sin royalties para juegos (revisada el 15/10/2026). Steamworks.NET sigue la API oficial de Valve.
- **Alternativas descartadas:** Netcode for GameObjects (mejor documentación, pero sin predicción integrada y más pesado por entidad); Mirror (maduro, pero con la predicción experimental). Facepunch.Steamworks (API cómoda pero menos mantenido).
- **Riesgos:** documentación más escasa (soporte por Discord) y dependencia de un autor principal. Mitigación: la arquitectura (autoridad del anfitrión, zombis por lotes, chunks con deltas) no depende de la librería, y el código de red se aísla en `CepaCero.Net`.

## D-006 · 15/10/2026 · Proyecto Unity en la subcarpeta `CepaCero/`
- **Estado:** Aceptada (convención de repo; reversible)
- **Decisión:** el repositorio contiene `CepaCero/` (proyecto Unity), `tools/` y `docs/` al mismo nivel.
- **Motivo:** separa el proyecto Unity de las herramientas Python y la documentación; Unity no
  importa nada fuera de su carpeta. El `.gitignore` usa patrones no anclados para que funcione en la subcarpeta.

## D-005 · 15/10/2026 · Convenciones de código: identificadores en inglés, docs en español
- **Estado:** Aceptada (reversible)
- **Decisión:** código y comentarios en inglés; documentación en español; textos del juego en tablas de localización (ES/EN).
- **Motivo:** las API de Unity y la documentación técnica están en inglés; evita mezclar idiomas en el código.

## D-004 · 15/10/2026 · Git + Git LFS para binarios
- **Estado:** Aceptada
- **Decisión:** imágenes, audio, vídeo, fuentes y binarios van por LFS (`.gitattributes`). Las escenas y prefabs de Unity como texto con UnityYAMLMerge.
- **Motivo:** los binarios inflan el historial de Git. Ojo: GitHub incluye 1 GB de almacenamiento LFS gratis; vigilarlo al llegar el arte.

## D-003 · 15/10/2026 · Cooperativo con anfitrión y Steam P2P; autoridad del anfitrión
- **Estado:** Aceptada (heredada del documento de contexto)
- **Decisión:** 1–4 jugadores; un jugador hace de anfitrión; conexión por Steam P2P/relay; sin servidores propios. El anfitrión es autoritativo.
- **Motivo:** coste cero de infraestructura y es lo que esperan los jugadores del género. La autoridad del anfitrión simplifica la sincronización de zombis y del mundo.

## D-002 · 15/10/2026 · Unity 6 + C# con 2D URP
- **Estado:** Aceptada (heredada del documento de contexto)
- **Decisión:** motor Unity 6, lenguaje C#, plantilla Universal 2D (URP).
- **Motivo:** ecosistema maduro para 2D con Tilemap y luces 2D (clave para la noche), varias librerías de red con transporte Steam y un camino conocido a móvil y consola.

## D-001 · 15/10/2026 · Concepto y alcance del MVP
- **Estado:** Aceptada (heredada del documento de contexto)
- **Decisión:** supervivencia zombi cooperativa cenital en pixel art, con bloques y mapas OSM. MVP: 2 especiales + 1 minijefe, 2 jugadores por Steam, 100 zombis en red y un barrio de Zaragoza.
- **Motivo:** hueco de mercado entre Project Zomboid y Core Keeper (ver contexto, §2).
