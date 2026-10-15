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
- Red: **FishNet** (gratis, no Pro) + **FishySteamworks** / **Steamworks.NET**; Tugboat para pruebas locales.
- Mapas: OSM → `tools/osm2tiles/` (Python) → `cepacero.map` v1 (JSON, 2 m/tile por defecto) que lee Unity.

- **Simulación sin motor** en `packages/com.cepacero.core` (paquete UPM local); Unity solo vista,
  entrada y red (D-010, `docs/arquitectura.md`). CI en GitHub Actions con `scripts/check.sh` (D-011).

**Pendientes**: tamaño de tile en píxeles (16/32) · resolución base · 4 u 8 direcciones · paleta ·
carpetas de `Assets/` y asmdefs de Unity · versión exacta de Unity (propuesta en
`docs/setup-unity.md`) · nombre definitivo.

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

## Núcleo `packages/com.cepacero.core`
- Sin `UnityEngine` (`noEngineReferences`). **C# 9 / .NET Standard 2.1**: nada de `record`, `init`, `namespace X;` ni `global using`.
- Rutas calientes sin asignaciones por tick. Tiempo en ticks fijos. Aleatoriedad con `DeterministicRandom`.
- Toda mutación del mundo queda registrada (`DrainChanges`): es lo que viaja por la red.
- Redondeo de juego con `MidpointRounding.AwayFromZero` (`Math.Round` por defecto es bancario).
- Clases de datos JSON (`CepaMapData`, `ZombieDefinition`): campos `snake_case` para `JsonUtility`. Única excepción a PascalCase.
- No editar `data/generated/` a mano: se genera desde `docs/` con `tools/datagen`.

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

## Herramientas y comprobaciones
- **`scripts/check.sh`** (`--fast` sin estrés): todo lo que no necesita Unity. Pasarlo antes de cada commit.
- `tests/`: `dotnet test tests/CepaCero.Core.Tests/CepaCero.Core.Tests.csproj` compila el núcleo como lo haría Unity.
- `tools/osm2tiles/`: `python -m osm2tiles --zone delicias-test`. Formato y licencia ODbL en su `README.md`.
- `tools/datagen/`: `python -m datagen` regenera `data/generated/bestiary.json` desde `docs/bestiario.md`.
- `tools/simbench/`: `dotnet run -c Release --project tools/simbench -- --zombies 100`. Prueba de estrés sin Unity.

## docs/
| Fichero | Para qué |
| --- | --- |
| `docs/cepa-cero-contexto-proyecto.md` | Traspaso original: mercado, posicionamiento, arte, Steam |
| `docs/GDD.md` | Diseño del juego en una página |
| `docs/arquitectura.md` | Capas, estructura del repo y modelo de red |
| `docs/decisiones.md` | Registro de decisiones (fecha, decisión, motivo) |
| `docs/bestiario.md` | Zombis con estadísticas (se convertirá en datos) |
| `docs/lore.md` | La cepa, cómo se descubre y el diario |
| `docs/plan.md` | Hitos semanales del prototipo |
| `docs/setup-unity.md` | Instalación de Unity y del MCP |
