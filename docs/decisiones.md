# Registro de decisiones

Formato: una entrada por decisión, la más reciente arriba. Si una decisión se
revoca, no se borra: se añade una nueva entrada que la sustituye y se marca la
antigua como **Sustituida por D-XXX**.

| Estado | Significado |
| --- | --- |
| Aceptada | Vigente |
| Propuesta | Pendiente de OK de Javier |
| Sustituida | Ya no vigente (ver la que la reemplaza) |

---

## Pendientes de decidir
| Tema | Notas |
| --- | --- |
| ~~Tamaño de tile en píxeles~~ | Cerrado: 32 px (D-021, antes 16 px en D-018). Los metros por tile, en D-008 |
| Resolución base | **Decidida: 960×540** con escalado entero (D-021, OK de Javier 16/10/2026); la de D-018, 640×360, era para 16 px |
| ~~Direcciones de animación~~ | Cerrado: 4 (D-018) |
| ~~Paleta del mundo~~ | Cerrada: 81 colores diseñados a mano para el mundo a 32 px (D-021). Iconos e interfaz siguen con los 32 de D-018 |
| Fuente de la interfaz | Public Pixel (CC0, GGBotNet) en `Resources/Cepa/PublicPixel.ttf`, tamaños múltiplos de 8; decidido en D-024 |
| ~~Versión exacta de Unity~~ | Cerrado: 6000.3.26f1 (D-016) |
| Nombre definitivo | Comprobar marca, Steam y redes |

---

## D-024 · 17/10/2026 · Salto visual: arte de 32 px, tejados, luz real y sensación de juego (referencias: Core Keeper y Project Zomboid)
- **Estado:** Aceptada (OK de Javier el 17/10/2026: "dale a todo lo que has dicho", más "que no se pueda ver el interior de las casas sin entrar" y "Core Keeper y Project Zomboid son perfectos"). Concreta D-021 y sustituye a D-019 en el mundo.
- **Problema:** el prototipo se veía "plano, básico y feo": todo a la misma luz, sin jerarquía visual, suelos ruidosos, sin respuesta a los golpes y con la interfaz de depuración.
- **Decisión** (por orden de trabajo; el estado de cada punto está en `docs/guia-arte.md`):
  1. **Mundo con el arte de 32 px de `tools/spritegen`** (D-021), resolución base **960×540**. Los iconos y la interfaz siguen a 16 px (`tools/artgen`) con su propia escala. Muros que se unen, fachadas y remates, escaleras con orientación, subsuelo con la pared cortada.
  2. **Tejados en todos los edificios con interior**, también los de una planta: desde fuera son opacos y **no se ve el interior**; al entrar se desvanece **solo el tejado de tu casa** (como Project Zomboid). Lo que hay bajo un tejado cerrado (muebles, zombis, objetos) no se dibuja.
  3. **Iluminación por casillas con sombras** (calculada por código sobre el mundo, sin URP 2D lights): noche oscura de verdad, linterna del jugador, farolas, ventanas encendidas, hogueras y antorchas; los muros dan sombra.
  4. **Jerarquía visual**: entorno apagado y de menor contraste; jugador, zombis y objetos interactivos más contrastados y con contorno.
  5. **Sensación de golpe**: destellos, sangre, polvo al andar, sacudida de cámara.
  6. **Interfaz compacta**: mochila con tecla, mensajes breves y fuente pixel.
  7. **Sonido** de ambiente y de acciones.
  8. **Guía de arte** (`docs/guia-arte.md`) como referencia de todo lo anterior.
- **Motivo:** la luz y la jerarquía importan más que los sprites (Core Keeper se apoya en la oscuridad y en pocos colores con mucho contraste de valor); Project Zomboid resuelve la lectura de los edificios con tejados que se abren al entrar. El arte generado por código tiene techo: para un aspecto propio y memorable hará falta un artista, y se decidirá aparte.
- **Cambios en el núcleo:** `BuildingFloors` pone tejado a todo edificio con interior (antes solo a los de varias plantas); tests actualizados.
- **Coste:** la luz por casillas es propia (más trabajo que usar luces 2D de URP) pero encaja con el mundo por casillas, no depende de sombras por geometría y se puede probar.

