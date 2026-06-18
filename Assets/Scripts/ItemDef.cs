using UnityEngine;

namespace PawnShop
{
    /// <summary>
    /// Forma visual del objeto. Apunta al generador de pixel art en PixelArtFactory.
    /// Cuando haya PNGs propios, se puede sustituir por un campo Sprite sin tocar el resto.
    /// </summary>
    public enum FormaItem { Anillo, Reloj, Gema }

    /// <summary>
    /// DATOS de un objeto (joya/reloj) como ScriptableObject editable en el Inspector.
    /// Separa los datos de la lógica: añadir un objeto nuevo = crear un asset, no tocar código.
    /// Preparado para Fase 2 (compraventa): aquí irán valor de compra, falsificable, defectos, etc.
    /// </summary>
    [CreateAssetMenu(fileName = "Item", menuName = "PawnShop/Item", order = 0)]
    public class ItemDef : ScriptableObject
    {
        [Header("Identidad")]
        public string nombreItem = "Item";
        public FormaItem forma = FormaItem.Anillo;

        [Header("Recompensas (Fase 1: servicio)")]
        public double valorDinero = 6;     // Dinero al completarlo (limpio o reparado).
        public double recompensaExp = 3;   // EXP total que reparte mientras lo limpias a mano.

        [Header("En qué trabajos puede aparecer")]
        public bool limpiable = true;      // puede salir como trabajo de LIMPIEZA (frotado).
        public bool reparable = false;     // puede salir como trabajo de REPARACIÓN (barra).
        public float frecuencia = 1f;      // peso de aparición (1 = normal; <1 = más raro, p.ej. turbios).

        [Header("Tono / objeto turbio")]
        public bool turbio = false;        // guiño cómico/moral: mercancía dudosa (se resalta en rojizo).
        [TextArea] public string fraseAlAparecer;   // frase que dice/aparece al llegar el objeto.

        [Header("Lotes (gambling)")]
        public bool soloLote = false;      // true = no aparece como cliente normal; solo sale de lotes.
        public bool esJackpot = false;     // true = PREMIO GORDO (feedback especial al revelarlo).
        public bool esBomba = false;       // true = BOMBA: al revelarlo pierdes dinero (penalizacion).
        public double penalizacion = 0;    // si esBomba: dinero que pierdes al descubrirlo.

        [Header("Notas de diseño")]
        [TextArea] public string descripcion;   // nota interna; no se muestra en juego.
    }
}
