# Cepa Cero — GDD de una página
*Versión 0.1 · 15/10/2026 · nombre provisional*

**Frase gancho:** *"Defiende tu barrio de verdad."* Supervivencia zombi cooperativa en
tu propia ciudad: saquea Delicias de día y aguanta la noche en el fuerte que construiste bloque a bloque.

| | |
| --- | --- |
| **Género** | Supervivencia zombi cooperativa con construcción |
| **Jugadores** | 1–4 (anfitrión + Steam P2P) |
| **Vista y arte** | Cenital, pixel art; noches muy oscuras con luz dinámica |
| **Plataforma** | PC/Steam (acceso anticipado, 10–15 €); móvil y consola después |
| **Referentes** | Project Zomboid (amenaza), Core Keeper (vista y bloques), Valheim (progresión por jefes), Darkwood (luz) |
| **Motor** | Unity 6 + C# (2D URP) |

## Pilares
1. **Cooperar o morir:** los especiales castigan al que va solo.
2. **Tu fuerte, tu ciudad:** todo se construye y se rompe, sobre calles reales.
3. **La noche da miedo:** se ve poco, se oye mucho.
4. **Accesible pero profundo:** se aprende en 10 minutos; las opciones de partida dan profundidad.

## Bucle principal
```
DÍA (saquear)  →  ATARDECER (preparar)  →  NOCHE (resistir)  →  AMANECER (reparar y repartir)
 explorar edificios     construir muros,        horda creciente,      botín, muestras,
 recoger recursos       trampas y luces         especiales y jefes    nuevas recetas
```
- **Corto plazo (minutos):** buscar, pelear o huir, gestionar ruido y luz.
- **Medio plazo (una noche):** decidir dónde y cómo fortificar con lo que hay.
- **Largo plazo (zonas):** limpiar barrios, vencer minijefes, desbloquear recetas y lore.

## Mecánicas clave
- **Bloques:** talar, picar y colocar en cuadrícula. Materiales con vida (madera < metal < hormigón). Puertas y trampas. Los zombis atacan lo que les bloquea el camino.
- **Ruido y luz:** disparar, romper y el fuego atraen zombis; la luz te protege pero te delata.
- **Horda creciente:** cada noche hay más zombis y más especiales; *mutación adaptativa*: si el grupo fortifica mucho, aparecen zombis que derriban muros; si hace ruido, llegan más.
- **Especiales y minijefes:** cada uno se reconoce por su silueta, su color y su sonido, y exige una respuesta táctica (ver `bestiario.md`).
- **Muestras:** los jefes sueltan muestras que desbloquean recetas (antídoto, hormigón) y entradas del diario (ver `lore.md`).
- **Ciudad real:** mapas generados desde OpenStreetMap a escala comprimida, sin marcas ni nombres comerciales.

## Progresión
Por zonas (barrios) y noches. Cada zona tiene un objetivo (llegar al hospital, encender la
radio, vencer a La Mole) que abre la siguiente. Estilo Valheim: el jefe es la puerta.

## Alcance del MVP (3–4 meses, formas básicas)
Un barrio de Zaragoza · 2–3 tipos de edificio saqueable · ciclo día/noche con horda creciente ·
muros, puerta y trampa por bloques · 2 comunes + 2 especiales (Gritón, Escupidor) + 1 minijefe
(La Mole) · cooperativo de 2 por Steam · 100 zombis en red estable · página "Coming Soon" y tráiler de 30 s.

**Fuera del MVP:** vehículos, granjas, clima, mods, más ciudades, móvil.

## Métrica de éxito
~5.000 wishlists en los primeros meses tras abrir la página de Steam. Por debajo, replantear el gancho.