## D-023 · 17/10/2026 · Puente de depuración: Claude juega el prototipo por `localhost`
- **Estado:** Aceptada (OK de Javier el 17/10/2026).
- **Decisión:** `PrototypeDebugBridge` (solo en el editor y en builds de desarrollo, `Debug.isDebugBuild`) escucha órdenes de texto en `http://localhost:7777/`. Las órdenes **escriben en `PrototypeInput`** las mismas pulsaciones que el teclado y el ratón (mover, apuntar, clic, mantener, arrastrar, Av Pág, Q/E, Ctrl+C…), así que recorren exactamente el camino de un jugador y el anfitrión las valida igual. Además: `walk` (camina por un camino calculado con la misma regla de colisión), `state` (JSON con jugador, mano, mochila, celdas cercanas, zombis y mensajes), `screenshot`, y trucos de depuración separados (`give`, `spawn`, `time`). Cuando no hay órdenes, no toca la entrada. Detalle y lista de órdenes en `tools/gamebridge/README.md`.
- **Motivo:** que Claude pueda probar el juego, ver capturas y comprobar resultados sin depender de que Javier lo maneje; sirve también para recorridos de regresión guionizados.
- **Seguridad:** solo acepta conexiones de la propia máquina y nunca existe en una build de publicación. Sin dependencias (usa `HttpListener` de .NET).

## D-022 · 16/10/2026 · Construcción avanzada: pisos y subsuelo, 14+ materiales, pintura, planos y construcción con la mano
- **Estado:** Aceptada (OK de Javier el 16/10/2026: orden de fases, niveles de −3 a +3, ruido de cavar y construcción "como Minecraft"). Amplía D-020 y **sustituye** su modo construcción con **B** (paso 1) por el modelo de abajo. Detalle en `docs/propuesta-construccion-avanzada.md`.
- **Decisión:**
  - **Altura:** niveles de **−3 a +3** (configurable hasta ±4): plantas altas y **subsuelo**, con **cavar** (pala para tierra, pico para roca) para casas subterráneas. Bajo tierra todo es macizo por defecto y solo se guarda lo excavado. **Cavar hace ruido y atrae a los zombis.**
  - **Construir con la mano:** no hay modo construcción. Se elige material o herramienta en la hotbar (9 huecos, mochila de 27) y **el clic hace la acción del objeto**: el arma ataca, el hacha tala, la pala y el pico cavan, el martillo desmonta y repara, el material **pone la pieza**. Clic derecho: interactúa primero; con material, quita. Forma con Q/E, color con [ ], rotar con R, cuentagotas con clic central, poner encima sustituye, habitación, planos y deshacer.
  - **Materiales:** 14 o más (palé, pladur, tablón, tronco, sacos de arena, chatarra, adobe, contenedor, ladrillo, piedra, hormigón, hormigón armado, acero, cristal) combinados con las formas; **16 colores de pintura**. Cada material tiene dureza y herramienta que lo rompe bien.
  - **Orden de trabajo:** A datos y arte · B núcleo (capas, color, nivel) · C red y guardado · D construcción con la mano (una planta) · E planos · F niveles para el jugador y cavar · G zombis multinivel · H mapa con plantas.
