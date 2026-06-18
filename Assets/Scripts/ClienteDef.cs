using UnityEngine;

namespace PawnShop
{
    /// <summary>
    /// Arquetipo de cliente. En Fase 1 define cara + frases + propina.
    /// En Fase 3 (regateo) determinará el precio tope secreto y los "tells". (GDD §7)
    /// </summary>
    public enum Arquetipo { Normal, Desesperado, Sabelotodo, Trilero, Pija, Yonki }

    /// <summary>
    /// DATOS de un cliente como ScriptableObject editable. El cliente trae el objeto, saluda,
    /// reacciona al terminar y a veces deja propina. La cara se genera por código (PixelArtFactory).
    /// </summary>
    [CreateAssetMenu(fileName = "Cliente", menuName = "PawnShop/Cliente", order = 1)]
    public class ClienteDef : ScriptableObject
    {
        [Header("Identidad")]
        public string nombreCliente = "Cliente";
        public Arquetipo arquetipo = Arquetipo.Normal;

        [Header("Cara (pixel art procedural)")]
        public Color piel = new Color(0.95f, 0.78f, 0.62f);
        public Color pelo = new Color(0.30f, 0.20f, 0.12f);
        public Color ropa = new Color(0.35f, 0.40f, 0.50f);

        [Header("Frases (tono cómico-gamberro)")]
        [TextArea] public string saludo   = "Buenas, ¿me lo dejas como nuevo?";
        [TextArea] public string contento = "¡Gracias, majo!";
        [TextArea] public string enfadado = "¡Pero serás manazas!";

        [Header("Propina (solo al servir A MANO)")]
        [Range(0f, 1f)] public float propinaProb = 0.30f;   // probabilidad de dejar propina.
        [Range(0f, 1f)] public float propinaMax  = 0.40f;   // propina máx como fracción del valor del objeto.
    }
}
