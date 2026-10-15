# Cepa Cero (nombre provisional)

Supervivencia zombi cooperativa (1–4 jugadores) en vista cenital y pixel art, con
construcción por bloques, zombis mutados con minijefes y mapas de ciudades
reales a partir de OpenStreetMap (primera ciudad: Zaragoza).

**Estado:** preproducción y prototipo con formas básicas. Unity 6 + C#, PC/Steam primero.

## Estructura

| Ruta | Contenido |
| --- | --- |
| `CepaCero/` | Proyecto Unity (se creará con Unity Hub; ver `docs/setup-unity.md`) |
| `tools/osm2tiles/` | Conversor OpenStreetMap → rejilla de tiles (Python) |
| `docs/` | GDD, decisiones, bestiario, lore, plan y guías |
| `CLAUDE.md` | Memoria del proyecto para Claude Code |

## Requisitos

- Git + [Git LFS](https://git-lfs.com) (`git lfs install` una vez por máquina).
- Unity 6 (versión exacta en `docs/setup-unity.md`).
- Python 3.11+ para `tools/`.

## Datos de mapas

Los mapas derivan de datos de © [OpenStreetMap contributors](https://www.openstreetmap.org/copyright),
disponibles bajo la licencia ODbL.

## Licencia

Código y assets propios: todos los derechos reservados (pendiente de decidir).
