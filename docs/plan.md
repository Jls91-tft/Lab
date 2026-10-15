# Plan del prototipo

*Versión 0.1 · 15/10/2026.* Formas básicas (cuadrados, círculos, colores planos), sin arte final.

**Calendario:** dedicación sin límite fijo (D-009). Si un hito acaba antes, empieza el siguiente;
lo que **no** se hace es añadir alcance. Revisamos el ritmo real al terminar la semana 2.
**Red:** FishNet + FishySteamworks (D-007).

## Por qué este orden
La lista original ponía el cooperativo en 5.º lugar. Lo **adelanto a la semana 2** porque la
regla del proyecto es "todo el estado del juego pensado para la red": si construimos bloques y
zombis en local y después metemos la red, toca reescribirlos. Con la red montada pronto, cada
sistema nuevo nace con autoridad del anfitrión y se prueba con 2 jugadores desde el primer día.
En las semanas 2–5 se prueba con **dos instancias en el mismo PC** (transporte local); Steam
entra en la semana 7, cuando haya algo que estresar.

Cada hito termina con una **prueba de aceptación** que haces tú en el editor o en una build.

| Sem. | Hito | Prueba de aceptación (la haces tú) |
| --- | --- | --- |
| 0 | Setup | Unity abre el proyecto, `git status` limpio, MCP conectado |
| 1 | Movimiento cenital y cámara | Moverte con teclado y mando; cámara sin temblores de píxel |
| 2 | Red base: 2 jugadores | Dos instancias, una anfitriona; ambos se ven moverse |
| 3 | Rejilla de bloques en red | Colocar/romper en una instancia y verlo en la otra; unirse tarde ve el mundo |
| 4 | Zombis básicos + pathfinding | 20 caminantes rodean muros y golpean el más débil si no hay paso |
| 5 | Día/noche + horda creciente | 3 noches seguidas; cada una con más zombis; luz 2D de linterna |
| 6 | Mapa de osm2tiles en Unity | Delicias cargado como Tilemap; los edificios son bloques rompibles |
| 7 | Steam + prueba de estrés | 100 zombis + 2 jugadores por Steam, ≥ 60 FPS, ancho de banda medido |
| 8 | Colchón e integración | Build de Windows desde CI y subida a una rama privada de Steam |

---

## Estado (15/10/2026)
Adelantado sin Unity: la **lógica** de varios hitos ya está en `packages/com.cepacero.core`
con tests (`scripts/check.sh`). Lo que queda en cada hito es sobre todo la parte de Unity
(vista, entrada y red con FishNet), que encaja sobre esa lógica.

| Sem. | Ya hecho en el núcleo | Falta (Unity) |
| --- | --- | --- |
| 1 | `Float2`/`Int2`, ticks fijos | Todo: Input System, motor del jugador, cámara |
| 2 | Codificación a bytes (`ByteWriter`/`ByteReader`) | FishNet + Tugboat, *spawn* de jugadores, predicción |
| 3 | `BlockWorld` por chunks: colocar, dañar, puertas, deltas y *snapshots* de chunk | Tilemap ↔ `BlockWorld`, RPCs de intención, envío de deltas |
| 4 | `FlowField` con coste de rotura, `ZombieSimulation`, codec de 8 B/zombi, réplica con interpolación | Vista de zombis (pool de sprites), mensajes FishNet |
| 5 | `DayNightCycle`, `HordeDirector`, `SpawnFinder`, `GameSettings` | Luz 2D global y linterna, UI del reloj |
| 6 | `MapImporter` (contrato probado con osm2tiles) | Importador de editor y créditos ODbL |
| 7 | `tools/simbench`: 100 zombis + 2 jugadores, p95 0,3 ms y 7,8 KB/s por cliente | Medirlo con Steam real y en una build IL2CPP |

**Pendiente de la lógica** (sin Unity, para cuando toque): habilidades de los especiales
(grito, escupitajo, carga de La Mole), ruido como objetivo del flow field, inventario y crafteo.

## Semana 0 · Setup (esta noche)
- Seguir `docs/setup-unity.md`: instalar Unity, crear `CepaCero/`, primer commit del proyecto vacío.
- Activar el MCP de Unity y comprobar que Claude Code lee la consola.
- **Hecho cuando:** el proyecto abre sin errores y el commit no incluye `Library/`.

