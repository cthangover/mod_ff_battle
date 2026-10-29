# FF Battle — combates con menú de comandos

FF Battle es un sistema de combate al estilo de las consolas RPG clásicas: el grupo abajo a la izquierda, los enemigos arriba y un menú de comandos — Attack, Items, Defend, Escape. Es el sistema de combate de RPG Plus. El combate en sí — el inicio, el fondo, las pantallas de victoria y derrota, la experiencia y el botín — lo pone Turn Battle Base; FF Battle es la manera de luchar del jugador.

## La pantalla de combate

El guerrero de RPG Plus se encuentra con un lobo en la aldea:

![La pantalla de combate de FF Battle: la carta del lobo arriba (31/31, ATK 7, DEF 2), la carta pequeña del héroe abajo a la izquierda (42/42, MP 6/6, DEF 4, un punto de acción amarillo), el panel Battle arriba a la izquierda (Round 1, Enemies 1, Party 1), y a la derecha los botones de velocidad y el registro de combate.](assets/ff-start.jpg)

- **Las cartas.** Cada personaje muestra la salud, el ataque y la defensa (DEF es un escudo que absorbe el daño primero), el maná si el juego lo tiene, y los puntos amarillos de acción (AP).
- **El panel Battle** — la ronda, cuántos enemigos y miembros del grupo quedan, y de quién es el turno.
- **El registro de combate** — cada acción y su resultado, lo más reciente abajo.

## Un turno

1. **Elige un personaje** — haz clic en su carta o pulsa **Enter**/**Space** para tomar al primero que tenga puntos de acción.
2. **Elige un comando** con el ratón o con **W/S**/las flechas y **Enter**:
   - **Attack** — las acciones de combate del personaje;
   - **Items** — objetos usables del inventario, 1 AP cada uno;
   - **Defend** — la acción de defensa del personaje sobre sí mismo;
   - **Escape** — 70% de probabilidad; si falla, el personaje pierde 1 AP.
3. **Elige la acción.** El panel junto al menú muestra la carta de acción y lo que hará:

![El menú de acciones del héroe — Basic attack (1P), Defence (1P), Power strike (1P), Back — y la carta de Basic attack con la pista «Daño 12 (ATK 12 × 1,0) · coste 1 AP».](assets/ff-actions.jpg)

4. **Haz clic en el objetivo.** Aparece «Selecciona un objetivo · RMB / Esc — cancelar» y las cartas enemigas se iluminan:

![La carta del lobo resaltada en rojo y la pista «Selecciona un objetivo · RMB / Esc — cancelar».](assets/ff-target.jpg)

Cuando el grupo gasta sus puntos de acción, los enemigos hacen su turno; después empieza una ronda nueva y los puntos se restauran.

## Lo que dice el registro

Tras un ataque básico y la respuesta del lobo:

![El registro de combate: Hero: Basic attack → Wolf; Wolf's shield absorbed 2 · left 0; Basic attack: Hero → Wolf · -10 HP; The party is out of AP; Enemy turn · round 1; Wolf: Defence.](assets/ff-log.jpg)

El ataque del héroe es 12. El escudo del lobo (2) absorbió los primeros 2 puntos y se agotó, así que el lobo perdió 10 de salud: 31 → 21. Después el lobo usó Defence y subió su escudo a 4 para el próximo golpe.

## Las acciones

El menú enumera las acciones que tiene el personaje (véase Characters Engine). Los tipos básicos los ejecuta el propio FF Battle:

- `Attack=1.5` — el daño del ataque multiplicado por el número; lo absorbe primero el escudo del objetivo;
- `Defence=4` — un escudo sobre un aliado;
- `Turn=2` — aturdimiento durante ese número de turnos;
- el uso de objetos.

Las acciones escritas como fórmulas, como el Power strike o el Fireball de Classic RPG Rules, funcionan mediante Battle Formulas; los costes de maná (`Cost=mp:4`) se pagan con el maná del personaje.

## Si algo sale mal

1. **El menú no se abre.** El personaje se quedó sin puntos de acción en esta ronda, o es el turno de los enemigos.
2. **Items aparece en gris.** No hay objetos en el inventario que se puedan usar en combate.
3. **Una habilidad no hace nada.** Sus valores necesitan Battle Formulas, o no está marcada como usable en combate.
