using UnityEngine;

namespace PawnShop
{
    /// <summary>
    /// Genera efectos de sonido POR CÓDIGO (sin archivos de audio externos).
    /// Cada clip se sintetiza con ondas simples + envolvente. Se reproducen con AudioSource.PlayOneShot.
    /// </summary>
    public static class AudioFactory
    {
        const int SR = 44100;   // sample rate

        static float Sine(float f, float t) => Mathf.Sin(2f * Mathf.PI * f * t);

        /// <summary>Envolvente: ataque corto + caída exponencial (suena "natural", no clicka).</summary>
        static float Env(float t, float dur, float ataque = 0.005f)
        {
            float a = t < ataque ? t / ataque : 1f;
            float caida = Mathf.Exp(-3.5f * (t / dur));
            return a * caida;
        }

        static AudioClip Construir(string nombre, float dur, System.Func<float, float> onda)
        {
            int n = Mathf.Max(1, (int)(SR * dur));
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SR;
                s[i] = Mathf.Clamp(onda(t), -1f, 1f);
            }
            var c = AudioClip.Create(nombre, n, 1, SR, false);
            c.SetData(s, 0);
            return c;
        }

        /// <summary>Cobro: arpegio ascendente (caja registradora alegre).</summary>
        public static AudioClip Cobro()
        {
            const float dur = 0.30f;
            return Construir("sfx_cobro", dur, t =>
            {
                float f = t < 0.12f ? 784f : 1175f;            // G5 -> D6
                float tono = Sine(f, t) * 0.6f + Sine(2f * f, t) * 0.15f;
                return tono * Env(t, dur) * 0.5f;
            });
        }

        /// <summary>Acierto de pieza (reparación): blip corto agudo.</summary>
        public static AudioClip Acierto()
        {
            const float dur = 0.12f;
            return Construir("sfx_acierto", dur, t => Sine(1046f, t) * Env(t, dur, 0.003f) * 0.45f);
        }

        /// <summary>Rotura: crujido descendente con ruido (mal rollo cómico).</summary>
        public static AudioClip Rotura()
        {
            const float dur = 0.38f;
            var rng = new System.Random(12345);
            return Construir("sfx_rotura", dur, t =>
            {
                float f = Mathf.Lerp(320f, 70f, t / dur);
                float tono = Sine(f, t);
                float ruido = (float)(rng.NextDouble() * 2.0 - 1.0);
                float mix = tono * 0.6f + ruido * 0.5f;
                return Mathf.Clamp(mix * 1.6f, -1f, 1f) * Env(t, dur, 0.002f) * 0.5f;   // distorsión leve
            });
        }

        /// <summary>Frote: roce de cepillo (ruido filtrado, suave). Se le varía el pitch al reproducir.</summary>
        public static AudioClip Frote()
        {
            const float dur = 0.09f;
            var rng = new System.Random(777);
            float prev = 0f;
            return Construir("sfx_frote", dur, t =>
            {
                float ruido = (float)(rng.NextDouble() * 2.0 - 1.0);
                prev = Mathf.Lerp(prev, ruido, 0.3f);          // paso bajo simple -> roce, no siseo
                return prev * Env(t, dur, 0.01f) * 0.28f;
            });
        }

        /// <summary>Compra de mejora: doble clic/moneda.</summary>
        public static AudioClip Compra()
        {
            const float dur = 0.18f;
            return Construir("sfx_compra", dur, t =>
            {
                float f = t < 0.07f ? 880f : 1320f;
                return Sine(f, t) * Env(t, dur, 0.003f) * 0.4f;
            });
        }
    }
}