## Semana 1 · Movimiento cenital y cámara
- Input System con un mapa de acciones: `Move`, `Aim`, `Interact`, `Attack`, `Build` (teclado + ratón y mando).
- Jugador como `Rigidbody2D` cinemático con velocidad de andar/correr (ver referencias en `bestiario.md`).
- Cámara: Cinemachine 2D con seguimiento suave + **Pixel Perfect Camera** (resolución base provisional 640×360).
- Separación simulación/vista: `PlayerMotor` (lógica) frente a `PlayerView` (sprite y animación).
- **Decisiones que se cierran aquí:** tamaño de tile (16/32 px) y resolución base.
- **Hecho cuando:** te mueves en 8 direcciones, corres, la cámara te sigue y no hay *jitter*.

## Semana 2 · Red base (2 jugadores)
- Instalar el paquete de red elegido y el transporte local.
- `NetworkManager` con flujo de anfitrión/cliente; *spawn* de jugadores.
- Movimiento del jugador: el cliente envía la entrada y el anfitrión valida. Predicción en el cliente si el paquete la ofrece.
- Herramienta de pruebas: lanzar 2 instancias (build + editor o *Multiplayer Play Mode*).
- **Hecho cuando:** con 100 ms de latencia simulada el movimiento propio se siente inmediato y el del otro, fluido.

## Semana 3 · Rejilla de bloques (colocar y romper) en red
- Mundo por **chunks** (p. ej. 32×32 tiles). Datos: `ushort` de tipo de bloque + vida, separados del Tilemap visual.
- El cliente envía `RequestPlaceBlock(x, y, type)` y `RequestDamageBlock(x, y, dmg)`; el anfitrión valida (distancia, recursos) y difunde el delta.
- Unirse tarde: el anfitrión envía *snapshots* de los chunks cercanos.
- Bloques: madera, metal y hormigón con vida distinta; puerta que se abre y se cierra.
- **Hecho cuando:** la segunda instancia ve los cambios al instante y, si entra tarde, ve el mundo tal cual.

## Semana 4 · Zombis básicos con pathfinding
- **Flow field** por objetivo (jugadores/fuerte) calculado en el anfitrión sobre la rejilla; se recalcula por chunk cuando cambia un bloque.
- Coste de paso: el bloque rompible tiene un coste proporcional a su vida, así el zombi decide entre rodear y romper.
- Caminante y Corredor con los datos del bestiario (`ScriptableObject`).
- Sincronización: posiciones cuantizadas en **lotes** a 10–15 Hz e interpolación en el cliente.
- **Hecho cuando:** 20 caminantes llegan hasta ti, rodean un muro abierto y golpean uno cerrado.

## Semana 5 · Ciclo día/noche con horda creciente
- Reloj de partida en el anfitrión (duración configurable); fases día, atardecer, noche y amanecer.
- Luz global 2D que se oscurece; linterna del jugador como Light 2D.
- `HordeDirector`: número de zombis por noche = base × crecimiento^noche (opciones de partida).
- Ruido: las acciones emiten ruido y los zombis lo oyen (`detect_hearing`).
- **Hecho cuando:** sobrevives 3 noches y notas que cada una es más dura.

## Semana 6 · Cargar el mapa de osm2tiles en Unity
- Importador (script de editor) que lee el JSON de `tools/osm2tiles` y crea los chunks del mundo.
- Suelo en Tilemap; muros de edificio como bloques rompibles; parques y agua con coste de paso.
- Atribución ODbL en los créditos del juego.
- **Hecho cuando:** caminas por tu trozo de Delicias y reconoces las calles.

## Semana 7 · Steam + prueba de estrés
- Transporte Steam (AppID de pruebas 480 hasta tener AppID propio); invitar por la lista de amigos.
- Escena de estrés: 100 zombis + 2 jugadores; medir FPS, ms del anfitrión y KB/s por cliente.
- Objetivo provisional: ≥ 60 FPS en el anfitrión y < 64 KB/s por cliente.
- **Hecho cuando:** jugáis tú y un amigo por Steam 10 minutos con 100 zombis sin desincronizarse.

## Semana 8 · Colchón e integración
- Recuperar retrasos. CI (GitHub Actions + GameCI) para la build de Windows y subida por SteamPipe a una rama privada.
- Revisión del plan del MVP con lo aprendido.

## Riesgos del plan
- **Red + muchos zombis** es el riesgo técnico n.º 1; por eso hay pruebas desde la semana 2 y la de estrés es un hito propio.
- El pathfinding en un mundo modificable es caro: flow fields compartidos, no A* por zombi.
- Steam necesita un AppID propio (100 $) para la página "Coming Soon"; para desarrollar sirve el 480.
