# Arquitectura

*Versión 0.1 · 15/10/2026 · **Propuesta (D-010), pendiente de OK de Javier.***

## Idea en una frase
La **simulación** del juego (mundo, zombis, reglas) es C# puro y no depende de Unity. Unity
solo **dibuja, suena, lee la entrada y transporta los mensajes de red**. El anfitrión ejecuta la
simulación autoritativa; los clientes mantienen una réplica que se actualiza con deltas.

```
          ANFITRIÓN                                         CLIENTE
 ┌─────────────────────────────┐                ┌─────────────────────────────┐
 │ Unity: Input → intenciones  │                │ Unity: Input → intenciones ─┼──┐
 │            │                │   FishNet      │                             │  │
 │            ▼                │◄───────────────┼── RequestPlaceBlock(x,y,t) ◄┼──┘
 │ CepaCero.Core (autoritativo)│                │                             │
 │  BlockWorld · ZombieSim ·   │──deltas───────►│ CepaCero.Core (réplica)     │
 │  FlowField · DayNight ·     │  snapshots     │  BlockWorld · ZombieReplica │
 │  HordeDirector              │  zombis 10 Hz  │            │                │
 │            │                │                │            ▼                │
 │ Unity: vista (Tilemap,      │                │ Unity: vista (interpolación)│
 │  sprites, luces, audio)     │                │                             │
 └─────────────────────────────┘                └─────────────────────────────┘
```

## Por qué
1. **Red desde el día 1.** Si el estado vive en datos simples con colas de cambios, la red solo
   tiene que mandar esos cambios. No hay estado escondido en `MonoBehaviour`s.
2. **Tests sin abrir Unity.** La simulación se prueba con `dotnet test` en segundos, en local y en CI.
   Es lo más parecido a tu día a día como SRE.
3. **Rendimiento.** 100+ zombis como arrays (*struct of arrays*), no como 100 GameObjects con
   scripts. Unity solo pinta lo que se ve.
4. **Móvil y consola.** La simulación no cambia entre plataformas; solo cambian la vista y la entrada.
5. **Independiente de la librería de red.** Los mensajes se codifican a bytes en el núcleo.
   FishNet solo los transporta, y cambiar de librería no toca la lógica.

## Estructura del repositorio
```
CepaCero/                               Proyecto Unity (lo creas esta noche)
  Assets/_Project/                      Todo lo nuestro (el "_" lo deja arriba)
    Scripts/
      Bootstrap/                        Arranque, escenas, servicios
      Net/      CepaCero.Unity.Net      FishNet: NetworkManager, mensajes ↔ Core
      World/    CepaCero.Unity.World    Tilemap a partir de BlockWorld
      Player/   CepaCero.Unity.Player   Input System, cámara, vista del jugador
      AI/       CepaCero.Unity.AI       Vista de zombis (pool de sprites, interpolación)
      UI/       CepaCero.Unity.UI
    Data/                               ScriptableObjects (SO_*)
    Prefabs/ Scenes/ Sprites/ Tiles/ Audio/ Settings/
  Packages/manifest.json                → "com.cepacero.core": "file:../../packages/com.cepacero.core"

packages/com.cepacero.core/             Simulación sin motor (paquete UPM local)
  Runtime/  CepaCero.Core.asmdef        noEngineReferences = true
    Util/    Bytes, RNG determinista
    World/   Bloques, chunks, deltas, snapshots
    Map/     Lector de cepacero.map v1 → BlockWorld
    AI/      Flow fields, simulación de zombis, codec de red
    Rules/   Ciclo día/noche, director de hordas, opciones de partida
  Tests/    CepaCero.Core.Tests.asmdef  NUnit (corren en Unity Test Runner y en dotnet)

tests/CepaCero.Core.Tests/              Proyecto dotnet que compila Runtime + Tests (CI sin Unity)
tools/osm2tiles/                        OSM → cepacero.map
tools/datagen/                          docs/bestiario.md → data/generated/bestiary.json
tools/simbench/                         Banco de pruebas: N zombis sobre un mapa, ms/tick y bytes/s
data/generated/                         Datos generados desde docs/ (no editar a mano)
```

## Reglas del núcleo (`CepaCero.Core`)
- **C# 9 y .NET Standard 2.1**, lo que acepta Unity 6. Sin `record`, `init` ni `namespace X;`. El `.csproj` de tests lo impone.
- Cero referencias a `UnityEngine`. Tipos propios (`Int2`, `Float2`) en lugar de `Vector2`.
- **Sin asignaciones de memoria por tick** en las rutas calientes. Los buffers se crean una vez y se reutilizan.
- Toda mutación pasa por métodos que **registran el cambio** (`BlockWorld.DrainChanges`), y eso es lo que viaja por la red.
- Tiempo en **ticks fijos** (p. ej. 20 Hz para la lógica), no en `Time.deltaTime`.
- Aleatoriedad con semilla (`DeterministicRandom`): partidas reproducibles y tests estables.

## Modelo de red (con FishNet)
| Qué | Dirección | Cómo | Frecuencia |
| --- | --- | --- | --- |
| Movimiento del jugador | C → A | Predicción en el cliente de FishNet; el anfitrión corrige | Tick de red |
| Intenciones (colocar, romper, atacar, interactuar) | C → A | RPC al servidor con validación | Al ocurrir |
| Deltas de bloques | A → C | Lote de `BlockChange` (x, y, tipo, vida) | Cada tick con cambios |
| Snapshot de chunk | A → C | Bytes del chunk completo | Al unirse y al entrar en zona |
| Zombis | A → C | Lote cuantizado (8 B/zombi) solo de los cercanos | 10 Hz + interpolación |
| Reloj, noche, horda | A → C | Estado pequeño | 1 Hz y en cada cambio de fase |

## Lo que **no** se decide aquí
Tamaño de tile en píxeles, resolución, paleta y direcciones de animación (semana 1); el diseño del inventario y del crafteo (más adelante).
