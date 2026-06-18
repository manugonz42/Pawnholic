using UnityEngine;

namespace PawnShop
{
    /// <summary>
    /// Genera texturas de pixel art POR CÓDIGO (sin necesidad de imágenes externas).
    /// Se dibuja en baja resolución y se muestra con filtro Point para que se vea pixelado.
    ///
    /// Cuando tengas un PNG propio, basta con meterlo en Assets/ y usarlo en su lugar:
    /// el resto del juego no cambia.
    /// </summary>
    public static class PixelArtFactory
    {
        /// <summary>Fondo: interior de una casa de empeños de noche.</summary>
        public static Texture2D CrearFondoTienda(int W = 320, int H = 180)
        {
            var px = new Color32[W * H];

            // ---- Paleta ----
            Color32 pared      = new Color32(150, 110, 78, 255);
            Color32 tablon     = new Color32(105, 74, 50, 255);
            Color32 mostrador  = new Color32(96, 62, 36, 255);
            Color32 mostradorT = new Color32(142, 96, 56, 255);
            Color32 marco      = new Color32(58, 38, 24, 255);
            Color32 cielo      = new Color32(26, 30, 64, 255);
            Color32 luna       = new Color32(235, 232, 205, 255);
            Color32 estrella   = new Color32(255, 255, 245, 255);
            Color32 estante    = new Color32(112, 80, 54, 255);
            Color32 oro        = new Color32(220, 180, 70, 255);
            Color32 cristal    = new Color32(230, 220, 190, 255);
            Color32 jarron     = new Color32(70, 170, 160, 255);
            Color32 caja       = new Color32(180, 60, 60, 255);

            // ---- Pared y tablones ----
            Fill(px, W, H, 0, 0, W, H, pared);
            for (int y = 56; y < H; y += 16) HLine(px, W, H, 0, W - 1, y, tablon);

            // ---- Ventana con cielo nocturno ----
            int wx = 26, wy = 92, ww = 80, wh = 66;
            Fill(px, W, H, wx, wy, ww, wh, cielo);
            int[,] estrellas = { { 40, 140 }, { 55, 150 }, { 70, 135 }, { 92, 148 }, { 50, 118 }, { 84, 126 }, { 64, 152 } };
            for (int i = 0; i < estrellas.GetLength(0); i++) Plot(px, W, H, estrellas[i, 0], estrellas[i, 1], estrella);
            FillCircle(px, W, H, 88, 146, 7, luna);
            FillCircle(px, W, H, 91, 148, 6, cielo);      // medialuna (recorte)
            RectOutline(px, W, H, wx - 2, wy - 2, ww + 4, wh + 4, marco, 2);
            VLine(px, W, H, wx + ww / 2, wy, wy + wh - 1, marco);
            HLine(px, W, H, wx, wx + ww - 1, wy + wh / 2, marco);

            // ---- Estantería a la derecha con objetos ----
            Fill(px, W, H, 196, 118, 110, 5, estante);
            FillCircle(px, W, H, 214, 134, 9, oro);        // reloj
            FillCircle(px, W, H, 214, 134, 5, cristal);
            Fill(px, W, H, 238, 124, 11, 20, jarron);       // jarrón
            Fill(px, W, H, 240, 142, 7, 4, marco);
            Fill(px, W, H, 264, 124, 18, 13, caja);         // cajita
            HLine(px, W, H, 264, 281, 130, oro);

            // ---- Mostrador (madera) en la parte baja ----
            Fill(px, W, H, 0, 0, W, 56, mostrador);
            HLine(px, W, H, 0, W - 1, 55, mostradorT);
            HLine(px, W, H, 0, W - 1, 54, mostradorT);
            for (int x = 16; x < W; x += 48) VLine(px, W, H, x, 0, 53, marco);

            // ---- Lámpara colgante que ilumina el mostrador ----
            VLine(px, W, H, 160, 168, H - 1, marco);
            Fill(px, W, H, 150, 162, 21, 6, new Color32(42, 42, 48, 255));
            FillCircle(px, W, H, 160, 160, 3, new Color32(255, 240, 180, 255));

            // ---- Luz cálida + viñeta para dar ambiente ----
            AplicarLuzCalida(px, W, H, 160, 120, 135f, 0.20f);
            AplicarVigneta(px, W, H, 0.5f);

            var t = new Texture2D(W, H, TextureFormat.RGBA32, false);
            t.SetPixels32(px);
            t.Apply();
            t.filterMode = FilterMode.Point;          // <- clave para el look pixel art
            t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }

        // Región de la "pantalla" del sprite donde se dibujan las ranuras (en coords 0..1).
        // Debe coincidir con el rectángulo de pantalla que se pinta en CrearMaquinaPulido.
        public const float PantallaXMin = 60f / 128f, PantallaXMax = 106f / 128f;
        public const float PantallaYMin = 42f / 128f, PantallaYMax = 96f / 128f;

        // Centro y radio de la rueda de pulido dentro del sprite de 128 (para superponer el overlay
        // giratorio en runtime). Normalizados 0..1 para colocar el overlay sobre el RectTransform.
        const int RuedaCx = 42, RuedaCy = 58, RuedaR = 20;
        public const float RuedaCentroX = RuedaCx / 128f;      // 0.328
        public const float RuedaCentroY = RuedaCy / 128f;      // 0.453
        public const float RuedaTam     = (2 * (RuedaR + 4)) / 128f;   // fracción del lado para el overlay

        /// <summary>Sprite detallado de la máquina de pulido (fondo transparente: solo la silueta).</summary>
        public static Texture2D CrearMaquinaPulido(int S = 128)
        {
            var px = new Color32[S * S];   // por defecto transparente (alpha 0)

            Color32 metal    = new Color32(116, 124, 138, 255);
            Color32 metalCl  = new Color32(158, 166, 180, 255);
            Color32 metalOsc = new Color32(80, 86, 100, 255);
            Color32 borde    = new Color32(44, 48, 58, 255);
            Color32 rivet    = new Color32(58, 64, 76, 255);
            Color32 padSomb  = new Color32(176, 178, 192, 255);   // alojamiento de la rueda
            Color32 pantalla = new Color32(18, 22, 28, 255);
            Color32 pantBor  = new Color32(38, 44, 54, 255);
            Color32 luz      = new Color32(120, 235, 140, 255);
            Color32 mando    = new Color32(168, 132, 64, 255);
            Color32 tolva    = new Color32(100, 106, 120, 255);
            Color32 tolvaOsc = new Color32(70, 76, 90, 255);

            // ---- Patas y base ----
            Fill(px, S, S, 26, 2, 12, 16, metalOsc);
            Fill(px, S, S, 90, 2, 12, 16, metalOsc);
            Fill(px, S, S, 20, 14, 88, 10, metal);
            HLine(px, S, S, 20, 107, 23, metalCl);

            // ---- Cuerpo ----
            Fill(px, S, S, 20, 22, 88, 82, metal);
            Fill(px, S, S, 20, 22, 6, 82, metalOsc);     // sombra izquierda
            Fill(px, S, S, 102, 22, 6, 82, metalCl);     // brillo derecha
            RectOutline(px, S, S, 20, 22, 88, 82, borde, 2);
            HLine(px, S, S, 23, 104, 102, metalCl);      // brillo superior
            int[] filas = { 30, 50, 70, 92 };
            foreach (int y in filas) { FillCircle(px, S, S, 26, y, 1, rivet); FillCircle(px, S, S, 101, y, 1, rivet); }

            // ---- Tolva (embudo) arriba ----
            for (int y = 104; y <= 119; y++)
            {
                float t = (y - 104) / 15f;
                int xl = (int)Mathf.Lerp(60, 48, t);
                int xr = (int)Mathf.Lerp(84, 96, t);
                HLine(px, S, S, xl, xr, y, (y > 116) ? tolvaOsc : tolva);
            }
            HLine(px, S, S, 48, 96, 119, metalCl);

            // ---- Rueda de pulido: SOLO el alojamiento ----
            // La parte giratoria (disco + radios) se superpone como overlay y se anima en runtime
            // (ver CrearRuedaPulido + RuedaPulido* abajo). Aquí solo el aro/hueco que la rodea.
            FillCircle(px, S, S, RuedaCx, RuedaCy, RuedaR + 3, borde);
            FillCircle(px, S, S, RuedaCx, RuedaCy, RuedaR + 1, padSomb);
            FillCircle(px, S, S, RuedaCx, RuedaCy, RuedaR - 1, metalOsc);   // hueco oscuro de fondo

            // ---- Pantalla (aquí se pintan las ranuras en runtime) ----
            Fill(px, S, S, 60, 42, 46, 54, pantalla);
            RectOutline(px, S, S, 60, 42, 46, 54, pantBor, 2);

            // ---- Mandos y luz ----
            FillCircle(px, S, S, 50, 34, 4, mando); FillCircle(px, S, S, 50, 34, 2, metalOsc);
            FillCircle(px, S, S, 70, 34, 4, mando); FillCircle(px, S, S, 70, 34, 2, metalOsc);
            FillCircle(px, S, S, 99, 99, 3, luz);

            var t2 = new Texture2D(S, S, TextureFormat.RGBA32, false);
            t2.SetPixels32(px);
            t2.Apply();
            t2.filterMode = FilterMode.Point;
            t2.wrapMode = TextureWrapMode.Clamp;
            return t2;
        }

