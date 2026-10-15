# Propuesta: objetos, inventario, botín y crafteo

*15/10/2026 · **Aceptada (D-012).** Las 5 recomendaciones de abajo están aprobadas.*

Encaja con el pilar 4, *accesible pero profundo*: menos microgestión que Project Zomboid y
más decisiones que Core Keeper.

## 1. Objetos (datos, como el bestiario)
`docs/objetos.md` con bloques YAML → `tools/datagen` → `data/generated/items.json`.

| Campo | Ejemplo |
| --- | --- |
| `id`, `name` | `scrap_metal`, "Chatarra" |
| `category` | `material`, `weapon`, `tool`, `food`, `medical`, `ammo`, `sample`, `key` |
| `stack` | 50 (materiales), 1 (armas) |
| `durability` | solo armas y herramientas |
| `damage`, `damage_type`, `range`, `noise` | solo armas (el ruido atrae zombis) |
| `tool_power` | cuánto daño hace a bloques (hacha > puño) |
| `places_block` | `wood_wall` si es un material de construcción |

**MVP (unos 25 objetos):** madera, chatarra, tela, cinta, clavos; bate, hacha, cuchillo, pistola, munición;
vendas, botiquín, latas, agua; muestra de La Mole; puerta, trampa de pinchos.

## 2. Inventario
- **Huecos, sin peso:** 20 huecos de mochila + 5 de acceso rápido. Lo pesado ocupa más (una
  viga ocupa 2 huecos). Es más fácil de leer que el peso de Zomboid.
- **Arcón compartido** como bloque construible: el almacén del fuerte.
- **Red:** el anfitrión guarda todos los inventarios. El cliente envía intenciones (`MoveItem`,
  `Craft`, `Use`, `Drop`, `PickUp`) y recibe solo los cambios de **su** inventario. Los demás solo
  ven el objeto que lleva en la mano. Encaja con `HostIntentRules` (`CanAffordBlock` ya existe para esto).

## 3. Botín
- Cada edificio del mapa trae su **categoría** desde osm2tiles (`food`, `medical`, `hardware`…).
  Cada categoría tiene su **tabla de botín**: el supermercado da comida y la ferretería, herramientas.
- Número de contenedores proporcional al tamaño del edificio, colocados al cargar el mapa con la semilla de la partida.
- Los zombis sueltan lo que dice su `loot` en el bestiario (ya está en los datos).

## 4. Crafteo y progresión
- Recetas en datos (`docs/recetas.md`): ingredientes, resultado y **dónde** (a mano o en banco de trabajo).
- Las **muestras** de los jefes desbloquean recetas para **todo el grupo** (el diario es compartido):
  La Mole → muro de hormigón; Paciente Cero → antídoto.

## 5. Caer y morir
- A 0 de vida, el jugador **cae** (ya existe `IsDowned`). Un compañero puede **levantarlo** en 30 s
  manteniendo una tecla. Si nadie lo hace, muere: deja su mochila y reaparece al amanecer en el fuerte.
- En solitario: muere directamente y reaparece al amanecer con la mochila vacía (opción de partida).

## Lo que necesito que decidas
1. **¿Huecos o peso?** Recomiendo huecos.
2. **¿Reaparece el botín?** Recomiendo que no por defecto (escasez) y una opción de partida para que sí.
3. **¿Fuego amigo?** Recomiendo desactivado por defecto, como opción de partida.
4. **¿Te encaja levantar al compañero y la mochila al morir?**
5. **¿El arma de fuego en el MVP?** Hace mucho ruido y atrae a la horda, lo que es una buena tensión. Recomiendo una pistola con poca munición.
