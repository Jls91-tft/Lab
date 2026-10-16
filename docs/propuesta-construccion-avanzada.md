# Propuesta: construcción avanzada ("locuras de casas")

**Estado: propuesta (D-022), en revisión.** Recoge lo que Javier eligió el 16/10/2026. Falta su OK al **modelo de construcción "en la mano"** (§3) y al **subsuelo** (§2).
Amplía D-020 (`docs/propuesta-construccion.md`), que sigue vigente: tres capas, ventanas, barricadas, cama como reaparición.

## 1. Lo que Javier ha decidido
| Tema | Decisión |
| --- | --- |
| Altura | **Pisos reales y también subsuelo**: sótanos y poder **cavar** para hacer una casa subterránea |
| Materiales | **14 o más, más pintura** |
| Interfaz | Primero fue "solo teclas"; después pidió **algo tan cómodo como construir en Minecraft** (§3) |
| Herramientas | **Planos**: copiar, pegar y guardar |
| Orden de trabajo | **Aprobado el de §7**: primero la construcción rica en una planta, las plantas y el subsuelo después |
| Objetivo | Que la gente pueda hacerse "auténticas locuras" de casas, y que elegir material y pieza sea cómodo e intuitivo |

## 2. Alturas: de bajo tierra a las plantas altas
Todo lo que tiene posición pasa a tener **nivel (Z)**. Z = 0 es el suelo; **Z < 0 es subsuelo**; **Z > 0 son plantas**.
Por defecto: de **−3 a +3** (tres sótanos y tres plantas), configurable en la partida hasta ±4 para quien quiera locuras.

| Módulo | Cambio |
| --- | --- |
| Mundo (`BlockWorld`, `Chunk`) | Capas por nivel; los niveles vacíos no ocupan memoria. **Bajo tierra, todo es tierra o roca maciza por defecto** y solo se guarda lo excavado o construido |
| Jugadores y zombis | `Z` por entidad; colisión por nivel; suben y bajan por escaleras, escalas y trampillas |
| Zombis | Flow field por nivel con "portales" en escaleras; caen por huecos. **Los zombis no cavan** (salvo especiales, más adelante) |
| Percepción | Vista y ruido por nivel; el ruido se atenúa al nivel contiguo y pasa por escaleras abiertas. **Cavar hace ruido** |
| Red / guardado | Los deltas llevan el nivel (+1 byte); protocolo v3 y guardado v6 con migración |
| Mapa | `osm2tiles` usa `building:levels` de OpenStreetMap: **los edificios reales de Zaragoza tendrán sus plantas**. Más adelante, sótanos de edificios, metro y alcantarillas desde OSM (`tunnel`, `subway`) |
| Unity | Un Tilemap por nivel; la cámara muestra el nivel del jugador, desvanece lo de arriba y oscurece lo de abajo |

### Cavar: casas subterráneas
- **Herramientas:** la **pala** abre tierra, el **pico** abre roca y hormigón. Tardan más cuanto más duro es el material, y se gastan.
- **Cómo se cava:** con la pala sobre una casilla del suelo (**Z=0**) se **excava hacia abajo**: aparece un pozo con una rampa o escalera de bajada. Ya en **Z=−1**, cada casilla de tierra que excavas se convierte en suelo de tierra; lo que no excavas se ve como pared de tierra oscura. **Una pared de tierra no necesita muro**: es el muro.
- **Lo que sacas es material:** tierra (para sacos de arena y adobe), arcilla, piedra y grava. Cavar también **alimenta la construcción**.
- **Reglas simples:** el techo de tierra aguanta mientras no haya más de 5 casillas excavadas seguidas sin un pilar o viga (derrumbes en cadena, fuera del MVP). Bajo tierra **no hay luz**: lámparas y linternas, y por eso es un buen refugio y un buen susto.
- **Seguridad:** los zombis solo llegan por la escalera. Una **trampilla** sellada con barricada convierte un sótano en búnker. Pero **el ruido de cavar los atrae**: se cava de día, o se paga el riesgo.
- **Agua:** cerca de agua del mapa, cavar profundo se **inunda** (fuera del MVP, pero el modelo lo permite).

## 3. Cómo se construye: "lo que tienes en la mano es lo que pones"
Lo que hace cómodo construir en Minecraft **no es la hotbar, es que no hay menús**: tienes un bloque en la mano, haces clic y se pone; el mismo clic rompe. Lo copiamos:

