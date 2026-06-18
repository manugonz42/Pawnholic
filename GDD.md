# GDD — Pawn Shop Incremental (título provisional)

> Documento vivo. Es el mapa del proyecto: lo actualizamos según evoluciona el diseño.
> Unity 6000.3.17f1 · URP 2D · Primer videojuego del autor · Objetivo: alcance abarcable, terminable, con ayuda de IA.
> **Duración objetivo: ~2h para completarlo (máximo). Incremental FINITO de contenido, con final — no endless/grind.**

---

## 1. Pitch

Empiezas siendo un don nadie **limpiando y reparando joyas y relojes ajenos** por una tarifa.
Con lo que ganas, aprendes a **tasar, detectar fallos y regatear**, y escalas hasta montar tu
propio imperio de casas de empeño. Un **incremental con skill**: la base es segura y se automatiza,
pero cada peldaño nuevo exige maña, no solo esperar.

## 2. Tono y personalidad

- **Cómico-gamberro, divertido y alegre**, con un **mal rollo de fondo** puntual (el tío que empeña
  la alianza fingiendo que "es un regalo"; el yonki nervioso; el trilero).
- Clientes exagerados y caricaturescos, comentarios chulescos, situaciones absurdas.
- La comedia nace de la tensión: objetos turbios, timos, y decisiones moralmente grises servidas con humor.

## 3. Decisión de diseño clave: incremental-first, gestión como capas

**No es "incremental simple" O "gestión": es un incremental cuyo esqueleto es terminable, y las
mecánicas de gestión entran después como capas activas desbloqueables (contenido, no cimientos).**

Motivo: es el primer juego del autor y debe ser abarcable. El esqueleto incremental se puede tener
jugable en semanas; cada mecánica de gestión se añade sin reescribir el núcleo, reduciendo el riesgo
de *scope creep* (la causa nº1 de que los primeros proyectos no se terminen).

## 4. Loop central

```
ACCIÓN BASE (segura)        →  CAPA ACTIVA (skill)         →  META (incremental)
limpiar / reparar              tasar, detectar fallos,        ahorrar → desbloquear
(clic manual → se automatiza)  regatear compra/venta          mercados, locales, skills
```

- **Red de seguridad:** limpiar/reparar **nunca se agota**. Si te arruinas, vuelves al trapo y la lupa.
  No hay game over: hay "regreso humillante" (en tono cómico).
- **Activo → idle:** la acción base empieza manual (minijuego ligero) y se automatiza al contratar
  empleados / comprar maquinaria → liberas tiempo para las capas con skill.

## 5. Escalera de progresión

```
SERVICIO            →  COMPRAVENTA               →  IMPERIO
limpiar/reparar         detectar valor,              gestionar locales,
de otros                comprar barato/vender caro   empleados, especular

PUNTOS DE COMPRA (se pagan para desbloquear):
  Calle  →  Mercadillo  →  [comprar PC]  →  Webs online  →  Subastas
  (chollos    (volumen,      (acceso          (catálogos       (pujas,
   y basura)   timos)         remoto)          filtrables)      bluffing)

LOCALES (estatus):
  cuartucho  →  puesto  →  tienda  →  PAWNSHOP propio  →  cadena
```

Cada puerta nueva **se paga** (entrada al mercadillo, comprar el PC, suscripción a webs, licencia de
subastas). Da metas de ahorro claras = motor del incremental.

## 6. Árbol de skills

| Skill | Qué hace | Riesgo que reduce |
|---|---|---|
| **Tasación** | estrecha el rango de valor estimado | comprar caro algo que no vale |
| **Detección de fallos** | revela defectos ocultos / si es falso | que te la cuelen |
| **Mano firme** (limpieza/reparación) | ↑ % de éxito | **romper la pieza** y perderla |
| **Labia** | mejor margen al regatear; muestra más "tells" | que el cliente se largue |
| **Olfato** | ↑ % de aparición de chollos | perder tiempo en basura |

El **% de romper la pieza** convierte limpieza/reparación en una mini-apuesta. Subir *Mano firme* la baja → progresión palpable.

## 7. Negociación (capa activa, se desbloquea tras el servicio)

Base: subir/bajar oferta en %. **Sin barra de paciencia que baje con el tiempo** (nada de estrés por
reloj). El único castigo es **pasarse de los límites del cliente**: si aprietas más allá de su tope
oculto, se larga. Un cliente que se va **tarda más en volver** (penalización con memoria, no game over).
Tres capas para darle personalidad:

