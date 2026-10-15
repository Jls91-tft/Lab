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
| Carpetas de `Assets/` y asmdefs de Unity | Propuesta en `docs/arquitectura.md`; se cierra con el proyecto creado |
| Versión exacta de Unity | Propuesta en `docs/setup-unity.md` |
| Hambre y sed | Sin decidir: de momento la comida solo cura un poco |
| Grupo entero caído a la vez | Aplicado: reaparecen al amanecer; `PermaDeath` como opción. Confirmar |
| Nombre definitivo | Comprobar marca, Steam y redes |

---

## D-013 · 15/10/2026 · Guardado sin zombis y zombis que no saben dónde estás
- **Estado:** Aceptada
- **Decisión:** las partidas guardadas no incluyen los zombis vivos; cargar de noche reinicia la horda de esa noche. Los zombis colocados de día empiezan sin saber dónde están los jugadores (vista y oído); la horda nocturna sí lo sabe.
- **Motivo:** guardados pequeños y robustos; el sigilo de día da sentido al ruido y a la luz (pilares 3 y 4).

## D-012 · 15/10/2026 · Objetos, inventario, botín y crafteo
- **Estado:** Aceptada (las 5 recomendaciones de `docs/propuesta-inventario.md`)
- **Decisión:** inventario por **huecos** (20 + 5 de acceso rápido; lo grande ocupa más); el botín **no reaparece** por defecto (opción de partida); **fuego amigo desactivado** por defecto (opción); al caer, un compañero puede **levantarte** (30 s para morir desangrado); al morir, la mochila se queda en el suelo y se reaparece al amanecer en el fuerte; **pistola** con poca munición en el MVP. Objetos, recetas y tablas de botín son datos en `docs/` (como el bestiario).
- **Motivo:** accesible pero con decisiones (pilar 4); la escasez y el ruido del arma de fuego dan tensión; levantar al compañero refuerza el cooperativo (pilar 1).
- **Añadido por Claude, pendiente de confirmar:** si cae todo el grupo a la vez, todos reaparecen al amanecer sin mochila; la opción `PermaDeath` hace que la partida termine.

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
