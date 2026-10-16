# Traspaso a la sesión local (16/10/2026)

Rama: `claude/lucid-mayer-vdw0gl` (todo subido; los tests del núcleo y de las herramientas pasan desde la sesión de la nube). Decisión de referencia: **D-022** y `docs/propuesta-construccion-avanzada.md`, `docs/propuesta-zombis-multinivel.md`.

## Qué trae la rama
Construcción avanzada completa (fases A-H): 14 materiales y pintura, capas y niveles −3..+3, construir con la mano (hotbar de 9, Q/E forma, R girar, `[` `]` color, clic central copia, Ctrl+Z), planos `.cepaplan` (Alt+arrastrar, Ctrl+C/V/S/L), subir/bajar (Re Pág / Av Pág), caídas, cavar con pala y pico, zombis multinivel, `cepacero.map` v2 con plantas y sótanos. Protocolo de red v5, guardado v6 (lee v3-v5). Núcleo: 267 tests.

## Lo que NO está en el repo y hay que hacer en local
1. **Atlas con LFS** (desde la nube no se puede subir): `cd tools/artgen && python -m artgen` (iconos e interfaz, 16 px; hoy `scripts/check.sh` falla en ese paso hasta que se suba) y `cd tools/spritegen && python -m spritegen` (mundo a 32 px, sale a `CepaCero/Assets/Resources/Cepa/Art32/`). Subirlos con `git lfs`.
2. **Ficheros `.meta`** de los `.cs` nuevos (Unity los genera al abrir el proyecto): `Runtime/Building/*` (incluida la carpeta), `BuildingFloors`, `BuildCostMode`, `BlockBuildInfo`, `BlockVariant`, `LevelRules`, `PrototypePlaceholders` y los tests nuevos. Commitearlos.
3. **Mapa real v2:** `python -m osm2tiles --zone delicias-test` (necesita OSM o la caché) y copiar el `.map.json` donde lo lea `RepoData`.
4. **Probar en el editor** (nada de esto se ha ejecutado en Unity, solo compilado contra firmas imitadas): Test Runner (267 tests), y el recorrido de abajo.

## Recorrido de prueba en Unity
1. Hotbar: 1 bate, 2 hacha, 3 pistola, 4 vendas, 5 comida, 6 martillo, 7 pala, 8 pico, 9 madera. Clic izquierdo hace lo que pida la mano.
2. Con madera: clic pone, arrastra línea, Mayús contorno, Ctrl habitación; Q/E cambia la forma (escalera, trampilla…).
3. Pala sobre suelo libre: varios clics y sale una fosa. Av Pág baja, Re Pág sube. Cavar de lado bajo tierra; pico desde el tercer nivel.
4. Escalera + Re Pág: subir; construir suelo hacia fuera; quitar un suelo bajo los pies para caer.
5. Alt+arrastrar, Ctrl+C, Ctrl+V (R gira), Ctrl+S, Ctrl+L.
6. Zombis: caen por fosas, bajan y suben por escaleras, aporrean trampillas cerradas.

## Trabajo de Unity pendiente (por orden)
- Cargar el atlas de 32 px (`Art32`, `ppu` 32, pivote en los pies con `pivot_y`) y fijar la resolución base (propuesta 960×540, pendiente de OK de Javier). Sustituir `PrototypePlaceholders` por los sprites de `spritegen` (muros con uniones, mapas de máscaras `wall_<m>_top_N`, cristal con alfa, tejados, escaleras, subsuelo).
- Cámara con corte de niveles (hoy solo se ve el nivel propio, los demás más oscuros).
- Menú del ajuste de coste de construcción (`GameSettings.BuildCost`: normal, barato, gratis) y del libro de piezas.
- Inventario/mochila de 27 huecos en la interfaz (hoy el HUD dibuja la barra de 9).
- Metro y alcantarillas (fase H, sin hacer) y zombis propios de plantas y sótanos de edificios.

## Prompt para pegar en la sesión local
> Estás en el proyecto Cepa Cero. Haz `git fetch` y `git checkout claude/lucid-mayer-vdw0gl`. Lee `CLAUDE.md` y `docs/traspaso-sesion-local.md`. Haz, en este orden: (1) abre Unity, deja que genere los `.meta` y commitéalos; (2) regenera y sube con LFS los atlas de artgen y spritegen; (3) pasa `scripts/check.sh` entero y el Test Runner de Unity (267 tests) y dime qué falla; (4) haz el recorrido de prueba y pásame los errores de consola. No empieces trabajo nuevo sin proponerlo antes.
