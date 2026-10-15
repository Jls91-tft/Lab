# Bestiario

Estadísticas **iniciales** (para equilibrar en el prototipo). Cada criatura tiene un
bloque ```` ```yaml ```` con claves fijas: un script podrá leer este fichero y generar los
`ScriptableObject` (`ZombieDefinition`) de Unity. **El YAML es la fuente de verdad**; el texto
de alrededor es solo explicación. Las claves van en inglés porque pasan tal cual al código; los valores de texto, en español.

## Unidades y referencias
| Magnitud | Unidad | Referencia del jugador |
| --- | --- | --- |
| `health` | puntos de vida | Jugador: 100 |
| `speed` | tiles/s (1 tile = 1 bloque) | Jugador: 3,5 andando, 5,5 corriendo |
| `damage` | daño por golpe al jugador | Puño: 8; hacha: 20 |
| `structure_damage` | daño por golpe a bloques | Bloque de madera 100 · metal 300 · hormigón 600 |
| `attack_interval` | segundos entre golpes | — |
| `attack_range` / `detect_*` | tiles | — |
| `damage_taken` | multiplicadores (1,0 = normal) | — |
| `loot` | `objeto: [mín, máx, probabilidad]` | — |

Tipos de daño: `blunt` (contundente), `sharp` (cortante), `ballistic` (balas), `fire` (fuego), `acid` (ácido).
Sentidos: `detect_sight` (vista, se reduce de noche ×0,5), `detect_hearing` (oído, radio que se suma al ruido emitido).
Silueta/tamaño en tiles para el *placeholder* del prototipo (formas básicas).

## Resumen
| id | Nombre | Nivel | Vida | Vel. | Daño | Papel | MVP |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `walker` | Caminante | Común | 60 | 1,2 | 10 | Masa, presión constante | ✅ |
| `runner` | Corredor | Común | 40 | 4,8 | 8 | Castiga al que va solo | ✅ |
| `screamer` | Gritón | Especial | 80 | 2,0 | 0 | Llama a la horda | ✅ |
| `spitter` | Escupidor | Especial | 70 | 1,5 | 15 (ácido) | Corroe la madera a distancia | ✅ |
| `armored` | Acorazado | Especial | 250 | 1,0 | 20 | Frontal blindado: flanquear | ❌ |
| `sapper` | Zapador | Especial (adaptativo) | 150 | 1,4 | 12 | Aparece si fortificáis mucho | ❌ |
| `brute` | La Mole | Minijefe | 1500 | 1,8 / 6,0 carga | 40 | Derriba muros; se aturde contra metal | ✅ |
| `patient_zero` | Paciente Cero | Minijefe | 2500 | 2,5 | 30 | Jefe del hospital; lore clave | ❌ |

---

## Comunes

### Caminante
El grueso de la horda. Lento, torpe y peligroso en grupo. Rodea obstáculos y, si no
hay camino, golpea el bloque más débil que lo separe del objetivo.
```yaml
id: walker
name: Caminante
tier: common
mvp: true
size_tiles: 0.8
health: 60
speed: 1.2
damage: 10
attack_interval: 1.5
attack_range: 0.8
structure_damage: 8
detect_sight: 8
detect_hearing: 12
behavior: [wander, follow_noise, chase, attack_blocking_structure]
damage_taken: { blunt: 1.0, sharp: 1.0, ballistic: 1.0, fire: 1.5, acid: 1.0 }
weakness: "Fuego y golpes a la cabeza; muy lento en giros"
loot:
  scrap_cloth: [0, 1, 0.30]
  bandage_dirty: [0, 1, 0.05]
