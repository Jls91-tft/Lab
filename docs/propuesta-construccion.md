# Propuesta: construcción

**Estado: decidida en D-020 (`docs/decisiones.md`), 16/10/2026.** Javier aprobó todo lo propuesto:
tres capas (B), ventanas y barricadas sobre los edificios reales, cama como reaparición, techos solo
visuales, el catálogo tal cual y empezar por el modo construcción (paso 1 del §9).

## El problema
El pilar 2 del GDD es "Tu fuerte, tu ciudad", y Javier quiere que construir sea tan importante y
tan creativo como en Minecraft. Hoy no lo es, y no por la vista cenital (Core Keeper, nuestro referente
visual, es cenital y construir ahí engancha), sino por lo poco que hay:

- 8 bloques, todos de 1 casilla y macizos, en **una sola capa**.
- Se eligen con **B a ciegas** y se colocan de uno en uno con clic derecho; no se ve qué vas a poner ni cuánto cuesta.
- No hay suelos, ventanas, techos, muebles con función ni muros que se unan entre sí.
- Lo construido se ve igual que lo que ya estaba: no da gusto mirarlo.

## Qué hace que construir enganche en un juego cenital
| Juego | Lo que copiaríamos |
| --- | --- |
| **Core Keeper** | Suelos y muros por casillas, muebles y luces que cambian cómo se siente la base, construir rápido |
| **RimWorld** | Habitaciones con suelo, techo y función; colocar en arrastre con un fantasma |
| **Project Zomboid** | Barricar ventanas y puertas de casas reales; reforzar lo que ya existe |
| **Valheim** | Que lo bonito también sea útil (fuego, cama, techo) |

Lo que **no** tenemos de Minecraft es la altura (pisos). Lo compensamos con capas, catálogo, luz y,
sobre todo, con algo que Minecraft no tiene: **construyes sobre tu barrio real** y **la horda pone a
prueba tu diseño** (los zombis ya buscan el punto más débil).

---

## 1. Capas por casilla
La decisión de fondo. Hoy cada casilla tiene un bloque. Propongo tres capas:

| Capa | Qué va | Ejemplos | Bloquea el paso |
| --- | --- | --- | --- |
| **Suelo** | Lo que pisas (sustituye al suelo del mapa donde lo pones) | Tarima, baldosa, chapa, hormigón | No |
| **Estructura** | Lo que cierra el espacio | Muro, puerta, ventana, valla, barricada baja | Sí (salvo puertas abiertas) |
| **Objeto** | Lo que pones dentro | Cama, banco de trabajo, cofre, lámpara, trampa, barril de lluvia | Algunos (cofre sí, alfombra no) |

| | A · Una capa (lo de hoy, con más tipos) | **B · Tres capas (recomendada)** | C · Varios pisos |
| --- | --- | --- | --- |
| Creatividad | Baja: solo líneas de muro | Alta: habitaciones con suelo, muebles y luz | Muy alta |
| Coste en el núcleo | Ninguno | Medio: dos arrays más por chunk | Muy alto: todo el juego en 3D lógico |
| Red y guardado | Igual | Deltas con 1 byte de capa; chunk ~1,5× más grande | Rehacer |
| Encaja con lo hecho | Sí | Sí | No (rompe mapa, flow fields, cámara) |

Con B, una puerta en un muro, una alfombra y una lámpara en la misma habitación son tres capas distintas,
y romper un muro no borra el suelo que pusiste.

## 2. Catálogo para el MVP
Pocos materiales que se combinan, en lugar de muchos bloques sueltos:

| Material | Vida | Coste base | Se consigue |
| --- | --- | --- | --- |
| Madera | baja | tablas + clavos | talando, desmontando muebles |
| Chatarra | media | chapa + cinta | coches, naves |
| Ladrillo | media-alta | ladrillos + cemento | derribando muros de la ciudad |
| Hormigón | alta | saco de cemento (receta bloqueada por muestra) | ferreterías, obras |

