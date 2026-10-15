# Cepa Cero (nombre provisional): contexto del proyecto

Documento de traspaso para continuar el proyecto en otra sesión de Claude.
Actualizado: 15/10/2026. Autor: Javier Lasheras (Zaragoza).

## 1. Idea

- Videojuego para publicar en Steam: mezcla de Minecraft y Project Zomboid, cooperativo.
- Equipo: Javier + Claude como compañero de desarrollo (código, diseño, arquitectura, CI/CD).
- Javier es DevOps/SRE, con ventaja en automatización de builds, CI/CD y backend.

## 2. Estudio de mercado (resumen)

Documento completo: https://claude.ai/code/artifact/44d934d0-13db-404c-a454-d5fe79ca92ac

- Steam 2025: ~19.000 lanzamientos (~55 al día). El juego mediano de 2025 ingresó ~249 $, y el 66 % no llegó a 1.000 $.
- La supervivencia con crafteo es uno de los géneros indie mejor pagados (100.000–350.000 $ de mediana entre los indies con 100+ reseñas).
- El cooperativo es la mecánica con más tirón: Project Zomboid multiplicó sus ventas por 23 al añadir el modo online.
- Referentes: Minecraft, Terraria, 7 Days to Die, Project Zomboid, Valheim, Enshrouded y Core Keeper.
- Hueco detectado: entre Project Zomboid (simulación zombi profunda pero dura) y Core Keeper (cenital, con bloques y cooperativo): un "Zomboid accesible con construcción libre".
- Validación: apuntar a ~5.000 wishlists en los primeros meses. Por debajo, hay que replantear el gancho.

## 3. Posicionamiento

| Aspecto | Decisión |
| --- | --- |
| Género | Supervivencia zombi cooperativa |
| Jugadores | 1–4; un jugador hace de anfitrión y la conexión va por Steam P2P, sin servidores propios |
| Vista | Cenital, pixel art |
| Núcleo | Saquear de día, fortificar de noche, hordas que crecen cada noche |
| Construcción | Por bloques en cuadrícula: talar, picar, muros, trampas y puertas que los zombis pueden destruir |
| Progresión | Por zonas o noches con objetivos y minijefes (estilo Valheim) |
| Precio | 10–15 € en acceso anticipado |
| Plataformas | PC/Steam primero; port a móvil en una fase 2 (arquitectura preparada desde el inicio); consola en fase 3 |
| Personalización | Opciones de partida configurables desde el inicio; mods más adelante |

## 4. Zombis, mutaciones y lore

Las mutaciones tienen una causa en el mundo del juego (una cepa o un laboratorio), y el lore se descubre con notas, laboratorios y restos de los jefes.

| Nivel | Ejemplos | Papel |
| --- | --- | --- |
| Común | Caminante, Corredor | Presión constante en masa |
| Especial | Gritón (llama a la horda), Escupidor (ácido que corroe la madera), Acorazado (protegido de frente) | Obligan a cambiar de táctica y a cooperar |
| Minijefe | La Mole (derriba muros y se aturde contra el metal), Paciente Cero (en un hospital) | Objetivo de progresión, botín único y lore |

- Mutaciones que reaccionan a cómo juega el grupo: si fortifica mucho, aparecen zombis que rompen muros; si hace ruido, llegan más zombis.
- Los jefes sueltan muestras que desbloquean recetas (antídotos, muros de hormigón) y entradas del diario.
- Cada especial debe reconocerse por su silueta, su color y su sonido.
- Alcance del MVP: 2 especiales + 1 minijefe.

## 5. Mapas: ciudades reales

- Fuente: datos de OpenStreetMap convertidos a tiles. Hay que revisar la licencia ODbL (cita de la fuente y condiciones sobre los datos derivados).
- Escala comprimida y estilizada: barrios reconocibles y monumentos reducidos. Sin logotipos ni nombres de marcas.
- Primera ciudad: Zaragoza (Delicias, Actur, Casco...). Idea de comunidad: "vota la siguiente ciudad".

## 6. Dirección de arte

- Estilo elegido: pixel art cenital tipo Core Keeper, porque encaja con talar, picar y construir.
- Seña de identidad: iluminación nocturna dramática tipo Darkwood (linternas, fuego y el verde de los mutados sobre un mundo casi negro).
- Por definir: tamaño de tile (16 o 32 px), resolución base (por ejemplo 640×360 escalado sin deformar), 4 u 8 direcciones de animación, paleta cerrada de unos 32 colores y cómo se ven los interiores (techos que desaparecen al entrar).
- Los mockups de UI que se hicieron se descartaron por verse "muy IA": https://claude.ai/artifact/H8pVtnLoNpN6mABzXj9yT1

### Herramientas de IA para el arte

