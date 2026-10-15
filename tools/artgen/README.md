# artgen · arte provisional generado por código

Genera todo el arte de la primera versión (D-018) a partir de código: tiles, bloques,
jugadores, zombis, iconos de objetos y piezas de interfaz, con la **paleta propia de 32 colores**
de `artgen/palette.py`. Es arte provisional: formas sencillas a 16 px, no el arte final.

```bash
cd tools/artgen
python -m venv .venv && source .venv/Scripts/activate   # Linux/macOS: source .venv/bin/activate
pip install -r requirements.txt
python -m artgen            # escribe el atlas
python -m artgen --check    # falla si el atlas commiteado está desfasado (lo usa scripts/check.sh)
pytest -q
```

## Salida
- `CepaCero/Assets/Resources/Cepa/Art/atlas.png`: todos los sprites, 16 px = 1 casilla.
- `CepaCero/Assets/Resources/Cepa/Art/atlas.json`: nombre y rectángulo de cada sprite, con el origen
  abajo a la izquierda (convención de Unity). **No se editan a mano**: se regeneran.
- `tools/artgen/out/preview.png`: el atlas ampliado ×4 para revisarlo (no se commitea).

## Nombres
| Prefijo | Contenido |
| --- | --- |
| `ground_<tipo>_<0-2>` | Suelo (`GroundType` en minúsculas), 3 variantes que el juego alterna por casilla |
| `block_<clave>` | Bloques de `BlockRegistry.CreateDefault()` |
| `player_<dir>_<0-3>`, `player_other_…`, `player_downed` | Jugador propio, compañero y caído |
| `zombie_<id>_<dir>_<0-3>` | Un juego por zombi del bestiario (La Mole a 32 px) |
| `item_<id>` · `pile` | Icono de cada objeto de `items.json` y saco de objetos en el suelo |
| `ui_*` · `icon_*` | Panel (9-slice, borde 4 px), huecos de inventario, píxel blanco para barras, iconos de estado |

`<dir>` es `down`, `up` o `left`; la derecha es la izquierda espejada. Fotogramas 0 y 2: de pie; 1 y 3: paso.

Si se añade un objeto o un zombi al diseño, el generador falla hasta que tenga su dibujo
(`items.ICONS` o `characters.ZOMBIES`): así no queda nada sin arte.
