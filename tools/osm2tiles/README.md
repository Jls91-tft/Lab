# osm2tiles

Convierte datos de OpenStreetMap en mapas de tiles para Cepa Cero (`cepacero.map` v2):
un JSON que leerá Unity y un PNG de previsualización. Independiente del motor. Solo depende de **Pillow**.

## Uso
```bash
cd tools/osm2tiles
python -m venv .venv && source .venv/bin/activate      # Windows: .venv\Scripts\activate
pip install -r requirements-dev.txt

python -m osm2tiles --list-zones                       # zonas predefinidas (zones.json)
python -m osm2tiles --zone delicias-test               # descarga por Overpass (con caché) y convierte
python -m osm2tiles --zone delicias-test --meters-per-tile 1.5
python -m osm2tiles --bbox 41.648,-0.912,41.654,-0.903 --name mi-zona
python -m osm2tiles --zone delicias-test --input export.osm   # sin red: un .osm exportado
python -m pytest                                       # tests
```
Salida en `out/` (ignorada por Git): `<nombre>.map.json` y `<nombre>.preview.png`.
La descarga se guarda en `cache/<nombre>.osm`; `--refresh` fuerza otra descarga.

**Sin red** (o si Overpass está saturado): en [openstreetmap.org](https://www.openstreetmap.org)
→ *Exportar* → selecciona la zona → *Exportar*; guarda el `.osm` y pásalo con `--input`.

**Límites:** como máximo 4 km² por petición (`--force` para más). Respeta los
[términos de uso de Overpass](https://dev.overpass-api.de/overpass-doc/en/preface/commons.html): las descargas se
cachean y no hay que repetirlas sin motivo.

## Qué hace
| OSM | Capa `ground` | Capa `blocks` |
| --- | --- | --- |
| `highway=*` de coches | `asphalt` + 2 m de `sidewalk` a cada lado (ancho según el tipo de vía) | — |
| `highway=footway/path/pedestrian/…` | `sidewalk` (`track` → `dirt`) | — |
| `building=*` | `floor` | contorno `building_wall` (medianeras de 1 tile) + `door` hacia la calle más cercana |
| parques, jardines, césped… | `grass` | — |
| bosques (`natural=wood`, `landuse=forest`) | `grass` | `tree` repartidos |
| agua (`natural=water`, ríos, canales) | `water` | — |
| `railway=rail` | `rail` (el tranvía va por la calle y se ignora) | — |
| `natural=tree`, `tree_row` | — | `tree` |
| `barrier=fence/wall/hedge` | — | `fence` |
| Túneles | se ignoran | — |

Los multipolígonos (patios interiores, islas) se respetan. La **categoría de cada edificio**
sale de sus etiquetas, de los puntos de interés que contiene (una tienda o una farmacia en un
bajo) y de las áreas que lo rodean (el recinto de un hospital). Gana la de mayor prioridad:
`medical > police > school > food/hardware > commercial > religious > office/industrial > garage > residential > generic`.
**Nunca se guardan nombres ni marcas.**

## Formato `cepacero.map` v2 (v1 más plantas, sótanos y tejado por edificio)
Pensado para leerse con `JsonUtility` de Unity: sin diccionarios ni arrays anidados.

```jsonc
{
  "format": "cepacero.map",
  "version": 2,
  "generator": "osm2tiles 0.1.0",
  "name": "delicias-test",
  "attribution": "© OpenStreetMap contributors, ODbL 1.0 — https://www.openstreetmap.org/copyright",
  "license": "ODbL-1.0",
  "source": { "bbox": [41.648, -0.912, 41.654, -0.903],   // sur, oeste, norte, este
              "osm_timestamp": "2026-10-15T08:00:00Z",       // fecha de los datos OSM
              "projection": "local-equirectangular" },
  "meters_per_tile": 2.0,
  "width": 375, "height": 335,
  "origin": "bottom-left",                                    // fila 0 = sur (eje Y de Unity)
  "legend": {                                                 // índice = id
    "ground": ["dirt", "asphalt", "sidewalk", "grass", "water", "floor", "rail"],
    "blocks": ["none", "building_wall", "tree", "door", "fence"],
    "building_kinds": ["generic", "residential", "commercial", "food", "hardware", "medical",
                       "police", "school", "religious", "office", "industrial", "garage"],
    "roofs": ["tile", "sheet", "thatch", "flat", "glasshouse"]
  },
  "layers": {
    "ground": [0, 1, 2, ...],   // width*height ids, fila a fila: índice = y * width + x
    "blocks": [0, 0, 1, ...]
  },
  "buildings": [
    { "id": 1, "kind": "food", "bbox": [120, 40, 138, 61],    // min_x, min_y, max_x, max_y (incluidos)
      "tiles": 342, "doors": [128, 40],                        // plano: x0, y0, x1, y1, …
      "levels": 2, "underground": 0, "roof": "tile" }          // v2: plantas con la baja (1..4), sótanos (0..3), tejado
  ]
}
```

**Reglas de compatibilidad:** los ids de las leyendas solo crecen añadiendo al final; nunca se
reordenan. Un cambio incompatible sube `version`. El lector de Unity debe comprobar `format` y `version`.

**Plantas, sótanos y tejado (v2).** `levels` sale de `building:levels` (se toma el primer número de "3;4", se
limita a 1–4); si falta, depende del tipo de edificio (`rules.DEFAULT_LEVELS`: casas y apartamentos 3, oficinas 4,
hospitales 3, comercios 2, resto 1). `underground` sale de `building:levels:underground` (0–3) y es 0 si OSM no lo dice.
`roof` sale de `roof:material` (teja, chapa, paja, vidrio, hormigón), luego de `roof:shape=flat` y por último del
tipo (residencial y religioso: teja; industrial y garaje: chapa; resto: plano). El lector de v1 sigue valiendo:
sin esos campos un edificio es de una planta, sin sótano y sin tejado.

## Licencia de los datos (ODbL)
Los datos de OpenStreetMap están bajo la [Open Database License 1.0](https://opendatacommons.org/licenses/odbl/1-0/).
Lo que implica para nosotros:

1. **Atribución obligatoria** ("© OpenStreetMap contributors" con enlace a
   <https://www.openstreetmap.org/copyright>) allí donde se vea el mapa:
   - en los **créditos del juego** y en una pantalla de "Datos del mapa" accesible desde el menú;
   - en la **página de Steam** (descripción o información legal);
   - en los PNG de previsualización y en el campo `attribution` del JSON (ya lo hace la herramienta).
2. **El juego** (el render del mapa jugable) es una *obra producida*: puede tener la licencia
   que queramos, siempre que lleve la atribución.
3. **El JSON generado** es una *base de datos derivada*. Si se usa públicamente (y va dentro del
   juego), hay que **ofrecerlo bajo ODbL**, o la forma de recrearlo (este script + la consulta).
   Decisión D-008: los `*.map.json` se distribuyen bajo ODbL. El código de `osm2tiles` sigue siendo nuestro.
4. Si en el futuro se publica un mapa modificado a mano a partir de estos datos, también queda bajo ODbL.

Texto de créditos propuesto:
> Datos de mapas © colaboradores de OpenStreetMap, disponibles bajo la Open Database License (ODbL).
> www.openstreetmap.org/copyright

## Pendiente / ideas
- Probar con datos reales de Delicias y ajustar los anchos de vía y la escala.
- Vías en varias capas (puentes): ahora se pintan por encima.
- Compresión de capas (RLE) si los mapas grandes pesan demasiado.
- Puntos de aparición (jugadores, horda) y zonas de saqueo como capa adicional.
