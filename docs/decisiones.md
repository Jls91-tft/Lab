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
| Paquete de red | Propuesta en curso: FishNet + Steamworks.NET (ver conversación del 15/10/2026) |
| Formato de salida de osm2tiles | Propuesta en curso |
| Tamaño de tile y metros por tile | 16 o 32 px; propuesta de 1 tile = 1 bloque = 1 m de juego |
| Resolución base | p. ej. 640×360 con escalado entero (Pixel Perfect Camera) |
| Direcciones de animación | 4 u 8 |
| Paleta | ~32 colores cerrada |
| Estructura de carpetas y asmdefs de Unity | Se propondrá con el proyecto creado |
| Versión exacta de Unity | Propuesta en `docs/setup-unity.md` |
| Horas por semana | Necesario para calibrar `docs/plan.md` |
| Nombre definitivo | Comprobar marca, Steam y redes |

---

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