        /// <summary>Disco giratorio de la rueda de pulido (overlay transparente que se rota en runtime).</summary>
        public static Texture2D CrearRuedaPulido(int S = 48)
        {
            var px = new Color32[S * S];
            Color32 pad     = new Color32(222, 222, 232, 255);
            Color32 padSomb = new Color32(176, 178, 192, 255);
            Color32 hub     = new Color32(96, 98, 110, 255);
            Color32 borde   = new Color32(44, 48, 58, 255);

            int cx = S / 2, cy = S / 2;
            int r = S / 2 - 4;                 // disco
            FillCircle(px, S, S, cx, cy, r, padSomb);
            FillCircle(px, S, S, cx, cy, r - 2, pad);
            for (int a = 0; a < 12; a++)       // radios (dan sensación de giro)
            {
                float ang = a * Mathf.PI / 6f;
                LineaSimple(px, S, S, cx, cy, cx + (int)(Mathf.Cos(ang) * (r - 3)), cy + (int)(Mathf.Sin(ang) * (r - 3)), hub);
            }
            FillCircle(px, S, S, cx, cy, r / 3, hub);
            FillCircle(px, S, S, cx, cy, 2, borde);

            var t = new Texture2D(S, S, TextureFormat.RGBA32, false);
            t.SetPixels32(px);
            t.Apply();
            t.filterMode = FilterMode.Point;
            t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }

        /// <summary>Icono de "lote misterioso": un saco atado con un "?" (se muestra mientras lo limpias).</summary>
        public static Texture2D CrearCajaMisterio(int S = 64)
        {
            var px = new Color32[S * S];
            Color32 saco    = new Color32(150, 120, 70, 255);
            Color32 sacoOsc = new Color32(110, 86, 48, 255);
            Color32 cuerda  = new Color32(86, 66, 38, 255);
            Color32 q       = new Color32(245, 225, 120, 255);

            // Cuerpo del saco.
            Fill(px, S, S, 12, 4, 40, 26, saco);
            FillCircle(px, S, S, 32, 28, 21, saco);
            Fill(px, S, S, 12, 4, 8, 26, sacoOsc);          // sombra izquierda
            // Cuello atado.
            Fill(px, S, S, 22, 44, 20, 5, sacoOsc);
            HLine(px, S, S, 22, 41, 46, cuerda);
            FillCircle(px, S, S, 23, 52, 4, saco);
            FillCircle(px, S, S, 41, 52, 4, saco);

            // Interrogante.
            Fill(px, S, S, 31, 8, 4, 4, q);                  // punto
            Fill(px, S, S, 31, 16, 4, 6, q);                 // tallo
            Fill(px, S, S, 31, 22, 8, 3, q);
            Fill(px, S, S, 36, 22, 3, 8, q);
            Fill(px, S, S, 27, 30, 12, 3, q);
            Fill(px, S, S, 27, 26, 3, 5, q);

            var t = new Texture2D(S, S, TextureFormat.RGBA32, false);
            t.SetPixels32(px);
            t.Apply();
            t.filterMode = FilterMode.Point;
            t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }

        // ----------------- CARAS de clientes -----------------

        static Color32 Escala(Color32 c, float k) => new Color32(
            (byte)Mathf.Min(255, c.r * k), (byte)Mathf.Min(255, c.g * k), (byte)Mathf.Min(255, c.b * k), c.a);

        /// <summary>Retrato pixel-art de un cliente. Cara base + accesorio según arquetipo.</summary>
        public static Texture2D CrearCara(Color32 piel, Color32 pelo, Color32 ropa, Arquetipo arq, int S = 64)
        {
            var px = new Color32[S * S];   // transparente
            Color32 sombra = Escala(piel, 0.82f);
            Color32 ojo   = new Color32(40, 30, 28, 255);
            Color32 boca  = new Color32(120, 60, 55, 255);
            Color32 metal = new Color32(60, 60, 70, 255);
            Color32 azul  = new Color32(120, 180, 235, 255);
            Color32 perla = new Color32(240, 238, 230, 255);

            int cx = 32, cy = 34;

            // Hombros / ropa.
            Fill(px, S, S, 8, 0, 48, 14, ropa);
            HLine(px, S, S, 8, 55, 13, Escala(ropa, 1.18f));
            // Cuello.
            Fill(px, S, S, 28, 10, 8, 8, sombra);
            // Orejas + cabeza.
            FillCircle(px, S, S, 15, cy, 3, piel);
            FillCircle(px, S, S, 49, cy, 3, piel);
            FillCircle(px, S, S, cx, cy, 17, piel);
            // Pelo (gorro en la parte alta de la cabeza).
            for (int y = -18; y <= 18; y++)
                for (int x = -18; x <= 18; x++)
                    if (x * x + y * y <= 18 * 18 && y >= 4)
                        Plot(px, S, S, cx + x, cy + y, pelo);
            // Ojos.
            FillCircle(px, S, S, cx - 6, cy + 2, 2, ojo);
            FillCircle(px, S, S, cx + 6, cy + 2, 2, ojo);
            // Boca.
            HLine(px, S, S, cx - 4, cx + 4, cy - 5, boca);

            // Accesorio por arquetipo (lo que les da personalidad de un vistazo).
            switch (arq)
            {
                case Arquetipo.Sabelotodo:   // gafas
                    RectOutline(px, S, S, cx - 10, cy - 1, 8, 7, metal, 1);
                    RectOutline(px, S, S, cx + 2,  cy - 1, 8, 7, metal, 1);
                    HLine(px, S, S, cx - 2, cx + 2, cy + 2, metal);
                    break;
                case Arquetipo.Trilero:      // bigote
                    Fill(px, S, S, cx - 6, cy - 3, 12, 2, Escala(pelo, 0.9f));
                    break;
                case Arquetipo.Pija:         // collar de perlas
                    FillCircle(px, S, S, cx - 8, 13, 1, perla);
                    FillCircle(px, S, S, cx,     12, 1, perla);
                    FillCircle(px, S, S, cx + 8, 13, 1, perla);
                    break;
                case Arquetipo.Yonki:        // gotas de sudor
                    FillCircle(px, S, S, cx + 13, cy + 6, 2, azul);
                    FillCircle(px, S, S, cx + 14, cy,     1, azul);
                    break;
                case Arquetipo.Desesperado:  // lagrimón
                    FillCircle(px, S, S, cx - 6, cy - 2, 2, azul);
                    break;
            }

            var t = new Texture2D(S, S, TextureFormat.RGBA32, false);
            t.SetPixels32(px);
            t.Apply();
            t.filterMode = FilterMode.Point;
            t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }

        static void LineaSimple(Color32[] px, int W, int H, int x0, int y0, int x1, int y1, Color32 c)
        {
            int dx = Mathf.Abs(x1 - x0), dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;
            while (true)
            {
                Plot(px, W, H, x0, y0, c);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x0 += sx; }
                if (e2 < dx) { err += dx; y0 += sy; }
            }
        }

        // ----------------- COMPONENTES DE AUTOMATIZACIÓN -----------------

        /// <summary>Placeholder: cofre de madera donde se acumulan piezas sucias.</summary>
        public static Texture2D CrearBaul(int S = 80)
        {
            var px = new Color32[S * S];
            Color32 madera      = new Color32(130, 90,  50, 255);
            Color32 maderaOsc   = new Color32( 90, 60,  30, 255);
            Color32 maderaClara = new Color32(175, 130, 80, 255);
            Color32 metal       = new Color32(140, 130, 100, 255);
            Color32 borde       = new Color32( 45,  28,  12, 255);

            // Cuerpo (parte baja en la textura = visual inferior)
            Fill(px, S, S, 5, 5, S - 10, S - 25, madera);
            // Tapa (parte alta en la textura = visual superior)
            Fill(px, S, S, 5, S - 20, S - 10, 15, maderaClara);
            // Línea divisoria tapa-cuerpo
            HLine(px, S, S, 5, S - 6, S - 21, borde);
            HLine(px, S, S, 5, S - 6, S - 22, maderaOsc);
            // Vetas horizontales
            HLine(px, S, S, 7, S - 8, 25, maderaOsc);
            HLine(px, S, S, 7, S - 8, 40, maderaOsc);
            // Cierre metálico centrado
            Fill(px, S, S, S / 2 - 5, S - 24, 10, 8, metal);
            FillCircle(px, S, S, S / 2, S - 18, 3, maderaOsc);
            // Contorno
            RectOutline(px, S, S, 5, 5, S - 10, S - 10, borde, 2);

            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            tex.SetPixels32(px); tex.Apply();
            tex.filterMode = FilterMode.Point; tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }

        /// <summary>Placeholder: tolva (embudo) que alimenta la máquina de pulido.</summary>
        public static Texture2D CrearTolva(int S = 80)
        {
            var px = new Color32[S * S];
            Color32 metal    = new Color32(116, 124, 138, 255);
            Color32 metalCl  = new Color32(158, 166, 180, 255);
            Color32 metalOsc = new Color32( 80,  86, 100, 255);
            Color32 borde    = new Color32( 44,  48,  58, 255);
            Color32 interior = new Color32( 22,  24,  32, 255);

            // Forma trapezoidal: ancha arriba (y alto), estrecha abajo (y bajo)
            int yMin = 8, yMax = S - 8;
            for (int y = yMin; y < yMax; y++)
            {
                float f = (float)(y - yMin) / (yMax - yMin); // 0=abajo, 1=arriba
                int xl = (int)Mathf.Lerp(S / 2 - 8,  S / 2 - 30, f);
                int xr = (int)Mathf.Lerp(S / 2 + 8,  S / 2 + 30, f);
                bool esRim = (y <= yMin + 1 || y >= yMax - 2);
                Color32 c = esRim ? metalOsc : (f > 0.85f ? metalCl : metal);
                HLine(px, S, S, xl, xr, y, c);
                if (!esRim && xr - xl > 4) HLine(px, S, S, xl + 2, xr - 2, y, interior);
            }
            // Rim superior con remaches
            HLine(px, S, S, S / 2 - 30, S / 2 + 30, yMax - 1, metalCl);
            HLine(px, S, S, S / 2 - 30, S / 2 + 30, yMax - 2, metalCl);
            FillCircle(px, S, S, S / 2 - 26, yMax - 5, 2, metalCl);
            FillCircle(px, S, S, S / 2 + 26, yMax - 5, 2, metalCl);
            RectOutline(px, S, S, 0, 0, S, S, new Color32(0, 0, 0, 0), 0); // limpiar borde alpha

            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            tex.SetPixels32(px); tex.Apply();
            tex.filterMode = FilterMode.Point; tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }

        // ----------------- ITEMS para limpiar / reparar -----------------

        /// <summary>Genera los píxeles de un objeto según su forma (la usa ItemDef vía PawnShopGame).</summary>
        public static Color32[] CrearForma(FormaItem forma, int size)
        {
            switch (forma)
            {
                case FormaItem.Reloj: return Reloj(size);
                case FormaItem.Gema:  return Gema(size);
                default:              return Anillo(size);
            }
        }

        static Color32[] Anillo(int s)
        {
            var px = new Color32[s * s];
            int cx = s / 2, cy = s / 2 - 4;
            Color32 oro = new Color32(222, 182, 72, 255);
            Color32 oroOsc = new Color32(168, 126, 42, 255);
            Color32 gema = new Color32(90, 170, 255, 255);
            Color32 gemaBri = new Color32(195, 228, 255, 255);
            int R = 32, r = 20;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    int dx = x - cx, dy = y - cy, d2 = dx * dx + dy * dy;
                    if (d2 <= R * R && d2 >= r * r)            // aro del anillo
                        Plot(px, s, s, x, y, (dx + dy < 0) ? oro : oroOsc);
                }
            Diamante(px, s, cx, cy + R - 1, 12, gema, gemaBri); // piedra arriba
            return px;
        }

        static Color32[] Reloj(int s)
        {
            var px = new Color32[s * s];
            int cx = s / 2, cy = s / 2 - 3;
            Color32 oro = new Color32(222, 182, 72, 255);
            Color32 oroOsc = new Color32(165, 125, 45, 255);
            Color32 esfera = new Color32(238, 232, 210, 255);
            Color32 aguja = new Color32(52, 40, 30, 255);
            FillCircle(px, s, s, cx, cy, 30, oroOsc);          // caja (borde)
            FillCircle(px, s, s, cx, cy, 27, oro);             // caja
            FillCircle(px, s, s, cx, cy, 22, esfera);          // esfera
            Fill(px, s, s, cx - 1, cy, 2, 16, aguja);          // aguja larga (arriba)
            Fill(px, s, s, cx, cy - 1, 12, 2, aguja);          // aguja corta (derecha)
            FillCircle(px, s, s, cx, cy, 2, aguja);            // centro
            FillCircle(px, s, s, cx, cy + 34, 5, oro);         // anilla
            FillCircle(px, s, s, cx, cy + 34, 2, new Color32(0, 0, 0, 0));
            Fill(px, s, s, cx - 2, cy + 29, 4, 4, oroOsc);     // corona
            return px;
        }

        static Color32[] Gema(int s)
        {
            var px = new Color32[s * s];
            int cx = s / 2, cy = s / 2;
            Color32 c1 = new Color32(80, 200, 140, 255);
            Color32 c2 = new Color32(150, 235, 190, 255);
            Color32 c3 = new Color32(40, 150, 100, 255);
            int h = 40;
            for (int y = -h; y <= h; y++)
                for (int x = -h; x <= h; x++)
                {
                    if (Mathf.Abs(x) + Mathf.Abs(y) > h) continue;  // rombo
                    Color32 c = c1;
                    if (x + y < -10) c = c2;          // brillo arriba-izquierda
                    else if (x - y > 10) c = c3;      // sombra abajo-derecha
                    Plot(px, s, s, cx + x, cy + y, c);
                }
            for (int x = -h / 2; x <= h / 2; x++) Plot(px, s, s, cx + x, cy + h / 2, c3); // faceta
            return px;
        }

        static void Diamante(Color32[] px, int s, int cx, int cy, int h, Color32 col, Color32 bri)
        {
            for (int y = -h; y <= h; y++)
                for (int x = -h; x <= h; x++)
                    if (Mathf.Abs(x) + Mathf.Abs(y) <= h)
                        Plot(px, s, s, cx + x, cy + y, (x + y < -h / 2) ? bri : col);
        }

        // ----------------- Utilidades de dibujo -----------------
        static void Plot(Color32[] px, int W, int H, int x, int y, Color32 c)
        {
            if (x < 0 || x >= W || y < 0 || y >= H) return;
            px[y * W + x] = c;
        }

        static void Fill(Color32[] px, int W, int H, int x0, int y0, int w, int h, Color32 c)
        {
            for (int y = y0; y < y0 + h; y++)
                for (int x = x0; x < x0 + w; x++)
                    Plot(px, W, H, x, y, c);
        }

        static void HLine(Color32[] px, int W, int H, int xa, int xb, int y, Color32 c)
        {
            for (int x = xa; x <= xb; x++) Plot(px, W, H, x, y, c);
        }

        static void VLine(Color32[] px, int W, int H, int x, int ya, int yb, Color32 c)
        {
            for (int y = ya; y <= yb; y++) Plot(px, W, H, x, y, c);
        }

        static void RectOutline(Color32[] px, int W, int H, int x0, int y0, int w, int h, Color32 c, int t)
        {
            for (int i = 0; i < t; i++)
            {
                HLine(px, W, H, x0, x0 + w - 1, y0 + i, c);
                HLine(px, W, H, x0, x0 + w - 1, y0 + h - 1 - i, c);
                VLine(px, W, H, x0 + i, y0, y0 + h - 1, c);
                VLine(px, W, H, x0 + w - 1 - i, y0, y0 + h - 1, c);
            }
        }

        static void FillCircle(Color32[] px, int W, int H, int cx, int cy, int r, Color32 c)
        {
            for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                    if (x * x + y * y <= r * r) Plot(px, W, H, cx + x, cy + y, c);
        }

        static void AplicarLuzCalida(Color32[] px, int W, int H, int lx, int ly, float radio, float fuerza)
        {
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    float d = Mathf.Sqrt((x - lx) * (x - lx) + (y - ly) * (y - ly));
                    float k = Mathf.Clamp01(1f - d / radio) * fuerza;
                    if (k <= 0f) continue;
                    int i = y * W + x;
                    var p = px[i];
                    px[i] = new Color32(
                        (byte)Mathf.Min(255, p.r + (int)(70 * k)),
                        (byte)Mathf.Min(255, p.g + (int)(50 * k)),
                        (byte)Mathf.Min(255, p.b + (int)(12 * k)),
                        255);
                }
            }
        }

        static void AplicarVigneta(Color32[] px, int W, int H, float fuerza)
        {
            float cx = W / 2f, cy = H / 2f;
            float maxd = Mathf.Sqrt(cx * cx + cy * cy);
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    float k = Mathf.Clamp01(d / maxd - 0.55f) * fuerza;
                    if (k <= 0f) continue;
                    int i = y * W + x;
                    var p = px[i];
                    float m = 1f - k;
                    px[i] = new Color32((byte)(p.r * m), (byte)(p.g * m), (byte)(p.b * m), 255);
                }
            }
        }
    }
}