- **Avance:** fases A a H hechas (queda el metro). El mundo tiene niveles −3..+3 y 4 capas por casilla (estructura, suelo, techo, objeto) con byte de variante (color 0..16 + rotación). Red: protocolo v3; guardado v5 (lee v3/v4). La API 2D antigua sigue valiendo para el nivel 0. D: sin modo construcción; la mano decide el clic (`HandResolver`), Q/E forma, R girar, `[` `]` color, clic central copia, Ctrl+Z deshace (`UndoPlace`), martillo o clic derecho con material desmontan (`Dismantle`, devuelve la mitad), poner encima sustituye o repinta, Ctrl+arrastrar levanta una habitación; coste de partida normal/barato/gratis (`GameSettings.BuildCost`); barra de 9 y mochila de 27. Planos (E): `Blueprint` guarda por clave de bloque en un texto `.cepaplan` (compartible, validado al leer); pegar usa la misma cola de obra, así que el anfitrión cobra y valida pieza a pieza y el fantasma avisa de lo que falta. Niveles (F): el jugador tiene `Level`; el subsuelo es macizo mientras no tenga estructura ni suelo (`IsSolidAt`, solo se guarda lo excavado); `cave` es el suelo de lo cavado y `pit` la fosa que deja cavar hacia abajo. Escaleras (estructura) y trampillas (suelo) se usan con Re Pág / Av Pág; quien no pisa suelo cae y pierde 15 de vida por nivel. Cavar: lateral bajo tierra, o hacia abajo en suelo libre; pala para tierra, pico para roca (desde el tercer nivel), la herramienta equivocada va a un cuarto; hace ruido. Las plantas altas se construyen hacia fuera desde lo que ya sostiene. Protocolo v4, guardado v6. Zombis multinivel (G1-G2, `docs/propuesta-zombis-multinivel.md`): los zombis tienen nivel, un campo de flujo por nivel enlazado por escaleras (dos sentidos) y por fosas o trampillas abiertas (solo caída, daño 10, aturdidos 0,6 s). La trampilla cerrada se aporrea (cede según el material) y queda abierta; quien está en una planta alta cae si le quitan el suelo. El ruido lleva nivel: al nivel contiguo llega el 70 % del radio (`AdjacentLevelNoise`) y a dos niveles nada, así que cavar despierta a los de la calle. Protocolo v5. Mapas (H): `cepacero.map` v2 añade por edificio `levels` (de `building:levels`, o por tipo si falta), `underground` y `roof`; el lector sigue leyendo v1. `BuildingFloors` genera al importar, desde el interior hallado por las puertas: plantas altas con suelo, muros de ladrillo u hormigón con ventanas, un hueco de escalera sin bloquear la puerta, tejado en la planta de arriba y sótanos excavados bajo la huella con su escalera. Sin datos de construcción en el registro, el mapa queda como antes. Tejados con forma: la forma (vertiente, esquina, cresta, plano) no se guarda; `RoofShapes.At` la calcula de los tejados vecinos y la vista usa el sprite `roof_<material>_<forma>` de `spritegen`. Alcantarillas: `SewerNetwork` excava al importar un túnel de una casilla en el nivel −1 bajo el centro de cada calle larga, con una boca de alcantarilla de acero (trampilla cerrada) cada 16 casillas; los zombis tienen que romper el acero para seguirte. Al empezar, `PopulateMap` deja zombis sin alertar en alcantarillas y sótanos (`UndergroundZombiesPer1000Cells`, tope 40). Vista: el techo se ve desde fuera y se desvanece al entrar; desde una planta alta se ve el nivel de abajo, más oscuro, por los huecos (corte de niveles); F6 cambia el coste de construir. Las teclas cambian: F ahora levanta a un compañero (antes R), Q/E ya no usan ni abren, B desaparece.
- **Motivo:** que la gente pueda hacerse "auténticas locuras" de casas y que construir sea tan cómodo como pegar un hachazo. La construcción rica funciona en una planta antes de pagar el coste de la verticalidad.
- **Riesgo:** los niveles tocan núcleo, red, guardado, mapa y Unity a la vez; es lo más caro del proyecto. Se entrega por fases y se puede parar en cualquiera con el juego jugable.

## D-021 · 16/10/2026 · Arte del mundo a 32 px con `tools/spritegen` (sustituye en parte a D-018 y D-019)
- **Estado:** Aceptada (OK de Javier el 16/10/2026: "quedamos en 32 y construye la herramienta", tras ver las pruebas de 32 px y elegir pulir a 32 antes que subir a 48). **Sustituye** el tile de 16 px de D-018 y el arte de mundo y personajes de D-019 (Kenney) para lo que `spritegen` cubre.
- **Decisión:**
  - El mundo pasa a **32 px por casilla** (personajes de 32×48, La Mole de 64×96). Lo genera `tools/spritegen/` por código (Python + Pillow, determinista, con tests), con **los mismos nombres de sprite** que el atlas de `tools/artgen`, `ppu: 32` y un pivote en los pies de los personajes.
  - Cubre: suelos, los 12 bloques de hoy, decoración (coches, árboles, mobiliario…), marcas del suelo, jugadores (propio y compañero, caído) y los 8 zombis del bestiario en 3 vistas (la cuarta es espejo) y 4 fotogramas, con variantes de caminante.
  - `tools/artgen` **sigue** para iconos de objetos, interfaz y cursor (16 px): son otra cuadrícula y no se mezclan con el mundo.
  - **Resolución base:** con 32 px, 640×360 solo muestra 11 casillas de alto. **Decidido 960×540** (×2 a 1080p, unas 17 casillas); `PrototypeArt.BaseHeight` pasa de 360 a 540 al cablear el atlas de 32 px.
