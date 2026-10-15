# Propuesta: hambre y sed

**Estado: decidida en D-015 (`docs/decisiones.md`): opción C (dura) por defecto, con el ajuste Off/Soft/Hard.**
Lo que sigue es la propuesta original; los valores finales están en D-015 (el agua dura 2 días, no 3).

## El problema
Hambre y sed dan motivo para salir de día y hacen que el botín importe. El riesgo es la gestión aburrida:
Zomboid es lento a propósito y nuestro pilar 4 es "accesible pero profundo".
Además, en cooperativo un jugador hambriento frena a todo el grupo.

## Opciones

| | A · Nada | B · Suave (recomendada) | C · Dura |
| --- | --- | --- | --- |
| Qué es | La comida solo cura | Dos barras que bajan; si están vacías **penalizan, nunca matan** | Al estilo Zomboid: si están vacías, quitan vida hasta matarte |
| Motivo para saquear | Bajo: solo materiales | Medio: el súper y las fuentes valen la pena | Alto, pero manda sobre todo lo demás |
| Castigo | — | Sin sed: la stamina se recupera a la mitad. Sin hambre: −25 % de vida máxima mientras dure | Pierdes vida poco a poco |
| Interfaz | Nada | Dos iconos pequeños que solo se ven por debajo del 30 % | Barras siempre visibles |
| Coste de red | 0 | 2 bytes por jugador, solo a su dueño | Igual que B |

## Recomendación: B, con su ajuste en `GameSettings`
- `Survival = Off | Soft | Hard`, **Soft por defecto**. Con eso las tres opciones quedan cubiertas sin tres juegos distintos:
  Off para partidas rápidas y Hard para quien quiera Zomboid.
- Ritmo: una barra llena dura **unos 3 días de juego** (unos 45 minutos reales con los días por defecto). Una lata o una botella rellenan el 40 %.
  Así se come una vez por día sin agobio.
- La noche no gasta más: el miedo lo pone la horda, no la barra.
- Valores en `docs/objetos.md` (`food: 40`, `water: 40` en cada objeto), así que se ajustan sin tocar código.
- **Agua de lluvia y grifos** (más adelante): recoger agua en un barril construido, para dar algo que hacer en el fuerte.

## Cómo encaja en el código (cuando digas OK)
- `PlayerState` gana `Food` y `Water` (bytes, 0-255). El anfitrión los baja en `GameSession.Step` por ticks y en el modo `Off` no hace nada.
- Comer y beber usan la intención `Use` que ya existe; el objeto declara `food`/`water` y `HostItemRules` lo aplica.
- Red: los valores viajan en el mensaje `Inventory` del dueño, no en `PlayerStates`: los compañeros no necesitan verlos.
  Esto cambia el protocolo a la versión 2 y el guardado a la v4, con migración (una partida v3 se carga con las barras llenas).
- Tests: que las barras bajen al ritmo previsto, que el castigo se aplique y se quite, que el modo Off no toque nada, y la migración del guardado.

## Qué necesito de ti
1. ¿A, B o C? (o B con otro ritmo o castigo)
2. ¿Te vale el ajuste Off/Soft/Hard en la configuración de partida?
