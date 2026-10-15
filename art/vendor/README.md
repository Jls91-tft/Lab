# Arte de terceros

Hojas originales que `tools/artgen` usa como materia prima (D-019). No se editan:
`artgen` selecciona piezas, las transforma (filtro, desgaste, variantes) y las mete en el atlas del juego.
Están fuera de Git LFS a propósito (ver `.gitattributes`): son pequeñas y la CI las necesita.

| Carpeta | Paquete | Autor | Licencia | Origen |
| --- | --- | --- | --- | --- |
| `kenney/roguelike-modern-city/` | Roguelike Modern City 2.0 | Kenney (www.kenney.nl) | CC0 1.0 | https://kenney.nl/assets/roguelike-modern-city |
| `kenney/roguelike-indoors/` | Roguelike Indoor pack | Kenney (www.kenney.nl) | CC0 1.0 | https://kenney.nl/assets/roguelike-indoors |
| `kenney/roguelike-characters/` | Roguelike Characters 2.0 (referencia de estilo) | Kenney (www.kenney.nl) | CC0 1.0 | https://kenney.nl/assets/roguelike-characters |

CC0 no obliga a dar crédito, pero lo damos: **"Arte base del mundo: Kenney (www.kenney.nl), CC0"**
debe aparecer en los créditos del juego. Cada carpeta conserva el `License.txt` original.

Antes de añadir un paquete nuevo: comprobar la licencia en la página del autor y en el `License`
del archivo, añadir una fila aquí y registrarlo en `docs/decisiones.md`.