1. **Número** — la oferta en sí (margen real depende de tu Tasación).
2. **Arquetipos con precio tope secreto** — cada cliente tiene un **precio límite oculto** y una
   personalidad (Desesperado, Sabelotodo, Trilero, Pija, Yonki...). Durante la negociación se van
   **revelando detalles/"tells"** (cómicos: sudan, fingen indignación, se les escapa info) que
   delatan **qué arquetipo es** y por tanto **cuánto se le puede apretar** y cuánto subir/bajar el
   precio sin que se largue. Subir **Labia** revela más tells. Leer el bluff = la skill real.
3. **Banter / piquito** — frases chulescas que mueven paciencia o sacan info, pero arriesgan ofender.

**Subastas** = versión "final boss": pujas contra rivales con sus propios tics, presupuesto y manías. Bluffing y sniping.

## 8. Núcleo incremental (lo que hay que implementar bien)

**Incremental FINITO** (~2h, con final), no endless. La duración la da el **contenido diseñado**
(mercados, skills, items), no muros exponenciales de grind. Curvas suaves, pensadas para llegar al
final en ~2h.

- **Moneda + generadores**: empleados/maquinaria que producen ingreso pasivo por segundo.
- **Curvas de coste** moderadas: `coste = base * growth^poseídos` (growth bajo, p.ej. ≈ 1.07–1.12),
  tuneadas para el objetivo de 2h, no para estirar.
- **Automatización en gradiente**: empieza activa (haces clic/minijuegos) y el esfuerzo se va
  reduciendo hasta quedar casi al mínimo en el tramo final.
- **Producción idle / offline**: ingreso = tasa * tiempo transcurrido (con tope offline).
- **Upgrades** que multiplican clic/producción.
- **Prestigio: DESCARTADO por defecto** (va contra el objetivo de 2h). Como mucho, un único
  "vender el negocio" como **final/clímax** opcional, no como bucle. Reconsiderar solo si al testear
  el juego diera para mucho más de 2h.
- **Guardado/carga** (serialización) y manejo de **números** (probablemente no hacen falta números
  gigantes dado el alcance finito).

## 9. Roadmap por fases (cada fase = algo jugable)

- **Fase 0 — Esqueleto incremental (MVP). ✅ HECHO (2026-06-16).** Limpiar = clic → dinero, mejora
  "Mejor trapo" con curva de coste, "Ayudante" que automatiza (ingreso/seg), guardado en disco con
  progreso offline. Scripts: `Assets/Scripts/GameData.cs` + `Assets/Scripts/PawnShopGame.cs`
  (la UI se construye por código). GameObject `GameManager` en `SampleScene`. → *Ya es un juego.*