- **Consecuencias (trabajo en Unity, aún no hecho):** `PrototypeArt` debe cargar un segundo atlas para el mundo y usar su `ppu`; el pivote de los personajes pasa a los pies (`pivot_y` del JSON); la cámara de píxel perfecto pasa de 360 a la altura base nueva; las piezas de Kenney dejan de usarse para el mundo.
- **Motivo:** el arte de 16 px "se veía plano y sin personalidad". A 32 px hay sitio para anatomía, sombreado de 5 tonos, ropa y heridas, mutaciones visibles y siluetas propias por tipo de zombi.
- **Paleta:** cerrada y diseñada a mano, **81 colores** (`tools/spritegen/spritegen/palette.py`); cada píxel del atlas pasa a su color más cercano y un test lo comprueba. Cuantizar automáticamente estropeaba los acentos.
- **Límites conocidos:** las piezas de construcción de D-020 (4 materiales × muro con uniones automáticas, puerta, ventana, barricada baja, valla y suelo, más grietas y objetos) ya están dibujadas pero falta integrarlas en Unity. Steam pide declarar el uso de IA: este arte lo escribe una IA, así que conviene revisar la norma vigente de Valve antes de publicar.

## D-020 · 16/10/2026 · Construcción: tres capas, ventanas y barricadas, modo construcción
- **Estado:** Aceptada (OK de Javier el 16/10/2026, respondiendo a las preguntas de `docs/propuesta-construccion.md`).
- **Decisión:**
  - **Tres capas por casilla:** suelo, estructura y objeto (opción B). Toca núcleo (`Chunk`, `BlockRegistry`), red (protocolo v3: 1 byte de capa en los deltas) y guardado (v5 con migración).
  - **Ventanas en los edificios del mapa** (osm2tiles, `cepacero.map` v2) y **barricadas** sobre ventanas y puertas como mecánica central.
  - **Cama = punto de reaparición** del grupo.
  - **Techos solo visuales** en el MVP: se ven desde fuera y se desvanecen al entrar.
  - **Catálogo del MVP:** madera, chatarra, ladrillo y hormigón × muro, puerta, ventana, barricada baja, valla y suelo; cama, lámpara/hoguera, barril de lluvia, trampas y muebles básicos.
  - **Orden:** 1) modo construcción (fantasma, arrastre, coste visible, rotar, desmontar), 2) capas, 3) ventanas y barricadas, 4) objetos con función y luz 2D, 5) muros que se unen, techos y grietas.
- **Motivo:** que construir sea central y creativo (pilar 2), con la vista cenital de Core Keeper; el gancho propio es fortificar el barrio real. Detalle completo en la propuesta.

## D-019 · 16/10/2026 · Mundo con piezas CC0 de Kenney, transformadas por código
- **Estado:** Aceptada (OK de Javier el 16/10/2026: "Vamos con B", "Kenney como base, transformado a nuestra paleta y ambiente", "intégralo, añade la decoración procedural y los personajes en estilo Kenney"). Modifica D-018 en el origen del arte del mundo.
- **Decisión:**
  - Base del mundo: **Kenney Roguelike Modern City** e **Indoors** (CC0), y **Roguelike Characters** como referencia de estilo. Las hojas originales y sus licencias están en `art/vendor/kenney/`, **fuera de LFS** (unos 60 kB) para que la CI, que no baja LFS (D-011), pueda regenerar el atlas.
  - `tools/artgen` las transforma: selecciona piezas, dibuja en su estilo lo que falta (asfalto liso, agua, vías, muros de metal y hormigón, coches quemados, manchas, sangre, basura, cristales) y aplica a todo un **filtro** (−30 % de saturación, algo más oscuro, tinte verdoso).
  - **Muros en vista 3/4:** un muro muestra la fachada de ladrillo si al sur no hay otro muro o puerta; si no, su remate.
  - **Personajes y zombis por código** imitando el estilo de Kenney (cabeza cuadrada, cuerpo ancho, bordes oscurecidos), con las 4 direcciones y el paso de D-018, que Kenney no tiene.
  - **Decoración procedural** (`DecorPlanner`): coches aparcados y quemados en la calzada, farolas, papeleras, bancos, bocas de riego y buzones en la acera, árboles y arbustos en los parques, muebles dentro de los edificios según su tipo, alfombras y desgaste disperso. Es determinista por mapa (todos los jugadores ven la misma calle sin enviar nada) y **solo visual**: no bloquea el paso y deja libres las puertas.
  - Los iconos de objetos y la interfaz siguen con la paleta propia de 32 colores (D-018).