| Forma | Qué hace |
| --- | --- |
| Muro | Cierra. Se une con los muros vecinos del mismo material (dibujo automático) |
| Puerta / portón doble | Se abre y cierra; los zombis la golpean primero si es el camino más corto |
| **Ventana** | Deja ver y disparar; es un punto débil que los zombis buscan. Se puede **barricar** encima |
| Barricada baja | Bloquea el paso pero deja disparar por encima |
| Valla | Barata y débil: marca perímetro, frena |
| Suelo | Marca la habitación; da "interior" (techo, luz, cama válida) |

Objetos con función (la clave para que construir tenga sentido):

| Objeto | Función |
| --- | --- |
| **Cama** | Punto de reaparición del grupo (hoy reaparecéis al amanecer en cualquier sitio) |
| Banco de trabajo | Ya existe: recetas avanzadas |
| Cofre | Ya existe: almacén compartido |
| **Lámpara / hoguera** | Luz 2D: ves en la noche, pero delata (la luz atrae, como ya dice el GDD) |
| Barril de lluvia | Agua sin salir (hambre y sed, D-015) |
| Trampas | Pinchos (ya existe), alambre de espino (frena) |
| Muebles decorativos | Mesa, silla, sofá, alfombra: sin función, para que la base sea tuya |

Fuera del MVP: generador y electricidad, torretas, puertas automáticas, cultivos, pintar muros.

## 3. Modo construcción
Lo que hace que construir **fluya**:
- **B** abre el modo construcción: una barra por categorías (Estructura, Suelo, Objeto, Trampa) con icono y coste.
- **Fantasma** en el cursor: verde si se puede, rojo con el motivo ("Te faltan 4 tablas", "Demasiado lejos", "Ocupado").
- **Arrastrar**: muros en línea y suelos en rectángulo, con el coste total antes de soltar. Se coloca hasta donde llegue el material.
- **R** rota (puertas, camas, objetos de 2 casillas). **Clic derecho** coloca, **X** desmonta y devuelve la mitad del material.
- En cooperativo, cualquiera ve los fantasmas de los demás: se puede planear juntos.
- Después del MVP: guardar y pegar planos.

> **Hecho (16/10/2026, paso 1, sin tocar el núcleo):** B entra y sale; barra de 8 piezas (1-8 o rueda) con nombre y coste;
> fantasma verde/rojo con el motivo (y azul si solo está lejos); **clic izquierdo** construye y arrastrar hace una línea,
> **Mayús + arrastrar** el contorno de una habitación; clic derecho o Esc cancelan. Lo arrastrado entra en una **cola de obra**
> que envía una colocación por cada espera de acción (0,35 s, lo que acepta el anfitrión) y espera a que te acerques a lo lejano.
> Cambio respecto al plan: se construye con el **clic izquierdo** (en este modo no se ataca). **Pendiente:** desmontar (X) y rotar (R),
> que necesitan reglas nuevas en el núcleo; van con el paso 2 (capas). Los fantasmas de los compañeros, también con la red.

## 4. Que se vea bien lo construido
- **Muros que se unen** (autotiling por material) y fachada en vista 3/4, como los edificios de la ciudad (D-019).
- **Techos**: una habitación cerrada con suelo muestra techo desde fuera y se **desvanece al entrar** un jugador local
  (como RimWorld o Zomboid). Solo visual: no cambia la simulación en el MVP.
- **Luz**: tus lámparas iluminan tu base en la noche oscura (luces 2D de URP). Esto es lo que hace que una base "se sienta hogar".
- **Daño visible**: grietas en 3 niveles según la vida del bloque; reparar con martillo.

