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
Formato pensado para datos: `id`, `fuente`, `desbloqueo`, `texto`.

---
**`diary_001` · Nota en una nevera · inicio de la partida**
> "Lunes. Papá sigue con la tos del viñedo. Dice que es alergia al cierzo, que todos los años
> igual. Hoy ha mordido al perro. He cerrado la puerta de su cuarto con llave."

---
**`diary_002` · Mensaje de contestador · primer edificio saqueado**
> "Soy Marta, del centro de salud. Si oye esto, no vaya a urgencias. Repito: no vaya a urgencias.
> Están llevando a todos al hospital y... (se oye un golpe) ...cierre las ventanas. Es el aire."

---
**`diary_003` · Folleto empapado · calle**
> *"Agrogénica del Ebro. Viñas que no tienen sed. Con tecnología CZ-0, el futuro del campo
> aragonés crece aunque no llueva."* Alguien ha escrito encima con rotulador: **ASESINOS**.

---
**`diary_004` · Diario del jugador · primera noche superada**
> "Los que brillan son peores. Cuanto más verde, más listos. Esta noche han rodeado la barricada
> en lugar de empujar. Como si aprendieran."

---
**`diary_005` · Muestra de La Mole · al derrotarla**
> *Análisis:* tejido muscular colonizado por micelio al 80 %. El hongo ha fabricado fibras
> parecidas a cables bajo la piel. Las fibras fallan con un impacto seco contra metal: se
> "desconectan" unos segundos. **Receta desbloqueada: muro de hormigón.**

---
**`diary_006` · Informe interno · laboratorio del polígono**
> "CONFIDENCIAL. Ensayo CZ-0/14: colonización de tejido animal confirmada en ratas expuestas a
> esporas en suspensión. Recomendación: suspender la floración en parcelas exteriores.
> Respuesta de dirección: *mantener el calendario; la cosecha no puede esperar*."

---
**`diary_007` · Grabación del hospital · ala de aislamiento**
> "Paciente cero. Varón, 34 años, temporero. Constantes estables aunque no responde a estímulos.
> Los demás infectados del ala se han quedado quietos desde que lo trajimos. Lo... miran.
> Todos hacia su habitación."

---
**`diary_008` · Muestra de Paciente Cero · post-MVP**
> *Análisis:* el micelio del paciente cero emite señales químicas que coordinan a los demás
> huéspedes. No es un individuo: es un **nodo**. Si hay un nodo, hay una red.
> **Receta desbloqueada: antídoto experimental.** *(Gancho para la siguiente ciudad.)*

## Ganchos para más adelante
- **"Vota la siguiente ciudad":** la red de nodos explica que la cepa aparezca en otras ciudades.
- Facciones de supervivientes (post-MVP): trabajadores de la empresa que quieren recuperar las muestras.
- Recetas ligadas al lore: el antídoto reduce la infección por mordisco, si se añade esa mecánica.