- **Motivo:** el arte 100 % por código se veía pobre ("cutre y feo"). El código hace bien lo sistemático (paleta, variantes, desgaste, colocación) y mal el dibujo de objetos; Kenney aporta el dibujo con licencia libre y el código lo hace nuestro.
- **Créditos:** CC0 no obliga, pero se acredita a Kenney en `art/vendor/README.md`.
- **Pendiente:** que la decoración bloquee el paso (requiere llevarla al núcleo), fuente pixel (falta el zip de Public Pixel) y arte final propio.

## D-018 · 15/10/2026 · Arte de la primera versión: 16 px, 640×360, 4 direcciones, generado por código
- **Estado:** Aceptada (OK de Javier el 15/10/2026: tile 16 px, 4 direcciones, arte generado por código, mundo e interfaz a la vez).
- **Decisión:**
  - Tile de **16 px**; resolución base **640×360** con escalado entero (cámara de píxel perfecto hecha a mano, sin depender del componente de URP).
  - **4 direcciones** de animación (abajo, arriba, izquierda; derecha es la izquierda espejada). Caminar de 4 fotogramas.
  - El arte de esta versión sale de `tools/artgen/` (Python + Pillow, ya usado en osm2tiles) con **paleta propia de 32 colores**, y se escribe en `CepaCero/Assets/Resources/Cepa/Art/` (un atlas PNG y su `atlas.json`). Se regenera, no se edita a mano. Mundo y personajes, e interfaz con iconos y barras, se hacen a la vez.
- **Motivo:** con 640×360 y 16 px caben ~22 casillas de alto: se ve una horda de 100 zombis. 4 direcciones cuestan la mitad de fotogramas que 8. Generar el arte por código es coste cero, reproducible y sin dudas de licencia.
- **Coste / límites:** es arte provisional de formas sencillas, no arte final; se sustituirá por dibujo a mano o encargo cuando se decida. Texto de la interfaz con la fuente por defecto hasta tener una fuente pixel (pendiente).