| Herramienta | Uso | Precio aprox. (sept. 2026) |
| --- | --- | --- |
| PixelLab | Personajes y zombis animados en 4–8 direcciones; tilesets | Prueba gratis; desde 12 $/mes |
| Retro Diffusion | Tiles y objetos, extensión de Aseprite | 20–65 $, pago único |
| Scenario | Entrenar un modelo con estilo propio | Gratis limitado; desde 15 $/mes |
| Aseprite | Retoque obligatorio de todo lo generado | 19,99 $ |

- Un artista para la cápsula de Steam, el logo y los minijefes.
- Steam exige declarar el contenido generado con IA (formulario actualizado en enero de 2026). Las herramientas internas, como asistentes de código, no se declaran.
- Revisar los derechos y las condiciones de uso comercial de cada herramienta.

## 7. Decisiones técnicas

- Motor: Unity (alternativa: Godot).
- Cooperativo diseñado desde el primer día: inventario, construcción e IA pensados para la red.
- Repositorio Git con Git LFS y copias de seguridad.
- Textos fuera del código; español e inglés desde el inicio.
- CI/CD de builds y subida automática a Steam (SteamPipe).

## 7b. Entorno de trabajo con Claude (LLM)

### Cuenta: personal, no la de empresa
- Con el plan Enterprise, la organización puede acceder al contenido de las sesiones de Claude Code (prompts, respuestas, llamadas a herramientas), de los chats y de Cowork mediante la Compliance API. También puede exportar los chats y configurar la telemetría (OpenTelemetry) para que registre el texto de los prompts.
- Decisión: desarrollar Cepa Cero con una cuenta personal de Claude (email personal), en un ordenador y un repositorio propios, sin herramientas de la empresa. Así se evita también reforzar una posible reclamación de propiedad intelectual por parte del empleador.
- La memoria de Claude de la cuenta de empresa no se traslada a la personal: este fichero es el traspaso.

### Herramientas
| Herramienta | Uso |
| --- | --- |
| Claude Code (terminal o VS Code) | Herramienta principal de desarrollo sobre el repositorio: C#, tests, commits |
| Claude chat / Cowork | Diseño, GDD, lore, balance, investigación de mercado, textos de Steam |
| Unity MCP (oficial, beta abierta para Unity 6) | Permite a Claude Code leer la jerarquía de la escena, crear y modificar objetos, editar scripts y leer los errores de la consola. Requiere un proyecto conectado a Unity Cloud y la suscripción o prueba de la beta |

### Configuración de Claude Code
- `CLAUDE.md` en la raíz: memoria del proyecto (resumen, pilares, decisiones técnicas, convenciones, forma de trabajar). Conciso, con referencias a `docs/`.
- `docs/`: GDD, este fichero de contexto y `decisiones.md` (registro de decisiones con fecha y motivo).
- Skills y subagentes propios para tareas repetitivas, por ejemplo "crear un zombi nuevo con IA, prefab y entrada en el bestiario" o "revisar el netcode".
- Modelos: el más capaz para arquitectura, netcode, el proceso OSM → tiles y depuración difícil; uno más rápido para tareas mecánicas. La lista actual está en https://docs.claude.com/en/docs/about-claude/models
- Plan: para un uso intensivo, una suscripción personal suele ser más predecible que pagar por API. Revisar los límites de uso de cada plan.

### Hábitos
- Tareas pequeñas y concretas; Javier prueba en el editor y pasa los errores (o Claude los lee por MCP).
- Commits frecuentes.
- Actualizar `CLAUDE.md` y `docs/decisiones.md` con cada decisión importante.