- **Fase 1 — Servicio completo.** *(En curso)* ✅ **Limpieza por FROTADO táctil GRADUAL** (mantén
  pulsado y frota; la suciedad no se borra de golpe, va aclarándose poco a poco bajando su alpha;
  al 90% limpio cobras y sale otro objeto). ✅ **3 objetos pixel art** (anillo, reloj, gema), cada
  uno con su silueta de suciedad y su valor. ✅ **Fondo pixel art** de tienda nocturna (`PixelArtFactory`).
  ✅ **DOS MONEDAS**: **EXP** (se gana trabajando) → habilidades. **DINERO** (al completar) → comprar cosas.
  ⏸️ **REPARACIÓN de relojes — DESACTIVADA por ahora (2026-06-17).** Era una barra de ajuste de
  precisión (pulsa en la zona verde, 4 piezas, riesgo de ROMPER). Al autor **no le convencía la
  mecánica**, así que se pausa: el trabajo a mano queda **solo en limpieza**. El código se conserva
  intacto (`IniciarReparacion`/`MoverMarcador`/`IntentarAjuste`) para retomarla o sustituirla por una
  mecánica táctil (encajar piezas) más adelante. *(Decisión: §11.)*
  ✅ **Skills de EXP**: Fuerza de frotado, Tamaño de cepillo, **Mano firme** (de momento sin uso activo
  al pausar reparación; vuelve cuando se retome).
  ✅ **MÁQUINA DE PULIDO** (sustituye al antiguo "Ayudante"; **se COMPRA**, hito ~70 dinero, no de inicio): **arrastras el
  OBJETO del banco** (o clicas la máquina) y entra en una **ranura**; la máquina lo pule sola con el
  tiempo → **DINERO** (sin EXP). Es una vía **idle LENTA**: **30 s/pieza** de base (2026-06-17), que baja
  con las mejoras de *Velocidad de pulido* del árbol (hasta ~6 s al máximo). El objeto es arrastrable
  mediante una capa invisible encima; frotar (ratón crudo, dentro del objeto) y arrastrar (fuera, hacia
  la máquina) conviven sin chocar; aparece un "fantasma" al sacar el cursor. Mejoras con dinero:
  **velocidad** y **nº de ranuras** (hasta 4).
  Es la vía idle/dinero; trabajar a mano sigue siendo la vía de EXP. La máquina es un **elemento
  aparte** (a la derecha del panel) con **sprite pixel art detallado** (128×128, fondo transparente:
  cuerpo, rueda de pulido, tolva, pantalla con las ranuras, mandos, luz). *(Nota: el drag necesita test
  del usuario; hay fallback clicando la máquina.)*
  ✅ **ITEMS COMO SCRIPTABLEOBJECTS** (2026-06-17): cada objeto es un `ItemDef` (asset editable en
  Inspector) con nombre, `FormaItem` (forma del pixel art), valorDinero, recompensaExp, flags
  `limpiable`/`reparable` y descripción. Se cargan con `Resources.LoadAll<ItemDef>("Items")` (sin
  cablear nada); hay *fallback* en código si no existen los assets. Los píxeles se generan por forma
  (`PixelArtFactory.CrearForma`) y se cachean. Añadir un objeto = crear un asset, no tocar código.
  Assets en `Assets/Resources/Items/` (Anillo, Reloj de bolsillo, Gema). → Prepara Fase 2 (valor de
  compra, falsificable, defectos irán en el mismo SO).
  ✅ **FEEDBACK / JUICE** (2026-06-17): textos flotantes que suben y se desvanecen (+dinero al cobrar
  limpieza/reparación/pulido, +EXP al acertar pieza, "¡ROTO!" al romper) y **flash** sobre la estación
  (verde acierto, rojo rotura, ámbar fallo). La **rueda de pulido gira** mientras la máquina trabaja
  (overlay giratorio superpuesto sobre su alojamiento del sprite). *(Necesita test de interacción.)*
  ✅ **SONIDO procedural** (2026-06-17): `AudioFactory` sintetiza los SFX por código (sin assets):
  frote (roce con pitch aleatorio), cobro (arpegio caja registradora), acierto (blip que sube de tono
  por pieza), rotura (crujido descendente), compra de mejora (moneda). `AudioSource` en el GameManager.
  ✅ **OBJETOS TURBIOS** (2026-06-17): `ItemDef` gana `turbio`, `fraseAlAparecer` y `frecuencia` (peso
  de aparición; turbios = 0.35, más raros). Al llegar el objeto se muestra su frase (tono cómico); los
  turbios se resaltan en rojizo y valen más. Sin mecánica nueva, solo personalidad. Assets turbios:
  Alianza 'de regalo', Reloj que no abras, Gema 'caída de un camión'. → 6 items en Resources/Items.
  ✅ **CLIENTES CON PERSONALIDAD** (2026-06-17): `ClienteDef` (ScriptableObject): nombre, **arquetipo**
  (Normal, Desesperado, Sabelotodo, Trilero, Pija, Yonki), paleta de cara, frases (saludo/contento/
  enfadado) y propina. **Cara pixel-art procedural** por arquetipo (`PixelArtFactory.CrearCara`):
  gafas/bigote/perlas/sudor/lagrimón. El cliente trae el objeto y saluda (a la **izquierda** del banco;
  máquina a la derecha); al servir **a mano** reacciona y **a veces deja propina** (la Pija mucha; el
  Desesperado/Sabelotodo poca), luego un "beat" de ~1,1 s y entra el siguiente. Los **objetos turbios**
  los "dice" el cliente (frase turbia en rojizo). 6 clientes en `Resources/Clientes` (+ fallback en
  código). → Base lista para el **regateo de Fase 3** (arquetipos con precio tope secreto).
  ✅ **FONDO del usuario** (2026-06-17): `fondo.png` (mostrador nocturno con flexo) movido a
  `Assets/Resources/`; el juego lo carga por código con *fallback* al fondo procedural.
  *(Pendiente)* más siluetas de pixel art, afinado de números, ¿pulido offline?, pasada de diseño (la
  imagen de fondo se estira al aspect ratio ancho; ajustar en la pasada visual).