visual: "Silueta encorvada, piel gris verdosa, brazos colgando; ojos con brillo verde tenue de noche"
audio: "Gemido grave y arrastre de pies; gruñido corto al atacar"
```

### Corredor
Infectado reciente: aún conserva fuerza muscular. Esprinta en cuanto te ve y se cansa pronto.
```yaml
id: runner
name: Corredor
tier: common
mvp: true
size_tiles: 0.7
health: 40
speed: 4.8
damage: 8
attack_interval: 0.8
attack_range: 0.8
structure_damage: 4
detect_sight: 12
detect_hearing: 10
behavior: [wander, chase_sprint, exhaust_after_seconds_6, rest_seconds_3]
damage_taken: { blunt: 1.2, sharp: 1.2, ballistic: 1.0, fire: 1.0, acid: 1.0 }
weakness: "Se agota tras ~6 s de carrera; poca vida"
loot:
  scrap_cloth: [0, 1, 0.20]
  food_can: [0, 1, 0.08]
visual: "Silueta erguida e inclinada hacia delante, ropa de deporte rasgada, venas verdes en el cuello"
audio: "Jadeo rápido y chillido agudo al detectarte"
```

---

## Especiales

### Gritón
No ataca: grita. Cada grito atrae a los zombis en un radio amplio y acelera a los cercanos.
Prioridad absoluta: si llega a gritar dos veces, la noche se complica.
```yaml
id: screamer
name: Gritón
tier: special
mvp: true
size_tiles: 0.8
health: 80
speed: 2.0
damage: 0
attack_interval: 0
attack_range: 0
structure_damage: 0
detect_sight: 14
detect_hearing: 14
behavior: [keep_distance_6, scream_on_sight, flee_when_hit]
ability:
  scream: { cooldown: 12, windup: 1.5, noise_radius: 40, ally_speed_buff: 1.3, buff_seconds: 8 }
damage_taken: { blunt: 1.0, sharp: 1.0, ballistic: 1.5, fire: 1.0, acid: 1.0 }
weakness: "Interrumpir la carga del grito (1,5 s) con un golpe o disparo; frágil a balas"
loot:
  vocal_gland: [1, 1, 1.0]    # material para el señuelo sonoro
  scrap_cloth: [0, 2, 0.4]
visual: "Mandíbula desencajada, cuello hinchado que brilla verde al cargar el grito; aspecto delgado"
audio: "Inspiración silbante de aviso (carga) y grito desgarrado que retumba con eco"
```

### Escupidor
Ataca a distancia con ácido que corroe la **madera** y deja un charco que daña al pisarlo.
Rompe la estrategia de "todo de madera" y obliga a mejorar los muros.
```yaml
id: spitter
name: Escupidor
tier: special
mvp: true
size_tiles: 0.9
health: 70
speed: 1.5
damage: 15
damage_type: acid
attack_interval: 3.0
attack_range: 7
structure_damage: 10
structure_multipliers: { wood: 3.0, metal: 0.5, concrete: 0.25 }
detect_sight: 10
detect_hearing: 10
behavior: [keep_distance_5, spit_projectile, target_wood_walls]
ability:
  acid_pool: { radius: 1.0, seconds: 5, dps: 6 }
damage_taken: { blunt: 1.0, sharp: 1.0, ballistic: 1.0, fire: 1.2, acid: 0.0 }
weakness: "Cuerpo a cuerpo (no huye bien); los muros de metal y hormigón apenas se corroen"
loot:
  acid_sac: [1, 1, 1.0]        # material para granada ácida
visual: "Torso hinchado con bolsas verdes translúcidas; goteo verde brillante en la boca"
audio: "Gorgoteo constante; arcada húmeda antes de escupir; chisporroteo al corroer"
```

### Acorazado *(post-MVP)*
Restos de equipo antidisturbios fundidos con la piel. Casi inmune de frente.
```yaml
id: armored
name: Acorazado
tier: special
mvp: false
size_tiles: 1.0
health: 250
speed: 1.0
damage: 20
attack_interval: 2.0
attack_range: 1.0
structure_damage: 25
detect_sight: 8
detect_hearing: 10
behavior: [chase, face_target, shield_bash]
armor: { front_arc_degrees: 120, front_multiplier: 0.1 }
damage_taken: { blunt: 0.8, sharp: 0.5, ballistic: 0.7, fire: 1.0, acid: 2.0 }
weakness: "Espalda y flancos; gira lento; el ácido funde la coraza"
loot:
  metal_plate: [1, 2, 0.8]
