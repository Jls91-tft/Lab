# artgen · atlas de arte del juego

Monta el atlas del juego (D-018, D-019):
- **Mundo y decoración:** piezas CC0 de Kenney (`art/vendor/kenney/`, ver `art/vendor/README.md`)
  transformadas aquí: selección de piezas (`kenney.py`, `decor.py`), lo que Kenney no tiene dibujado en su
  estilo (asfalto, agua, vías, muros de metal y hormigón, coches quemados, desgaste) y un **filtro**
  común (−30 % de saturación, más oscuro, tinte verdoso).
- **Personajes y zombis:** dibujados por código imitando a Kenney (`characters.py`), en 4 direcciones con paso.
- **Iconos e interfaz:** dibujados por código con la **paleta propia de 32 colores** (`palette.py`).

```bash
cd tools/artgen
python -m venv .venv && source .venv/Scripts/activate   # Linux/macOS: source .venv/bin/activate
pip install -r requirements.txt
python -m artgen                       # escribe el atlas
python -m artgen --check               # falla si el atlas commiteado está desfasado (lo usa scripts/check.sh)
python -m artgen --map-preview <mapa>  # pinta un cepacero.map, antes y después del filtro, en out/
pytest -q
```

## Salida
- `CepaCero/Assets/Resources/Cepa/Art/atlas.png`: todos los sprites, 16 px = 1 casilla.
- `CepaCero/Assets/Resources/Cepa/Art/atlas.json`: nombre y rectángulo de cada sprite, con el origen
  abajo a la izquierda (convención de Unity). **No se editan a mano**: se regeneran.
- `tools/artgen/out/`: vistas previas (atlas ×4, mapas). No se commitea.

## Nombres
| Prefijo | Contenido |
| --- | --- |
| `ground_<tipo>_<0-2>` | Suelo (`GroundType` en minúsculas), 3 variantes que el juego alterna por casilla |
| `block_<clave>` | Bloques de `BlockRegistry.CreateDefault()`, más `block_building_wall_face` (fachada) |
| `player_<dir>_<0-3>`, `player_other_…`, `player_downed` | Jugador propio, compañero y caído |
| `zombie_<id>_<dir>_<0-3>` | Un juego por zombi del bestiario (La Mole a 32 px) |
| `decor_*` | Decoración que se alza (coches, farolas, árboles, muebles); el juego la ordena por altura |
| `decal_*` | Marcas planas (sangre, manchas, basura, cristales, alfombras) entre el suelo y los bloques |
| `item_<id>` · `pile` | Icono de cada objeto de `items.json` y saco de objetos en el suelo |
| `ui_*` · `icon_*` | Panel (9-slice, borde 4 px), huecos, cursor, píxel blanco para barras, iconos de estado |

`<dir>` es `down`, `up` o `left`; la derecha es la izquierda espejada. Fotogramas 0 y 2: de pie; 1 y 3: paso.

Dónde va cada `decor_*`/`decal_*` lo decide el juego (`DecorPlanner` en `com.cepacero.unity`), no artgen.

Si se añade un objeto o un zombi al diseño, el generador falla hasta que tenga su dibujo
(`items.ICONS` o `characters.ZOMBIES`): así no queda nada sin arte.
