# gamebridge · jugar el prototipo desde la terminal (D-023)

Con el prototipo en marcha (Play en el editor, o una build de desarrollo), `PrototypeDebugBridge` escucha en
`http://localhost:7777/` (puerto en el Inspector de *Prototype Game*, 0 lo apaga). Solo acepta conexiones de la propia
máquina y no existe en builds de publicación.

Cada orden se convierte en **las mismas pulsaciones de teclado y ratón** que daría un jugador: pasa por el código real del
juego y el anfitrión la valida igual. Mientras no hay órdenes, el teclado funciona como siempre.

```bash
tools/gamebridge/play.sh "select 7; walk 120 85; hold 121 85 3; key down; state"
tools/gamebridge/play.sh < mi-recorrido.txt            # una orden por línea, "#" comenta
curl -s -X POST --data "state" http://localhost:7777/   # lo mismo sin el script
```
La respuesta es JSON: `ok`, `results` (una línea por orden, o `ERROR …` y se para) y `state`
(día y fase, jugador con casilla, nivel, vida, stamina, hambre y sed; mano, barra y mochila; celdas construidas o
excavadas a 3 casillas en su nivel; zombis a 15 casillas, del anfitrión; últimos mensajes de la pantalla).

## Órdenes
Coordenadas en casillas del mapa (las de `state.player.tile`).

| Orden | Equivale a |
| --- | --- |
| `state` | Nada: solo devuelve el estado |
| `select N` | Tecla 1-9 (hueco de la barra) |
| `aim X Y` | Poner el ratón sobre la casilla |
| `click [X Y]` | Clic izquierdo (lo que pida la mano: atacar, romper, cavar, construir, usar) |
| `hold X Y SEG` | Mantener el clic izquierdo (cavar, talar) |
| `rclick [X Y]` / `mclick [X Y]` | Clic derecho (abrir, quitar pieza) / central (copiar pieza) |
| `drag X1 Y1 X2 Y2 [line\|outline\|room\|select]` | Arrastrar con clic izquierdo; `outline` = Mayús, `room` = Ctrl, `select` = Alt (planos) |
| `key K` | `up`/`down` (Re Pág / Av Pág), `shape+`/`shape-` (E/Q), `color+`/`color-`, `rotate` (R), `undo` (Ctrl+Z), `copy`/`paste`/`saveplan`/`loadplan` (Ctrl+C/V/S/L), `esc`, `take` (T), `pickup` (G), `equip` (V), `bandage` (C), `save`/`load` (F5/F9), `cost` (F6), `help` (H) |
| `move DX DY SEG [run]` | Mantener WASD en esa dirección |
| `walk X Y [run]` | Caminar hasta la casilla por un camino calculado con la regla de colisión del jugador |
| `wait SEG` | Esperar |
| `screenshot NOMBRE` | Captura de la vista Game en `tools/gamebridge/out/NOMBRE.png` (no en modo batch sin gráficos) |

**Trucos de depuración** (no son jugar; actúan en el anfitrión):

| Orden | Hace |
| --- | --- |
| `give ITEM [N]` | Mete objetos en tu mochila (ids de `data/generated/items.json`) |
| `spawn ZOMBI X Y [NIVEL]` | Suelta un zombi despierto (ids del bestiario) |
| `time day\|night` | Salta al principio del día o de la noche actual |

Separador: salto de línea o `;`. Si una orden falla, las siguientes no se ejecutan.