## D-017 · 15/10/2026 · Paquetes de Unity alineados con el editor 6000.3.26f1
- **Estado:** Aceptada (OK de Javier el 15/10/2026: "sube collab-proxy e Input System a versiones compatibles").
- **Decisión:** `com.unity.collab-proxy` pasa de 2.6.0 a **2.13.6** y `com.unity.inputsystem` de 1.12.0 a **1.20.0**, las versiones que el propio editor trae embebidas (`Editor\Data\Resources\PackageManager\Editor\`). Son las que la plantilla Universal 2D debía usar, pero con las anteriores el compilador de Unity daba errores (`ObjectInfo` ambiguo, `BuildTarget.ReservedCFE`).
- **Motivo:** el editor compila sin errores con las versiones que ya conoce; no depender de descargas externas ni de versiones que no se han probado con 6000.3.26f1.
- **Cómo revisar:** al subir de versión de editor (D-016) o de LTS, comparar de nuevo con los `.tgz` embebidos.

## D-016 · 15/10/2026 · Versión de Unity: 6000.3.26f1 (LTS)
- **Estado:** Aceptada (OK de Javier el 15/10/2026: "instala la última 6000.3.x LTS").
- **Decisión:** motor **Unity 6000.3.26f1** (última 6000.3.x LTS que ofrece el Hub), con IL2CPP y Documentation. Proyecto `CepaCero/` creado a partir de la plantilla Universal 2D del propio editor.
- **Motivo:** LTS con soporte hasta diciembre de 2027 (ver §1 de `docs/setup-unity.md`). 6.6 es más nueva pero no LTS. 6.0 LTS terminaba su soporte en octubre de 2026.
- **Coste:** Unity Hub intentó descargar por su cuenta 6000.6.6f1, que no es la versión del proyecto. No afecta al repo; hay que confirmar que la descarga quedó cancelada en el Hub (pestaña Downloads).

## D-015 · 15/10/2026 · Hambre y sed duras, configurables
- **Estado:** Aceptada (OK de Javier el 15/10/2026: opción C y ajuste de partida).
- **Decisión:** cada jugador tiene barras de **hambre y sed** (0-100). Ajuste de partida `Survival = Off | Soft | Hard`, **Hard por defecto**.
  - Ritmo: la comida dura **3 días** de juego y el agua **2** (con días de 15 min, unos 45 y 30 min reales). Lata y botella rellenan 40.
  - Barras vacías (Soft y Hard): sin comida la vida máxima baja al 75 %; sin agua la stamina se recupera a la mitad.
  - Solo Hard: sin comida pierdes 1 de vida cada 10 s y sin agua 1 cada 5 s, hasta caer (y solo, morir).
  - Caídos y muertos no gastan barras; al reaparecer vuelven llenas. Avisos al 25 % y al vaciarse.
  - Red: el anfitrión las calcula; su dueño las recibe en el mensaje `Vitals` (2 bytes, solo cuando cambia un 1 %). Protocolo v2, guardado v4 (lee v3 con las barras llenas).
- **Motivo:** dar peso al saqueo de día y a las tablas de botín de comida; el ajuste mantiene la partida accesible (pilar 4) para quien no lo quiera.
- **Alternativas:** A (sin barras) y B (castigan sin matar) quedan como `Off` y `Soft`. Propuesta completa en `docs/propuesta-hambre-sed.md`.

## D-014 · 15/10/2026 · Código de Unity en un paquete local (`com.cepacero.unity`)
- **Estado:** Aceptada (OK de Javier el 15/10/2026).
- **Decisión:** el código que usa Unity (vistas, entrada, después FishNet) vive en `packages/com.cepacero.unity`, junto al núcleo; `CepaCero/Assets/_Project/` queda para contenido (escenas, prefabs, sprites, audio, ScriptableObjects). Incluye ya un **prototipo de depuración** (`PrototypeGame`) que juega la simulación con cuadrados.
- **Motivo:** Unity Hub no crea proyectos en carpetas con archivos, así que el código no podía esperar dentro de `CepaCero/`; además separa código revisable de assets binarios y permite un asmdef claro (`CepaCero.Unity` → `CepaCero.Core`, `Unity.InputSystem`).
- **Alternativa:** mover los scripts a `Assets/_Project/Scripts/` con sus asmdefs (es solo mover archivos).

## D-013 · 15/10/2026 · Guardado sin zombis y zombis que no saben dónde estás
- **Estado:** Aceptada
- **Decisión:** las partidas guardadas no incluyen los zombis vivos; cargar de noche reinicia la horda de esa noche. Los zombis colocados de día empiezan sin saber dónde están los jugadores (vista y oído); la horda nocturna sí lo sabe.
- **Motivo:** guardados pequeños y robustos; el sigilo de día da sentido al ruido y a la luz (pilares 3 y 4).

## D-012 · 15/10/2026 · Objetos, inventario, botín y crafteo
- **Estado:** Aceptada (las 5 recomendaciones de `docs/propuesta-inventario.md`)
- **Decisión:** inventario por **huecos** (20 + 5 de acceso rápido; lo grande ocupa más); el botín **no reaparece** por defecto (opción de partida); **fuego amigo desactivado** por defecto (opción); al caer, un compañero puede **levantarte** (30 s para morir desangrado); al morir, la mochila se queda en el suelo y se reaparece al amanecer en el fuerte; **pistola** con poca munición en el MVP. Objetos, recetas y tablas de botín son datos en `docs/` (como el bestiario).
- **Motivo:** accesible pero con decisiones (pilar 4); la escasez y el ruido del arma de fuego dan tensión; levantar al compañero refuerza el cooperativo (pilar 1).
- **Añadido por Claude, confirmado por Javier el 15/10/2026:** (1) si cae todo el grupo a la vez, todos reaparecen al amanecer sin mochila; la opción `PermaDeath` hace que la partida termine. (2) **Recolección:** romper bloques da materiales (árbol → 4 de madera, muro de madera → 2, muro de metal → 2 de chatarra…, `Items/BlockDrops.cs`); lo que rompen los zombis no da nada. (3) Analizar una muestra (usarla) la consume y desbloquea sus recetas para el grupo. (4) Levantar a alguien: mantener la acción 3 s a menos de 1,5 tiles; vuelve con 30 de vida.

## D-011 · 15/10/2026 · CI con GitHub Actions
- **Estado:** Aceptada
- **Decisión:** `.github/workflows/checks.yml` ejecuta `scripts/check.sh` (tests de Python y del núcleo C#, datos generados al día) y simbench en cada push y PR, sin licencia de Unity. El informe de simbench se guarda como artefacto.
- **Motivo:** detectar regresiones (incluidas las de rendimiento) sin abrir Unity. Coste: minutos de GitHub Actions dentro del plan gratuito; checkout sin LFS para no gastar ancho de banda.
- **Más adelante:** builds de Unity (GameCI) y subida a Steam (SteamPipe) cuando haya proyecto; requieren licencia de Unity como secreto del repo y se decidirán aparte.

## D-010 · 15/10/2026 · Simulación sin motor en un paquete UPM local
- **Estado:** Aceptada (OK de Javier el 15/10/2026).
- **Decisión:** la simulación (mundo de bloques, mapa, flow fields, zombis, reglas) vive en `packages/com.cepacero.core/` como C# sin `UnityEngine` (`noEngineReferences`), C# 9 / .NET Standard 2.1. Unity la referencia con `file:` en `Packages/manifest.json`. Se testea con `dotnet test` (`tests/`) y con el Test Runner de Unity. Unity solo hace vista, entrada y transporte de red. Detalle en `docs/arquitectura.md`.
- **Motivo:** el estado del juego queda en datos con colas de cambios, que es justo lo que viaja por la red. Tests rápidos en local y en CI sin licencia de Unity. Rendimiento con arrays para cientos de zombis. Independiente de FishNet y de la plataforma.
- **Coste:** una capa de adaptadores en Unity (Tilemap ↔ BlockWorld, mensajes FishNet ↔ bytes del núcleo) y tipos propios (`Int2`, `Float2`) en lugar de `Vector2`.

## D-009 · 15/10/2026 · Dedicación: sin límite fijo de horas
- **Estado:** Aceptada
- **Decisión:** Javier dedica "las horas que hagan falta". El plan mantiene hitos semanales; si un hito acaba antes, se adelanta el siguiente, nunca se amplía el alcance del MVP.
- **Motivo:** el riesgo pasa de "falta de tiempo" a "crecimiento del alcance" y agotamiento, compatibilizando con el trabajo. Se revisa el ritmo real al terminar la semana 2.

## D-008 · 15/10/2026 · Formato de salida de osm2tiles (`cepacero.map` v1)
- **Estado:** Aceptada
- **Decisión:** JSON con cabecera (bbox, fecha de los datos OSM, atribución, `meters_per_tile`, tamaño), leyenda, dos capas planas fila a fila con la fila 0 al sur (`ground` = suelo, `blocks` = bloques del mundo) y una lista `buildings` con la categoría de cada edificio (sin nombres ni marcas). PNG de previsualización aparte. Por defecto, **2 m por tile**, configurable. Leyendas como listas (índice = id) y puertas como array plano, para que Unity lo lea con `JsonUtility` sin paquetes extra. Especificación en `tools/osm2tiles/README.md`.
- **Motivo:** las dos capas encajan con el modelo del juego (Tilemap visual + rejilla de bloques rompibles). JSON se lee en Unity sin dependencias. La fila 0 al sur coincide con el eje Y de Unity. Las categorías alimentan el botín.
- **Licencia:** el JSON es una base de datos derivada de OSM: se distribuye bajo **ODbL 1.0** con atribución "© OpenStreetMap contributors".

## D-007 · 15/10/2026 · Red: FishNet + FishySteamworks sobre Steamworks.NET
- **Estado:** Aceptada
- **Decisión:** librería de red **FishNet** (versión gratuita, no Pro). Transporte Steam **FishySteamworks** con **Steamworks.NET** (MIT). Para desarrollo local, el transporte **Tugboat** de FishNet (dos instancias en el mismo PC). AppID de pruebas 480 hasta tener AppID propio.
- **Motivo:** predicción en el cliente integrada (movimiento inmediato con latencia), pool de objetos, visibilidad por zonas y serialización compacta: encajan con 100+ zombis y un mundo por chunks. Licencia gratuita y sin royalties para juegos (revisada el 15/10/2026). Steamworks.NET sigue la API oficial de Valve.
- **Alternativas descartadas:** Netcode for GameObjects (mejor documentación, pero sin predicción integrada y más pesado por entidad); Mirror (maduro, pero con la predicción experimental). Facepunch.Steamworks (API cómoda pero menos mantenido).
- **Riesgos:** documentación más escasa (soporte por Discord) y dependencia de un autor principal. Mitigación: la arquitectura (autoridad del anfitrión, zombis por lotes, chunks con deltas) no depende de la librería, y el código de red se aísla en `CepaCero.Net`.

## D-006 · 15/10/2026 · Proyecto Unity en la subcarpeta `CepaCero/`
- **Estado:** Aceptada (convención de repo; reversible)
- **Decisión:** el repositorio contiene `CepaCero/` (proyecto Unity), `tools/` y `docs/` al mismo nivel.
- **Motivo:** separa el proyecto Unity de las herramientas Python y la documentación; Unity no
  importa nada fuera de su carpeta. El `.gitignore` usa patrones no anclados para que funcione en la subcarpeta.

## D-005 · 15/10/2026 · Convenciones de código: identificadores en inglés, docs en español
- **Estado:** Aceptada (reversible)
- **Decisión:** código y comentarios en inglés; documentación en español; textos del juego en tablas de localización (ES/EN).
- **Motivo:** las API de Unity y la documentación técnica están en inglés; evita mezclar idiomas en el código.

## D-004 · 15/10/2026 · Git + Git LFS para binarios
- **Estado:** Aceptada
- **Decisión:** imágenes, audio, vídeo, fuentes y binarios van por LFS (`.gitattributes`). Las escenas y prefabs de Unity como texto con UnityYAMLMerge.
- **Motivo:** los binarios inflan el historial de Git. Ojo: GitHub incluye 1 GB de almacenamiento LFS gratis; vigilarlo al llegar el arte.

## D-003 · 15/10/2026 · Cooperativo con anfitrión y Steam P2P; autoridad del anfitrión
- **Estado:** Aceptada (heredada del documento de contexto)
- **Decisión:** 1–4 jugadores; un jugador hace de anfitrión; conexión por Steam P2P/relay; sin servidores propios. El anfitrión es autoritativo.
- **Motivo:** coste cero de infraestructura y es lo que esperan los jugadores del género. La autoridad del anfitrión simplifica la sincronización de zombis y del mundo.

## D-002 · 15/10/2026 · Unity 6 + C# con 2D URP
- **Estado:** Aceptada (heredada del documento de contexto)
- **Decisión:** motor Unity 6, lenguaje C#, plantilla Universal 2D (URP).
- **Motivo:** ecosistema maduro para 2D con Tilemap y luces 2D (clave para la noche), varias librerías de red con transporte Steam y un camino conocido a móvil y consola.

## D-001 · 15/10/2026 · Concepto y alcance del MVP
- **Estado:** Aceptada (heredada del documento de contexto)
- **Decisión:** supervivencia zombi cooperativa cenital en pixel art, con bloques y mapas OSM. MVP: 2 especiales + 1 minijefe, 2 jugadores por Steam, 100 zombis en red y un barrio de Zaragoza.
- **Motivo:** hueco de mercado entre Project Zomboid y Core Keeper (ver contexto, §2).
