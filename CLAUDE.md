# CLAUDE.md — Cepa Cero

Memoria del proyecto para Claude Code. Concisa: el detalle está en `docs/`.

## El juego en 5 líneas
Supervivencia zombi **cooperativa (1–4)**, vista **cenital**, **pixel art**.
Saquear de día, fortificar de noche; cada noche la horda crece.
Construcción por **bloques en cuadrícula** que los zombis pueden destruir.
Zombis mutados (comunes, especiales y minijefes) con lore que se descubre jugando.
Mapas de **ciudades reales** desde OpenStreetMap, empezando por **Zaragoza**.

## Pilares de diseño
1. **Cooperar o morir**: los especiales obligan a repartirse los papeles.
2. **Tu fuerte, tu ciudad**: construir y romper sobre calles reconocibles.
3. **La noche da miedo**: oscuridad casi total, luz de linternas y fuego, el verde de los mutados.
4. **Accesible pero profundo**: un "Zomboid accesible" con partidas configurables.

## Decisiones técnicas
**Tomadas** (motivos en `docs/decisiones.md`):
- Unity 6 + C#, plantilla 2D URP. Proyecto Unity en la subcarpeta `CepaCero/`.
- Cooperativo anfitrión + Steam P2P, sin servidores propios. **Autoridad del anfitrión.**
- Git + Git LFS para binarios. Textos fuera del código (ES + EN).
- Mapas: OSM → `tools/osm2tiles/` (Python) → datos que lee Unity.

**Pendientes**: paquete de red · formato de salida de osm2tiles · tamaño de tile
(16/32 px) y metros por tile · resolución base · 4 u 8 direcciones · paleta ·
estructura de carpetas y asmdefs de Unity · versión exacta de Unity (propuesta en
`docs/setup-unity.md`) · horas/semana disponibles · nombre definitivo.

## Reglas de arquitectura (red desde el día 1)
- El **anfitrión es la única fuente de verdad**: los clientes envían *intenciones*
  (mover, colocar bloque, atacar) y el anfitrión valida y aplica.
- Nada de estado de juego en `MonoBehaviour` de presentación: separar
  **simulación** (datos + reglas, testeable sin escena) de **vista** (sprites, sonido, UI).
- Zombis: simulación en el anfitrión y sincronización compacta por lotes, no un
  `NetworkTransform` por zombi. Pathfinding con *flow fields* compartidos.
- Mundo de bloques en **chunks**; cambios como deltas; *snapshot* de chunk al unirse alguien.
- Datos de diseño (zombis, bloques, objetos) en `ScriptableObject`, generados desde `docs/` cuando proceda.
- Entrada abstraída (Input System) para teclado, mando y, en el futuro, táctil.

## Convenciones C#
- Identificadores y comentarios de código en **inglés**; documentación en **español**.
- Namespace `CepaCero.<Área>` (`Core`, `Net`, `World`, `AI`, `Player`, `UI`). Una clase por archivo; archivo = clase.
- `PascalCase` para tipos, métodos, propiedades y constantes; `camelCase` para locales y parámetros;
  campos privados `_camelCase`; interfaces `IThing`; booleanos `isX`/`hasX`/`canX`.
- Campos serializados: `[SerializeField] private`, nunca `public` para exponer en el inspector.
- Sin `Find*`, `GetComponent` ni `new` de objetos en `Update`; cachear y usar *pooling*.
- Llaves de estilo Allman y 4 espacios (estilo por defecto de .NET).
- Assets: `PascalCase` con prefijo de tipo: `SPR_`, `TS_` (tileset), `SFX_`, `MUS_`, `MAT_`,
  `PF_` (prefab), `SO_` (ScriptableObject), `SCN_` (escena). Ej.: `SPR_Zombie_Walker_Idle`.
- No nombrar carpetas `Build/`, `Builds/`, `Temp/`, `Logs/` ni `Obj/` dentro del proyecto: el `.gitignore` las ignora en cualquier nivel.

## Cómo trabajamos
- **Javier** prueba, decide y juega. **Claude** programa, diseña la arquitectura y documenta.
- Tareas pequeñas y concretas. **Un commit por bloque terminado**, con mensaje claro.
- Tras cada paso, Claude dice **exactamente qué probar** en el editor. Javier pasa los
  errores de la consola (o Claude los lee por el MCP de Unity si está activo).
- **Antes de decisiones de arquitectura o cambios grandes: proponer y esperar OK.**
- Sin dependencias, assets ni servicios de pago o externos sin preguntar.
- Cada decisión importante: actualizar este fichero y `docs/decisiones.md`.

## docs/
| Fichero | Para qué |
| --- | --- |
| `docs/cepa-cero-contexto-proyecto.md` | Traspaso original: mercado, posicionamiento, arte, Steam |
| `docs/GDD.md` | Diseño del juego en una página |
| `docs/decisiones.md` | Registro de decisiones (fecha, decisión, motivo) |
| `docs/bestiario.md` | Zombis con estadísticas (se convertirá en datos) |
| `docs/lore.md` | La cepa, cómo se descubre y el diario |
| `docs/plan.md` | Hitos semanales del prototipo |
| `docs/setup-unity.md` | Instalación de Unity y del MCP |