1. **Los materiales son objetos del inventario**: tablones, ladrillos, cemento, chatarra… Una **bolsa de construcción** aparte (16 huecos, montones de 200) guarda los materiales sin atascar la mochila.
2. **Tu hotbar de construcción son tus materiales**, con su cantidad (como Minecraft). Rueda del ratón o 1-9 para elegir. Sin catálogo ni recetas en medio.
3. **La forma se elige aparte**, con el material ya en la mano: **Q / E** (o Mayús+rueda) pasa por muro, medio muro, pilar, arco, puerta, portón, ventana, ventanal, valla, suelo, techo, escalera… El fantasma cambia al instante. Un solo material sirve para todas las formas.
4. **Color**: **[ ]**, 16 colores, solo en piezas pintables.
5. **Clic izquierdo** pone y **clic derecho** rompe, con el mismo ritmo. **Mantener y arrastrar** dibuja una línea; el mismo gesto con **Mayús** lo bloquea en un eje y con **Ctrl** rellena un rectángulo.
6. **Poner encima sustituye**: una puerta sobre un muro lo reemplaza en su sitio, un ladrillo sobre un tablón **mejora el muro** y te devuelve lo que había. Esto es lo que hace rápido rehacer.
7. **Cuentagotas (clic central)** copia pieza, material y color de lo que señalas.
8. **Herramienta "habitación"**: arrastras un rectángulo y construye muros, suelo y techo con un hueco para la puerta. Para construir rápido o planear.
9. **Planos**: **V** selecciona, **Ctrl+C / Ctrl+V** copia y pega con fantasma y **R** para rotar; **Ctrl+S** guarda un archivo `.cepaplan`; se comparte como archivo. Pegar usa la cola de obra y dice qué material falta.
10. **Ctrl+Z** deshace. **Re Pág / Av Pág** cambia de nivel (incluidos los sótanos). **G** muestra la rejilla.
11. **Ajuste de partida de coste**: *normal*, *barato* o *gratis* (modo creativo), para quien solo quiere crear.

Un **libro de piezas** (Tab, solo de consulta) enseña todas las formas con su coste en unidades de material, para que se descubran. No hace falta para jugar.

Coste de las formas en **unidades de material** (el catálogo concreto se afina): muro 1, medio muro ½, pilar 1, arco 2, puerta 2, portón 4, ventana 2, ventanal 4, valla 1, suelo 1, techo 1, escalera 3, trampilla 2.

## 4. Materiales
Los materiales se **combinan** con las formas (no hay un bloque por combinación):

| # | Material | Vida (madera = 100) | Notas |
| --- | --- | --- | --- |
| 1 | Palé | 40 | Casi gratis, se saquea; la barricada de urgencia |
| 2 | Pladur | 60 | Tabiques interiores; se pinta bien |
| 3 | Tablón | 100 | Madera clásica (hoy `wood_wall`) |
| 4 | Tronco | 160 | Cabañas |
| 5 | Sacos de arena | 130 | Solo formas bajas; **se rellenan con la tierra que cavas** |
| 6 | Chatarra | 300 | Hoy `metal_wall`; ruidosa al golpear |
| 7 | Adobe | 260 | Barato; sale de **tierra y arcilla**; lento de construir |
| 8 | Contenedor | 350 | Acero corrugado; piezas grandes |
| 9 | Ladrillo | 400 | Hoy en los edificios del mapa |
| 10 | Piedra | 500 | Muros de castillo; sale de **cavar roca** |
| 11 | Hormigón | 600 | Hoy `concrete_wall` (receta con muestra) |
| 12 | Hormigón armado | 900 | El mejor muro, caro y lento |
| 13 | Acero | 1000 | Puertas y portones blindados |
| 14 | Cristal | 40 | Ventanales e invernaderos: **deja pasar luz y vista** |
| + | Vidrio blindado | 400 | Ventanas fuertes |

Tejados (visuales por ahora): teja, chapa, paja, plano y cristal. Pintura: 16 colores de la paleta, 1 byte por celda; `spritegen` hará una versión en grises de las piezas pintables.
Las cifras son un primer reparto; se afinan con una prueba de noches en `simbench`.

