# simbench

Prueba de estrés **sin Unity** del núcleo de simulación (`packages/com.cepacero.core`).
Adelanta el hito 7 de `docs/plan.md`: N zombis asedian a los jugadores, que están dentro
de un fuerte de madera en el centro del mapa. Ejecuta una **partida completa** (`GameSession`
con objetos y diario) y el **protocolo real** (`HostBroadcaster`): mide lo que cuesta en el
anfitrión y los bytes exactos que recibe cada cliente.

```bash
# Desde la raíz del repo (necesita .NET 8 SDK)
dotnet run -c Release --project tools/simbench -- --zombies 100
dotnet run -c Release --project tools/simbench -- --zombies 400 --players 4
dotnet run -c Release --project tools/simbench -- --map tools/osm2tiles/out/delicias-test.map.json
dotnet run -c Release --project tools/simbench -- --help
```

| Opción | Por defecto | |
| --- | --- | --- |
| `--map` | ciudad sintética | Un `*.map.json` de osm2tiles |
| `--size` | `375x335` | Tamaño de la ciudad sintética (≈ Delicias a 2 m/tile) |
| `--zombies` | 100 | |
| `--players` | 2 | 1–4; el jugador 0 es el anfitrión |
| `--seconds` | 60 | A 20 ticks/s |
| `--json` | — | Guarda el informe (útil en CI) |

**Qué mide**
- *Host tick*: la partida entera (jugadores, zombis, flow field, objetos) + codificar los mensajes de todos los clientes, en ms (media, p95, p99, máx.). El presupuesto a 20 Hz es de 50 ms; el objetivo es que el p95 no pase del 25 %.
- *Network per client*: todos los mensajes reales (estados de jugadores, snapshots de zombis a 10 Hz y solo a menos de 40 tiles, deltas de bloques, eventos, reloj, inventario). Objetivo: < 64 KB/s. También muestra el tick más pesado y lo que ocupa unirse a mitad de partida.
- Sale con código 0 si se cumplen los objetivos, y con 1 si no.

**Limitaciones:** los jugadores no se mueven ni disparan, y no hay especiales con habilidades.
Los tiempos son de .NET 8 (JIT); en Unity con IL2CPP deberían ser parecidos y con Mono (en el editor), más lentos.

## Resultados de referencia (15/10/2026, contenedor de desarrollo, ciudad sintética 375×335)
| Escenario | media | p95 | p99 | máx. | red/cliente | unirse |
| --- | --- | --- | --- | --- | --- | --- |
| 100 zombis, 2 jugadores | 0,28 ms | 0,45 ms | 1,31 ms | 6,5 ms | 8,7 KB/s | 147 KB |
| 400 zombis, 4 jugadores | 0,73 ms | 1,94 ms | 3,16 ms | 11,5 ms | 21,6 KB/s (tope de 255 zombis por snapshot) | 147 KB |

Lo que se aprendió al hacerla:
1. El flow field de mapa completo (~50 ms) producía tirones: ahora la búsqueda está acotada a ~80 tiles y calcula costes de forma perezosa (~1 ms).
2. Cada golpe a un muro forzaba un recálculo: ahora solo cuentan los cambios de tipo de bloque (`StructureRevision`).