visual: "Silueta ancha y cuadrada, placas oscuras con reflejo; marcas verdes en las juntas"
audio: "Pasos pesados metálicos; golpe sordo cuando le aciertas de frente"
```

### Zapador *(post-MVP, mutación adaptativa)*
Aparece cuando el grupo construye mucho (p. ej. > 150 bloques colocados). Busca los muros y los derriba.
```yaml
id: sapper
name: Zapador
tier: special
mvp: false
spawn_condition: "blocks_placed_by_team > 150"
size_tiles: 0.9
health: 150
speed: 1.4
damage: 12
attack_interval: 1.2
attack_range: 0.8
structure_damage: 45
detect_sight: 6
detect_hearing: 8
behavior: [seek_nearest_player_structure, attack_structure, ignore_players_unless_adjacent]
damage_taken: { blunt: 1.0, sharp: 1.0, ballistic: 1.2, fire: 1.0, acid: 1.0 }
weakness: "Ignora a los jugadores: fácil de cazar si alguien sale del fuerte"
loot:
  bone_claw: [1, 2, 0.7]
visual: "Brazos alargados terminados en garras de hueso; caminar a cuatro patas"
audio: "Rascado continuo y crujido de madera astillándose"
```

---

## Minijefes

### La Mole *(MVP)*
Un antiguo trabajador de la construcción hipertrofiado por la cepa. Carga contra los muros y los
atraviesa. **Si choca contra metal, queda aturdido**: es la ventana para atacarle en grupo.
```yaml
id: brute
name: La Mole
tier: miniboss
mvp: true
size_tiles: 2.0
health: 1500
health_per_extra_player: 0.5     # +50 % de vida por cada jugador extra
speed: 1.8
damage: 40
attack_interval: 2.5
attack_range: 1.5
structure_damage: 150
detect_sight: 12
detect_hearing: 20
behavior: [chase, charge_when_blocked, slam_area]
ability:
  charge: { speed: 6.0, windup: 1.2, cooldown: 10, breaks_blocks: [wood, concrete], stunned_by: [metal] }
  stun: { seconds: 4, damage_taken_multiplier: 2.0 }
  slam: { radius: 2.5, damage: 25, knockback: 3, cooldown: 8 }
damage_taken: { blunt: 0.6, sharp: 1.0, ballistic: 0.8, fire: 1.0, acid: 1.2 }
weakness: "Atraerlo contra un muro de metal: aturdido 4 s con daño doble"
loot:
  sample_brute: [1, 1, 1.0]      # muestra: desbloquea muro de hormigón + entrada del diario
  metal_scrap: [3, 6, 1.0]
visual: "El doble de alto que un jugador; espalda deformada con bultos verdes; casco de obra incrustado"
audio: "Respiración profunda; bramido y temblor de cámara antes de la carga; estruendo al romper muros"
```

### Paciente Cero *(post-MVP)*
Está en el hospital de la zona (ubicación ficticia; no usar el nombre real del hospital). Controla a
los zombis cercanos y se cura con ellos. Su muestra es la llave de la trama.
```yaml
id: patient_zero
name: Paciente Cero
tier: miniboss
mvp: false
size_tiles: 1.2
health: 2500
health_per_extra_player: 0.5
speed: 2.5
damage: 30
attack_interval: 1.5
attack_range: 1.2
structure_damage: 60
detect_sight: 16
detect_hearing: 16
behavior: [summon_horde, drain_allies_to_heal, teleport_through_vents]
ability:
  summon: { count: 8, cooldown: 20 }
  drain: { radius: 4, heal_per_ally: 100, cooldown: 15 }
damage_taken: { blunt: 1.0, sharp: 1.0, ballistic: 1.0, fire: 1.5, acid: 0.5 }
weakness: "Fuego; matar a sus invocados antes de que se cure"
loot:
  sample_zero: [1, 1, 1.0]       # muestra: receta de antídoto + diario
visual: "Bata de hospital, vías intravenosas colgando que brillan verde; filamentos que le salen de la espalda"
audio: "Susurro coral (muchas voces); pitido de monitor cardiaco distorsionado al invocar"
```
