using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace PawnShop
{
    /// <summary>
    /// FASE 1 (servicio). Dos formas de trabajar:
    ///   - A MANO en el banco: LIMPIAR (frotado táctil gradual) o REPARAR (barra de ajuste). Da EXP + dinero.
    ///   - MÁQUINA DE PULIDO: arrastras (o clicas) items de la "pila de sucios" a sus ranuras y los pule
    ///     sola con el tiempo. Da DINERO (sin EXP). Se mejora con dinero (velocidad y ranuras).
    ///
    /// Dos monedas: EXP (trabajando a mano -> habilidades) y DINERO (al completar -> comprar cosas).
    /// El script construye su propia interfaz al arrancar; no hay que cablear nada en el editor.
    /// </summary>
    public class PawnShopGame : MonoBehaviour
    {
        enum TipoTrabajo { Limpieza, Reparacion }

        // ----------------- Balance: LIMPIEZA a mano -----------------
        const int    DesgasteBase        = 6;
        const int    DesgastePorNivel    = 3;
        const double CosteBaseFrotado    = 4;     // EXP barata: se compra durante la fase 100% manual.
        const double CrecimientoFrotado  = 1.35;
        const int    RadioCepilloBase     = 5;
        const int    RadioCepilloPorNivel = 2;
        const int    RadioCepilloMax      = 22;
        const double CosteBaseCepillo    = 6;
        const double CrecimientoCepillo  = 1.4;

        // ----------------- Balance: REPARACIÓN a mano -----------------
        const float  ProbReparacion      = 0.35f;
        const int    PiezasNecesarias    = 4;
        const double RecompensaReparar   = 22;    // paga más que limpiar: compensa el riesgo de romper.
        const double ExpPorAcierto       = 1;
        const float  VelMarcadorBase     = 0.9f;
        const float  VelExtraPorAcierto  = 0.12f;
        const float  ZonaVerdeBase       = 0.12f;
        const float  ZonaVerdePorNivel   = 0.02f;
        const float  ZonaVerdeMax        = 0.30f;
        const float  RomperBase          = 0.25f;
        const float  RomperReduccion     = 0.05f;
        const float  RomperMin           = 0.05f;
        const double CosteBaseManoFirme  = 6;
        const double CrecimientoManoFirme = 1.4;

        // ----------------- Balance: MÁQUINA DE PULIDO -----------------
        const int    MaxRanuras          = 4;     // Tope de ranuras de la máquina.
        const double CosteMaquina        = 70;    // Coste DINERO de COMPRAR la máquina (1er hito; ~2-3 min a mano).
        const double VelPulidoBase       = 1.0 / 30.0;  // 30 s/pieza sin mejoras (idle LENTO, no instantáneo).
        const double VelPulidoPorNivel   = 0.01;        // cada nivel de velocidad acelera (~30s -> ~6s al máximo).
        const double CosteBaseMaqVel     = 28;    // Coste DINERO de la 1ª mejora de velocidad.
        const double CrecimientoMaqVel   = 1.22;

        // ----------------- Balance: CADENA DE AUTOMATIZACIÓN -----------------
        // Baúl (cofre) → Brazo → Tolva → Máquina
        const float BaulX = 870f, BaulY = 60f;       // posición baúl en canvas (anchor centro)
        const float TolvaX = 590f, TolvaY = 200f;    // posición tolva (encima de la máquina)
        const float BrazoPivotX = 730f, BrazoPivotY = 130f; // pivot del brazo
        const float BrazoLargo = 150f;                // longitud del brazo en px
        const float BaulIntervaloBase = 20f;          // segundos entre items que llegan al baúl
        const float RangoConexion = 200f;             // dist. max (canvas px) brazo↔componente para conectar

        // ----------------- Balance: GENERAL -----------------
        const float  IntervaloAutoguardado = 5f;

        // ----------------- Textura de limpieza -----------------
        const int   TamTex      = 128;
        const float UmbralLimpio = 0.9f;

        // ----------------- Layout -----------------
        // Centro del TAPETE en la imagen de fondo (fracción de pantalla). El objeto a limpiar se
        // coloca aquí: se frota directamente sobre el tapete de la mesa, sin caja/panel.
        const float MatX = 0.53f, MatY = 0.26f;

        GameData data;
        string RutaGuardado => Path.Combine(Application.persistentDataPath, "savegame.json");

        // ----------------- Valores derivados -----------------
        int    RadioCepillo      => Mathf.Min(RadioCepilloBase + RadioCepilloPorNivel * data.nivelCepillo, RadioCepilloMax);
        int    Desgaste          => DesgasteBase + DesgastePorNivel * data.nivelFrotado;
        float  ZonaVerdeMitad    => Mathf.Min(ZonaVerdeBase + ZonaVerdePorNivel * data.nivelManoFirme, ZonaVerdeMax);
        float  ProbRomper        => Mathf.Max(RomperMin, RomperBase - RomperReduccion * data.nivelManoFirme);
        double VelPulido         => VelPulidoBase + VelPulidoPorNivel * data.nivelMaquinaVel;
        int    RanurasTotal      => data.nivelTolva >= 1 ? Mathf.Min(1 + data.nivelTolva, 3) : 1;
        bool   MaquinaComprada   => data.maquinaComprada >= 1;
        bool   BaulComprado      => data.nivelBaul  >= 1;
        bool   BrazoComprado     => data.nivelBrazo >= 1;
        bool   TolvaComprada     => data.nivelTolva >= 1;
        bool   BaulBrazoEnRango      => BaulComprado && BrazoComprado && baulRect != null && brazoHombro != null
                                        && Vector2.Distance(baulRect.anchoredPosition, brazoHombro.anchoredPosition) < RangoConexion;
        bool   BrazoTolvaEnRango     => BrazoComprado && TolvaComprada && tolvaRect != null && brazoHombro != null
                                        && Vector2.Distance(tolvaRect.anchoredPosition, brazoHombro.anchoredPosition) < RangoConexion;
        bool   TolvaMaquinaEnRango   => TolvaComprada && MaquinaComprada && tolvaRect != null && machineRect != null
                                        && Vector2.Distance(tolvaRect.anchoredPosition  + new Vector2(0f, -50f),
                                                            machineRect.anchoredPosition + new Vector2(0f, 140f)) < RangoConexion;
        bool   CadenaCompleta        => BaulComprado && BrazoComprado && TolvaComprada
                                        && BaulBrazoEnRango && BrazoTolvaEnRango && TolvaMaquinaEnRango;
        int    BaulCapacidad     => 2 + data.nivelBaul * 2;           // 1→4, 2→6, 3→8, 4→10
        float  BrazoCooldown     => Mathf.Max(2f, 12f - (data.nivelBrazo - 1) * 2f);
        float  BrazoVelGiro      => 160f + data.nivelBrazo * 30f;

        // ----------------- Estado del trabajo a mano -----------------
        TipoTrabajo trabajoActual;
        ItemDef[] items;
        ItemDef itemActual;
        readonly Dictionary<FormaItem, Color32[]> pixelCache = new Dictionary<FormaItem, Color32[]>();

        Texture2D texJoya, texSuciedad;
        Color32[] pixelesSuciedad;
        double totalSuciedad, suciedadQuitada, expPorUnidad;

        int   piezasHechas, fallos;
        float markerFase, markerPos, velExtra, zonaVerdeCentro;

        // ----------------- Estado de la máquina -----------------
        bool[]   slotActivo = new bool[MaxRanuras];
        double[] slotProg   = new double[MaxRanuras];
        double[] slotValor  = new double[MaxRanuras];

        // ----------------- Referencias de UI -----------------
        Font fuente;
        RectTransform canvasRect;
        Text textoDinero, textoExp, textoMaquina, textoInstruccion, textoProgreso, textoReparacion;
        RectTransform suciedadRect, barraRelleno, markerRect, zonaVerdeRect, machineRect, estacionRect, ruedaRect;
        RawImage suciedadImg;
        Image flashEstacion;
        GameObject barraProgresoGO, reparacionGO, ghost, itemDragGO;
        Image[] slotBg = new Image[MaxRanuras];
        RectTransform[] slotFill = new RectTransform[MaxRanuras];
        GameObject ventanaMejoras;
        Text textoFrase;
        List<NodoMejora> nodos;
        float autoFeedTimer;

        // ----------------- Estado de la cadena de automatización -----------------
        int   baulItems;
        float baulTimer;
        bool  brazoCoroutineActiva;
        float brazoAnguloActual;
        float angleBaulPivot, angleTolvaPivot;
        RectTransform baulRect, tolvaRect, brazoHombro, brazoCuerpo;
        RectTransform brazoBaseRect, brazoProngL, brazoProngR, brazoSeg2;
        RectTransform tolvaConectorRect;
        Text  baulTexto, tolvaLabel;
        GameObject brazoPiezaGO;
        // Arrastre libre de componentes
        RectTransform compEnArrastre;
        Vector2 offsetArrastre;
        // Puntos de conexión visuales
        PuntoConexion pcBaulOut, pcBrazoIn, pcBrazoOut, pcTolvaIn, pcTolvaOut, pcMaqIn;

        // ----------------- Clientes -----------------
        ClienteDef[] clientes;
        ClienteDef clienteActual;
        RectTransform clienteRect;
        RawImage clienteImg;
        Text textoNombre;
        readonly Dictionary<ClienteDef, Texture2D> caraCache = new Dictionary<ClienteDef, Texture2D>();
        bool enTransicion;     // beat tras servir: el cliente reacciona antes de que llegue el siguiente.

        enum Resultado { LimpiezaOk, ReparacionOk, Roto }

        // ----------------- Lotes (gambling: jackpot / bomba, revelados al limpiar) -----------------
        class LoteEntrada { public string nombre; public float peso; }
        class LoteTier { public string nombre; public double coste; public LoteEntrada[] tabla; public int nivelOjoMax; public Button boton; public Text texto; }
        LoteTier[] lotes;
        bool esLote;                          // hay un LOTE abierto (en su ventana).
        bool loteResuelto;                    // el lote ya se reveló; esperando que el jugador RECOJA.
        Color32[] blobMask;                   // máscara genérica de mugre que tapa el lote (oculta qué es).
        Texture2D cajaMisterioTex;
        readonly Dictionary<string, ItemDef> itemsPorNombre = new Dictionary<string, ItemDef>();

        // Ventana de apertura de lote (modal con efectos).
        GameObject ventanaLote;
        RectTransform ventanaLotePanel, loteSuciedadRect, loteEfectos;
        RawImage loteJoyaImg, loteSuciedadImg;
        Image loteFlash;
        RectTransform loteBarraRelleno;
        Text loteTitulo, loteHint, loteResultado;
        Button loteRecogerBoton;

        // ----------------- Audio -----------------
        AudioSource audioFuente;
        AudioClip sfxCobro, sfxAcierto, sfxRotura, sfxFrote, sfxCompra;
        float ultimoFrote;

        enum Rama { Manual, Maquina, Minerales, CompraVenta }

        class NodoMejora
        {
            public string id, nombre, prereq, prereq2;
            public Rama rama;
            public bool conDinero;          // true = cuesta dinero; false = EXP
            public double costeBase, crecimiento;
            public int nivelMax;
            public bool disponible;         // false = "próximamente" (aún no jugable)
            public Func<int> getNivel;
            public Action subir;
            public Button boton;
            public Text texto;
        }

        class PuntoConexion
        {
            public RectTransform dueño;
            public Vector2 offsetLocal;
            public Image dot;
            static readonly Color Libre     = new Color(0.45f, 0.48f, 0.55f, 0.9f);
            static readonly Color Conectado = new Color(0.25f, 0.92f, 0.40f, 1.0f);
            public void Refrescar(bool conectado)
            { if (dot != null) dot.color = conectado ? Conectado : Libre; }
        }

        float tiempoDesdeGuardado;

        void Awake()
        {
            Cargar();
            CrearNodos();
            PrepararTexturas();
            PrepararAudio();
            ConstruirUI();
            NuevoTrabajo();
            // Si el juego se cargó con la cadena ya comprada, activar componentes y brazo
            if (TolvaComprada && tolvaRect        != null) tolvaRect.gameObject.SetActive(true);
            if (TolvaComprada && tolvaConectorRect != null) tolvaConectorRect.gameObject.SetActive(true);
            if (BaulComprado  && baulRect          != null) baulRect.gameObject.SetActive(true);
            if (BrazoComprado && brazoHombro   != null) brazoHombro.gameObject.SetActive(true);
            if (BrazoComprado && brazoBaseRect != null) brazoBaseRect.gameObject.SetActive(true);
            if (BrazoComprado && !brazoCoroutineActiva) StartCoroutine(CicloBrazo());
            RecalcularAngulosBrazo();
            ActualizarConectorTolva();
        }

        void PrepararAudio()
        {
            audioFuente = gameObject.AddComponent<AudioSource>();
            audioFuente.playOnAwake = false;
            sfxCobro   = AudioFactory.Cobro();
            sfxAcierto = AudioFactory.Acierto();
            sfxRotura  = AudioFactory.Rotura();
            sfxFrote   = AudioFactory.Frote();
            sfxCompra  = AudioFactory.Compra();
        }

        void Sonido(AudioClip clip, float pitch = 1f)
        {
            if (audioFuente == null || clip == null) return;
            audioFuente.pitch = pitch;
            audioFuente.PlayOneShot(clip);
        }

        void Update()
        {
            ProcesarMaquina();
            GirarRueda();
            ActualizarBaul();

            if (!enTransicion)
            {
                if (trabajoActual == TipoTrabajo.Limpieza) LeerFrotado();
                else MoverMarcador();
            }

            ActualizarUI();
            ActualizarIndicadoresConexion();

            tiempoDesdeGuardado += Time.deltaTime;
            if (tiempoDesdeGuardado >= IntervaloAutoguardado)
            {
                tiempoDesdeGuardado = 0f;
                Guardar();
            }
        }

        // ================= MÁQUINA DE PULIDO =================
        void ProcesarMaquina()
        {
            if (!MaquinaComprada) return;
            double dv = VelPulido * Time.deltaTime;
            for (int i = 0; i < RanurasTotal; i++)
            {
                if (!slotActivo[i]) continue;
                slotProg[i] += dv;
                if (slotProg[i] >= 1.0)
                {
                    data.dinero += slotValor[i];     // pulido terminado -> dinero
                    Flotante(machineRect, "+" + Formatear(slotValor[i]), new Color(0.4f, 1f, 0.55f), 26,
                        new Vector2(0f, 40f));
                    Sonido(sfxCobro, 0.82f);         // pulido: cobro con tono distinto al de mano
                    slotActivo[i] = false;
                    slotProg[i] = 0;
                }
            }

        }

        /// <summary>Mete el objeto ACTUAL del banco en la primera ranura libre y saca un trabajo nuevo.</summary>
        void EnviarItemAMaquina()
        {
            if (enTransicion) return;
            if (esLote) return;                                  // un lote hay que revelarlo a mano
            if (!MaquinaComprada) return;                        // aún no se ha comprado la máquina
            if (trabajoActual != TipoTrabajo.Limpieza) return;   // la pulidora solo limpia (no repara)
            for (int i = 0; i < RanurasTotal; i++)
            {
                if (!slotActivo[i])
                {
                    slotActivo[i] = true;
                    slotProg[i] = 0;
                    slotValor[i] = itemActual.valorDinero;
                    NuevoTrabajo();   // llega otro objeto al banco
                    return;
                }
            }
            // Todas las ranuras ocupadas: no se puede meter más por ahora.
        }

        void OnItemDragBegin(PointerEventData e) { }

        void OnItemDrag(PointerEventData e)
        {
            // El "fantasma" del objeto aparece solo cuando sacas el cursor fuera del objeto
            // (así frotar dentro y arrastrar fuera no chocan).
            bool fuera = !RectTransformUtility.RectangleContainsScreenPoint(suciedadRect, e.position, null);
            if (fuera)
            {
                if (ghost == null) CrearGhostItem();
                PosicionarGhost(e.position);
            }
            else if (ghost != null)
            {
                Destroy(ghost);
                ghost = null;
            }
        }

        void OnItemDragEnd(PointerEventData e)
        {
            if (ghost != null) { Destroy(ghost); ghost = null; }
            if (machineRect != null && RectTransformUtility.RectangleContainsScreenPoint(machineRect, e.position, null))
                { EnviarItemAMaquina(); return; }
            if (tolvaRect  != null && TolvaComprada  && RectTransformUtility.RectangleContainsScreenPoint(tolvaRect,  e.position, null))
                { EnviarItemAMaquina(); return; }
            if (baulRect   != null && BaulComprado   && RectTransformUtility.RectangleContainsScreenPoint(baulRect,   e.position, null))
                { MeterItemEnBaul(); return; }
        }

        void CrearGhostItem()
        {
            ghost = new GameObject("Ghost", typeof(RawImage));
            ghost.transform.SetParent(canvasRect, false);
            var img = ghost.GetComponent<RawImage>();
            img.texture = texJoya;
            img.raycastTarget = false;
            var rt = ghost.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(120, 120);
        }

        void PosicionarGhost(Vector2 pantalla)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, pantalla, null, out Vector2 local))
                ghost.GetComponent<RectTransform>().anchoredPosition = local;
        }

        // ================= TRABAJOS A MANO =================
        void NuevoTrabajo()
        {
            esLote = false;
            clienteActual = ClienteAleatorio();

            // REPARACIÓN DESACTIVADA por ahora (decisión 2026-06-17): solo limpieza a mano.
            // Se conserva todo el código de reparación (IniciarReparacion/MoverMarcador/IntentarAjuste)
            // para retomarla más adelante; basta volver a llamarla aquí con su probabilidad.
            trabajoActual = TipoTrabajo.Limpieza;
            IniciarLimpieza();

            ActualizarModoUI();
            ActualizarCliente();
        }

        /// <summary>
        /// Cierra el trabajo a mano: el cliente reacciona (frase + posible propina) y, tras un
        /// breve "beat", llega el siguiente cliente. Bloquea la entrada durante la transición.
        /// </summary>
        void FinTrabajo(Resultado r)
        {
            if (enTransicion) return;

            if (clienteActual != null)
            {
                if (r == Resultado.Roto)
                {
                    textoFrase.text = "\"" + clienteActual.enfadado + "\"";
                    textoFrase.color = new Color(1f, 0.45f, 0.4f);
                }
                else
                {
                    textoFrase.text = "\"" + clienteActual.contento + "\"";
                    textoFrase.color = new Color(0.6f, 1f, 0.65f);

                    // Propina (solo al servir a mano).
                    if (UnityEngine.Random.value < clienteActual.propinaProb)
                    {
                        double tip = Math.Max(1, Math.Floor(itemActual.valorDinero *
                            UnityEngine.Random.Range(0.2f, clienteActual.propinaMax)));
                        data.dinero += tip;
                        Flotante(clienteRect, "+" + Formatear(tip) + " propina", new Color(1f, 0.85f, 0.3f), 24, new Vector2(0f, 30f));
                        Sonido(sfxCompra, 1.1f);
                    }
                }
            }

            enTransicion = true;
            StartCoroutine(TransicionCliente());
        }

        IEnumerator TransicionCliente()
        {
            yield return new WaitForSeconds(1.1f);
            enTransicion = false;
            NuevoTrabajo();
        }

        // ================= LOTES (gambling) =================
        void CrearLotes()
        {
            // Geodos: 5 tiers. Pesos = probabilidad relativa. Tunear al final cuando todo esté montado.
            // Items actuales usados como placeholder hasta tener assets de minerales propios.
            lotes = new[]
            {
                new LoteTier { nombre = "Gravilla", coste = 15, nivelOjoMax = 3, tabla = new[] {
                    Ent("Chatarra", 55), Ent("Anillo", 20), Ent("Gema", 12),
                    Ent("Diamante rosa", 7), Ent("Objeto robado", 6) } },

                new LoteTier { nombre = "Piedra bruta", coste = 80, nivelOjoMax = 6, tabla = new[] {
                    Ent("Chatarra", 40), Ent("Anillo", 20), Ent("Gema", 18),
                    Ent("Reloj de bolsillo", 10), Ent("Diamante rosa", 8), Ent("Objeto robado", 4) } },

                new LoteTier { nombre = "Geodo", coste = 450, nivelOjoMax = 10, tabla = new[] {
                    Ent("Chatarra", 25), Ent("Gema", 25), Ent("Reloj de bolsillo", 18),
                    Ent("Diamante rosa", 15), Ent("Reloj de oro", 10), Ent("Objeto robado", 7) } },

                new LoteTier { nombre = "Cristal", coste = 2500, nivelOjoMax = 15, tabla = new[] {
                    Ent("Chatarra", 12), Ent("Gema", 22), Ent("Reloj de bolsillo", 20),
                    Ent("Diamante rosa", 22), Ent("Reloj de oro", 18), Ent("Objeto robado", 6) } },

                new LoteTier { nombre = "Meteorito", coste = 18000, nivelOjoMax = 22, tabla = new[] {
                    Ent("Chatarra", 5), Ent("Reloj de bolsillo", 15), Ent("Diamante rosa", 28),
                    Ent("Reloj de oro", 32), Ent("Objeto robado", 20) } },
            };
        }

        LoteEntrada Ent(string nombre, float peso) => new LoteEntrada { nombre = nombre, peso = peso };

        ItemDef ResolverItem(string nombre) => itemsPorNombre.TryGetValue(nombre, out var it) ? it : items[0];

        ItemDef SortearLote(LoteTier tier)
        {
            float total = 0f;
            foreach (var e in tier.tabla) total += e.peso;
            float r = UnityEngine.Random.value * total;
            foreach (var e in tier.tabla) { r -= e.peso; if (r <= 0f) return ResolverItem(e.nombre); }
            return ResolverItem(tier.tabla[tier.tabla.Length - 1].nombre);
        }

        void ComprarLote(LoteTier tier)
        {
            if (enTransicion || esLote) return;
            if (data.dinero < tier.coste) return;
            data.dinero -= tier.coste;
            Sonido(sfxCompra);

            esLote = true;
            loteResuelto = false;
            trabajoActual = TipoTrabajo.Limpieza;
            itemActual = SortearLote(tier);     // el resultado queda SELLADO; se revela al frotar
            IniciarLimpiezaLote();

            // Abre la ventana modal con efecto de entrada.
            ventanaLote.SetActive(true);
            loteTitulo.text = "Abriendo: " + tier.nombre;
            loteHint.text = "Frota para revelar...";
            loteResultado.gameObject.SetActive(false);
            loteRecogerBoton.gameObject.SetActive(false);
            loteFlash.color = new Color(1f, 1f, 1f, 0f);
            StartCoroutine(EfectoAbrir());
        }

        void IniciarLimpiezaLote()
        {
            var px = PixelesDe(itemActual);
            texJoya.SetPixels32(px);
            texJoya.Apply();
            RellenarSuciedad(BlobMask());       // mugre genérica que tapa qué es
            expPorUnidad = 0;                   // los lotes son la vía de DINERO/gamble, no de EXP
        }

        /// <summary>Máscara genérica (elipse) que cubre el lote para que no se adivine qué objeto es.</summary>
        Color32[] BlobMask()
        {
            if (blobMask != null) return blobMask;
            blobMask = new Color32[TamTex * TamTex];
            int cx = TamTex / 2, cy = TamTex / 2;
            for (int y = 0; y < TamTex; y++)
                for (int x = 0; x < TamTex; x++)
                {
                    float dx = (x - cx) / 52f, dy = (y - cy) / 48f;
                    blobMask[y * TamTex + x] = (dx * dx + dy * dy <= 1f)
                        ? new Color32(0, 0, 0, 255) : new Color32(0, 0, 0, 0);
                }
            return blobMask;
        }

        /// <summary>Se llama al terminar de limpiar el LOTE: lo revela con EFECTOS dentro de la ventana.</summary>
        void ResolverLote()
        {
            loteResuelto = true;
            loteHint.text = "";
            loteResultado.gameObject.SetActive(true);
            loteRecogerBoton.gameObject.SetActive(true);

            if (itemActual.esBomba)
            {
                double perd = Math.Min(data.dinero, itemActual.penalizacion);
                data.dinero -= perd;
                loteResultado.text = "OBJETO ROBADO\n-" + Formatear(perd);
                loteResultado.color = new Color(1f, 0.4f, 0.35f);
                FlashLote(new Color(0.9f, 0.2f, 0.18f));
                Burst(new Color(0.9f, 0.25f, 0.2f), 26, 520f, false);
                StartCoroutine(ShakeLote());
                Sonido(sfxRotura);
            }
            else if (itemActual.esJackpot)
            {
                data.dinero += itemActual.valorDinero;
                loteResultado.text = "PREMIO GORDO!\n" + itemActual.nombreItem + "  +" + Formatear(itemActual.valorDinero);
                loteResultado.color = new Color(1f, 0.88f, 0.4f);
                FlashLote(new Color(1f, 0.9f, 0.5f));
                Burst(new Color(1f, 0.85f, 0.3f), 40, 680f, true);
                Burst(new Color(1f, 1f, 0.7f), 24, 480f, true);
                Sonido(sfxCobro, 1.3f);
            }
            else
            {
                data.dinero += itemActual.valorDinero;
                loteResultado.text = itemActual.nombreItem + "  +" + Formatear(itemActual.valorDinero);
                loteResultado.color = new Color(0.7f, 1f, 0.75f);
                FlashLote(new Color(0.4f, 0.9f, 0.45f));
                Burst(new Color(0.45f, 0.95f, 0.55f), 16, 420f, true);
                Sonido(sfxCobro);
            }

            StartCoroutine(PunchTexto(loteResultado.rectTransform));
        }

        void RecogerLote()
        {
            ventanaLote.SetActive(false);
            esLote = false;
            loteResuelto = false;
            NuevoTrabajo();        // vuelve el flujo normal de clientes
        }

        // ----- Efectos de la ventana de lote -----
        static float EaseOut(float k) => 1f - (1f - k) * (1f - k);

        IEnumerator EfectoAbrir()
        {
            float t = 0f; const float dur = 0.25f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float s = Mathf.Lerp(0.7f, 1f, EaseOut(t / dur));
                ventanaLotePanel.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            ventanaLotePanel.localScale = Vector3.one;
        }

        void FlashLote(Color c)
        {
            StopCoroutine(nameof(AnimarFlashLote));
            loteFlash.color = new Color(c.r, c.g, c.b, 0.7f);
            StartCoroutine(AnimarFlashLote());
        }

        IEnumerator AnimarFlashLote()
        {
            var c = loteFlash.color;
            float a = c.a;
            while (a > 0f)
            {
                a -= Time.deltaTime * 2f;
                loteFlash.color = new Color(c.r, c.g, c.b, Mathf.Max(0f, a));
                yield return null;
            }
        }

        IEnumerator ShakeLote()
        {
            Vector2 ini = ventanaLotePanel.anchoredPosition;
            float t = 0f; const float dur = 0.35f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float m = (1f - t / dur) * 16f;
                ventanaLotePanel.anchoredPosition = ini + new Vector2(UnityEngine.Random.Range(-m, m), UnityEngine.Random.Range(-m, m));
                yield return null;
            }
            ventanaLotePanel.anchoredPosition = ini;
        }

        IEnumerator PunchTexto(RectTransform rt)
        {
            float t = 0f; const float dur = 0.3f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float s = Mathf.Lerp(1.7f, 1f, EaseOut(t / dur));
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        /// <summary>Estallido de partículas (cuadraditos) desde el centro de la ventana.</summary>
        void Burst(Color color, int n, float vel, bool gravedad)
        {
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("p", typeof(Image));
                go.transform.SetParent(loteEfectos, false);
                var img = go.GetComponent<Image>(); img.color = color; img.raycastTarget = false;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                float s = UnityEngine.Random.Range(8f, 18f);
                rt.sizeDelta = new Vector2(s, s);
                rt.anchoredPosition = Vector2.zero;
                float ang = UnityEngine.Random.value * Mathf.PI * 2f;
                Vector2 v = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * vel * UnityEngine.Random.Range(0.5f, 1f);
                StartCoroutine(AnimarParticula(rt, img, v, gravedad));
            }
        }

        IEnumerator AnimarParticula(RectTransform rt, Image img, Vector2 v, bool gravedad)
        {
            float t = 0f; float dur = UnityEngine.Random.Range(0.6f, 0.95f);
            Color c0 = img.color; Vector2 pos = Vector2.zero;
            while (t < dur)
            {
                t += Time.deltaTime;
                if (gravedad) v.y -= 1100f * Time.deltaTime;
                pos += v * Time.deltaTime;
                rt.anchoredPosition = pos;
                rt.Rotate(0f, 0f, 360f * Time.deltaTime);
                img.color = new Color(c0.r, c0.g, c0.b, 1f - t / dur);
                yield return null;
            }
            Destroy(rt.gameObject);
        }

        // Rect sobre el que se frota: la ventana del lote si está abierta; si no, el banco.
        RectTransform RectoFrotado => (esLote && ventanaLote != null && ventanaLote.activeSelf) ? loteSuciedadRect : suciedadRect;

        // ----- Limpieza (frotado) -----
        void IniciarLimpieza()
        {
            itemActual = ItemAleatorio(it => it.limpiable && !it.soloLote);
            var px = PixelesDe(itemActual);
            texJoya.SetPixels32(px);
            texJoya.Apply();
            RellenarSuciedad(px);
            expPorUnidad = totalSuciedad > 0 ? itemActual.recompensaExp / totalSuciedad : 0;
        }

        bool RatonPulsado(out Vector2 posicion)
        {
#if ENABLE_INPUT_SYSTEM
            var m = Mouse.current;
            if (m != null && m.leftButton.isPressed) { posicion = m.position.ReadValue(); return true; }
#else
            if (Input.GetMouseButton(0)) { posicion = Input.mousePosition; return true; }
#endif
            posicion = default;
            return false;
        }

        void LeerFrotado()
        {
            if (esLote && loteResuelto) return;             // ya revelado: no se sigue frotando
            var rect = RectoFrotado;
            if (!RatonPulsado(out Vector2 pantalla)) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, pantalla, null, out Vector2 local))
                return;

            Rect r = rect.rect;
            float u = (local.x - r.x) / r.width;
            float v = (local.y - r.y) / r.height;
            if (u < 0f || u > 1f || v < 0f || v > 1f) return;

            Frotar((int)(u * TamTex), (int)(v * TamTex));
        }

        void Frotar(int cx, int cy)
        {
            int radio = RadioCepillo;
            int radio2 = radio * radio;
            int desgaste = Desgaste;
            int quitadoFrame = 0;

            for (int y = -radio; y <= radio; y++)
            {
                for (int x = -radio; x <= radio; x++)
                {
                    if (x * x + y * y > radio2) continue;
                    int px = cx + x, py = cy + y;
                    if (px < 0 || px >= TamTex || py < 0 || py >= TamTex) continue;

                    int idx = py * TamTex + px;
                    int a = pixelesSuciedad[idx].a;
                    if (a > 0)
                    {
                        int nueva = a - desgaste;
                        if (nueva < 0) nueva = 0;
                        quitadoFrame += a - nueva;
                        pixelesSuciedad[idx].a = (byte)nueva;
                    }
                }
            }

            if (quitadoFrame == 0) return;

            if (Time.time - ultimoFrote > 0.1f)   // roce throttleado para no spamear cada frame
            {
                ultimoFrote = Time.time;
                Sonido(sfxFrote, UnityEngine.Random.Range(0.85f, 1.15f));
            }

            suciedadQuitada += quitadoFrame;
            data.exp += quitadoFrame * expPorUnidad;
            texSuciedad.SetPixels32(pixelesSuciedad);
            texSuciedad.Apply();

            if (totalSuciedad > 0 && suciedadQuitada >= totalSuciedad * UmbralLimpio)
            {
                if (esLote)
                {
                    if (!loteResuelto) ResolverLote();   // revela jackpot / bomba / normal
                }
                else
                {
                    data.dinero += itemActual.valorDinero;
                    Flotante(estacionRect, "+" + Formatear(itemActual.valorDinero), new Color(0.4f, 1f, 0.55f), 34, new Vector2(0f, 30f));
                    Flash(new Color(0.35f, 0.9f, 0.45f));
                    Sonido(sfxCobro);
                    FinTrabajo(Resultado.LimpiezaOk);
                }
            }
        }

        void RellenarSuciedad(Color32[] mascara)
        {
            totalSuciedad = 0;
            suciedadQuitada = 0;
            for (int i = 0; i < pixelesSuciedad.Length; i++)
            {
                if (mascara[i].a > 0)
                {
                    byte g = (byte)UnityEngine.Random.Range(70, 105);
                    pixelesSuciedad[i] = new Color32(g, (byte)(g - 14), (byte)(g - 22), 255);
                    totalSuciedad += 255;
                }
                else
                {
                    pixelesSuciedad[i] = new Color32(0, 0, 0, 0);
                }
            }
            texSuciedad.SetPixels32(pixelesSuciedad);
            texSuciedad.Apply();
        }

        // ----- Reparación (barra de ajuste) -----
        void IniciarReparacion()
        {
            itemActual = ItemAleatorio(it => it.reparable);
            texJoya.SetPixels32(PixelesDe(itemActual));
            texJoya.Apply();
            piezasHechas = 0;
            fallos = 0;
            markerFase = 0f;
            velExtra = 0f;
            NuevaZonaVerde();
        }

        void MoverMarcador()
        {
            markerFase += Time.deltaTime * (VelMarcadorBase + velExtra);
            markerPos = Mathf.PingPong(markerFase, 1f);
            markerRect.anchorMin = new Vector2(markerPos, 0f);
            markerRect.anchorMax = new Vector2(markerPos, 1f);
        }

        void NuevaZonaVerde()
        {
            float h = ZonaVerdeMitad;
            zonaVerdeCentro = UnityEngine.Random.Range(h, 1f - h);
            zonaVerdeRect.anchorMin = new Vector2(zonaVerdeCentro - h, 0f);
            zonaVerdeRect.anchorMax = new Vector2(zonaVerdeCentro + h, 1f);
            zonaVerdeRect.offsetMin = Vector2.zero;
            zonaVerdeRect.offsetMax = Vector2.zero;
        }

        void IntentarAjuste()
        {
            if (enTransicion) return;
            if (trabajoActual != TipoTrabajo.Reparacion) return;

            if (Mathf.Abs(markerPos - zonaVerdeCentro) <= ZonaVerdeMitad)
            {
                piezasHechas++;
                data.exp += ExpPorAcierto;
                Flotante(estacionRect, "+" + Formatear(ExpPorAcierto) + " EXP", new Color(0.6f, 0.85f, 1f), 26, new Vector2(0f, 10f));
                Flash(new Color(0.35f, 0.9f, 0.45f));
                Sonido(sfxAcierto, 1f + 0.06f * piezasHechas);   // sube de tono con cada pieza
                if (piezasHechas >= PiezasNecesarias)
                {
                    data.dinero += RecompensaReparar;
                    Flotante(estacionRect, "+" + Formatear(RecompensaReparar), new Color(0.4f, 1f, 0.55f), 36, new Vector2(0f, 40f));
                    Sonido(sfxCobro);
                    FinTrabajo(Resultado.ReparacionOk);
                    return;
                }
                velExtra += VelExtraPorAcierto;
                NuevaZonaVerde();
            }
            else
            {
                if (UnityEngine.Random.value < ProbRomper)
                {
                    Flotante(estacionRect, "¡ROTO!", new Color(1f, 0.4f, 0.35f), 38, new Vector2(0f, 20f));
                    Flash(new Color(0.9f, 0.25f, 0.2f));
                    Sonido(sfxRotura);
                    FinTrabajo(Resultado.Roto);
                    return;
                }
                fallos++;
                Flash(new Color(0.9f, 0.6f, 0.2f));   // fallo sin rotura: aviso ámbar
            }
        }

        // ================= ÁRBOL DE MEJORAS =================
        void CrearNodos()
        {
            nodos = new List<NodoMejora>();

            // --- Rama MANUAL (se paga con EXP) ---
            AddNodo("man_frotado", "Fuerza de frotado", Rama.Manual, null, false, CosteBaseFrotado, CrecimientoFrotado, 10,
                () => data.nivelFrotado, () => data.nivelFrotado++);
            AddNodo("man_cepillo", "Tamano de cepillo", Rama.Manual, "man_frotado", false, CosteBaseCepillo, CrecimientoCepillo, 8,
                () => data.nivelCepillo, () => data.nivelCepillo++);
            AddNodo("man_manofirme", "Mano firme", Rama.Manual, "man_frotado", false, CosteBaseManoFirme, CrecimientoManoFirme, 8,
                () => data.nivelManoFirme, () => data.nivelManoFirme++);

            // --- Rama MAQUINA (se paga con dinero) ---
            // Raíz: COMPRAR la máquina. Todo lo demás de la rama cuelga de aquí.
            AddNodo("maq_comprar", "Comprar maquina de pulido", Rama.Maquina, null, true, CosteMaquina, 1, 1,
                () => data.maquinaComprada, () => data.maquinaComprada++);
            AddNodo("maq_vel", "Velocidad de pulido", Rama.Maquina, "maq_comprar", true, CosteBaseMaqVel, CrecimientoMaqVel, 14,
                () => data.nivelMaquinaVel, () => data.nivelMaquinaVel++);
            // Cadena de automatización: Baúl → Brazo → Tolva (solo funciona si los 3 están)
            AddNodo("maq_tolva", "Tolva de entrada (+ranura)", Rama.Maquina, "maq_comprar", true, 90, 1.8, 3,
                () => data.nivelTolva, () => data.nivelTolva++);
            AddNodo("maq_baul", "Baul de sucios", Rama.Maquina, "maq_comprar", true, 80, 1.6, 4,
                () => data.nivelBaul, () => data.nivelBaul++);
            AddNodoDual("maq_brazo", "Brazo automatico", Rama.Maquina, "maq_baul", "maq_tolva", true, 200, 1.5, 5,
                () => data.nivelBrazo, () => data.nivelBrazo++);

            // --- Rama MINERALES ---
            // Ojo de tasador: activo (mejora probabilidades en geodos). Resto: estructura bloqueada.
            AddNodo("min_ojo", "Ojo de tasador", Rama.Minerales, null, false, 40, 1.7, 22,
                () => data.nivelOjo, () => data.nivelOjo++);
            AddBloqueado("min_triturador",  "Triturador",            Rama.Minerales, "min_ojo");
            AddBloqueado("min_cinta",       "Cinta de alimentacion", Rama.Minerales, "min_triturador");  // usa data.nivelCintaGeodo
            AddBloqueado("min_clasificador","Clasificador de gemas", Rama.Minerales, "min_cinta");
            AddBloqueado("min_ojo_elec",    "Ojo electronico",       Rama.Minerales, "min_clasificador");

            // --- Rama COMPRA-VENTA (aún no jugable: solo estructura del árbol) ---
            AddBloqueado("cv_mercadillo", "Acceso a mercadillo", Rama.CompraVenta, null);
            AddBloqueado("cv_tasacion", "Tasacion", Rama.CompraVenta, "cv_mercadillo");
            AddBloqueado("cv_subastas", "Subastas", Rama.CompraVenta, "cv_tasacion");
        }

        void AddNodo(string id, string nombre, Rama rama, string prereq, bool dinero, double cb, double cg, int max, Func<int> get, Action subir)
        {
            nodos.Add(new NodoMejora { id = id, nombre = nombre, rama = rama, prereq = prereq, conDinero = dinero,
                costeBase = cb, crecimiento = cg, nivelMax = max, disponible = true, getNivel = get, subir = subir });
        }

        void AddNodoDual(string id, string nombre, Rama rama, string prereq, string prereq2, bool dinero, double cb, double cg, int max, Func<int> get, Action subir)
        {
            nodos.Add(new NodoMejora { id = id, nombre = nombre, rama = rama, prereq = prereq, prereq2 = prereq2,
                conDinero = dinero, costeBase = cb, crecimiento = cg, nivelMax = max, disponible = true, getNivel = get, subir = subir });
        }

        void AddBloqueado(string id, string nombre, Rama rama, string prereq)
        {
            nodos.Add(new NodoMejora { id = id, nombre = nombre, rama = rama, prereq = prereq, conDinero = true,
                costeBase = 0, crecimiento = 1, nivelMax = 1, disponible = false, getNivel = () => 0, subir = () => { } });
        }

        NodoMejora BuscarNodo(string id) => nodos.Find(n => n.id == id);
        double CosteNodo(NodoMejora n) => Math.Floor(n.costeBase * Math.Pow(n.crecimiento, n.getNivel()));
        bool PrereqOk(NodoMejora n) =>
            (string.IsNullOrEmpty(n.prereq)  || BuscarNodo(n.prereq).getNivel()  >= 1) &&
            (string.IsNullOrEmpty(n.prereq2) || BuscarNodo(n.prereq2).getNivel() >= 1);

        void Comprar(NodoMejora n)
        {
            if (!n.disponible || !PrereqOk(n) || n.getNivel() >= n.nivelMax) return;
            double c = CosteNodo(n);
            if (n.conDinero) { if (data.dinero < c) return; data.dinero -= c; }
            else { if (data.exp < c) return; data.exp -= c; }
            bool eraPrimero = n.getNivel() == 0;
            n.subir();
            Sonido(sfxCompra);
            if (eraPrimero) OnComponenteComprado(n.id);
        }

        void OnComponenteComprado(string id)
        {
            switch (id)
            {
                case "maq_tolva":
                    if (tolvaRect != null) StartCoroutine(AnimarCaida(tolvaRect, new Vector2(TolvaX, TolvaY)));
                    if (tolvaConectorRect != null) tolvaConectorRect.gameObject.SetActive(true);
                    break;
                case "maq_baul":
                    if (baulRect != null) StartCoroutine(AnimarCaida(baulRect, new Vector2(BaulX, BaulY)));
                    break;
                case "maq_brazo":
                    if (brazoHombro != null)
                    {
                        StartCoroutine(AnimarCaida(brazoHombro, new Vector2(BrazoPivotX, BrazoPivotY)));
                        if (brazoBaseRect != null) brazoBaseRect.gameObject.SetActive(true);
                        if (!brazoCoroutineActiva) StartCoroutine(CicloBrazo());
                    }
                    break;
            }
        }

        // ================= FEEDBACK (juice) =================
        bool MaquinaTrabajando()
        {
            for (int i = 0; i < RanurasTotal; i++) if (slotActivo[i]) return true;
            return false;
        }

        void GirarRueda()
        {
            if (ruedaRect == null) return;
            if (MaquinaTrabajando())
                ruedaRect.Rotate(0f, 0f, -360f * Time.deltaTime);   // gira mientras pule
        }

        /// <summary>Texto que sube y se desvanece (p.ej. "+12") sobre un elemento.</summary>
        void Flotante(RectTransform anclaje, string txt, Color color, int tam, Vector2 desde)
        {
            if (anclaje == null) return;
            var go = new GameObject("Flotante", typeof(Text), typeof(Outline));
            go.transform.SetParent(anclaje, false);
            var t = go.GetComponent<Text>();
            t.font = fuente; t.text = txt; t.fontSize = tam; t.color = color;
            t.fontStyle = FontStyle.Bold; t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var ol = go.GetComponent<Outline>();
            ol.effectColor = new Color(0f, 0f, 0f, 0.85f);
            ol.effectDistance = new Vector2(2f, -2f);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(260f, 60f);
            rt.anchoredPosition = desde;
            StartCoroutine(AnimarFlotante(rt, t, ol));
        }

        IEnumerator AnimarFlotante(RectTransform rt, Text t, Outline ol)
        {
            const float dur = 0.9f;
            float el = 0f;
            Vector2 ini = rt.anchoredPosition;
            Color c0 = t.color, o0 = ol.effectColor;
            while (el < dur)
            {
                el += Time.deltaTime;
                float k = el / dur;
                rt.anchoredPosition = ini + new Vector2(0f, 70f * k);
                float a = 1f - k;
                t.color = new Color(c0.r, c0.g, c0.b, a);
                ol.effectColor = new Color(o0.r, o0.g, o0.b, o0.a * a);
                yield return null;
            }
            Destroy(rt.gameObject);
        }

        /// <summary>Destello de color sobre la estación de trabajo (verde acierto / rojo rotura).</summary>
        void Flash(Color color)
        {
            if (flashEstacion == null) return;
            StopCoroutine(nameof(AnimarFlash));
            flashEstacion.color = new Color(color.r, color.g, color.b, 0.5f);
            StartCoroutine(AnimarFlash());
        }

        IEnumerator AnimarFlash()
        {
            float a = flashEstacion.color.a;
            var c = flashEstacion.color;
            while (a > 0f)
            {
                a -= Time.deltaTime * 2.2f;
                flashEstacion.color = new Color(c.r, c.g, c.b, Mathf.Max(0f, a));
                yield return null;
            }
        }

        // ================= GUARDADO =================
        void Guardar()
        {
            data.ultimoCierreBinario = DateTime.UtcNow.ToBinary();
            File.WriteAllText(RutaGuardado, JsonUtility.ToJson(data));
        }

        void Cargar()
        {
            if (File.Exists(RutaGuardado))
                data = JsonUtility.FromJson<GameData>(File.ReadAllText(RutaGuardado)) ?? new GameData();
            else
                data = new GameData();
        }

        void OnApplicationQuit() => Guardar();
        void OnApplicationPause(bool pausado) { if (pausado) Guardar(); }

        // ================= TEXTURAS =================
        void PrepararTexturas()
        {
            texJoya = new Texture2D(TamTex, TamTex, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            texSuciedad = new Texture2D(TamTex, TamTex, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            pixelesSuciedad = new Color32[TamTex * TamTex];
            CargarItems();
            CargarClientes();
        }

        void CargarClientes()
        {
            clientes = Resources.LoadAll<ClienteDef>("Clientes");
            if (clientes == null || clientes.Length == 0)
                clientes = ClientesPorDefecto();
        }

        ClienteDef[] ClientesPorDefecto()
        {
            ClienteDef C(string nombre, Arquetipo arq, Color piel, Color pelo, Color ropa,
                string saludo, string contento, string enfadado, float prob, float max)
            {
                var c = ScriptableObject.CreateInstance<ClienteDef>();
                c.name = nombre; c.nombreCliente = nombre; c.arquetipo = arq;
                c.piel = piel; c.pelo = pelo; c.ropa = ropa;
                c.saludo = saludo; c.contento = contento; c.enfadado = enfadado;
                c.propinaProb = prob; c.propinaMax = max;
                return c;
            }
            return new[]
            {
                C("Dona Mari", Arquetipo.Normal, new Color(0.95f,0.78f,0.62f), new Color(0.7f,0.7f,0.72f), new Color(0.5f,0.3f,0.4f),
                    "Buenas, me lo deja como nuevo?", "Que maravilla, gracias!", "Vaya por dios, que pena.", 0.35f, 0.4f),
                C("Ramon el del apuro", Arquetipo.Desesperado, new Color(0.9f,0.72f,0.58f), new Color(0.25f,0.18f,0.12f), new Color(0.4f,0.4f,0.42f),
                    "Por favor, necesito venderlo ya...", "Gracias, me salvas la vida.", "No! Era lo unico que tenia!", 0.15f, 0.2f),
                C("Don Listillo", Arquetipo.Sabelotodo, new Color(0.96f,0.8f,0.64f), new Color(0.5f,0.35f,0.2f), new Color(0.3f,0.45f,0.35f),
                    "Yo de esto se mas que tu, cuidadito.", "Bueno, no esta mal del todo.", "Lo sabia! Manazas.", 0.1f, 0.2f),
                C("El Tito Paco", Arquetipo.Trilero, new Color(0.88f,0.7f,0.55f), new Color(0.15f,0.12f,0.1f), new Color(0.5f,0.42f,0.2f),
                    "Mira que ganga te traigo, jefe.", "Un placer hacer negocios.", "Eh eh, que eso no lo pago yo.", 0.3f, 0.45f),
                C("Dona Chantal", Arquetipo.Pija, new Color(0.97f,0.82f,0.7f), new Color(0.6f,0.45f,0.25f), new Color(0.7f,0.3f,0.45f),
                    "Cielo, esto es carisimo, tratalo bien.", "Mira que mono ha quedado.", "Inutil! Esto es un escandalo.", 0.6f, 0.8f),
                C("Kevin", Arquetipo.Yonki, new Color(0.85f,0.72f,0.6f), new Color(0.35f,0.25f,0.15f), new Color(0.3f,0.35f,0.4f),
                    "Tu dame algo rapido y no preguntes.", "Guay guay guay, gracias tio.", "Me has roto el rollo, tio!", 0.4f, 0.3f),
            };
        }

        ClienteDef ClienteAleatorio() => clientes[UnityEngine.Random.Range(0, clientes.Length)];

        Texture2D CaraDe(ClienteDef c)
        {
            if (!caraCache.TryGetValue(c, out var t))
            {
                t = PixelArtFactory.CrearCara(c.piel, c.pelo, c.ropa, c.arquetipo);
                caraCache[c] = t;
            }
            return t;
        }

        /// <summary>
        /// Carga los objetos desde ScriptableObjects en Resources/Items. Si no hay ninguno
        /// (assets aún sin crear), genera unos por defecto en memoria para que el juego nunca falle.
        /// </summary>
        void CargarItems()
        {
            items = Resources.LoadAll<ItemDef>("Items");
            if (items == null || items.Length == 0)
                items = ItemsPorDefecto();

            itemsPorNombre.Clear();
            foreach (var it in items) itemsPorNombre[it.nombreItem] = it;
        }

        ItemDef[] ItemsPorDefecto()
        {
            ItemDef Crear(string nombre, FormaItem forma, double valor, double exp, bool limpiable, bool reparable)
            {
                var it = ScriptableObject.CreateInstance<ItemDef>();
                it.name = nombre;
                it.nombreItem = nombre; it.forma = forma; it.valorDinero = valor; it.recompensaExp = exp;
                it.limpiable = limpiable; it.reparable = reparable;
                return it;
            }
            ItemDef Lote(string nombre, FormaItem forma, double valor, bool jackpot, bool bomba, double penal)
            {
                var it = Crear(nombre, forma, valor, 0, true, false);
                it.soloLote = true; it.esJackpot = jackpot; it.esBomba = bomba; it.penalizacion = penal;
                if (bomba) it.turbio = true;
                return it;
            }
            return new[]
            {
                Crear("Anillo",            FormaItem.Anillo, 6,  3, true,  false),
                Crear("Reloj de bolsillo", FormaItem.Reloj,  9,  4, true,  true),
                Crear("Gema",              FormaItem.Gema,   14, 5, true,  false),
                Lote("Chatarra",      FormaItem.Anillo, 2,   false, false, 0),
                Lote("Diamante rosa", FormaItem.Gema,   120, true,  false, 0),
                Lote("Reloj de oro",  FormaItem.Reloj,  70,  true,  false, 0),
                Lote("Objeto robado", FormaItem.Gema,   0,   false, true,  40),
            };
        }

        /// <summary>Píxeles de un objeto, cacheados por forma (se generan una sola vez).</summary>
        Color32[] PixelesDe(ItemDef item)
        {
            if (!pixelCache.TryGetValue(item.forma, out var px))
            {
                px = PixelArtFactory.CrearForma(item.forma, TamTex);
                pixelCache[item.forma] = px;
            }
            return px;
        }

        ItemDef ItemAleatorio(Func<ItemDef, bool> filtro)
        {
            var aptos = items.Where(filtro).ToList();
            if (aptos.Count == 0) aptos = items.ToList();

            // Selección ponderada por 'frecuencia' (turbios < 1 = más raros).
            float total = 0f;
            foreach (var it in aptos) total += Mathf.Max(0.0001f, it.frecuencia);
            float r = UnityEngine.Random.value * total;
            foreach (var it in aptos)
            {
                r -= Mathf.Max(0.0001f, it.frecuencia);
                if (r <= 0f) return it;
            }
            return aptos[aptos.Count - 1];
        }

        // ================= INTERFAZ =================
        void ConstruirUI()
        {
            AsegurarEventSystem();
            fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasRect = canvasGO.GetComponent<RectTransform>();
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;

            var fondoGO = new GameObject("Fondo", typeof(RawImage));
            fondoGO.transform.SetParent(canvasGO.transform, false);
            var fondoRect = fondoGO.GetComponent<RectTransform>();
            fondoRect.anchorMin = Vector2.zero; fondoRect.anchorMax = Vector2.one;
            fondoRect.offsetMin = fondoRect.offsetMax = Vector2.zero;
            var fondoImg = fondoGO.GetComponent<RawImage>();
            // Fondo: usa la imagen del usuario (Assets/Resources/fondo.png) si existe; si no, el procedural.
            var fondoTex = Resources.Load<Texture2D>("fondo");
            fondoImg.texture = fondoTex != null ? fondoTex : PixelArtFactory.CrearFondoTienda();
            fondoImg.raycastTarget = false;

            // HUD flotante (SIN caja): el fondo es la escena de la tienda; los textos llevan contorno
            // para leerse sobre la imagen. El objeto se limpia directamente sobre el tapete (ConstruirEstacion).
            var titulo = CrearTexto(canvasGO.transform, "CASA DE EMPENOS", 38, fuente, new Color(1f, 0.85f, 0.3f));
            AnclarEnPantalla(titulo.rectTransform, 0.5f, 0.955f, 760, 52);
            textoDinero = CrearTexto(canvasGO.transform, "", 34, fuente, Color.white);
            AnclarEnPantalla(textoDinero.rectTransform, 0.5f, 0.90f, 600, 46);
            textoExp = CrearTexto(canvasGO.transform, "", 26, fuente, new Color(0.6f, 0.85f, 1f));
            AnclarEnPantalla(textoExp.rectTransform, 0.5f, 0.855f, 600, 36);
            textoInstruccion = CrearTexto(canvasGO.transform, "", 20, fuente, new Color(0.95f, 0.95f, 0.98f));
            AnclarEnPantalla(textoInstruccion.rectTransform, 0.5f, 0.07f, 1100, 40);

            ConstruirEstacion(canvasGO.transform);          // objeto a limpiar SOBRE EL TAPETE
            ConstruirBarraProgreso(canvasGO.transform, fuente);
            ConstruirReparacion(canvasGO.transform, fuente);
            ConstruirBotones(canvasGO.transform, fuente);
            ConstruirCliente(canvasGO.transform, fuente);   // cliente a la izquierda
            ConstruirMaquina(canvasGO.transform, fuente);   // máquina a la derecha
            ConstruirLotes(canvasGO.transform, fuente);
            ConstruirVentanaLote(canvasGO.transform, fuente);
            ConstruirBaul(canvasGO.transform);
            ConstruirTolva(canvasGO.transform);
            ConstruirBrazoAutomatico(canvasGO.transform);
            ConstruirVentanaMejoras(canvasGO.transform, fuente);
        }

        void AnclarEnPantalla(RectTransform rt, float ax, float ay, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(ax, ay);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = Vector2.zero;
        }

        void ConstruirEstacion(Transform canvasT)
        {
            var estacion = new GameObject("Estacion", typeof(RectTransform));
            estacion.transform.SetParent(canvasT, false);
            estacionRect = estacion.GetComponent<RectTransform>();
            estacionRect.anchorMin = estacionRect.anchorMax = estacionRect.pivot = new Vector2(MatX, MatY);
            estacionRect.sizeDelta = new Vector2(185, 185);
            estacionRect.anchoredPosition = Vector2.zero;

            var joyaGO = new GameObject("Joya", typeof(RawImage));
            joyaGO.transform.SetParent(estacion.transform, false);
            CentrarCuadrado(joyaGO.GetComponent<RectTransform>(), 175);
            var joyaImg = joyaGO.GetComponent<RawImage>();
            joyaImg.texture = texJoya;
            joyaImg.raycastTarget = false;

            var suciedadGO = new GameObject("Suciedad", typeof(RawImage));
            suciedadGO.transform.SetParent(estacion.transform, false);
            suciedadRect = suciedadGO.GetComponent<RectTransform>();
            CentrarCuadrado(suciedadRect, 175);
            suciedadImg = suciedadGO.GetComponent<RawImage>();
            suciedadImg.texture = texSuciedad;
            suciedadImg.raycastTarget = false;

            // Capa invisible encima del objeto que captura el ARRASTRE hacia la máquina.
            // (Frotar usa el ratón "crudo" por debajo, así que conviven.)
            itemDragGO = new GameObject("ArrastreItem", typeof(Image), typeof(DragHandler));
            itemDragGO.transform.SetParent(estacion.transform, false);
            CentrarCuadrado(itemDragGO.GetComponent<RectTransform>(), 175);
            itemDragGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);   // invisible pero raycast-able
            var idh = itemDragGO.GetComponent<DragHandler>();
            idh.onBegin = OnItemDragBegin;
            idh.onDragging = OnItemDrag;
            idh.onEnd = OnItemDragEnd;

            // Capa de flash (verde acierto / rojo rotura). Encima de todo, sin capturar raycast.
            var flashGO = new GameObject("Flash", typeof(Image));
            flashGO.transform.SetParent(estacion.transform, false);
            CentrarCuadrado(flashGO.GetComponent<RectTransform>(), 185);
            flashEstacion = flashGO.GetComponent<Image>();
            flashEstacion.color = new Color(1f, 1f, 1f, 0f);
            flashEstacion.raycastTarget = false;
        }

        void ConstruirBarraProgreso(Transform canvasT, Font fuente)
        {
            barraProgresoGO = new GameObject("BarraProgreso", typeof(Image));
            barraProgresoGO.transform.SetParent(canvasT, false);
            barraProgresoGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);
            var bpr = barraProgresoGO.GetComponent<RectTransform>();
            bpr.anchorMin = bpr.anchorMax = bpr.pivot = new Vector2(MatX, MatY);
            bpr.sizeDelta = new Vector2(280, 22);
            bpr.anchoredPosition = new Vector2(0f, -112f);   // justo debajo del objeto, sobre el tapete

            var rellenoGO = new GameObject("Relleno", typeof(Image));
            rellenoGO.transform.SetParent(barraProgresoGO.transform, false);
            rellenoGO.GetComponent<Image>().color = new Color(0.3f, 0.85f, 0.45f, 1f);
            barraRelleno = rellenoGO.GetComponent<RectTransform>();
            barraRelleno.anchorMin = new Vector2(0f, 0f);
            barraRelleno.anchorMax = new Vector2(0f, 1f);
            barraRelleno.pivot = new Vector2(0f, 0.5f);
            barraRelleno.offsetMin = Vector2.zero;
            barraRelleno.offsetMax = Vector2.zero;

            textoProgreso = CrearTexto(barraProgresoGO.transform, "", 16, fuente, Color.white);
            var tr = textoProgreso.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
        }

        void ConstruirReparacion(Transform canvasT, Font fuente)
        {
            reparacionGO = new GameObject("Reparacion", typeof(RectTransform), typeof(VerticalLayoutGroup));
            reparacionGO.transform.SetParent(canvasT, false);
            var rrt = reparacionGO.GetComponent<RectTransform>();
            rrt.anchorMin = rrt.anchorMax = rrt.pivot = new Vector2(MatX, MatY);
            rrt.sizeDelta = new Vector2(360, 90);
            rrt.anchoredPosition = new Vector2(0f, -150f);
            var rvlg = reparacionGO.GetComponent<VerticalLayoutGroup>();
            rvlg.spacing = 6;
            rvlg.childAlignment = TextAnchor.UpperCenter;
            rvlg.childControlWidth = rvlg.childControlHeight = true;
            rvlg.childForceExpandWidth = true;
            rvlg.childForceExpandHeight = false;

            textoReparacion = CrearTexto(reparacionGO.transform, "", 19, fuente, Color.white);

            var barGO = new GameObject("BarraAjuste", typeof(Image), typeof(Button));
            barGO.transform.SetParent(reparacionGO.transform, false);
            barGO.GetComponent<Image>().color = new Color(0.55f, 0.18f, 0.18f, 1f);
            barGO.GetComponent<Button>().onClick.AddListener(IntentarAjuste);
            barGO.AddComponent<LayoutElement>().minHeight = 44;

            var verdeGO = new GameObject("ZonaVerde", typeof(Image));
            verdeGO.transform.SetParent(barGO.transform, false);
            verdeGO.GetComponent<Image>().color = new Color(0.3f, 0.85f, 0.4f, 1f);
            verdeGO.GetComponent<Image>().raycastTarget = false;
            zonaVerdeRect = verdeGO.GetComponent<RectTransform>();

            var marcGO = new GameObject("Marcador", typeof(Image));
            marcGO.transform.SetParent(barGO.transform, false);
            marcGO.GetComponent<Image>().color = Color.white;
            marcGO.GetComponent<Image>().raycastTarget = false;
            markerRect = marcGO.GetComponent<RectTransform>();
            markerRect.anchorMin = new Vector2(0f, 0f);
            markerRect.anchorMax = new Vector2(0f, 1f);
            markerRect.pivot = new Vector2(0.5f, 0.5f);
            markerRect.sizeDelta = new Vector2(6f, 0f);
            markerRect.anchoredPosition = Vector2.zero;
        }

        void ConstruirCliente(Transform canvasT, Font fuente)
        {
            // Retrato del cliente, a la izquierda del panel (mira al banco).
            var go = new GameObject("Cliente", typeof(RawImage));
            go.transform.SetParent(canvasT, false);
            clienteRect = go.GetComponent<RectTransform>();
            clienteRect.anchorMin = clienteRect.anchorMax = clienteRect.pivot = new Vector2(0.5f, 0.5f);
            clienteRect.sizeDelta = new Vector2(240, 240);
            clienteRect.anchoredPosition = new Vector2(-560, 110);
            clienteImg = go.GetComponent<RawImage>();
            clienteImg.raycastTarget = false;

            textoNombre = CrearTexto(go.transform, "", 24, fuente, new Color(1f, 0.9f, 0.55f));
            var nr = textoNombre.GetComponent<RectTransform>();
            nr.anchorMin = new Vector2(0f, 0f); nr.anchorMax = new Vector2(1f, 0f);
            nr.pivot = new Vector2(0.5f, 1f);
            nr.anchoredPosition = new Vector2(0f, -8f);
            nr.sizeDelta = new Vector2(120, 34);

            textoFrase = CrearTexto(go.transform, "", 20, fuente, new Color(0.9f, 0.88f, 0.7f));
            var sr = textoFrase.GetComponent<RectTransform>();
            sr.anchorMin = sr.anchorMax = new Vector2(0.5f, 0f);
            sr.pivot = new Vector2(0.5f, 1f);
            sr.anchoredPosition = new Vector2(0f, -48f);
            sr.sizeDelta = new Vector2(380, 130);
            textoFrase.alignment = TextAnchor.UpperCenter;
            textoFrase.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        void ActualizarCliente()
        {
            if (clienteActual == null || clienteImg == null) return;
            clienteImg.texture = CaraDe(clienteActual);
            textoNombre.text = clienteActual.nombreCliente;

            // El cliente "dice" la frase turbia del objeto si lo es; si no, su saludo.
            bool turbio = itemActual != null && itemActual.turbio && !string.IsNullOrEmpty(itemActual.fraseAlAparecer);
            string linea = turbio ? itemActual.fraseAlAparecer : clienteActual.saludo;
            textoFrase.text = "\"" + linea + "\"";
            textoFrase.color = turbio ? new Color(1f, 0.55f, 0.45f) : new Color(0.9f, 0.88f, 0.7f);
        }

        void ConstruirMaquina(Transform canvasT, Font fuente)
        {
            // Elemento independiente, a la derecha del panel. Solo la silueta del sprite (fondo transparente).
            var maqGO = new GameObject("MaquinaPulido", typeof(RawImage), typeof(Button));
            maqGO.transform.SetParent(canvasT, false);
            machineRect = maqGO.GetComponent<RectTransform>();
            machineRect.anchorMin = machineRect.anchorMax = new Vector2(0.5f, 0.5f);
            machineRect.pivot = new Vector2(0.5f, 0.5f);
            machineRect.sizeDelta = new Vector2(280, 280);
            machineRect.anchoredPosition = new Vector2(590, -20);
            maqGO.GetComponent<RawImage>().texture = PixelArtFactory.CrearMaquinaPulido();
            maqGO.GetComponent<Button>().onClick.AddListener(EnviarItemAMaquina);

            // Disco giratorio de la rueda de pulido, superpuesto sobre su alojamiento del sprite.
            var ruedaGO = new GameObject("Rueda", typeof(RawImage));
            ruedaGO.transform.SetParent(maqGO.transform, false);
            ruedaGO.GetComponent<RawImage>().texture = PixelArtFactory.CrearRuedaPulido();
            ruedaGO.GetComponent<RawImage>().raycastTarget = false;
            ruedaRect = ruedaGO.GetComponent<RectTransform>();
            ruedaRect.anchorMin = ruedaRect.anchorMax = ruedaRect.pivot = new Vector2(0.5f, 0.5f);
            float lado = 280f * PixelArtFactory.RuedaTam;
            ruedaRect.sizeDelta = new Vector2(lado, lado);
            ruedaRect.anchoredPosition = new Vector2(
                (PixelArtFactory.RuedaCentroX - 0.5f) * 280f,
                (PixelArtFactory.RuedaCentroY - 0.5f) * 280f);

            // Ranuras dentro de la "pantalla" del sprite.
            float sxMin = PixelArtFactory.PantallaXMin + 0.01f, sxMax = PixelArtFactory.PantallaXMax - 0.01f;
            float syMin = PixelArtFactory.PantallaYMin + 0.01f, syMax = PixelArtFactory.PantallaYMax - 0.01f;
            for (int i = 0; i < MaxRanuras; i++)
            {
                float lo = Mathf.Lerp(syMin, syMax, (float)i / MaxRanuras) + 0.012f;
                float hi = Mathf.Lerp(syMin, syMax, (float)(i + 1) / MaxRanuras) - 0.012f;

                var bgGO = new GameObject("Ranura" + i, typeof(Image));
                bgGO.transform.SetParent(maqGO.transform, false);
                slotBg[i] = bgGO.GetComponent<Image>();
                slotBg[i].color = new Color(0f, 0f, 0f, 0.5f);
                slotBg[i].raycastTarget = false;
                var brt = bgGO.GetComponent<RectTransform>();
                brt.anchorMin = new Vector2(sxMin, lo);
                brt.anchorMax = new Vector2(sxMax, hi);
                brt.offsetMin = brt.offsetMax = Vector2.zero;

                var fGO = new GameObject("Relleno", typeof(Image));
                fGO.transform.SetParent(bgGO.transform, false);
                fGO.GetComponent<Image>().color = new Color(0.95f, 0.8f, 0.3f, 1f);
                fGO.GetComponent<Image>().raycastTarget = false;
                slotFill[i] = fGO.GetComponent<RectTransform>();
                slotFill[i].anchorMin = new Vector2(0f, 0f);
                slotFill[i].anchorMax = new Vector2(0f, 1f);
                slotFill[i].pivot = new Vector2(0f, 0.5f);
                slotFill[i].offsetMin = slotFill[i].offsetMax = Vector2.zero;
            }

            // Etiqueta de estado debajo de la máquina.
            textoMaquina = CrearTexto(maqGO.transform, "", 15, fuente, new Color(0.95f, 0.85f, 0.5f));
            var lrt = textoMaquina.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(1f, 0f);
            lrt.pivot = new Vector2(0.5f, 1f);
            lrt.anchoredPosition = new Vector2(0f, -6f);
            lrt.sizeDelta = new Vector2(60f, 40f);

            // Punto de conexión superior (recibe la tolva)
            pcMaqIn = CrearIndicador(maqGO.transform, machineRect, new Vector2(0f, 142f));

            machineRect.gameObject.SetActive(MaquinaComprada);   // oculta hasta comprarla
        }

        void ConstruirBotones(Transform canvasT, Font fuente)
        {
            var b = CrearBoton(canvasT, "ARBOL DE MEJORAS", fuente, out _,
                () => { if (ventanaMejoras != null) ventanaMejoras.SetActive(true); });
            var rt = b.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.985f, 0.95f);   // esquina sup. derecha
            rt.sizeDelta = new Vector2(300, 66);
            rt.anchoredPosition = Vector2.zero;
        }

        void ConstruirLotes(Transform canvasT, Font fuente)
        {
            CrearLotes();
            cajaMisterioTex = PixelArtFactory.CrearCajaMisterio();

            // Título y botones de geodo, izquierda central.
            var titulo = CrearTexto(canvasT, "GEODOS", 20, fuente, new Color(1f, 0.8f, 0.45f));
            titulo.alignment = TextAnchor.MiddleLeft;
            var tr = titulo.rectTransform;
            tr.anchorMin = tr.anchorMax = tr.pivot = new Vector2(0.012f, 0.57f);
            tr.sizeDelta = new Vector2(300, 28);
            tr.anchoredPosition = Vector2.zero;

            float y = 0.51f;
            foreach (var tier in lotes)
            {
                var b = CrearBoton(canvasT, "", fuente, out tier.texto, () => ComprarLote(tier));
                var rt = b.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.012f, y);
                rt.sizeDelta = new Vector2(300, 58);
                rt.anchoredPosition = Vector2.zero;
                tier.boton = b;
                y -= 0.09f;
            }
        }

        void ConstruirVentanaLote(Transform canvasT, Font fuente)
        {
            ventanaLote = new GameObject("VentanaLote", typeof(Image));
            ventanaLote.transform.SetParent(canvasT, false);
            var vr = ventanaLote.GetComponent<RectTransform>();
            vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one; vr.offsetMin = vr.offsetMax = Vector2.zero;
            ventanaLote.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.82f);   // oscurece el fondo

            var panel = new GameObject("Panel", typeof(Image));
            panel.transform.SetParent(ventanaLote.transform, false);
            ventanaLotePanel = panel.GetComponent<RectTransform>();
            ventanaLotePanel.anchorMin = ventanaLotePanel.anchorMax = ventanaLotePanel.pivot = new Vector2(0.5f, 0.5f);
            ventanaLotePanel.sizeDelta = new Vector2(720, 780);
            ventanaLotePanel.anchoredPosition = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.10f, 0.10f, 0.13f, 0.98f);

            loteTitulo = CrearTexto(panel.transform, "", 34, fuente, new Color(1f, 0.85f, 0.3f));
            ColocarEnPanel(loteTitulo.rectTransform, 0f, 320f, 660f, 50f);

            // Zona del objeto (joya + mugre + flash), centro-arriba.
            var joyaGO = new GameObject("LoteJoya", typeof(RawImage));
            joyaGO.transform.SetParent(panel.transform, false);
            loteJoyaImg = joyaGO.GetComponent<RawImage>(); loteJoyaImg.texture = texJoya; loteJoyaImg.raycastTarget = false;
            ColocarEnPanel(loteJoyaImg.rectTransform, 0f, 60f, 360f, 360f);

            var sucGO = new GameObject("LoteSuciedad", typeof(RawImage));
            sucGO.transform.SetParent(panel.transform, false);
            loteSuciedadImg = sucGO.GetComponent<RawImage>(); loteSuciedadImg.texture = texSuciedad; loteSuciedadImg.raycastTarget = false;
            loteSuciedadRect = loteSuciedadImg.rectTransform;
            ColocarEnPanel(loteSuciedadRect, 0f, 60f, 360f, 360f);

            var flashGO = new GameObject("LoteFlash", typeof(Image));
            flashGO.transform.SetParent(panel.transform, false);
            loteFlash = flashGO.GetComponent<Image>(); loteFlash.color = new Color(1f, 1f, 1f, 0f); loteFlash.raycastTarget = false;
            ColocarEnPanel(loteFlash.rectTransform, 0f, 60f, 380f, 380f);

            loteHint = CrearTexto(panel.transform, "", 22, fuente, new Color(0.92f, 0.85f, 0.55f));
            ColocarEnPanel(loteHint.rectTransform, 0f, -150f, 560f, 40f);

            // Barra de progreso de limpieza.
            var barGO = new GameObject("LoteBarra", typeof(Image));
            barGO.transform.SetParent(panel.transform, false);
            barGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);
            ColocarEnPanel(barGO.GetComponent<RectTransform>(), 0f, -205f, 380f, 22f);
            var fillGO = new GameObject("Relleno", typeof(Image));
            fillGO.transform.SetParent(barGO.transform, false);
            fillGO.GetComponent<Image>().color = new Color(0.3f, 0.85f, 0.45f, 1f);
            loteBarraRelleno = fillGO.GetComponent<RectTransform>();
            loteBarraRelleno.anchorMin = new Vector2(0f, 0f); loteBarraRelleno.anchorMax = new Vector2(0f, 1f);
            loteBarraRelleno.pivot = new Vector2(0f, 0.5f); loteBarraRelleno.offsetMin = loteBarraRelleno.offsetMax = Vector2.zero;

            loteResultado = CrearTexto(panel.transform, "", 40, fuente, Color.white);
            ColocarEnPanel(loteResultado.rectTransform, 0f, -150f, 660f, 120f);

            loteRecogerBoton = CrearBoton(panel.transform, "RECOGER", fuente, out _, RecogerLote);
            ColocarEnPanel(loteRecogerBoton.GetComponent<RectTransform>(), 0f, -310f, 260f, 64f);

            // Capa de partículas, encima de todo y centrada en el objeto.
            var efGO = new GameObject("Efectos", typeof(RectTransform));
            efGO.transform.SetParent(panel.transform, false);
            loteEfectos = efGO.GetComponent<RectTransform>();
            ColocarEnPanel(loteEfectos, 0f, 60f, 10f, 10f);

            ventanaLote.SetActive(false);
        }

        void ColocarEnPanel(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, y);
        }

        void ConstruirVentanaMejoras(Transform canvasT, Font fuente)
        {
            // Ventana aparte (overlay a pantalla completa). Diseño PROVISIONAL: solo el esquema funcional.
            ventanaMejoras = new GameObject("VentanaMejoras", typeof(Image));
            ventanaMejoras.transform.SetParent(canvasT, false);
            var vr = ventanaMejoras.GetComponent<RectTransform>();
            vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one; vr.offsetMin = vr.offsetMax = Vector2.zero;
            ventanaMejoras.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.8f);

            var cont = new GameObject("Cont", typeof(Image), typeof(VerticalLayoutGroup));
            cont.transform.SetParent(ventanaMejoras.transform, false);
            var crt = cont.GetComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(1500, 920);
            cont.GetComponent<Image>().color = new Color(0.10f, 0.10f, 0.13f, 0.98f);
            var cvlg = cont.GetComponent<VerticalLayoutGroup>();
            cvlg.padding = new RectOffset(24, 24, 20, 20);
            cvlg.spacing = 14;
            cvlg.childAlignment = TextAnchor.UpperCenter;
            cvlg.childControlWidth = cvlg.childControlHeight = true;
            cvlg.childForceExpandWidth = true;
            cvlg.childForceExpandHeight = false;

            CrearTexto(cont.transform, "ARBOL DE MEJORAS", 34, fuente, new Color(1f, 0.85f, 0.3f));

            var fila = new GameObject("Ramas", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            fila.transform.SetParent(cont.transform, false);
            fila.AddComponent<LayoutElement>().minHeight = 720;
            var hlg = fila.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 18;
            hlg.childAlignment = TextAnchor.UpperCenter;
            hlg.childControlWidth = hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            ConstruirColumna(fila.transform, Rama.Manual, "MANUAL  (EXP)", fuente);
            ConstruirColumna(fila.transform, Rama.Maquina, "MAQUINA  (dinero)", fuente);
            ConstruirColumna(fila.transform, Rama.CompraVenta, "COMPRA-VENTA  (proximamente)", fuente);

            var cerrar = CrearBoton(cont.transform, "CERRAR", fuente, out _, () => ventanaMejoras.SetActive(false));
            cerrar.gameObject.AddComponent<LayoutElement>().minHeight = 60;

            ventanaMejoras.SetActive(false);
        }

        void ConstruirColumna(Transform padre, Rama rama, string titulo, Font fuente)
        {
            var col = new GameObject("Col", typeof(Image), typeof(VerticalLayoutGroup));
            col.transform.SetParent(padre, false);
            col.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.05f);
            var vlg = col.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(10, 10, 10, 10);
            vlg.spacing = 8;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            CrearTexto(col.transform, titulo, 22, fuente, Color.white);
            foreach (var n in nodos)
            {
                if (n.rama != rama) continue;
                n.boton = CrearBoton(col.transform, "", fuente, out n.texto, () => Comprar(n));
                n.boton.gameObject.AddComponent<LayoutElement>().minHeight = 76;
            }
        }

        void AnadirContorno(GameObject go)
        {
            var ol = go.AddComponent<Outline>();
            ol.effectColor = new Color(0f, 0f, 0f, 0.9f);
            ol.effectDistance = new Vector2(2f, -2f);
        }

        void CentrarCuadrado(RectTransform rt, float lado)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(lado, lado);
            rt.anchoredPosition = Vector2.zero;
        }

        void AsegurarEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        Text CrearTexto(Transform padre, string contenido, int tam, Font fuente, Color color)
        {
            var go = new GameObject("Texto", typeof(Text));
            go.transform.SetParent(padre, false);
            var t = go.GetComponent<Text>();
            t.font = fuente; t.text = contenido; t.fontSize = tam; t.color = color;
            t.fontStyle = FontStyle.Bold;                 // negrita = más legible
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            AnadirContorno(go);                           // borde oscuro que lo despega del fondo
            go.AddComponent<LayoutElement>().minHeight = tam + 8;
            return t;
        }

        Button CrearBoton(Transform padre, string etiqueta, Font fuente, out Text label, UnityAction onClick)
        {
            var go = new GameObject("Boton", typeof(Image), typeof(Button));
            go.transform.SetParent(padre, false);
            go.GetComponent<Image>().color = new Color(0.20f, 0.26f, 0.36f, 1f);
            go.GetComponent<Button>().onClick.AddListener(onClick);

            var labelGO = new GameObject("Label", typeof(Text));
            labelGO.transform.SetParent(go.transform, false);
            var lr = labelGO.GetComponent<RectTransform>();
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
            lr.offsetMin = Vector2.zero; lr.offsetMax = Vector2.zero;
            label = labelGO.GetComponent<Text>();
            label.font = fuente; label.text = etiqueta; label.fontSize = 20; label.color = Color.white;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            AnadirContorno(labelGO);
            return go.GetComponent<Button>();
        }

        // ----------------- Refresco -----------------
        void ActualizarModoUI()
        {
            bool limp = trabajoActual == TipoTrabajo.Limpieza;
            suciedadImg.gameObject.SetActive(limp);
            barraProgresoGO.SetActive(limp);
            reparacionGO.SetActive(!limp);
            if (itemDragGO != null) itemDragGO.SetActive(limp && !esLote);   // un lote no se arrastra a la máquina

            if (esLote)
                textoInstruccion.text = "Frota el LOTE para descubrir que hay dentro...  (premio gordo o sorpresa)";
            else
                textoInstruccion.text = limp
                    ? (MaquinaComprada
                        ? "Frota el objeto para limpiarlo  ·  o arrastralo a la maquina para pulirlo solo"
                        : "Frota el objeto para limpiarlo  ·  ahorra para comprar la maquina de pulido")
                    : "Reparar: pulsa la barra cuando el marcador este en VERDE";
        }

        void ActualizarUI()
        {
            textoDinero.text = "Dinero: " + Formatear(data.dinero);
            textoExp.text    = "EXP: " + Formatear(data.exp);
            ActualizarLotes();

            if (trabajoActual == TipoTrabajo.Limpieza)
            {
                float prog01 = totalSuciedad > 0 ? Mathf.Clamp01((float)(suciedadQuitada / totalSuciedad) / UmbralLimpio) : 0f;
                barraRelleno.anchorMax = new Vector2(prog01, 1f);
                string nombre = esLote ? "Lote misterioso" : itemActual.nombreItem;   // no destripar el lote
                textoProgreso.text = nombre + "  —  Limpieza: " + Mathf.RoundToInt(prog01 * 100f) + "%";
                if (esLote && loteBarraRelleno != null) loteBarraRelleno.anchorMax = new Vector2(prog01, 1f);
            }
            else
            {
                textoReparacion.text = "Reparando " + itemActual.nombreItem
                    + "  —  Pieza " + piezasHechas + "/" + PiezasNecesarias
                    + (fallos > 0 ? "   (fallos: " + fallos + ")" : "");
            }

            // Máquina: aparece al comprarla.
            if (machineRect.gameObject.activeSelf != MaquinaComprada)
                machineRect.gameObject.SetActive(MaquinaComprada);
            if (!MaquinaComprada)
            {
                if (ventanaMejoras != null && ventanaMejoras.activeSelf) ActualizarNodos();
                return;
            }

            // Máquina: ranuras y rellenos.
            int ocupadas = 0;
            for (int i = 0; i < MaxRanuras; i++)
            {
                bool usable = i < RanurasTotal;
                slotBg[i].color = usable ? new Color(0f, 0f, 0f, 0.5f) : new Color(0f, 0f, 0f, 0.18f);
                float f = (usable && slotActivo[i]) ? (float)slotProg[i] : 0f;
                slotFill[i].anchorMax = new Vector2(f, 1f);
                if (usable && slotActivo[i]) ocupadas++;
            }
            string brazo = CadenaCompleta ? "  ·  brazo AUTO" : (BrazoComprado ? "  ·  brazo (sin cadena)" : "");
            double segsPieza = VelPulido > 0 ? 1.0 / VelPulido : 0;
            textoMaquina.text = "Pulido: " + ocupadas + "/" + RanurasTotal + " ranuras   ·   " + segsPieza.ToString("0") + "s/pieza" + brazo;
            if (tolvaLabel != null)
                tolvaLabel.text = TolvaComprada ? "↓ " + RanurasTotal + " ranuras" : "";

            if (ventanaMejoras != null && ventanaMejoras.activeSelf)
                ActualizarNodos();
        }

        void ActualizarLotes()
        {
            if (lotes == null) return;
            foreach (var tier in lotes)
            {
                if (tier.texto != null) tier.texto.text = tier.nombre + "\n" + Formatear(tier.coste) + " dinero";
                if (tier.boton != null) tier.boton.interactable = !esLote && !enTransicion && data.dinero >= tier.coste;
            }
        }

        void ActualizarNodos()
        {
            foreach (var n in nodos)
            {
                if (n.boton == null) continue;
                int nivel = n.getNivel();
                string sufijoNivel = n.nivelMax > 1 ? "  Nv." + nivel : "";
                bool comprable;

                if (!n.disponible)
                {
                    n.texto.text = n.nombre + "\n(proximamente)";
                    comprable = false;
                }
                else if (!PrereqOk(n))
                {
                    n.texto.text = n.nombre + "\n(requiere: " + BuscarNodo(n.prereq).nombre + ")";
                    comprable = false;
                }
                else if (nivel >= n.nivelMax)
                {
                    n.texto.text = n.nombre + sufijoNivel + "\nMAX";
                    comprable = false;
                }
                else
                {
                    double c = CosteNodo(n);
                    n.texto.text = n.nombre + sufijoNivel + "\n" + Formatear(c) + (n.conDinero ? " dinero" : " EXP");
                    comprable = n.conDinero ? data.dinero >= c : data.exp >= c;
                }
                n.boton.interactable = comprable;
            }
        }

        string Formatear(double v)
        {
            if (v < 1000) return v.ToString("0.0");
            string[] sufijos = { "", "K", "M", "B", "T" };
            int i = 0;
            while (v >= 1000 && i < sufijos.Length - 1) { v /= 1000; i++; }
            return v.ToString("0.00") + sufijos[i];
        }

        // ================= CADENA DE AUTOMATIZACIÓN =================

        void ConstruirBaul(Transform canvasT)
        {
            var go = new GameObject("Baul", typeof(RawImage));
            go.transform.SetParent(canvasT, false);
            baulRect = go.GetComponent<RectTransform>();
            baulRect.anchorMin = baulRect.anchorMax = baulRect.pivot = new Vector2(0.5f, 0.5f);
            baulRect.sizeDelta = new Vector2(150, 120);
            baulRect.anchoredPosition = new Vector2(BaulX, BaulY);
            go.GetComponent<RawImage>().texture = PixelArtFactory.CrearBaul();
            go.GetComponent<RawImage>().raycastTarget = true;
            HacerArrastrable(baulRect);
            pcBaulOut = CrearIndicador(go.transform, baulRect, new Vector2(-65f, 5f));

            baulTexto = CrearTexto(go.transform, "0/4", 18, fuente, new Color(0.9f, 0.8f, 0.5f));
            var lt = baulTexto.GetComponent<RectTransform>();
            lt.anchorMin = new Vector2(0f, 0f); lt.anchorMax = new Vector2(1f, 0f);
            lt.pivot = new Vector2(0.5f, 1f);
            lt.anchoredPosition = new Vector2(0f, -6f);
            lt.sizeDelta = new Vector2(0f, 28f);

            go.SetActive(false);
        }

        void ConstruirTolva(Transform canvasT)
        {
            // Conector visual entre tolva y máquina (tubo de bajada)
            // Tolva bottom = TolvaY - 50 = 150; machine top = -20 + 140 = 120. Centro = 135, alto = 32.
            var cGO = new GameObject("TolvaConector", typeof(Image));
            cGO.transform.SetParent(canvasT, false);
            tolvaConectorRect = cGO.GetComponent<RectTransform>();
            tolvaConectorRect.anchorMin = tolvaConectorRect.anchorMax = tolvaConectorRect.pivot = new Vector2(0.5f, 0.5f);
            tolvaConectorRect.sizeDelta = new Vector2(10, 32);
            tolvaConectorRect.anchoredPosition = new Vector2(TolvaX, 135f);
            cGO.GetComponent<Image>().color = new Color(0.20f, 0.22f, 0.26f, 1f);
            cGO.GetComponent<Image>().raycastTarget = false;
            cGO.SetActive(false);

            var go = new GameObject("Tolva", typeof(RawImage));
            go.transform.SetParent(canvasT, false);
            tolvaRect = go.GetComponent<RectTransform>();
            tolvaRect.anchorMin = tolvaRect.anchorMax = tolvaRect.pivot = new Vector2(0.5f, 0.5f);
            tolvaRect.sizeDelta = new Vector2(120, 100);
            tolvaRect.anchoredPosition = new Vector2(TolvaX, TolvaY);
            go.GetComponent<RawImage>().texture = PixelArtFactory.CrearTolva();
            go.GetComponent<RawImage>().raycastTarget = true;
            HacerArrastrable(tolvaRect);
            pcTolvaIn  = CrearIndicador(go.transform, tolvaRect, new Vector2( 55f,   5f));
            pcTolvaOut = CrearIndicador(go.transform, tolvaRect, new Vector2(  0f, -52f));

            // Label: ranuras que aporta la tolva
            tolvaLabel = CrearTexto(go.transform, "", 17, fuente, new Color(0.75f, 0.88f, 0.65f));
            var lr = tolvaLabel.GetComponent<RectTransform>();
            lr.anchorMin = new Vector2(0f, 0f); lr.anchorMax = new Vector2(1f, 0f);
            lr.pivot = new Vector2(0.5f, 1f);
            lr.anchoredPosition = new Vector2(0f, -5f);
            lr.sizeDelta = new Vector2(0f, 26f);

            go.SetActive(false);
        }

        void ConstruirBrazoAutomatico(Transform canvasT)
        {
            Vector2 pivotPos = new Vector2(BrazoPivotX, BrazoPivotY);
            angleBaulPivot  = AnguloHacia(new Vector2(BaulX,  BaulY)  - pivotPos);
            angleTolvaPivot = AnguloHacia(new Vector2(TolvaX, TolvaY) - pivotPos);
            brazoAnguloActual = angleBaulPivot;

            // ── BASE (placa estática con tornillos en esquinas) ──
            var baseGO = new GameObject("BrazoBase", typeof(Image));
            baseGO.transform.SetParent(canvasT, false);
            brazoBaseRect = baseGO.GetComponent<RectTransform>();
            brazoBaseRect.anchorMin = brazoBaseRect.anchorMax = brazoBaseRect.pivot = new Vector2(0.5f, 0.5f);
            brazoBaseRect.sizeDelta = new Vector2(40, 40);
            brazoBaseRect.anchoredPosition = pivotPos;
            baseGO.GetComponent<Image>().color = new Color(0.18f, 0.20f, 0.24f, 1f);
            baseGO.GetComponent<Image>().raycastTarget = true;
            HacerArrastrable(brazoBaseRect);
            pcBrazoIn  = CrearIndicador(baseGO.transform, brazoBaseRect, new Vector2( 22f, 0f));
            pcBrazoOut = CrearIndicador(baseGO.transform, brazoBaseRect, new Vector2(-22f, 0f));

            // Cuatro tornillos en las esquinas
            Vector2[] bolts = { new Vector2(-14f,14f), new Vector2(14f,14f), new Vector2(-14f,-14f), new Vector2(14f,-14f) };
            foreach (var b in bolts)
            {
                var bGO = new GameObject("Bolt", typeof(Image));
                bGO.transform.SetParent(baseGO.transform, false);
                var br2 = bGO.GetComponent<RectTransform>();
                br2.anchorMin = br2.anchorMax = br2.pivot = new Vector2(0.5f, 0.5f);
                br2.sizeDelta = new Vector2(5, 5);
                br2.anchoredPosition = b;
                bGO.GetComponent<Image>().color = new Color(0.34f, 0.37f, 0.44f, 1f);
                bGO.GetComponent<Image>().raycastTarget = false;
            }
            // Aro central (articulación)
            var ringGO = new GameObject("Ring", typeof(Image));
            ringGO.transform.SetParent(baseGO.transform, false);
            var rr = ringGO.GetComponent<RectTransform>();
            rr.anchorMin = rr.anchorMax = rr.pivot = new Vector2(0.5f, 0.5f);
            rr.sizeDelta = new Vector2(24, 24);
            rr.anchoredPosition = Vector2.zero;
            ringGO.GetComponent<Image>().color = new Color(0.34f, 0.37f, 0.44f, 1f);
            ringGO.GetComponent<Image>().raycastTarget = false;
            // Pin central
            var pinGO = new GameObject("Pin", typeof(Image));
            pinGO.transform.SetParent(baseGO.transform, false);
            var prn = pinGO.GetComponent<RectTransform>();
            prn.anchorMin = prn.anchorMax = prn.pivot = new Vector2(0.5f, 0.5f);
            prn.sizeDelta = new Vector2(10, 10);
            prn.anchoredPosition = Vector2.zero;
            pinGO.GetComponent<Image>().color = new Color(0.16f, 0.18f, 0.22f, 1f);
            pinGO.GetComponent<Image>().raycastTarget = false;
            baseGO.SetActive(false);

            // ── PIVOTE (sin visual, solo transform de rotación) ──
            var hombro = new GameObject("BrazoHombro", typeof(RectTransform));
            hombro.transform.SetParent(canvasT, false);
            brazoHombro = hombro.GetComponent<RectTransform>();
            brazoHombro.anchorMin = brazoHombro.anchorMax = brazoHombro.pivot = new Vector2(0.5f, 0.5f);
            brazoHombro.sizeDelta = Vector2.zero;
            brazoHombro.anchoredPosition = pivotPos;
            brazoHombro.localRotation = Quaternion.Euler(0f, 0f, brazoAnguloActual);

            // ── SEGMENTO 1: hombro → codo (naranja oscuro) ──
            var seg1GO = new GameObject("BrazoCuerpo", typeof(Image));
            seg1GO.transform.SetParent(hombro.transform, false);
            brazoCuerpo = seg1GO.GetComponent<RectTransform>();
            brazoCuerpo.anchorMin = brazoCuerpo.anchorMax = new Vector2(0.5f, 0f);
            brazoCuerpo.pivot = new Vector2(0.5f, 0f);
            brazoCuerpo.sizeDelta = new Vector2(9, 80);
            brazoCuerpo.anchoredPosition = Vector2.zero;
            seg1GO.GetComponent<Image>().color = new Color(0.72f, 0.44f, 0.07f, 1f);
            seg1GO.GetComponent<Image>().raycastTarget = false;

            // Destello lateral en Seg1
            var hl1GO = new GameObject("Hl1", typeof(Image));
            hl1GO.transform.SetParent(seg1GO.transform, false);
            var hl1r = hl1GO.GetComponent<RectTransform>();
            hl1r.anchorMin = hl1r.anchorMax = new Vector2(0.5f, 0.5f);
            hl1r.pivot = new Vector2(0.5f, 0.5f);
            hl1r.sizeDelta = new Vector2(3, 70);
            hl1r.anchoredPosition = new Vector2(-2f, 0f);
            hl1GO.GetComponent<Image>().color = new Color(1f, 0.72f, 0.22f, 0.40f);
            hl1GO.GetComponent<Image>().raycastTarget = false;

            // ── SEGMENTO 2: codo → garra (naranja más claro, hijo del extremo de Seg1) ──
            var seg2GO = new GameObject("BrazoSeg2", typeof(Image));
            seg2GO.transform.SetParent(seg1GO.transform, false);
            brazoSeg2 = seg2GO.GetComponent<RectTransform>();
            brazoSeg2.anchorMin = brazoSeg2.anchorMax = new Vector2(0.5f, 1f);   // anclado al extremo de Seg1
            brazoSeg2.pivot = new Vector2(0.5f, 0f);                              // rota desde el codo
            brazoSeg2.sizeDelta = new Vector2(9, 80);
            brazoSeg2.anchoredPosition = Vector2.zero;
            seg2GO.GetComponent<Image>().color = new Color(0.84f, 0.54f, 0.11f, 1f);
            seg2GO.GetComponent<Image>().raycastTarget = false;

            // Destello lateral en Seg2
            var hl2GO = new GameObject("Hl2", typeof(Image));
            hl2GO.transform.SetParent(seg2GO.transform, false);
            var hl2r = hl2GO.GetComponent<RectTransform>();
            hl2r.anchorMin = hl2r.anchorMax = new Vector2(0.5f, 0.5f);
            hl2r.pivot = new Vector2(0.5f, 0.5f);
            hl2r.sizeDelta = new Vector2(3, 70);
            hl2r.anchoredPosition = new Vector2(-2f, 0f);
            hl2GO.GetComponent<Image>().color = new Color(1f, 0.75f, 0.25f, 0.40f);
            hl2GO.GetComponent<Image>().raycastTarget = false;

            // Articulación del codo (base de Seg2)
            var codoGO = new GameObject("Codo", typeof(Image));
            codoGO.transform.SetParent(seg2GO.transform, false);
            var codoR = codoGO.GetComponent<RectTransform>();
            codoR.anchorMin = codoR.anchorMax = codoR.pivot = new Vector2(0.5f, 0f);
            codoR.sizeDelta = new Vector2(15, 15);
            codoR.anchoredPosition = Vector2.zero;
            codoGO.GetComponent<Image>().color = new Color(0.18f, 0.20f, 0.24f, 1f);
            codoGO.GetComponent<Image>().raycastTarget = false;
            var codoIGO = new GameObject("CodoI", typeof(Image));
            codoIGO.transform.SetParent(seg2GO.transform, false);
            var codoIR = codoIGO.GetComponent<RectTransform>();
            codoIR.anchorMin = codoIR.anchorMax = codoIR.pivot = new Vector2(0.5f, 0f);
            codoIR.sizeDelta = new Vector2(8, 8);
            codoIR.anchoredPosition = Vector2.zero;
            codoIGO.GetComponent<Image>().color = new Color(0.36f, 0.40f, 0.48f, 1f);
            codoIGO.GetComponent<Image>().raycastTarget = false;

            // ── GARRA EN LA PUNTA DE SEG2 ──
            var garraGO = new GameObject("Garra", typeof(RectTransform));
            garraGO.transform.SetParent(seg2GO.transform, false);
            var garraRt = garraGO.GetComponent<RectTransform>();
            garraRt.anchorMin = garraRt.anchorMax = new Vector2(0.5f, 1f);
            garraRt.pivot = new Vector2(0.5f, 0f);
            garraRt.sizeDelta = Vector2.zero;
            garraRt.anchoredPosition = Vector2.zero;

            // Palma (base horizontal que une los dos prongs)
            var palmaGO = new GameObject("Palma", typeof(Image));
            palmaGO.transform.SetParent(garraGO.transform, false);
            var palmaR = palmaGO.GetComponent<RectTransform>();
            palmaR.anchorMin = palmaR.anchorMax = palmaR.pivot = new Vector2(0.5f, 0f);
            palmaR.sizeDelta = new Vector2(20, 5);
            palmaR.anchoredPosition = Vector2.zero;
            palmaGO.GetComponent<Image>().color = new Color(0.84f, 0.54f, 0.11f, 1f);
            palmaGO.GetComponent<Image>().raycastTarget = false;

            // Prong izquierdo
            var prongLGO = new GameObject("ProngL", typeof(Image));
            prongLGO.transform.SetParent(garraGO.transform, false);
            brazoProngL = prongLGO.GetComponent<RectTransform>();
            brazoProngL.anchorMin = brazoProngL.anchorMax = new Vector2(0.5f, 0f);
            brazoProngL.pivot = new Vector2(1f, 0f);
            brazoProngL.sizeDelta = new Vector2(7, 22);
            brazoProngL.anchoredPosition = new Vector2(-1f, 5f);
            prongLGO.GetComponent<Image>().color = new Color(0.94f, 0.64f, 0.14f, 1f);
            prongLGO.GetComponent<Image>().raycastTarget = false;
            brazoProngL.localRotation = Quaternion.Euler(0f, 0f, 28f);

            // Prong derecho
            var prongRGO = new GameObject("ProngR", typeof(Image));
            prongRGO.transform.SetParent(garraGO.transform, false);
            brazoProngR = prongRGO.GetComponent<RectTransform>();
            brazoProngR.anchorMin = brazoProngR.anchorMax = new Vector2(0.5f, 0f);
            brazoProngR.pivot = new Vector2(0f, 0f);
            brazoProngR.sizeDelta = new Vector2(7, 22);
            brazoProngR.anchoredPosition = new Vector2(1f, 5f);
            prongRGO.GetComponent<Image>().color = new Color(0.94f, 0.64f, 0.14f, 1f);
            prongRGO.GetComponent<Image>().raycastTarget = false;
            brazoProngR.localRotation = Quaternion.Euler(0f, 0f, -28f);

            // Item transportado (entre los prongs)
            brazoPiezaGO = new GameObject("BrazoPieza", typeof(Image));
            brazoPiezaGO.transform.SetParent(garraGO.transform, false);
            var pr = brazoPiezaGO.GetComponent<RectTransform>();
            pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(0.5f, 0.5f);
            pr.sizeDelta = new Vector2(12, 12);
            pr.anchoredPosition = new Vector2(0f, 14f);
            brazoPiezaGO.GetComponent<Image>().color = new Color(1f, 0.90f, 0.35f, 1f);
            brazoPiezaGO.GetComponent<Image>().raycastTarget = false;
            brazoPiezaGO.SetActive(false);

            hombro.SetActive(false);
        }

        /// <summary>Angulo Z (grados) que hace que un brazo apuntando hacia +Y local apunte hacia dir.</summary>
        float AnguloHacia(Vector2 dir)
        {
            // Derivación: arm tip en local +Y rotado por Z=A → canvas offset = (-sin A, cos A)*len
            // Queremos (-sin A, cos A) = normalize(dir) → A = atan2(-dir.x, dir.y)
            return Mathf.Atan2(-dir.x, dir.y) * Mathf.Rad2Deg;
        }

        void ActualizarBaul()
        {
            if (!BaulComprado) return;
            if (baulTexto != null)
            {
                baulTexto.text = baulItems + "/" + BaulCapacidad;
                baulTexto.color = baulItems == 0
                    ? new Color(0.5f, 0.45f, 0.35f)
                    : new Color(0.9f, 0.8f, 0.5f);
            }
        }

        void MeterItemEnBaul()
        {
            if (!BaulComprado)      return;
            if (enTransicion)       return;
            if (esLote)             return;
            if (itemActual == null) return;
            if (trabajoActual != TipoTrabajo.Limpieza) return;
            if (baulItems >= BaulCapacidad)
            {
                Flotante(baulRect, "LLENO", new Color(0.9f, 0.35f, 0.25f), 20, new Vector2(0f, 30f));
                return;
            }
            baulItems++;
            NuevoTrabajo();
            Sonido(sfxCompra, 0.75f);
            Flotante(baulRect, "+1", new Color(1f, 0.88f, 0.35f), 22, new Vector2(0f, 38f));
            StartCoroutine(PulsarEscala(baulRect, 1.18f, 0.20f));
        }

        IEnumerator PulsarEscala(RectTransform rt, float pico, float dur)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = t / dur;
                rt.localScale = Vector3.one * (1f + (pico - 1f) * Mathf.Sin(k * Mathf.PI));
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        int SlotLibreIndex()
        {
            for (int i = 0; i < RanurasTotal; i++)
                if (!slotActivo[i]) return i;
            return -1;
        }

        // ----- Animación de caída al comprar componente -----
        IEnumerator AnimarCaida(RectTransform rt, Vector2 posFinal)
        {
            rt.anchoredPosition = new Vector2(posFinal.x, posFinal.y + 520f);
            rt.gameObject.SetActive(true);
            float t = 0f; const float dur = 0.55f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = t / dur;
                float y = posFinal.y + 520f * (1f - BounceEase(k));
                rt.anchoredPosition = new Vector2(posFinal.x, y);
                yield return null;
            }
            rt.anchoredPosition = posFinal;
        }

        static float BounceEase(float t)
        {
            if (t < 1f / 2.75f)  return 7.5625f * t * t;
            if (t < 2f / 2.75f)  { t -= 1.5f   / 2.75f; return 7.5625f * t * t + 0.75f; }
                                  { t -= 2.625f / 2.75f; return 7.5625f * t * t + 0.9375f; }
        }

        // ----- Ciclo principal del brazo automático -----
        IEnumerator CicloBrazo()
        {
            brazoCoroutineActiva = true;
            // Garra abierta + brazo en reposo en el baúl
            if (brazoProngL != null) brazoProngL.localRotation = Quaternion.Euler(0f, 0f,  28f);
            if (brazoProngR != null) brazoProngR.localRotation = Quaternion.Euler(0f, 0f, -28f);
            brazoAnguloActual = angleBaulPivot;
            if (brazoHombro != null) brazoHombro.localRotation = Quaternion.Euler(0f, 0f, brazoAnguloActual);

            while (true)
            {
                // Esperar en el baúl hasta que la cadena esté lista y haya items + hueco
                while (!CadenaCompleta || baulItems <= 0 || SlotLibreIndex() < 0)
                    yield return new WaitForSeconds(0.4f);

                // El ciclo completo dura exactamente BrazoCooldown.
                // Tiempo fijo de acciones: garra_cerrar(0.13) + pausa_agarre(0.10)
                //                        + garra_abrir(0.13)  + pausa_deposito(0.15) = 0.51s
                // Cada pierna = (BrazoCooldown - 0.51) / 2
                const float TiempoAcciones = 0.51f;
                float pierna = Mathf.Max(0.20f, (BrazoCooldown - TiempoAcciones) * 0.5f);

                // 1. Cerrar garra: coger item del baúl
                yield return StartCoroutine(AnimarGarra(true));
                ItemDef item = ItemAleatorio(it => it.limpiable && !it.soloLote);
                if (item == null)
                {
                    yield return StartCoroutine(AnimarGarra(false));
                    yield return new WaitForSeconds(1f);
                    continue;
                }
                baulItems = Mathf.Max(0, baulItems - 1);
                if (brazoPiezaGO != null)
                {
                    brazoPiezaGO.transform.localScale = Vector3.zero;
                    brazoPiezaGO.SetActive(true);
                    StartCoroutine(EscalarHasta(brazoPiezaGO.transform, 0.14f));
                }
                Sonido(sfxCompra, 0.65f);
                yield return new WaitForSeconds(0.10f);

                // 2. IDA: girar a tolva en exactamente `pierna` segundos
                yield return StartCoroutine(RotarBrazo(angleTolvaPivot, pierna));

                // 3. Depositar en ranura
                int slot = SlotLibreIndex();
                if (slot >= 0)
                {
                    slotActivo[slot] = true;
                    slotProg[slot]   = 0;
                    slotValor[slot]  = item.valorDinero;
                    Sonido(sfxCompra, 0.9f);
                    Flotante(tolvaRect, "▼", new Color(0.7f, 0.9f, 0.6f), 22, new Vector2(0f, 20f));
                }
                yield return StartCoroutine(AnimarGarra(false));
                if (brazoPiezaGO != null) brazoPiezaGO.SetActive(false);
                yield return new WaitForSeconds(0.15f);

                // 4. VUELTA: regresar al baúl en exactamente `pierna` segundos
                yield return StartCoroutine(RotarBrazo(angleBaulPivot, pierna));
                // → ciclo total ≈ BrazoCooldown; al subir nivel baja el cooldown y el brazo va más rápido
            }
        }

        IEnumerator AnimarGarra(bool cerrar)
        {
            if (brazoProngL == null || brazoProngR == null) yield break;
            // Cerrar: prongs a 0° (paralelos). Abrir: prongs a ±28°.
            float targetL = cerrar ? 0f :  28f;
            float targetR = cerrar ? 0f : -28f;
            float dur = 0.13f, t = 0f;
            float startL = brazoProngL.localEulerAngles.z; if (startL > 180f) startL -= 360f;
            float startR = brazoProngR.localEulerAngles.z; if (startR > 180f) startR -= 360f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                brazoProngL.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(startL, targetL, k));
                brazoProngR.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(startR, targetR, k));
                yield return null;
            }
            brazoProngL.localRotation = Quaternion.Euler(0f, 0f, targetL);
            brazoProngR.localRotation = Quaternion.Euler(0f, 0f, targetR);
        }

        IEnumerator RotarBrazo(float targetAngle, float durFija = -1f)
        {
            float startAngle = brazoAnguloActual;
            float span = Mathf.Abs(Mathf.DeltaAngle(startAngle, targetAngle));
            if (span < 0.5f) yield break;
            float duration = durFija > 0f ? durFija : span / BrazoVelGiro;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t    = Mathf.Clamp01(elapsed / duration);
                float tE   = EaseInOut(t);
                brazoAnguloActual = Mathf.LerpAngle(startAngle, targetAngle, tE);
                if (brazoHombro != null)
                    brazoHombro.localRotation = Quaternion.Euler(0f, 0f, brazoAnguloActual);
                // Codo: arco sinusoidal (se curva en el centro del giro, plano al inicio y al final)
                float bend = Mathf.Sin(t * Mathf.PI) * 26f;
                if (brazoSeg2 != null)
                    brazoSeg2.localRotation = Quaternion.Euler(0f, 0f, bend);
                yield return null;
            }
            brazoAnguloActual = targetAngle;
            if (brazoHombro != null)
                brazoHombro.localRotation = Quaternion.Euler(0f, 0f, brazoAnguloActual);
            if (brazoSeg2 != null)
                brazoSeg2.localRotation = Quaternion.Euler(0f, 0f, 0f);
        }

        static float EaseInOut(float t) =>
            t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;

        IEnumerator EscalarHasta(Transform tr, float dur)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                tr.localScale = Vector3.one * EaseInOut(Mathf.Clamp01(t / dur));
                yield return null;
            }
            tr.localScale = Vector3.one;
        }

        // ================= ARRASTRE Y PUNTOS DE CONEXIÓN =================

        void HacerArrastrable(RectTransform comp)
        {
            var et = comp.gameObject.AddComponent<EventTrigger>();
            RegistrarTrigger(et, EventTriggerType.BeginDrag, d => IniciarArrastre(comp, (PointerEventData)d));
            RegistrarTrigger(et, EventTriggerType.Drag,      d => DuranteArrastre((PointerEventData)d));
            RegistrarTrigger(et, EventTriggerType.EndDrag,   d => FinArrastre((PointerEventData)d));
        }

        void RegistrarTrigger(EventTrigger et, EventTriggerType tipo, UnityAction<BaseEventData> cb)
        {
            var entry = new EventTrigger.Entry { eventID = tipo };
            entry.callback.AddListener(cb);
            et.triggers.Add(entry);
        }

        Vector2 ScreenToCanvas(Vector2 screenPos)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, null, out var p);
            return p;
        }

        void IniciarArrastre(RectTransform comp, PointerEventData e)
        {
            compEnArrastre = comp;
            offsetArrastre = comp.anchoredPosition - ScreenToCanvas(e.position);
        }

        void DuranteArrastre(PointerEventData e)
        {
            if (compEnArrastre == null) return;
            Vector2 novaPos = ScreenToCanvas(e.position) + offsetArrastre;
            compEnArrastre.anchoredPosition = novaPos;
            // Brazo: mover también el pivote rotante (misma posición que la base)
            if (compEnArrastre == brazoBaseRect && brazoHombro != null)
                brazoHombro.anchoredPosition = novaPos;
            RecalcularAngulosBrazo();
            if (compEnArrastre == tolvaRect) ActualizarConectorTolva();
        }

        void FinArrastre(PointerEventData e)
        {
            if (compEnArrastre == null) return;
            RecalcularAngulosBrazo();
            if (compEnArrastre == tolvaRect) ActualizarConectorTolva();
            compEnArrastre = null;
        }

        void RecalcularAngulosBrazo()
        {
            if (brazoHombro == null) return;
            Vector2 pivot = brazoHombro.anchoredPosition;
            if (baulRect  != null && BaulComprado)  angleBaulPivot  = AnguloHacia(baulRect.anchoredPosition  - pivot);
            if (tolvaRect != null && TolvaComprada) angleTolvaPivot = AnguloHacia(tolvaRect.anchoredPosition - pivot);
        }

        void ActualizarIndicadoresConexion()
        {
            bool bb = BaulBrazoEnRango;
            bool bt = BrazoTolvaEnRango;
            bool tm = TolvaMaquinaEnRango;
            pcBaulOut?.Refrescar(bb);
            pcBrazoIn?.Refrescar(bb);
            pcBrazoOut?.Refrescar(bt);
            pcTolvaIn?.Refrescar(bt);
            pcTolvaOut?.Refrescar(tm);
            pcMaqIn?.Refrescar(tm);
        }

        void ActualizarConectorTolva()
        {
            if (tolvaConectorRect == null || tolvaRect == null || machineRect == null) return;
            float bottom = tolvaRect.anchoredPosition.y - 50f;
            float top    = machineRect.anchoredPosition.y + 140f;
            float h      = Mathf.Max(2f, bottom - top);
            tolvaConectorRect.anchoredPosition = new Vector2(tolvaRect.anchoredPosition.x, top + h * 0.5f);
            tolvaConectorRect.sizeDelta = new Vector2(10f, h + 4f);
        }

        PuntoConexion CrearIndicador(Transform parent, RectTransform dueño, Vector2 offset)
        {
            var go = new GameObject("PC", typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(14, 14);
            rt.anchoredPosition = offset;
            var img = go.GetComponent<Image>();
            img.color = new Color(0.45f, 0.48f, 0.55f, 0.9f);
            img.raycastTarget = false;
            return new PuntoConexion { dueño = dueño, offsetLocal = offset, dot = img };
        }
    }
}
