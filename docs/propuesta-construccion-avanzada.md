# Propuesta: construcción avanzada ("locuras de casas")

**Estado: propuesta (D-022).** Recoge lo que Javier eligió el 16/10/2026; falta su OK al plan y al orden de trabajo (§6).
Amplía D-020 (`docs/propuesta-construccion.md`), que sigue vigente: tres capas, ventanas, barricadas, cama como reaparición.

## 1. Lo que Javier ha decidido
| Tema | Decisión |
| --- | --- |
| Altura | **Pisos reales, 2-3 plantas** (la opción más ambiciosa) |
| Materiales | **14 o más, más pintura** (colores) |
| Interfaz | **Solo teclas, estilo Minecraft** (hotbar de 9 huecos y rueda del ratón) |
| Herramientas | **Planos**: copiar, pegar y guardar |
| Objetivo | Que la gente pueda hacerse "auténticas locuras" de casas, y que elegir material y pieza sea cómodo e intuitivo |

## 2. Qué implica cada decisión (lo bueno y lo caro)
### Pisos reales: el cambio más grande del proyecto
Hoy el mundo es plano: una casilla, tres capas. Con plantas, **todo lo que tiene posición pasa a tener planta (Z)**:

| Módulo | Cambio |
| --- | --- |
| Mundo (`BlockWorld`, `Chunk`) | Un conjunto de capas por planta; las plantas vacías no ocupan memoria |
| Jugadores | `PlayerState.Z`; movimiento y colisión por planta; subir y bajar por escaleras |
| Zombis | `Z` por zombi; **flow field por planta con "portales" en las escaleras**; caen por los huecos |
| Percepción | Vista y ruido por planta; el ruido se atenúa a la planta contigua y baja por las escaleras |
| Red | Los deltas llevan la planta (+1 byte); el snapshot de zombis, 2 bits más; protocolo v3 |
| Guardado | v6, con migración (una partida antigua carga con las plantas altas vacías) |
| Mapa | `osm2tiles` usa `building:levels` de OpenStreetMap: **los edificios reales de Zaragoza tendrán sus plantas** |
| Unity | Un Tilemap por planta; la cámara muestra la planta del jugador y desvanece las de arriba; las de abajo se ven oscurecidas por los huecos |

Reglas mínimas para que no sea un caos:
- **Suelo obligatorio** en una planta alta para pisarla. Sin suelo es un hueco: se cae (daño) y **los zombis también caen**.
- **Apoyo estructural simple**: una pieza de planta alta necesita un muro, pilar o columna debajo, o estar a 2 casillas de uno. Si se destruye el apoyo, lo de arriba cae (fase posterior; al principio solo se comprueba al colocar).
- **Escalera** = pieza de 2 casillas con orientación (recta). Más adelante, de caracol y escotilla.
- Máximo configurable de plantas (por defecto 3: baja, primera y segunda; la azotea es el tejado de la última).

**Riesgo:** es varias veces más trabajo que cualquier otra cosa hecha hasta ahora y toca núcleo, red, guardado, mapa y Unity a la vez.
Por eso propongo hacerlo **por fases** (§6) y entregar valor antes: primero la construcción rica en una planta, con las plantas después.

### 14 o más materiales y pintura
Los materiales se **combinan** con las piezas (no hay un bloque por cada combinación), así que el catálogo crece sin explotar:

