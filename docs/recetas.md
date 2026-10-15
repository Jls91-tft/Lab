# Recetas

Fuente de verdad para `data/generated/recipes.json` (D-012). Dos tipos de receta:
- **De objeto** (`result`): se fabrica en el inventario (`station: hand`) o junto a un banco de trabajo (`station: workbench`).
- **De construcción** (`result_block`): es el coste de **colocar** ese bloque.

`unlocked_by`: la muestra que la desbloquea para **todo el grupo**. Sin ella, la receta está disponible desde el principio.

| Clave | Obligatoria | |
| --- | --- | --- |
| `id` | sí | `snake_case` |
| `result` o `result_block` | uno de los dos | id de objeto o clave de bloque |
| `count` | no (1) | unidades del resultado (solo para `result`) |
| `ingredients` | sí | `objeto: cantidad` |
| `station` | no (`hand`) | `hand` o `workbench` |
| `unlocked_by` | no | id de una muestra (`category: sample`) |

## Construcción
```yaml
id: build_wood_wall
result_block: wood_wall
ingredients: { wood: 4 }
```
```yaml
id: build_metal_wall
result_block: metal_wall
ingredients: { metal_scrap: 4, nails: 4 }
```
```yaml
id: build_concrete_wall
result_block: concrete_wall
ingredients: { concrete_mix: 2 }
unlocked_by: sample_brute
```
```yaml
id: build_door
result_block: door
ingredients: { wood: 6, nails: 4 }
```
```yaml
id: build_fence
result_block: fence
ingredients: { wood: 2 }
```
```yaml
id: build_workbench
result_block: workbench
ingredients: { wood: 10, nails: 10 }
```
```yaml
id: build_chest
result_block: chest
ingredients: { wood: 8 }
```

## Objetos
```yaml
id: craft_bandage
result: bandage
ingredients: { scrap_cloth: 2 }
```
```yaml
id: craft_clean_bandage
result: bandage
ingredients: { bandage_dirty: 1, water_bottle: 1 }
```
```yaml
id: craft_bat
result: bat
ingredients: { wood: 3, tape: 1 }
```
```yaml
id: craft_medkit
result: medkit
ingredients: { bandage: 3, tape: 1, scrap_cloth: 2 }
station: workbench
```
```yaml
id: craft_metal_plate
result: metal_plate
ingredients: { metal_scrap: 6 }
station: workbench
```
```yaml
id: craft_concrete_mix
result: concrete_mix
count: 2
ingredients: { metal_scrap: 2, bone_claw: 2 }
station: workbench
unlocked_by: sample_brute
```
