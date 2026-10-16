# Construcción

Fuente de verdad para `data/generated/construction.json` (D-022). Igual que bestiario y objetos: cada bloque
```` ```yaml ```` lleva `kind` y claves fijas que `tools/datagen` valida, y **los bloques del juego se generan combinando formas y materiales**
(`forma_material`, p. ej. `wall_brick`, `door_wood`), no se escriben uno a uno.

| `kind` | Qué es |
| --- | --- |
| `rules` | Reglas generales (herramienta equivocada, devolución al desmontar, ruido de cavar) |
| `material` | Un material de obra: vida, coste por unidad, herramienta que lo rompe bien, ruido, formas que admite |
| `shape` | Una forma (muro, puerta, ventana…): capa, unidades de material, tamaño, si bloquea el paso |
| `roof` | Un tejado (solo visual por ahora) |
| `natural` | Lo que se cava: tierra y roca, con lo que sueltan |

**Coste de una pieza** = `ceil(units de la forma × cantidad del material)` para cada ingrediente (mínimo 1).
**Vida** = `hp del material × hp_factor de la forma`. Herramientas (`tool_kind` en `docs/objetos.md`): `axe`, `pickaxe`, `shovel`, `hammer`.

## Reglas
```yaml
kind: rules
wrong_tool_factor: 0.25
dismantle_refund: 0.5
dig_noise: 9
```

## Formas
```yaml
kind: shape
id: wall
name: Muro
layer: structure
units: 1
hp_factor: 1.0
solid: true
```
```yaml
kind: shape
id: halfwall
name: Medio muro
layer: structure
units: 0.5
hp_factor: 0.6
solid: true
see_through: true
```
```yaml
kind: shape
id: pillar
name: Pilar
layer: structure
units: 1
hp_factor: 0.8
solid: true
see_through: true
```
```yaml
kind: shape
id: arch
name: Arco
layer: structure
units: 2
hp_factor: 0.7
solid: false
see_through: true
```
```yaml
kind: shape
id: door
name: Puerta
layer: structure
units: 2
hp_factor: 0.5
solid: true
opens: true
```
```yaml
kind: shape
id: gate
name: Portón
layer: structure
units: 4
hp_factor: 0.8
solid: true
opens: true
width: 2
```
```yaml
kind: shape
id: window
name: Ventana
layer: structure
units: 2
hp_factor: 0.5
solid: true
see_through: true
```
```yaml
kind: shape
id: bigwindow
name: Ventanal
layer: structure
units: 4
hp_factor: 0.5
solid: true
see_through: true
width: 2
```
```yaml
kind: shape
id: fence
name: Valla
layer: structure
units: 1
hp_factor: 0.4
solid: true
see_through: true
```
```yaml
kind: shape
id: floor
name: Suelo
layer: floor
units: 1
hp_factor: 0.8
solid: false
```
```yaml
kind: shape
id: stairs
name: Escalera
layer: structure
units: 3
hp_factor: 0.8
solid: false
connects: up
```
```yaml
kind: shape
id: hatch
name: Trampilla
layer: floor
units: 2
hp_factor: 0.5
solid: false
opens: true
connects: down
```

## Materiales
```yaml
kind: material
id: pallet
name: Palé
ingredients: { pallet: 1 }
hp: 40
tool: axe
noise: 4
build_seconds: 0.3
shapes: [wall, halfwall, fence, door, floor]
```
```yaml
kind: material
id: drywall
name: Pladur
ingredients: { plasterboard: 1 }
hp: 60
tool: hammer
noise: 3
build_seconds: 0.5
paintable: true
shapes: [wall, halfwall, pillar, arch, door, window, floor]
```
```yaml
kind: material
id: wood
name: Tablón
ingredients: { wood: 2, nails: 1 }
hp: 100
tool: axe
noise: 6
build_seconds: 0.8
paintable: true
shapes: [wall, halfwall, pillar, arch, door, gate, window, bigwindow, fence, floor, stairs, hatch]
```
```yaml
kind: material
id: log
name: Tronco
ingredients: { log: 2 }
hp: 160
tool: axe
noise: 7
build_seconds: 1.0
shapes: [wall, halfwall, pillar, arch, door, gate, window, bigwindow, fence, floor, stairs]
```
```yaml
kind: material
id: sandbag
name: Sacos de arena
ingredients: { sandbag: 3 }
hp: 130
tool: shovel
noise: 2
build_seconds: 0.6
shapes: [wall, halfwall, fence]
```
```yaml
kind: material
id: scrap
name: Chatarra
ingredients: { metal_scrap: 2, nails: 1 }
hp: 300
tool: pickaxe
noise: 12
build_seconds: 1.0
paintable: true
shapes: [wall, halfwall, pillar, arch, door, gate, window, bigwindow, fence, floor]
```
```yaml
kind: material
id: adobe
name: Adobe
ingredients: { adobe_brick: 3 }
hp: 260
tool: shovel
noise: 4
build_seconds: 1.4
paintable: true
shapes: [wall, halfwall, pillar, arch, door, window, fence, floor]
```
```yaml
kind: material
id: container
name: Contenedor
ingredients: { container_panel: 1, nails: 2 }
hp: 350
tool: pickaxe
noise: 14
build_seconds: 1.2
paintable: true
shapes: [wall, halfwall, door, gate, window, bigwindow, fence, floor]
```
```yaml
kind: material
id: brick
name: Ladrillo
ingredients: { brick: 3 }
hp: 400
tool: pickaxe
noise: 8
build_seconds: 1.6
shapes: [wall, halfwall, pillar, arch, door, gate, window, bigwindow, fence, floor, stairs]
```
```yaml
kind: material
id: stone
name: Piedra
ingredients: { stone: 3 }
hp: 500
tool: pickaxe
noise: 9
build_seconds: 2.0
shapes: [wall, halfwall, pillar, arch, door, gate, window, bigwindow, fence, floor, stairs]
```
```yaml
kind: material
id: concrete
name: Hormigón
ingredients: { concrete_mix: 1 }
hp: 600
tool: pickaxe
noise: 10
build_seconds: 2.0
paintable: true
shapes: [wall, halfwall, pillar, arch, door, gate, window, bigwindow, fence, floor, stairs]
```
```yaml
kind: material
id: rebar
name: Hormigón armado
ingredients: { concrete_mix: 1, rebar: 1 }
hp: 900
tool: pickaxe
noise: 11
build_seconds: 2.6
shapes: [wall, halfwall, pillar, arch, door, gate, window, bigwindow, fence, floor]
```
```yaml
kind: material
id: steel
name: Acero
ingredients: { metal_plate: 1, nails: 1 }
hp: 1000
tool: pickaxe
noise: 14
build_seconds: 2.4
paintable: true
shapes: [wall, halfwall, pillar, door, gate, window, bigwindow, fence, floor, stairs, hatch]
```
```yaml
kind: material
id: glass
name: Cristal
ingredients: { glass_pane: 1 }
hp: 40
tool: hammer
noise: 16
build_seconds: 1.0
see_through: true
shapes: [wall, halfwall, pillar, door, window, bigwindow, fence, floor]
```

## Tejados
```yaml
kind: roof
id: tile
name: Teja
ingredients: { brick: 1 }
hp: 80
```
```yaml
kind: roof
id: sheet
name: Chapa
ingredients: { metal_scrap: 1 }
hp: 100
```
```yaml
kind: roof
id: thatch
name: Paja
ingredients: { straw: 2 }
hp: 40
```
```yaml
kind: roof
id: flat
name: Tejado plano
ingredients: { concrete_mix: 1 }
hp: 200
```
```yaml
kind: roof
id: glasshouse
name: Invernadero
ingredients: { glass_pane: 1, metal_scrap: 1 }
hp: 40
```

## Lo que se cava
```yaml
kind: natural
id: earth
name: Tierra
hp: 40
tool: shovel
noise: 5
drops: { earth: 1 }
```
```yaml
kind: natural
id: rock
name: Roca
hp: 160
tool: pickaxe
noise: 9
drops: { stone: 1, gravel: 1 }
```

Bajo tierra todo es macizo por defecto (tierra en los niveles −1 y −2, roca más abajo) y no se guarda. Lo que se cava se marca con un **suelo de cueva** y queda abierto.
La **fosa** es lo que deja cavar el suelo del nivel 0: se puede bajar por ella al nivel −1 y volver a subir.
```yaml
kind: natural
id: cave
name: Suelo de cueva
layer: floor
solid: false
hp: 1
tool: shovel
noise: 0
drops: { earth: 1 }
```
```yaml
kind: natural
id: pit
name: Fosa
layer: structure
solid: false
connects: down
hp: 1
tool: shovel
noise: 0
drops: { earth: 1 }
```