### Programa Claude Startups
- Existe (https://claude.com/programs/startups): hasta 45.000 $ en ofertas de empresas asociadas y créditos de API (hasta 100.000 $ adicionales a través de fondos de inversión asociados).
- A 15/10/2026 la convocatoria está cerrada. Los créditos requieren una empresa de menos de 4 años con inversión institucional, y solo sirven para la API de Claude Console, no para suscripciones.
- Reconsiderarlo si Cepa Cero se convierte en una sociedad o consigue inversión.

### Prompt de arranque para la primera sesión de Claude Code
Requisitos previos: proyecto Unity 6 vacío creado y este fichero copiado en `docs/cepa-cero-contexto-proyecto.md`.

```
Hola. Vamos a empezar el desarrollo de mi videojuego "Cepa Cero" (nombre provisional).
Tú y yo somos el equipo: yo pruebo, decido y juego; tú programas, diseñas la arquitectura y documentas.

CONTEXTO
Lee primero docs/cepa-cero-contexto-proyecto.md. Ahí está todo lo decidido:
- Supervivencia zombi cooperativa (1-4 jugadores) en vista cenital y pixel art.
- Construcción por bloques en cuadrícula y zombis mutados con minijefes.
- Mapas basados en ciudades reales (OpenStreetMap), empezando por Zaragoza.
- PC/Steam primero, con la arquitectura preparada para un port a móvil.

ENTORNO
- Unity 6, C#. El proyecto ya está creado en esta carpeta.
- Cooperativo: anfitrión + Steam P2P, sin servidores propios.
- Soy DevOps/SRE: me manejo bien con Git, CI/CD y automatización, pero soy nuevo en Unity y en desarrollo de juegos. Explícame las decisiones específicas de Unity.

OBJETIVO DE ESTA PRIMERA SESIÓN (todavía no es gameplay)
1. Revisa el proyecto y el documento de contexto. Dime qué te falta o qué ves arriesgado antes de tocar nada.
2. Propón la estructura de carpetas del proyecto (scripts, prefabs, tiles, datos, docs) y la arquitectura base pensada para el cooperativo desde el día 1. Elige el paquete de red y justifica la elección (por ejemplo Netcode for GameObjects frente a Mirror/FishNet con transporte Steam).
3. Configura Git: .gitignore para Unity, Git LFS para assets (png, wav, etc.) y .gitattributes.
4. Crea un CLAUDE.md en la raíz, conciso, con:
   - El resumen del juego y los pilares de diseño.
   - Las decisiones técnicas tomadas y las pendientes.
   - Las convenciones de código y de nombres.
   - Cómo trabajamos: tareas pequeñas, commits frecuentes, yo pruebo y te paso los errores.
   - Una referencia a docs/ para el detalle, sin duplicarlo.
5. Crea docs/GDD.md, de una página, a partir del documento de contexto, y docs/decisiones.md como registro de decisiones (fecha, decisión, motivo).
6. Propón el plan del prototipo dividido en hitos semanales, con formas básicas y sin arte final:
   - Movimiento cenital y cámara.
   - Rejilla de bloques (colocar y romper).
   - Ciclo día/noche con una horda que crece.
   - Zombis básicos con pathfinding.
   - Cooperativo de 2 jugadores.
   - Prueba de estrés: 100 zombis + 2 jugadores en red.
   - Prueba de conversión OSM → tiles de un barrio.

REGLAS
- Antes de cambios grandes o decisiones de arquitectura, propónmelo y espera mi OK.
- Pasos pequeños; después de cada uno, dime exactamente qué tengo que probar en el editor.
- Todo lo que toque estado del juego (inventario, construcción, zombis) debe pensarse para la red, aunque al principio juguemos en local.
- Mantén CLAUDE.md y docs/decisiones.md actualizados cuando decidamos algo.
- No añadas dependencias ni assets de pago sin preguntarme.
- Si el MCP de Unity está disponible, úsalo para leer la escena y los errores de la consola.

Empieza por el punto 1.
```

## 8. MVP / prototipo (3–4 meses, con formas básicas antes del arte)

1. Mapa pequeño con 2–3 tipos de edificio para saquear.
2. Ciclo día/noche con una horda que crece cada noche.
3. Construcción de muros y trampas por bloques.
4. Cooperativo de 2 jugadores por Steam.
5. Prueba técnica: 100 zombis + 2 jugadores en red.
6. Prueba de conversión OSM → tiles de un barrio de Zaragoza.
7. Página "Coming Soon" en Steam y un tráiler de 30 segundos.

## 9. Publicación en Steam (recordatorio)

- Steam Direct: 100 $ por juego, recuperables al llegar a 1.000 $ de ingresos.
- Espera de 30 días tras el primer pago. La página "Coming Soon" debe estar publicada al menos 2 semanas antes del lanzamiento (mejor, meses antes).
- Reparto de ingresos: 70/30 (75/25 a partir de 10 M$ y 80/20 a partir de 50 M$).

## 10. Pendientes antes de diseñar

- [ ] Decir cuántas horas a la semana puede dedicarle (dato para planificar).
- [ ] Crear una cuenta personal de Claude y un repositorio propio, y llevar allí este fichero.
- [ ] Instalar Unity 6 y activar el MCP de Unity (prueba de la beta).
- [ ] Suscribirse a la newsletter de Claude Startups por si reabre la convocatoria.
- [ ] Elegir el giro temático y el nombre definitivo (comprobar marca, Steam y redes).
- [ ] Escribir un GDD de una página con las decisiones técnicas cerradas.
- [ ] Revisar las cláusulas de exclusividad y propiedad intelectual del contrato laboral.
- [ ] Consultar a un gestor cómo cobrar de Steam (autónomo o sociedad) cuando se acerque el lanzamiento.
- [ ] Probar PixelLab (gratis) con un Caminante y un Gritón, y retocarlos en Aseprite.
- [ ] Abrir redes y devlog pronto para empezar a acumular wishlists.

## Siguiente paso propuesto

Preparar el GDD de una página y empezar el prototipo con formas básicas.