- **Fase 2 — Compraventa básica.** Comprar en la Calle, tasar (rango), vender. Skills Tasación + Olfato.
- **Fase 3 — Negociación.** Clientes con paciencia, arquetipos, tells, banter. Skill Labia.
- **Fase 4 — Mercados.** Mercadillo → PC → webs online. Desbloqueos de pago.
- **Fase 5 — Imperio + final.** Locales por estatus, subastas, y un cierre/clímax ("vender el
  negocio" opcional) que da por completado el juego. Sin bucle de prestigio.

## 10. Notas técnicas (Unity)

- **Datos como ScriptableObjects**: items, arquetipos de cliente, definiciones de skill/upgrade, mercados.
- Separar **lógica de datos** (números, estado) de **presentación** (UI/escena) desde el principio.
- UI con uGUI o UI Toolkit (a decidir). Mucho del juego es UI: priorizar layout limpio.
- Guardado: JSON serializable. Idle: guardar timestamp y calcular delta al volver.

## 10c. Árbol de mejoras (sistema data-driven)

Las mejoras viven en un **árbol por ramas** que se abre en una **ventana aparte** (botón "ABRIR ÁRBOL
DE MEJORAS"). Sistema **data-driven** (`NodoMejora`: id, nombre, rama, prereq, moneda, coste base+
crecimiento, nivelMax, getNivel/subir) → añadir nodos es trivial. Cada nodo puede requerir otro
(`prereq`) y subir de nivel hasta `nivelMax`.

**Ramas actuales (3):**
- **MANUAL (EXP):** Fuerza de frotado → (Tamaño de cepillo, Mano firme).
- **MÁQUINA (dinero):** **Comprar máquina de pulido** (raíz/hito) → Velocidad de pulido → Ranuras /
  Baúl → Cinta → Brazo (auto-pulir) → Tolva.
  La cadena Baúl→Cinta→Brazo es la **automatización**: con el Brazo, la máquina se auto-alimenta sola
  (idle); la Tolva acelera ese auto-alimentado.
- **COMPRA-VENTA (próximamente):** Mercadillo → Tasación → Subastas (bloqueadas; la mecánica es Fase 2+).

Ampliar = añadir más nodos y/o ramas. El diseño visual de la ventana es provisional.

## 10d. Balance / economía (1ª pasada — 2026-06-17, a playtestear)

Objetivo de ritmo acordado: **Fase 1 ≈ 40-50 min** (la limpieza+máquina pesa media partida);
la **máquina es un hito de compra breve (~2-3 min a mano, coste 70 dinero)**, NO está de inicio.

- **Estructura de progresión:** (A) solo a mano → ganas EXP + algo de dinero; (B) **compras la
  máquina (70)** → vía idle: la alimentas y subes velocidad/ranuras; (C) automatización
  (Baúl→Cinta→Brazo→Tolva) → la máquina se auto-alimenta y el esfuerzo baja al mínimo. Luego Fase 2.
- **La máquina está oculta hasta comprarla** (`GameData.maquinaComprada`); el nodo raíz de la rama
  Máquina es "Comprar máquina de pulido". Todo lo demás de la rama cuelga de ese nodo.
- **Curvas suaves, sin muros exponenciales** (coherente con incremental finito): se bajaron techos
  (`nivelMax`) y crecimientos. EXP barata (se compra en la fase manual). Hitos de dinero crecientes:
  máquina 70 → velocidad/ranuras → Baúl 90 → Cinta 200 → Brazo 450 → Tolva.
- **Reparación: PAUSADA** (2026-06-17) — no convencía la mecánica de barra. Solo limpieza a mano por
  ahora. Si se retoma, valorar mecánica táctil (encajar piezas) en vez de la barra de timing.
- **Máquina de pulido: 30 s/pieza** de base (era ~5,5 s; iba "limpiando sola" demasiado rápido). Baja
  con *Velocidad de pulido*. Es idle lento a propósito.
- **Máquina SOLO con inserción a mano** (2026-06-17): se desactivó el auto-alimentado del "brazo" (no
  gustaba que limpiara sola). La máquina solo pule lo que arrastras/clicas. La rama de automatización
  del árbol (Baúl→Cinta→Brazo→Tolva) queda **inerte de momento** → revisar/repensar.
- **Layout sin caja** (2026-06-17): se quitó el panel central. El objeto se limpia **directamente sobre
  el tapete** del fondo (`MatX`/`MatY` en código); HUD flotante arriba (con contorno para leerse), botón
  del árbol en la esquina, cliente a la izquierda, máquina a la derecha. La imagen de fondo (`fondo.png`)
  se estira al aspect ratio: queda pendiente decidir "cover"/aspect fijo en la pasada de diseño.
- **Knobs para alargar/acortar Fase 1 al playtestear:** valores de los items (dinero/EXP), velocidad
  de pulido, y sobre todo los **crecimientos** y costes de los hitos de la rama Máquina.
- ⚠️ El tuneado fino real depende de tener Fases 2-5 y de **playtest**: estos son valores de partida.

## 10e. Lotes misteriosos (gambling: premio gordo / bomba) — 2026-06-17

Capa de **varianza** sobre el trabajo estable: el servicio a clientes da los primeros euros (seguro);
los **lotes** son la apuesta (riesgo/recompensa). Decisión: **lotes revelados al limpiar** + **bomba de
riesgo real ocasional**.

- **Compra de lote** (botones abajo-izq, por tiers): pagas dinero → se abre una **VENTANA MODAL de
  apertura** (oscurece el fondo, panel con efecto de entrada). Dentro está el **objeto tapado** por mugre
  genérica (no se ve qué es); el contenido se **sortea al comprar** (sellado) y se **revela frotando**
  dentro de la ventana → el reveal lento es la tensión. Los lotes **no dan EXP** y **no se pueden mandar
  a la máquina** (hay que revelarlos a mano).
- **Efectos al revelar** (en la ventana): destello, **estallido de partículas** (cuadraditos que salen
  del centro), *punch* del texto de resultado, **temblor** del panel si es bomba; y sonido. Tras revelar
  aparece el botón **RECOGER** (cierra la ventana y vuelve el flujo de clientes).
- **Resultados** (al terminar de limpiar): **PREMIO GORDO** (item `esJackpot`, valor alto, feedback
  dorado + chime agudo), **BOMBA** (item `esBomba`: "Objeto robado" → pierdes `penalizacion` dinero,
  feedback rojo + crujido), o **normal/chatarra**. Tiers actuales: *Bolsa de la calle* (12) y *Caja de
  mercadillo* (60), con tablas de probabilidad ponderadas (en código, `CrearLotes()` — 1ª pasada, tunear).
- **Datos**: `ItemDef` gana `soloLote` (no sale como cliente), `esJackpot`, `esBomba`, `penalizacion`.
  Items de lote (assets en Resources/Items): Chatarra, Diamante rosa, Reloj de oro, Objeto robado.
- **Esto ES el embrión de la Fase 2 (compraventa)**: comprar barato/arriesgado para sacar valor con el
  trabajo. La tasación/detección de fallos (skills) encaja luego encima (reducir varianza pagando skill).
- *(Pendiente/tunear)*: EV de los tiers, más tiers (subasta), mover las tablas a ScriptableObject,
  ¿reducir el riesgo de bomba con la skill de "Detección de fallos"?

## 10b. UI / presentación (pendiente de pasada de diseño)

La interfaz actual es **funcional, no definitiva**. La **colocación y el estilo de los botones de
mejora** (Fuerza de frotado, Cepillo, Mano firme, Ayudante) se rediseñará más adelante —
probablemente un **menú desplegable** u otro modo más compacto. **No invertir esfuerzo en el aspecto
de los botones por ahora**; lo importante es que las mecánicas funcionen. Pasada de diseño visual = fase de pulido.

## 11. Decisiones

**Cerradas (2026-06-16):**
- ✅ **Regateo**: precio tope secreto por personalidad; se revelan detalles que delatan arquetipo y
  cuánto se puede presionar / subir-bajar. (Sección 7)
- ✅ **Automatización**: gradiente, de activa a casi mínima al final. (Sección 8)
- ✅ **Duración**: incremental finito ~2h con final. **Sin prestigio de bucle** (como mucho un final
  opcional). (Secciones 1, 8, 9)

- ✅ **Paciencia en regateo**: sin barra temporal. El cliente solo se va si te pasas de su límite; si
  se va, tarda más en volver. (Sección 7)

**Abiertas (pendientes de cerrar):**
1. UI: ¿uGUI o UI Toolkit?
2. Título del juego.
