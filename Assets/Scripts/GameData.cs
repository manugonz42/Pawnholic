using System;

namespace PawnShop
{
    /// <summary>
    /// Estado del juego que se guarda en disco. Solo son datos, sin lógica.
    /// Todo lo que deba persistir entre sesiones vive aquí.
    /// </summary>
    [Serializable]
    public class GameData
    {
        public double dinero = 0;             // Dinero (se gana al completar objetos; se gasta en comprar cosas).
        public double exp = 0;                // Experiencia (se gana frotando; se gasta en habilidades).
        public int nivelFrotado = 0;          // Habilidad "Fuerza de frotado" (limpia más fuerte). Se mejora con EXP.
        public int nivelCepillo = 0;          // Habilidad "Tamaño de cepillo" (área de frotado más grande). Se mejora con EXP.
        public int nivelManoFirme = 0;        // Habilidad "Mano firme" (más zona verde y menos romper al reparar). Se mejora con EXP.
        public int ayudantes = 0;             // (Obsoleto) antiguo ayudante pasivo; sustituido por la máquina de pulido.
        public int maquinaComprada = 0;       // 0 = aún no comprada (solo trabajo a mano); 1 = comprada (hito). Se compra con DINERO.
        public int nivelMaquinaVel = 0;       // Máquina: velocidad de pulido. Se compra con DINERO.
        public int nivelMaquinaRanuras = 0;   // Máquina: nº de ranuras. Se compra con DINERO.
        public int nivelBaul = 0;             // Máquina: baúl para acumular items.
        public int nivelCinta = 0;            // Máquina: cinta transportadora.
        public int nivelBrazo = 0;            // Máquina: brazo que auto-alimenta la tolva (automatiza).
        public int nivelTolva = 0;            // Máquina: tamaño de tolva (auto-alimenta más rápido).
        public long ultimoCierreBinario = 0;  // Momento del último guardado (DateTime.ToBinary) para el progreso offline.

        // --- Sistema de Geodos ---
        public int nivelOjo = 0;              // "Ojo de tasador": mejora probabilidades al abrir geodos.
        public int nivelTriturador = 0;       // Automatización: compra y parte geodos solo.
        public int nivelCintaGeodo = 0;       // Automatización: alimenta al triturador (distinto de nivelCinta de la máquina).
        public int nivelClasificador = 0;     // Automatización: separa gemas y las manda al baúl.
        public int nivelOjoElectronico = 0;   // Automatización: aplica nivelOjo al procesado automático.
    }
}