## 5. Que el diseño importe
Ya está en el núcleo: los zombis eligen entre rodear o romper según la vida de cada bloque, y la horda crece cada noche.
Lo que añade esta propuesta:
- **Ventanas y puertas como puntos débiles** que hay que reforzar (barricadas encima de la estructura: +vida, no ocupan capa nueva).
- **Reparar** entre noches, y la decisión de dónde gastar el material.
- La **mutación adaptativa** del GDD (Zapador) castiga los fuertes "muro gordo" y premia los diseños con trampas y pasillos.

## 6. Construir sobre tu barrio
El gancho propio de Cepa Cero:
- `osm2tiles` marca **ventanas** en las fachadas de los edificios del mapa (cada pocas casillas) y las puertas que ya tiene.
- Puedes **barricar** las ventanas y puertas de un portal real, **derribar** un tabique para unir dos locales,
  o cerrar una calle con coches y vallas para hacer un campamento.
- Los edificios del mapa ya tienen suelo y techo: fortificar uno es más barato que construir de cero. Elegir *dónde* es una decisión.

## 7. Cómo encaja en el código (cuando digas OK)
- **Núcleo:** `Chunk` gana dos arrays (`floor` y `object`, `ushort` de tipo + vida donde haga falta).
  `BlockRegistry` declara la capa de cada tipo. `BlockChange` añade 1 byte de capa (8 → 9 bytes).
  Los objetos de 2 casillas (cama, portón) ocupan dos celdas que se rompen juntas.
- **Reglas del anfitrión:** `PlayerIntent.Place` lleva capa y rotación; el arrastre llega como un lote de intenciones
  con límite por tick (anti-abuso) y el anfitrión coloca hasta donde alcance el material.
- **Ventanas** en el flow field: coste bajo (punto débil) y se ven a través para la percepción de los zombis.
- **Red:** protocolo v3 (capa en los deltas, snapshot de chunk más grande: ~1,5×). Medirlo con simbench.
- **Guardado:** v5, con migración (una partida v4 carga con las capas nuevas vacías).
- **Mapa:** `cepacero.map` v2 con ventanas en las fachadas (osm2tiles) y retrocompatible con v1.
- **Tests:** colocar y romper por capas, coste del arrastre, cama como reaparición, migraciones, y una prueba de
  aceptación: "una casa del mapa barricada aguanta 2 noches con el material de un día de saqueo".

## 8. Qué entra en el MVP
| Entra | Después |
| --- | --- |
| Tres capas | Varios pisos |
| 4 materiales × muro, puerta, ventana, barricada baja, valla, suelo | Pintar, formas especiales (esquinas, medias casillas) |
| Cama, lámpara/hoguera, barril, trampas, muebles básicos | Generador, electricidad, torretas, cultivos |
| Modo construcción con fantasma, arrastre, rotar, desmontar | Planos guardados |
| Muros que se unen, techos que se desvanecen, grietas | Iluminación por habitación, clima |
| Ventanas en los edificios de osm2tiles y barricadas | Interiores generados por tipo con habitaciones |

## 9. Orden de trabajo propuesto
1. **Modo construcción** sobre lo que ya existe (fantasma, arrastre, coste visible): mejora inmediata sin tocar el núcleo.
2. **Capas** en el núcleo + protocolo v3 + guardado v5 (lo caro; con tests).
3. **Ventanas y barricadas** (núcleo + osm2tiles v2).
4. **Objetos con función** (cama, lámpara, barril) + **luz 2D**.
5. **Muros que se unen, techos y grietas** (vista).

## Qué necesito de ti
1. **Capas:** ¿B (tres capas)? ¿O prefieres A para ir más rápido?
2. **Ventanas y barricadas** sobre los edificios reales como mecánica central: ¿sí?
3. **Cama como punto de reaparición** del grupo: ¿sí?
4. **Techos solo visuales** en el MVP (no afectan a la simulación): ¿te vale?
5. **Catálogo**: ¿te vale la lista de materiales, formas y objetos, o quieres añadir/quitar algo?
6. **Orden**: ¿empezamos por el modo construcción (paso 1), que se nota enseguida, o por las capas?
