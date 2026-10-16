# Propuesta: zombis multinivel (D-022, fase G)

Estado: **aceptada con ajustes (Javier, 16/10/2026)**. Ajustes: la fosa y la trampilla abierta son verticales, así que los zombis **solo caen** por ellas (no trepan; daño de caída ×0,7) y vuelven a la calle por escaleras; la trampilla cerrada se aporrea y se rompe según el material; esconderse bajo tierra no debe ser seguro. Hoy los zombis viven solo en la calle (nivel 0): el subsuelo y las plantas altas son refugios seguros (a propósito, hasta esta fase).

## 1. Qué hay hoy
- `ZombieSimulation` guarda posición 2D y un solo `FlowField` (un Dijkstra desde los objetivos; coste independiente del número de zombis). Se reconstruye si cambia `StructureRevision` (solo estructuras del nivel 0) o se mueven los objetivos.
- `GameSession` solo pasa a los zombis los jugadores con `Level == 0`.
- La instantánea de red (8 B por zombi) no lleva nivel; ruido y golpes tampoco.

## 2. Diseño propuesto
1. **Nivel por zombi** (`sbyte`, 0 por defecto). Movimiento, colisión y línea de visión usan ese nivel (`IsSolidAt`, `HasClearLine(..., level)`, ya existen).
2. **Un campo de flujo por nivel con jugadores o con ruido**, creado al vuelo y soltado al dejar de usarse (no 7 campos permanentes: en un mapa de 1000×1000 cada uno cuesta ~100 MB).
   - Objetivos del campo del nivel *z*: los jugadores de *z* **y los conectores de *z* que llevan a un nivel con objetivos**, sembrados con el coste que tiene ese otro nivel desde el aterrizaje. Así un zombi de la calle "ve" la escalera o la fosa como camino hacia quien está abajo.
   - Se construyen en orden: primero los niveles con jugadores, luego los vecinos hacia fuera. En la práctica 1–3 campos.
   - Hay que añadir a `FlowField` la siembra con costes iniciales distintos de 0 (hoy todos los objetivos valen 0).
3. **Conectores para zombis:** escaleras en los dos sentidos; **fosa y trampilla abierta solo hacia abajo, cayendo** (daño ×0,7, sin vuelta por ahí; en el campo de flujo son aristas dirigidas con coste de caída, así que prefieren escaleras). Trampilla cerrada: Una trampilla cerrada cuenta como pieza que romper, así que **es el cuello de botella** de un búnker (tiene vida y herramienta como cualquier pieza). Cambiar de nivel es instantáneo al pisar el conector, con un enfriamiento corto.
4. **Los zombis no cavan** (ya decidido). Si el subsuelo no tiene conector con la calle, no pueden entrar. Si un suelo de planta alta se rompe, **un zombi encima cae** y se hace daño como un jugador.
5. **Ruido con nivel:** el ruido se emite en un nivel con su radio completo y **a la mitad en los niveles contiguos** (ya se amortigua a la mitad para pasos bajo tierra). Cavar (radio 5–9) atrae a los de la calle: van al conector más barato hacia el sitio del ruido. Cuando se cava hacia abajo el aviso llega con la proyección en la calle.
6. **Red:** la instantánea de zombis gana 1 byte de nivel (8 → 9 B, +12 %) y solo se envían los del **mismo nivel que el espectador** (menos tráfico, no más). Protocolo v5. La réplica del cliente guarda el nivel.
7. **Aparición:** la horda sigue saliendo en la calle. Los zombis de dentro de edificios en plantas altas y los de metro o alcantarillas llegan con la fase H (`building:levels` de OSM).
8. **Presupuesto:** se mide con `tools/simbench` (100 y 300 zombis repartidos en 3 niveles). Límite: no más de un 15 % de coste de tick sobre hoy con todos en la calle.

## 3. Trabajo, en pasos (un commit por paso)
| Paso | Qué | Tests clave |
| --- | --- | --- |
| G1 ✔ | Nivel en la simulación y la red, línea de visión y golpes por nivel (hecho: protocolo v5, 9 B por zombi, solo se envían los del nivel del espectador) | un zombi no golpea a quien está en otro nivel; la instantánea lleva el nivel |
| G2 ✔ | `FlowField` con siembra y un campo por nivel con portales (hecho: escaleras en los dos sentidos, caída por fosa y trampilla abierta, trampilla cerrada aún no es camino) | un zombi baja por una fosa hasta un jugador; sube por unas escaleras |
| G3 ✔ | Ruido con nivel y cavar atrae (hecho: al nivel contiguo llega el **70 %** del radio, no la mitad, para que esconderse bajo tierra no sea fácil; a dos niveles no llega) | cavar a 6 casillas bajo la calle hace que un zombi vaya a la fosa |
| G4 ✔ | Trampilla cerrada como cuello de botella; caída de zombis (hecho: la aporrean según material —madera cede, acero aguanta mucho más— y queda abierta; los zombis de plantas altas caen si les quitan el suelo) | zombis rompen la trampilla; el suelo roto los deja caer con daño |
| G5 ✔ | `simbench` multinivel y ajuste (hecho: `--levels`) | el coste de tick está dentro del presupuesto |

## 4. Decisiones que necesito de Javier
1. **¿Los zombis usan escaleras y fosas?** Recomiendo **sí**: si no, una fosa en el patio es un búnker perfecto sin esfuerzo.
2. **¿La trampilla cerrada los frena?** Recomiendo **sí** (es la forma de tener un búnker seguro, pagando una pieza fuerte y reparándola).
3. **¿Caen los zombis cuando se rompe un suelo?** Recomiendo **sí** y que se hagan daño: es una trampa que el jugador puede preparar.
4. **¿Ruido contiguo a la mitad?** Recomiendo **sí**; si te parece demasiado fácil esconderse abajo, se sube a 0,7.
5. **Fuera de esta fase:** zombis que trepan por fuera, que abran puertas, o que cavan. No entran.

## 5. Riesgos
- Rendimiento del campo por nivel en mapas grandes: por eso es perezoso y acotado por `FlowMaxDistance`.
- Un bunker sin ninguna salida a la calle deja de ser peligroso. Lo asumimos: para eso están los conectores.
- `StructureRevision` hoy mira solo el nivel 0; hará falta una por nivel para saber qué campo reconstruir.

## 6. Medición (G5, `simbench --levels`, 60 s, 3 jugadores en niveles 0 / −1 / +1, mapa sintético de 375×335)
| Zombis | Solo calle: media / p95 | Multinivel: media / p95 | Campos | Memoria gestionada |
| --- | --- | --- | --- | --- |
| 100 | 0,32 / 0,45 ms | 0,26 / 0,34 ms | 3 | 9,9 MB |
| 300 | 0,51 / 0,79 ms | 0,43 / 0,97 ms | 3 | 9,9 MB |

Presupuesto del tick: 50 ms; el p95 queda por debajo del 2 % en los dos casos. Una primera versión reconstruía los campos cada vez que un zombi cruzaba una escalera (p95 de 1,8 y 4,2 ms); ahora la firma solo mira qué niveles tienen zombis. En un mapa de 1000×1000 cada campo de nivel pesa unos 24 MB (seis tablas de enteros por casilla), no los 100 MB que estimé al principio; con 3 niveles son ~72 MB. Los campos se crean al vuelo y se conservan; si hiciera falta se liberan los que lleven un rato sin usarse.
