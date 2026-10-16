# spritegen · arte del mundo a 32 px

Genera por código (Python + Pillow, sin dependencias nuevas) el arte del mundo a **32 px por casilla** (D-021):
suelos, bloques, decoración, marcas del suelo, jugadores y los zombis del bestiario, en **4 direcciones**
(abajo, arriba e izquierda; la derecha es la izquierda espejada) y **4 fotogramas** de caminar.

Es determinista: la misma ejecución da los mismos píxeles. Nada se dibuja a mano ni se descarga.

```bash
cd tools/spritegen
python -m venv .venv && source .venv/bin/activate        # Windows: .venv\Scripts\activate
pip install -r requirements.txt
python -m spritegen --preview     # escribe el atlas en CepaCero/Assets/Resources/Cepa/Art32/ y una vista previa ×3
python -m spritegen --out /ruta   # otra carpeta
python -m spritegen --check       # falla si el atlas commiteado está desfasado (pasa si aún no hay ninguno)
pytest -q
```

## Salida
- `atlas.png`: todos los sprites (1024 px de ancho).
- `atlas.json`: formato `cepacero.atlas` v1, **igual que el de `tools/artgen`** (rectángulos con origen abajo a la izquierda),
  con `ppu: 32`. Los personajes llevan además `pivot_x`/`pivot_y` para que los pies queden en la posición de la entidad.
- `tools/spritegen/out/preview.png`: vista previa ×3 (no se commitea).

## Nombres (los mismos que usa el juego con el atlas de 16 px)
| Prefijo | Contenido | Tamaño |
| --- | --- | --- |
| `ground_<tipo>_<0-2>` | Suelo de `GroundType`: dirt, asphalt, sidewalk, grass, water, floor, rail | 32×32 |
| `block_<clave>` | Bloques de `BlockRegistry`, más `block_building_wall_face` (fachada) | 32×32 |
| `decor_*` | Decoración que se alza: coches (`_h`/`_v`), árboles, farolas, muebles… | según pieza |
| `decal_*` | Marcas planas (sangre, manchas, basura, cristales, alfombras ×3 casillas) | 32×32 · 96×32 |
| `player_<dir>_<0-3>`, `player_other_…`, `player_downed` | Jugador propio, compañero y caído | 32×48 · 32×32 |
| `zombie_<id>_<dir>_<0-3>` | Un juego por zombi del bestiario (La Mole, 64×96: dos casillas) | 32×48 |
| `zombie_walker_v1…v9_…` | 9 variantes del caminante (corpulento, delgado, policía, enfermera, oficinista, sudadera, anciano, mecánico…) para que la horda parezca gente de la ciudad; el juego puede elegir una por zombi | 32×48 |
| `anim_water_<0-3>` · `decor_barrel_fire_<0-3>` | Agua en movimiento y barril con fuego (con punto de luz), para ciclar a ~4 fps | 32×32 · 24×44 |

Fotogramas 0 y 2: de pie con pasos a ras; 1 y 3: paso. `<dir>` es `down`, `up` o `left`.

## Paleta
Todo el atlas usa una **paleta cerrada de 81 colores, diseñada a mano** (`palette.py`): rampas por familia (grises fríos, piedra cálida,
maderas, ladrillo, pieles, verdes, azules, agua, morados, naranjas, sangre, hueso, olivas) y, aparte, los únicos verdes vivos, los de la cepa.
El dibujo usa rampas de 5 tonos y ruido; al final cada píxel opaco pasa a su color más cercano. Un test comprueba que no se cuela ningún
otro color y que no sobra ninguno. Para cambiar el ambiente de todo el juego basta con tocar la paleta. (Extraerla automáticamente
no sirve: estropea los acentos.)

## Lo que NO hace (todavía)
- **Iconos de objetos, interfaz y cursor**: siguen en `tools/artgen` (16 px). Son otra cuadrícula de píxeles y no se mezclan con el mundo.


## Piezas de construcción (D-020)
`structures.py`: **14 materiales** × pieza: `pallet` (palé), `drywall` (pladur), `wood` (tablón), `log` (tronco), `sandbag` (sacos de arena),
`scrap` (chatarra), `adobe`, `container` (contenedor), `brick` (ladrillo), `stone` (piedra), `concrete` (hormigón), `rebar` (hormigón armado),
`steel` (acero) y `glass` (cristal, **translúcido**: alpha 78 en los paneles para ver el suelo y dejar pasar la luz). El juego decide qué piezas
admite cada material (los sacos de arena, por ejemplo, solo en piezas bajas). Los muros **se unen solos**: el juego elige el sprite
según los vecinos que sean del mismo muro.

| Nombre | Qué es |
| --- | --- |
| `wall_<m>_top_<0-15>` | Muro visto desde arriba (cuando hay otro muro al sur). Máscara de vecinos: N=1, E=2, S=4, O=8 |
| `wall_<m>_face_<0-3>` | Fachada sur (cuando no hay muro debajo): remate + cara. Máscara: E=1, O=2 |
| `door_<m>_closed` · `door_<m>_open` | Puerta en fachada |
| `window_<m>` | Ventana en fachada (punto débil). Se barrica con `overlay_barricade` encima |
| `lowbarricade_<m>_<0-3>` · `fence_<m>_<0-3>` | Barricada baja y valla; máscara E=1, O=2 para los extremos |
| `floor_<m>_<0-1>` | Suelo: tarima, chapa, adoquín, hormigón |
| `overlay_crack_<1-2>` · `overlay_barricade` | Se dibujan encima de cualquier pieza (daño en niveles, barricada) |
| `obj_campfire_<0-3>` (animada, con luz) · `obj_lantern` · `obj_rain_barrel` · `obj_barbed_wire` · `obj_sofa` | Objetos con función o decorativos |

## Cómo se añade algo
- Un zombi nuevo en `docs/bestiario.md`: el generador falla hasta que tenga su `Look` en `characters.zombie_looks()`.
- Una pieza de decoración: función en `decor.py` y entrada en `decor.build()`.
- Cambiar la apariencia de un tipo: sus parámetros están en `zombie_looks()`; el dibujo, en `draw_front/back/side`.
