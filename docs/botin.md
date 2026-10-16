# Botín de los edificios

Fuente de verdad para `data/generated/loot.json` (D-012). Cada edificio del mapa trae su
**categoría** desde osm2tiles (`tools/osm2tiles/README.md`). Al cargar el mapa se colocan
contenedores dentro de cada edificio: `1 + baldosas / tiles_per_container`, hasta `max_containers`.
Cada contenedor tira `rolls` veces de su tabla la **primera vez que alguien lo abre**.
Por defecto el botín no reaparece (opción de partida).

Formato de cada entrada: `objeto: [mín, máx, probabilidad]`, igual que el botín de los zombis.

```yaml
kind: generic
rolls: 2
loot:
  wood: [1, 3, 0.4]
  scrap_cloth: [1, 2, 0.3]
  nails: [2, 6, 0.2]
  food_can: [1, 1, 0.15]
  pallet: [1, 1, 0.1]
  earth: [1, 2, 0.05]
```
```yaml
kind: residential
rolls: 3
loot:
  food_can: [1, 2, 0.35]
  water_bottle: [1, 2, 0.3]
  scrap_cloth: [1, 3, 0.4]
  knife: [1, 1, 0.06]
  bandage: [1, 1, 0.1]
  wood: [1, 2, 0.25]
  tape: [1, 1, 0.08]
  plasterboard: [1, 1, 0.1]
  glass_pane: [1, 1, 0.08]
  straw: [1, 2, 0.05]
```
```yaml
kind: commercial
rolls: 3
loot:
  scrap_cloth: [1, 3, 0.35]
  tape: [1, 2, 0.15]
  water_bottle: [1, 2, 0.2]
  metal_scrap: [1, 3, 0.2]
  bat: [1, 1, 0.04]
  pallet: [1, 2, 0.2]
  glass_pane: [1, 2, 0.2]
  plasterboard: [1, 2, 0.12]
```
```yaml
kind: food
rolls: 4
loot:
  food_can: [1, 3, 0.6]
  water_bottle: [1, 3, 0.5]
  knife: [1, 1, 0.08]
```
```yaml
kind: hardware
rolls: 4
loot:
  nails: [5, 20, 0.6]
  tape: [1, 3, 0.4]
  metal_scrap: [2, 6, 0.4]
  axe: [1, 1, 0.12]
  wood: [2, 6, 0.4]
  concrete_mix: [1, 1, 0.1]
  pallet: [1, 2, 0.25]
  brick: [2, 6, 0.3]
  stone: [1, 4, 0.1]
  plasterboard: [1, 3, 0.2]
  glass_pane: [1, 2, 0.15]
  rebar: [1, 2, 0.12]
  shovel: [1, 1, 0.14]
  pickaxe: [1, 1, 0.1]
  hammer: [1, 1, 0.16]
  metal_plate: [1, 1, 0.1]
```
```yaml
kind: medical
rolls: 4
loot:
  bandage: [1, 3, 0.5]
  bandage_dirty: [1, 3, 0.3]
  medkit: [1, 1, 0.12]
  water_bottle: [1, 1, 0.2]
```
```yaml
kind: police
rolls: 3
loot:
  pistol: [1, 1, 0.15]
  pistol_ammo: [3, 8, 0.35]
  bat: [1, 1, 0.1]
  bandage: [1, 2, 0.25]
```
```yaml
kind: school
rolls: 2
loot:
  scrap_cloth: [1, 3, 0.35]
  water_bottle: [1, 2, 0.3]
  bat: [1, 1, 0.08]
  tape: [1, 2, 0.2]
```
```yaml
kind: religious
rolls: 2
loot:
  scrap_cloth: [1, 3, 0.4]
  water_bottle: [1, 1, 0.2]
  wood: [1, 3, 0.3]
```
```yaml
kind: office
rolls: 2
loot:
  tape: [1, 2, 0.25]
  scrap_cloth: [1, 2, 0.25]
  water_bottle: [1, 1, 0.25]
  metal_scrap: [1, 2, 0.15]
```
```yaml
kind: industrial
rolls: 4
loot:
  metal_scrap: [2, 8, 0.6]
  nails: [3, 12, 0.4]
  wood: [2, 6, 0.4]
  concrete_mix: [1, 2, 0.15]
  axe: [1, 1, 0.05]
  container_panel: [1, 2, 0.2]
  rebar: [1, 3, 0.25]
  metal_plate: [1, 2, 0.2]
  pallet: [1, 3, 0.3]
  brick: [2, 8, 0.2]
  hammer: [1, 1, 0.08]
  pickaxe: [1, 1, 0.06]
```
```yaml
kind: garage
rolls: 3
loot:
  metal_scrap: [1, 5, 0.5]
  tape: [1, 2, 0.25]
  nails: [2, 8, 0.3]
  bat: [1, 1, 0.05]
  pallet: [1, 1, 0.12]
  metal_plate: [1, 1, 0.08]
```

## Parámetros de colocación
```yaml
kind: _placement
tiles_per_container: 80
max_containers: 6
```
