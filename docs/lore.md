# Lore — La Cepa Cero

> Borrador 0.1. Todas las instituciones, empresas y personas son **ficticias**. En el juego no
> se usan nombres reales de hospitales, laboratorios ni marcas, aunque el mapa sea real.

## Idea central
**"Cepa" tiene doble sentido:** es la *cepa* de un patógeno y la *cepa* de una vid. La
infección nace en el campo aragonés, no en un laboratorio militar, y la extiende el **cierzo**.

## Origen
1. **La sequía.** Tras varios veranos sin lluvia en el valle del Ebro, la empresa ficticia
   **Agrogénica del Ebro** desarrolla una vid resistente a la sequía. Su secreto es un hongo
   simbionte modificado, el **CZ-0** ("Cepa Zero"), que vive en las raíces y retiene el agua.
2. **El salto.** El hongo se adapta demasiado bien: aprende a colonizar tejido animal húmedo.
   En humanos invade el sistema nervioso, apaga la corteza y mantiene vivo el tronco cerebral:
   hambre, agresividad y una tolerancia al dolor altísima.
3. **La dispersión.** En otoño el hongo esporula. El cierzo lleva las esporas de los viñedos
   experimentales a la ciudad. Primero hay alergias raras y después fiebre, desorientación y mordiscos.
4. **El laboratorio.** La empresa tenía un laboratorio en un polígono de las afueras y un convenio
   con un hospital de la ciudad para estudiar a los primeros afectados. Allí se aisló al
   **Paciente Cero**: un temporero que trabajó en los viñedos experimentales.

### Por qué los mutados son distintos
El hongo **se adapta a su huésped y a su entorno**. Eso explica las mutaciones en el juego:
- Los cuerpos fuertes generan masas musculares (La Mole).
- Las glándulas se hipertrofian: ácido (Escupidor) y cuerdas vocales (Gritón).
- **Adaptación al grupo:** si los supervivientes se encierran, la colonia "aprende" a romper muros (Zapador).
  Si hacen ruido, la colonia responde al sonido. Es la justificación en el mundo de la *mutación adaptativa*.
- El **verde luminoso** es bioluminiscencia del hongo: los mutados brillan de noche. Encaja con la dirección de arte.

## Cómo lo descubre el jugador
| Fuente | Dónde | Qué aporta |
| --- | --- | --- |
| **Notas** | Pisos, comercios, coches | Testimonios: los primeros días, rumores, el cierzo |
| **Grabaciones** | Contestadores, radios | Voces de la empresa y del hospital |
| **Documentos** | Laboratorio del polígono, oficinas | Informes técnicos del CZ-0, convenios, encubrimiento |
| **Muestras** | Restos de minijefes | Desbloquean recetas y una entrada del diario cada una |
| **Laboratorio** | Zona de progresión | El microscopio permite *analizar* muestras (antídoto, materiales) |

Las entradas se guardan en el **diario** del jugador. En cooperativo, el diario es **compartido**:
lo que encuentra un jugador lo leen todos (estado del grupo, sincronizado por el anfitrión).

## Primeras entradas del diario
Cada entrada es un bloque `yaml` que `tools/datagen` convierte en `data/generated/diary.json`.
Se desbloquea para **todo el grupo** cuando se cumple su `trigger`:

| `trigger` | Cuándo |
| --- | --- |
| `match_start` | Al empezar la partida |
| `first_container` | La primera vez que alguien abre un contenedor del mapa |
| `night_started:N` | Al empezar la noche N |
| `night_survived:N` | Al amanecer tras la noche N, con alguien en pie |
| `building_kind:K` | La primera vez que se abre un contenedor de un edificio de categoría K (osm2tiles) |
| `sample:ID` | Al analizar la muestra ID |

El texto va sin formato (lo pinta la UI del juego) y en español; la traducción irá a las tablas de localización.

```yaml
id: diary_001
source: Nota en una nevera
trigger: match_start
text: |
  "Lunes. Papá sigue con la tos del viñedo. Dice que es alergia al cierzo, que todos los años
  igual. Hoy ha mordido al perro. He cerrado la puerta de su cuarto con llave."
```
```yaml
id: diary_002
source: Mensaje de contestador
trigger: first_container
text: |
  "Soy Marta, del centro de salud. Si oye esto, no vaya a urgencias. Repito: no vaya a urgencias.
  Están llevando a todos al hospital y... (se oye un golpe) ...cierre las ventanas. Es el aire."
```
```yaml
id: diary_003
source: Folleto empapado
trigger: night_started:1
text: |
  "Agrogénica del Ebro. Viñas que no tienen sed. Con tecnología CZ-0, el futuro del campo
  aragonés crece aunque no llueva." Alguien ha escrito encima con rotulador: ASESINOS.
```
```yaml
id: diary_004
source: Diario del grupo
trigger: night_survived:1
text: |
  "Los que brillan son peores. Cuanto más verde, más listos. Esta noche han rodeado la barricada
  en lugar de empujar. Como si aprendieran."
```
```yaml
id: diary_005
source: Muestra de La Mole
trigger: sample:sample_brute
text: |
  Análisis: tejido muscular colonizado por micelio al 80 %. El hongo ha fabricado fibras
  parecidas a cables bajo la piel. Las fibras fallan con un impacto seco contra metal: se
  "desconectan" unos segundos. Receta desbloqueada: muro de hormigón.
```
```yaml
id: diary_006
source: Informe interno
trigger: building_kind:industrial
text: |
  "CONFIDENCIAL. Ensayo CZ-0/14: colonización de tejido animal confirmada en ratas expuestas a
  esporas en suspensión. Recomendación: suspender la floración en parcelas exteriores.
  Respuesta de dirección: mantener el calendario; la cosecha no puede esperar."
```
```yaml
id: diary_007
source: Grabación del hospital
trigger: building_kind:medical
text: |
  "Paciente cero. Varón, 34 años, temporero. Constantes estables aunque no responde a estímulos.
  Los demás infectados del ala se han quedado quietos desde que lo trajimos. Lo... miran.
  Todos hacia su habitación."
```
```yaml
id: diary_008
source: Muestra del Paciente Cero
trigger: sample:sample_zero
text: |
  Análisis: el micelio del paciente cero emite señales químicas que coordinan a los demás
  huéspedes. No es un individuo: es un nodo. Si hay un nodo, hay una red.
  Receta desbloqueada: antídoto experimental.
```

## Ganchos para más adelante
- **"Vota la siguiente ciudad":** la red de nodos explica que la cepa aparezca en otras ciudades.
- Facciones de supervivientes (post-MVP): trabajadores de la empresa que quieren recuperar las muestras.
- Recetas ligadas al lore: el antídoto reduce la infección por mordisco, si se añade esa mecánica.