| # | Material | Vida (madera = 100) | Notas |
| --- | --- | --- | --- |
| 1 | Palé | 40 | Casi gratis, se saquea; la barricada de urgencia |
| 2 | Pladur | 60 | Tabiques interiores; se pinta bien |
| 3 | Tablón | 100 | Madera clásica (hoy `wood_wall`) |
| 4 | Tronco | 160 | Cabañas; aguanta más que el tablón |
| 5 | Sacos de arena | 130 | Solo piezas bajas; frena y deja disparar |
| 6 | Chatarra | 300 | Hoy `metal_wall`; ruidosa al golpear |
| 7 | Adobe | 260 | Barato de materia prima, lento de construir |
| 8 | Contenedor | 350 | Acero corrugado; piezas grandes |
| 9 | Ladrillo | 400 | Hoy en los edificios del mapa |
| 10 | Piedra | 500 | Muros de castillo |
| 11 | Hormigón | 600 | Hoy `concrete_wall` (receta con muestra) |
| 12 | Hormigón armado | 900 | El mejor muro, caro y lento |
| 13 | Acero | 1000 | Puertas y portones blindados |
| 14 | Cristal | 40 | Ventanales y invernaderos: **deja pasar luz y vista** (los zombis ven dentro) |
| + | Vidrio blindado | 400 | Ventanas fuertes |

Materiales de **tejado** (solo se ven y, más adelante, protegen de la lluvia): teja, chapa, paja, plano y cristal.
Las cifras son un primer reparto; se afinan con una prueba de noches en `simbench`.

**Pintura:** 16 colores (de la paleta del juego) que se aplican a cualquier pieza pintable. Cuesta 1 byte por celda. En arte,
`spritegen` genera una versión en grises de las piezas pintables y Unity la tiñe; no hay una copia por color.

### Interfaz "solo teclas", con un refinamiento
Con ~14 materiales × ~20 piezas, **una hotbar de 9 huecos sola no basta para descubrir el catálogo**. Propongo el truco de Minecraft:
la hotbar sigue siendo lo que usas, y un **catálogo** (Tab) sirve para llenarla.

El mismo principio que hace cómodo construir: **tres selectores independientes**, en vez de una pieza por combinación.
| Selector | Cómo |
| --- | --- |
| **Pieza** (muro, puerta, ventana, suelo, techo, escalera, valla…) | Hotbar 1-9; rueda del ratón o Mayús+rueda para pasar de página (3 páginas = 27 huecos) |
| **Material** | **Q / E** (o Ctrl+rueda) cambian el material de la pieza al vuelo; se ve en el fantasma y en el coste |
| **Color** | **[ ]** cambian el color; sin color = el natural |

| Tecla / ratón | Acción |
| --- | --- |
| **B** | Entrar y salir del modo construcción |
| Clic izq. | Construir; arrastrar = línea; **Mayús + arrastrar** = contorno de habitación; **Ctrl + arrastrar** = rectángulo relleno |
| Clic derecho / Esc | Cancelar |
| **Clic central** | **Cuentagotas**: copia pieza, material y color de lo que señalas |
| **R** | Rotar |
| **X** | Desmontar (devuelve la mitad) |
| **Ctrl+Z** | Deshacer la última obra (la cola de obra aún no enviada se cancela; lo ya colocado se desmonta) |
| **Re Pág / Av Pág** | Subir y bajar de planta (se ve y se construye en esa planta) |
| **Tab** | Catálogo: todas las piezas por categoría, con buscador, para asignarlas a la hotbar |
| **G** | Rejilla |

El fantasma se mantiene como ya está hecho: verde si se puede, rojo con el motivo, y el coste total antes de soltar.

### Planos: copiar, pegar y guardar
- **V** (modo selección) marca un rectángulo; **Ctrl+C** copia; **Ctrl+V** pega con un fantasma que se rota con **R**.
- **Ctrl+S** guarda el plano como archivo (`.cepaplan`, JSON) en la carpeta de datos del juego; el catálogo (Tab) tiene una pestaña **Planos**.
- El plano guarda **claves de texto** (`wall`, `brick`, color, planta, rotación), no números, para que sobreviva a cambios del juego, y se puede compartir como archivo.
- Pegar usa la **cola de obra** de siempre: el anfitrión valida cada pieza y coloca hasta donde llegue el material. Muestra el material que falta.