## 5. Piezas
| Grupo | Piezas |
| --- | --- |
| Estructura | Muro, medio muro, pilar, columna, esquina (automática), arco o vano |
| Puertas | Puerta simple, portón doble (2 casillas), cancela, trampilla de suelo |
| Ventanas | Ventana, ventanal (2 casillas), tronera, reja |
| Cierre | Valla de palos, alambrada, barandilla, muro bajo |
| Suelo | Tarima, chapa, adoquín, hormigón, baldosa, césped, **hueco** |
| Tejado | A un agua, a dos aguas, plano, cúpula |
| Verticales | **Escalera** recta (sube y baja), **escala**, trampilla, balcón / porche |
| Subsuelo | **Pared de tierra y de roca** (se excavan), viga y puntal, **pozo y rampa** de bajada |
| Objetos | Los de D-020 más camas, mesas, sofás, armarios, chimenea con luz, lámparas, estanterías, cocina, plantas, alfombras |

> **Hecho en arte (16/10/2026):** `tools/spritegen` ya dibuja los **14 materiales** (muro con uniones, puerta, ventana, barricada, valla y suelo),
> pilares, arcos, portones y ventanales de 2 casillas, escaleras, 5 tejados con 11 formas y los objetos de D-020 (~700 sprites).
> También el **subsuelo**: paredes de tierra y roca, suelos de cueva, pozos, escaleras de bajada, escalas, trampillas, vigas, marcas de excavación y antorchas.
> Falta: pintura (versión en grises) e integración en el juego.

## 6. Cómo encaja en el código
- **Datos, no código**: materiales, formas y costes se declaran en `docs/construccion.md` (YAML, como bestiario y objetos) y `tools/datagen` genera `data/generated/construccion.json`.
  El `BlockRegistry` se rellena desde ahí (forma × material), en vez de escribir 200 bloques a mano.
- **Celda**: `(forma, material, color, rotación)` por capa y nivel.
- **Delta de red**: `x, y, z, capa, tipo, color/rotación` (≈ 11 bytes). Medir con `simbench` con 4 jugadores construyendo y cavando a la vez.
- **Bolsa de construcción**: un segundo inventario (`Inventory`) con montones grandes; las formas cobran unidades de material de ahí.
- **Subsuelo**: bajo Z=0 la "tierra" no se almacena; solo las celdas excavadas o construidas. Excavar es una intención nueva validada por el anfitrión (alcance, herramienta, ruido).
- **Arte**: `spritegen` por material, los mismos juegos de muro, puerta, ventana, valla y suelo; más tejados, escaleras y el subsuelo.
- **Pruebas**: construir por nivel, apoyos, caer por un hueco, cavar y bajar, zombis que suben una escalera, sustituir una pieza por otra, planos que se guardan y se pegan igual, migraciones, y una prueba de aceptación
  ("una casa de 2 plantas y un sótano con material de un día de saqueo aguanta la primera noche").

## 7. Orden de trabajo (aprobado)
| Fase | Qué | Tamaño |
| --- | --- | --- |
| **A** | Datos de materiales y formas (`docs/construccion.md` → datagen → `BlockRegistry`); `spritegen` con los 14 materiales y el subsuelo | M |
| **B** | Núcleo: capas (D-020) + color + rotación + **nivel (Z) en el modelo** sin gameplay aún | L |
| **C** | Red v3 y guardado v6 con migración | M |
| **D** | Construir "en la mano": bolsa de construcción, forma con Q/E, color, sustituir, cuentagotas, habitación, deshacer, libro de piezas (**ya usable en una planta**) | L |
| **E** | Planos: copiar, pegar, guardar | M |
| **F** | Niveles para el jugador: escaleras, suelo, caídas, **cavar**, cámara con corte de niveles | L |
| **G** | Zombis multinivel: flow field con portales, caída por huecos, percepción, ruido de cavar | L |
| **H** | `osm2tiles` con `building:levels`, tejados, escaleras generadas y, más adelante, metro y alcantarillas | M |

Con A-E la gente ya construye casas ricas con 14 materiales y pintura en una planta; F-H añaden la verticalidad y el subsuelo. Se puede parar en cualquier punto con el juego jugable.

## 8. Qué no entra todavía
Derrumbes en cadena, fuego que se propaga por la madera, electricidad y generadores, inundaciones y pintura por patrones.

## 9. Preguntas abiertas para Javier
1. ¿Te encaja el modelo "en la mano" (§3), con la bolsa de construcción aparte y la forma con Q/E?
2. ¿Niveles por defecto de −3 a +3 (configurable hasta ±4)?
3. ¿Cavar con ruido que atrae a los zombis, como riesgo de la mecánica?
