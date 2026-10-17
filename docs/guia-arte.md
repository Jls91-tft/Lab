# Guía de arte

Decidida en D-024 (17/10/2026). Referencias: **Core Keeper** (luz y oscuridad, pocos colores con mucho contraste de valor,
construcción en cuadrícula) y **Project Zomboid** (los edificios se leen desde fuera por su tejado y se abren al entrar;
atmósfera de ciudad abandonada). Es la regla para decidir cómo se ve cualquier cosa nueva.

## Lo que queremos que sienta quien juega
1. **Se ve poco y se oye mucho.** De noche la luz es un recurso: la que llevas, la que construyes y la que te delata.
2. **Un golpe de vista lee la escena**: dónde estoy, qué es peligroso, qué puedo usar.
3. **Una ciudad que fue de alguien**: cosas cotidianas rotas, no un decorado de pixel art vacío.
4. **Tu fuerte se nota**: lo que construyes tiene que verse bien y dar ganas de seguir construyendo.

## Reglas
### Valor (claro/oscuro) antes que color
- Tres planos de valor: **fondo** (suelos, tejados, fachadas: medios y de poco contraste), **medio** (muros, mobiliario) y **primer plano**
  (jugador, zombis, objetos que se pueden usar: los más contrastados y con contorno oscuro).
- Lo que es peligroso o interactivo no puede perderse entre el ruido del suelo. Si hay duda, bajar el contraste del fondo, no subir el del resto.
- Suelos con **poco ruido** y formas grandes; el detalle va en el mobiliario y en las marcas (sangre, manchas, basura), repartido con calma.

### Luz
- La noche es **oscura**: sin luz propia casi no se ve. El día lleva un tinte cálido a mediodía y frío al amanecer y al atardecer.
- Fuentes de luz: linterna (cono), farolas, ventanas de casas, hogueras, antorchas, barriles con fuego y las lámparas que construyas.
- **Los muros dan sombra** y las puertas y ventanas dejan pasar luz. Dentro de una casa sin luz se ve solo lo que está a tu lado.
- Los mutados brillan en verde (lore): es el único verde vivo del juego (ver paleta).

### Tejados e interiores (D-024)
- Todo edificio con interior tiene tejado. **Desde fuera nunca se ve el interior.** Al entrar, solo se abre el tejado de esa casa
  (con un fundido corto) y lo que hay dentro aparece; al salir, se cierra.
- Lo que hay bajo un tejado cerrado (muebles, objetos, zombis, otros jugadores) no se dibuja.
- Las plantas altas se ven con el tejado más alto; bajo tierra, sin tejados.

### Escala y cámara
- Casilla de **32 px**; personajes de **32×48** (La Mole, 64×96); resolución base del mundo **960×540** con escala entera (sin píxeles a medias).
- La interfaz usa su propia cuadrícula de 16 px y su escala, para que el texto y los iconos no dependan del zoom del mundo.

### Paleta
- Mundo: la paleta cerrada de **81 colores** de `tools/spritegen/spritegen/palette.py`. Los únicos verdes vivos son los de la cepa.
- Interfaz: los 32 colores de `tools/artgen/artgen/palette.py`.
- Cambiar el ambiente de todo el juego se hace tocando la paleta, no sprite a sprite.

### Sensación de juego
- Cada acción tiene respuesta: golpe (destello y chispas o sangre), construir (polvo y pequeño "asiento" de la pieza), romper (escombros),
  recibir daño (sacudida y viñeta roja), pasos (polvo suave al correr).
- Pocas partículas, cortas y legibles. Nada que tape al jugador.

### Interfaz
- Compacta y casi siempre cerrada: vitales y reloj arriba, barra de 9 abajo; la mochila se abre con una tecla; los mensajes son una línea que se desvanece.
- Fuente pixel con soporte de español (pendiente de elegir: `Public Pixel`, CC0, comprobar á é í ó ú ñ ¿ ¡).

### Sonido
- Ambiente de ciudad vacía (viento, lejanía) que cambia de día a noche; pasos según el suelo; golpes, disparos y construcción con sonido propio;
  gruñidos de zombi con dirección. El miedo lo pone el sonido tanto como la luz.

## Origen del arte
Mundo, personajes y zombis: **por código** (`tools/spritegen`, `tools/artgen`), deterministas y regenerables. Es arte provisional con calidad de prototipo
cuidado; para un aspecto propio y memorable se necesitará un artista (decisión aparte). Piezas de terceros: `art/vendor/` (D-019).

## Estado (17/10/2026)
| Punto | Estado |
| --- | --- |
| Arte de 32 px cableado, 960×540 | En curso |
| Tejados por edificio, interior oculto | En curso |
| Luz por casillas con sombras | Hecho (primera pasada: farolas, hogueras, casas, linterna con L; falta calibrar y lámparas construibles) |
| Jerarquía de valor y suelos limpios | En curso (zombis brillan en verde en la oscuridad; faltan contornos y suelos más limpios) |
| Golpes, partículas, cámara | Hecho (destello al golpear, sangre y manchas, sacudida al recibir daño; faltan polvo y niebla) |
| Interfaz compacta y fuente pixel | Hecho (mochila con Tab, mensajes sin caja que se desvanecen, fuente Public Pixel CC0)
| Sonido | Hecho en código (primera pasada, sin oír: pasos, golpes, disparo, gruñidos, viento, cuerno de noche); falta ajustarlo de oído |