## 3. Piezas para casas de verdad (además de las de D-020)
| Grupo | Piezas |
| --- | --- |
| Estructura | Muro, **medio muro/antepecho**, **pilar**, **columna decorativa**, esquina (automática), **arco / vano sin hoja** |
| Puertas | Puerta simple, **portón doble** (2 casillas), cancela, **escotilla** (suelo) |
| Ventanas | Ventana, **ventanal** (2 casillas), **tronera** (para disparar), reja |
| Cierre | Valla de palos, alambrada, **barandilla**, muro bajo |
| Suelo | Tarima, chapa, adoquín, hormigón, baldosa, césped, **hueco** |
| Tejado | **A un agua, a dos aguas, plano, cúpula** (visuales; se desvanecen al entrar) |
| Verticales | **Escalera recta**, escalera de caracol, escotilla, **balcón / porche** |
| Objetos | Los de D-020 más camas (simple, doble, litera), mesas, sillas, sofás, armarios, chimenea con luz, lámparas, estanterías, cocina, plantas, alfombras |

## 4. Cómo encaja en el código
- **Datos, no código**: materiales y piezas se declaran en `docs/construccion.md` (YAML, como bestiario y objetos) y `tools/datagen` genera `data/generated/construccion.json`.
  El `BlockRegistry` se rellena desde ahí (pieza × material = bloque), en vez de escribir 200 bloques a mano.
- **Celda**: `(pieza, material, color, rotación)` en vez de un único `BlockType`. Los ids de bloque siguen siendo `ushort` (combinaciones) y la rotación y el color, bytes aparte.
- **Delta de red**: `x, y, z, capa, tipo, color/rotación` (≈ 11 bytes). Medir con `simbench` con 4 jugadores construyendo a la vez.
- **Arte (`spritegen`)**: por material, los mismos juegos de muro (16 + 4 variantes), puerta, ventana, valla, suelo, más tejados y escaleras. ~500-700 sprites; el atlas pasa a 2048 de alto.
- **Pruebas**: construir por planta, apoyos, caer por un hueco, zombis que suben una escalera, planos que se guardan y se pegan igual, migraciones, y una prueba de aceptación
  ("una casa de 2 plantas con material de un día de saqueo aguanta la primera noche").

## 5. Qué no entra todavía
Derrumbes en cadena, fuego que se propaga por la madera, electricidad y generadores, plantas por debajo del suelo (sótanos) y pintura por patrones.

## 6. Orden de trabajo propuesto (entrega valor pronto)
| Fase | Qué | Tamaño |
| --- | --- | --- |
| **A** | Datos de materiales y piezas (`docs/construccion.md` → datagen → `BlockRegistry`) y `spritegen` con los 14 materiales | M |
| **B** | Núcleo: capas (D-020) + color + rotación + **planta (Z) en el modelo** sin gameplay aún | L |
| **C** | Red v3 y guardado v6 con migración | M |
| **D** | UI de construcción: hotbar, Q/E material, color, cuentagotas, deshacer, catálogo (**ya usable en una planta**) | L |
| **E** | Planos: copiar, pegar, guardar | M |
| **F** | Plantas para el jugador: escaleras, suelo, caídas, cámara con corte de plantas | L |
| **G** | Zombis multiplanta: flow field con portales, caída por huecos, percepción | L |
| **H** | `osm2tiles` con `building:levels`, tejados y escaleras generadas | M |

Con A-E la gente ya construye casas ricas con 14 materiales y pintura en una planta; F-H añaden la verticalidad. Es un orden que se puede parar en cualquier punto con el juego jugable.

## 7. Preguntas abiertas para Javier
1. ¿Te vale el orden de §6 (primero la construcción rica en una planta, las plantas después)?
2. ¿Aceptas el refinamiento de la interfaz (hotbar + Tab para el catálogo) sin dejar de ser "solo teclas"?
3. ¿Máximo de plantas por defecto: 3 (baja, primera, segunda) o 2?
