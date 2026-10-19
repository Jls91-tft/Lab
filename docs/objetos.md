# Objetos

Datos **iniciales** del MVP (D-012). Igual que el bestiario: cada objeto es un bloque
```` ```yaml ```` con claves fijas que `tools/datagen` valida y convierte en
`data/generated/items.json`. **El YAML es la fuente de verdad.**

| Clave | Obligatoria | Significado |
| --- | --- | --- |
| `id`, `name` | sí | `snake_case`; nombre en español (la traducción irá a tablas de localización) |
| `category` | sí | `material`, `weapon`, `tool`, `food`, `medical`, `ammo`, `sample`, `misc` |
| `stack` | sí | máximo por montón |
| `size` | no (1) | huecos que ocupa **cada montón** (lo grande ocupa más) |
| `weapon` | no | `damage`, `damage_type`, `range` (tiles), `cooldown` (s), `noise` (tiles), `arc` (grados, cuerpo a cuerpo) o `ammo` (id, a distancia) |
| `tool_power` | no | daño a bloques por golpe (el puño hace 8) |
| `tool_kind` | no | `axe`, `pickaxe`, `shovel` o `hammer`: la herramienta que rompe bien cada material (D-022); con otra, rompe mucho más despacio |
| `durability` | no | usos antes de romperse (armas y herramientas) |
| `heal` | no | vida que cura al usarlo |
| `food`, `water` | no | puntos de las barras de hambre y sed que rellena (solo `category: food`, hasta 100) |

Referencias del jugador: vida 100; puño 8 de daño y 8 a bloques.
*Hambre y sed (D-015): `food` y `water` rellenan esas barras (100 = llena); `heal` cura además un poco. Con el ajuste de partida `Survival = Off` solo cuenta `heal`.*

## Materiales
```yaml
id: wood
name: Madera
category: material
stack: 50
```
```yaml
id: metal_scrap
name: Chatarra
category: material
stack: 50
```
```yaml
id: scrap_cloth
name: Trapos
category: material
stack: 50
```
```yaml
id: tape
name: Cinta americana
category: material
stack: 20
```
```yaml
id: nails
name: Clavos
category: material
stack: 100
```
```yaml
id: concrete_mix
name: Saco de cemento
category: material
stack: 10
size: 2
```
```yaml
id: metal_plate
name: Placa de metal
category: material
stack: 10
size: 2
```
```yaml
id: bone_claw
name: Garra de hueso
category: material
stack: 20
```

## Restos de mutados y muestras
```yaml
id: vocal_gland
name: Glándula vocal
category: material
stack: 10
```
```yaml
id: acid_sac
name: Saco de ácido
category: material
stack: 10
```
```yaml
id: sample_brute
name: Muestra de La Mole
category: sample
stack: 1
```
```yaml
id: sample_zero
name: Muestra del Paciente Cero
category: sample
stack: 1
```

## Materiales de construcción (D-022)
*Lo que se coloca con el clic y lo que sale de cavar (`earth`, `clay`, `gravel`, `stone`).*
```yaml
id: pallet
name: Palé
category: material
stack: 20
```
```yaml
id: plasterboard
name: Placa de pladur
category: material
stack: 40
```
```yaml
id: log
name: Tronco
category: material
stack: 50
```
```yaml
id: sandbag
name: Saco de arena
category: material
stack: 50
```
```yaml
id: adobe_brick
name: Adobe
category: material
stack: 100
```
```yaml
id: container_panel
name: Panel de contenedor
category: material
stack: 20
```
```yaml
id: brick
name: Ladrillo
category: material
stack: 100
```
```yaml
id: stone
name: Piedra
category: material
stack: 100
```
```yaml
id: rebar
name: Varilla de acero
category: material
stack: 50
```
```yaml
id: glass_pane
name: Cristal
category: material
stack: 40
```
```yaml
id: earth
name: Tierra
category: material
stack: 100
```
```yaml
id: clay
name: Arcilla
category: material
stack: 100
```
```yaml
id: gravel
name: Grava
category: material
stack: 100
```
```yaml
id: straw
name: Paja
category: material
stack: 100
```

## Herramientas de obra (D-022)
```yaml
id: shovel
name: Pala
category: tool
stack: 1
size: 2
durability: 140
tool_power: 25
tool_kind: shovel
```
```yaml
id: pickaxe
name: Pico
category: tool
stack: 1
size: 2
durability: 160
weapon: { damage: 20, damage_type: sharp, range: 0.75, arc: 90, cooldown: 0.8, noise: 8 }
tool_power: 40
tool_kind: pickaxe
```
```yaml
id: hammer
name: Martillo
category: tool
stack: 1
durability: 220
weapon: { damage: 14, damage_type: blunt, range: 0.6, arc: 80, cooldown: 0.5, noise: 6 }
tool_power: 12
tool_kind: hammer
```

## Armas y herramientas
```yaml
id: bat
name: Bate
category: weapon
stack: 1
size: 2
durability: 120
weapon: { damage: 18, damage_type: blunt, range: 0.8, arc: 110, cooldown: 0.5, noise: 6 }
tool_power: 12
```
```yaml
id: knife
name: Cuchillo de cocina
category: weapon
stack: 1
durability: 80
weapon: { damage: 14, damage_type: sharp, range: 0.55, arc: 80, cooldown: 0.35, noise: 3 }
tool_power: 6
```
```yaml
id: axe
name: Hacha
category: tool
stack: 1
size: 2
durability: 150
weapon: { damage: 24, damage_type: sharp, range: 0.75, arc: 100, cooldown: 0.7, noise: 7 }
tool_power: 40
tool_kind: axe
```
```yaml
id: pistol
name: Pistola
category: weapon
stack: 1
durability: 400
weapon: { damage: 35, damage_type: ballistic, range: 12, cooldown: 0.45, noise: 30, ammo: pistol_ammo }
```
```yaml
id: pistol_ammo
name: Balas de pistola
category: ammo
stack: 30
```

## Curación y comida
```yaml
id: bandage_dirty
name: Venda sucia
category: medical
stack: 10
heal: 5
```
```yaml
id: bandage
name: Venda
category: medical
stack: 10
heal: 20
```
```yaml
id: medkit
name: Botiquín
category: medical
stack: 3
size: 2
heal: 60
```
```yaml
id: food_can
name: Lata de comida
category: food
stack: 10
heal: 5
food: 40
```
```yaml
id: water_bottle
name: Botella de agua
category: food
stack: 10
heal: 2
water: 40
```
