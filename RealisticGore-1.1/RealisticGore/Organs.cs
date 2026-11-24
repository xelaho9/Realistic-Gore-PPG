using System.Collections.Generic;
using UnityEngine;

namespace Mod
{
    public static class OrganArt
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        static List<Vector2> gut;

        public static Sprite Get(string kind)
        {
            int variant = kind == "gib" ? Random.Range(0, 4) : 0;
            string key = kind + variant;
            Sprite s;
            if (cache.TryGetValue(key, out s) && s) return s;
            s = Build(kind, variant);
            cache[key] = s;
            return s;
        }

        static float Sq(float v) { return v * v; }
        static float P(float x, float y) { return Mathf.PerlinNoise(x, y); }

        static float Worley(float x, float y, out float f2)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float f1 = 9f; f2 = 9f;
            for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    int cx = ix + i, cy = iy + j;
                    uint h = (uint)(cx * 374761393 + cy * 668265263); h = (h ^ (h >> 13)) * 1274126177u;
                    float px = cx + (h & 1023) / 1023f, py = cy + ((h >> 10) & 1023) / 1023f;
                    float d = Mathf.Sqrt(Sq(px - x) + Sq(py - y));
                    if (d < f1) { f2 = f1; f1 = d; } else if (d < f2) f2 = d;
                }
            return f1;
        }

        static float Tube(float x, float y, float x0, float y0, float y1, float r, out bool lumen)
        {
            lumen = false;
            if (y < y0 || y > y1) return -1f;
            float d = Mathf.Abs(x - x0) / r;
            if (d >= 1f) return -1f;
            if (y > y1 - r * 0.5f && d < 0.62f) lumen = true;
            return Mathf.Sqrt(1f - d * d) * 0.55f;
        }

        static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }

        static void BuildGut()
        {
            gut = new List<Vector2>();
            float[] rows = { 0.62f, 0.31f, 0f, -0.31f, -0.62f };
            for (int r = 0; r < rows.Length; r++)
            {
                float w = Mathf.Sqrt(Mathf.Max(0f, 0.85f * 0.85f - rows[r] * rows[r])) - 0.16f;
                bool fwd = r % 2 == 0;
                for (int k = 0; k <= 20; k++)
                {
                    float t = k / 20f; float x = fwd ? Mathf.Lerp(-w, w, t) : Mathf.Lerp(w, -w, t);
                    gut.Add(new Vector2(x, rows[r] + Mathf.Sin(x * 7f + r * 1.7f) * 0.045f));
                }
                if (r < rows.Length - 1)
                {
                    Vector2 a = gut[gut.Count - 1];
                    float ny = rows[r + 1] + Mathf.Sin(a.x * 7f + (r + 1) * 1.7f) * 0.045f;
                    Vector2 c = new Vector2(a.x, (a.y + ny) * 0.5f); float rad = (a.y - ny) * 0.5f;
                    float side = fwd ? 1f : -1f;
                    for (int k = 1; k < 8; k++)
                    {
                        float ang = Mathf.PI * 0.5f - Mathf.PI * k / 8f;
                        gut.Add(c + new Vector2(Mathf.Cos(ang) * rad * 0.9f * side, Mathf.Sin(ang) * rad));
                    }
                }
            }
        }

        static float Sample(string kind, float x, float y, float sd, out Color a, out float gloss, out float rough, out float film)
        {
            a = Color.black; gloss = 1f; rough = 45f; film = 1f;
            float h = -1f; bool lumen;
            switch (kind)
            {
                case "heart":
                    {
                        float cx = -0.10f * (0.6f - y) / 1.5f;
                        float w = 0.80f * Mathf.Pow(Mathf.Clamp01((y + 0.92f) / 1.45f), 0.6f);
                        if (w > 0.02f)
                        {
                            float rx = (x - cx) / w, ry = y > 0.30f ? (y - 0.30f) / 0.30f : 0f;
                            float f = rx * rx + ry * ry;
                            if (f < 1f) h = Mathf.Sqrt(1f - f) * Mathf.Clamp01((y + 0.92f) * 3f);
                        }
                        a = Color.Lerp(new Color(0.44f, 0.06f, 0.09f), new Color(0.62f, 0.13f, 0.15f), P(x * 3f + sd, y * 3f));
                        gloss = 1.1f; rough = 50f;
                        if (h > 0f)
                        {
                            Color fat = new Color(0.93f, 0.79f, 0.44f);
                            float gx = cx + 0.10f + y * 0.14f, gd = Mathf.Abs(x - gx);
                            if (gd < 0.11f && y > -0.65f) a = Color.Lerp(a, fat, Mathf.Clamp01(1f - gd / 0.11f) * Mathf.Clamp01((y + 0.45f) * 1.4f) * (0.6f + 0.4f * P(x * 8f, y * 8f + sd)));
                            if (gd < 0.024f && y > -0.75f) { a = new Color(0.36f, 0.02f, 0.05f); h += 0.025f; }
                            if (Mathf.Abs(P(x * 5f + sd, y * 5f) - 0.5f) < 0.016f) a = Color.Lerp(a, new Color(0.32f, 0.02f, 0.05f), 0.7f);
                            if (y > 0.16f && y < 0.42f) a = Color.Lerp(a, fat, Mathf.Clamp01(P(x * 6f, y * 6f + sd) - 0.45f) * 1.4f);
                        }
                        float ao = Tube(x, y, 0.05f, 0.40f, 0.95f, 0.13f, out lumen);
                        if (ao > 0f && ao + 0.25f > h)
                        {
                            h = ao + 0.25f; a = lumen ? new Color(0.22f, 0.0f, 0.02f) : Color.Lerp(new Color(0.88f, 0.72f, 0.66f), new Color(0.78f, 0.55f, 0.52f), P(x * 6f, y * 6f));
                            gloss = 0.9f; rough = 40f;
                        }
                        float pa = Tube(x, y, -0.26f, 0.36f, 0.82f, 0.11f, out lumen);
                        if (pa > 0f && pa + 0.2f > h)
                        {
                            h = pa + 0.2f; a = lumen ? new Color(0.2f, 0.0f, 0.03f) : new Color(0.56f, 0.38f, 0.48f);
                            gloss = 0.9f; rough = 40f;
                        }
                        break;
                    }
                case "lung":
                    {
                        float wy = y > 0.4f ? 1f - (y - 0.4f) * 0.55f : 1f;
                        float f = Sq(x / (0.62f * wy)) + Sq((y + 0.02f) / 0.93f);
                        if (f >= 1f || Sq(x - 0.78f) + Sq(y + 0.3f) < 0.16f) return -1f;
                        float f2, f1 = Worley(x * 5f + sd, y * 5f, out f2);
                        float border = 1f - Mathf.Clamp01((f2 - f1) / 0.12f);
                        h = Mathf.Sqrt(1f - f) * 0.9f + (1f - f1) * 0.04f;
                        a = Color.Lerp(new Color(0.86f, 0.58f, 0.58f), new Color(0.60f, 0.33f, 0.37f), P(x * 2.5f + sd, y * 2.5f));
                        a = Color.Lerp(a, new Color(0.45f, 0.24f, 0.30f), border * 0.5f);
                        if (P(x * 14f + sd, y * 14f) > 0.7f) a = Color.Lerp(a, new Color(0.22f, 0.18f, 0.20f), 0.75f);
                        if (Mathf.Abs(y - (-0.55f * x + 0.05f)) < 0.022f) { a *= 0.45f; h -= 0.08f; }   // oblique fissure
                        gloss = 0.9f; rough = 38f; film = 0.6f;
                        break;
                    }
                case "liver":
                    {
                        float hh = Mathf.Lerp(0.64f, 0.30f, (x + 1f) * 0.5f);
                        float f = Sq(x / 0.96f) + Sq((y - 0.05f + x * 0.08f) / hh);
                        if (f >= 1f) return -1f;
                        float yb = 0.05f - x * 0.08f - hh * Mathf.Sqrt(Mathf.Max(0f, 1f - Sq(x / 0.96f)));
                        if (Mathf.Abs(x - 0.12f) < 0.035f && y < yb + 0.16f) return -1f;   // ligament notch
                        h = Mathf.Sqrt(1f - f) * 0.8f;
                        a = Color.Lerp(new Color(0.36f, 0.08f, 0.06f), new Color(0.52f, 0.16f, 0.11f), P(x * 2f + sd, y * 2f));
                        a *= 0.92f + 0.08f * P(x * 18f, y * 18f + sd);
                        float gb = Sq((x + 0.2f) / 0.16f) + Sq((y - yb - 0.08f) / 0.11f);
                        if (gb < 1f) { a = Color.Lerp(a, new Color(0.30f, 0.42f, 0.22f), Mathf.Clamp01((1f - gb) * 3f)); h += Mathf.Sqrt(1f - gb) * 0.12f; }   // gallbladder
                        gloss = 1.8f; rough = 75f;
                        break;
                    }
                case "stomach":
                    {
                        float f = Sq((x + 0.12f) / 0.62f) + Sq((y - 0.08f) / 0.72f);
                        float inner = Sq((x - 0.58f) / 0.42f) + Sq((y - 0.42f) / 0.38f);
                        float py = Sq((x - 0.42f) / 0.42f) + Sq((y + 0.52f) / 0.2f);
                        float h1 = f < 1f && inner > 1f ? Mathf.Sqrt(1f - f) * Mathf.Min(1f, (inner - 1f) * 2.5f) : -1f;
                        float h2 = py < 1f ? Mathf.Sqrt(1f - py) * 0.6f : -1f;
                        h = Mathf.Max(h1, h2);
                        if (h <= 0f) return -1f;
                        a = Color.Lerp(new Color(0.82f, 0.56f, 0.50f), new Color(0.68f, 0.40f, 0.38f), P(x * 2.5f + sd, y * 2.5f));
                        if (Mathf.Abs(P(x * 4f + sd, y * 4f) - 0.5f) < 0.02f) a = Color.Lerp(a, new Color(0.60f, 0.10f, 0.13f), 0.7f);
                        if (f > 0.7f && x < 0f) a = Color.Lerp(a, new Color(0.95f, 0.84f, 0.50f), Mathf.Clamp01(P(x * 7f, y * 7f + sd) - 0.4f) * 1.6f);   // omentum fat
                        gloss = 1.1f; rough = 45f;
                        break;
                    }
                case "kidney":
                    {
                        float f = Sq(x / 0.58f) + Sq(y / 0.88f);
                        float hil = Sq((x - 0.62f) / 0.3f) + Sq(y / 0.3f);
                        if (f >= 1f || hil < 1f)
                        {
                            if (x > 0.3f && x < 0.95f && Mathf.Abs(y - 0.08f) < 0.07f)
                            { a = new Color(0.58f, 0.10f, 0.12f); h = 0.3f * Mathf.Sqrt(1f - Sq((y - 0.08f) / 0.07f)); gloss = 1f; rough = 40f; break; }
                            float ux = 0.45f - y * 0.15f;
                            if (y < 0.05f && y > -0.95f && Mathf.Abs(x - ux) < 0.045f)
                            { a = new Color(0.88f, 0.80f, 0.68f); h = 0.25f * Mathf.Sqrt(1f - Sq((x - ux) / 0.045f)); gloss = 1f; rough = 40f; break; }
                            return -1f;
                        }
                        h = Mathf.Sqrt(1f - f) * 0.8f;
                        a = Color.Lerp(new Color(0.48f, 0.13f, 0.11f), new Color(0.62f, 0.22f, 0.17f), P(x * 3f + sd, y * 3f));
                        if (hil < 1.9f) a = Color.Lerp(a, new Color(0.95f, 0.82f, 0.46f), Mathf.Clamp01((1.9f - hil) / 0.9f) * 0.85f);
                        gloss = 1.3f; rough = 55f;
                        break;
                    }
                case "intestines":
                    {
                        if (gut == null) BuildGut();
                        Vector2 p = new Vector2(x, y);
                        float d = 9f; int best = 0;
                        for (int k = 0; k < gut.Count - 1; k++) { float dd = SegDist(p, gut[k], gut[k + 1]); if (dd < d) { d = dd; best = k; } }
                        float r = 0.15f * (0.9f + 0.2f * P(best * 0.3f + sd, 1f));
                        if (d < r)
                        {
                            h = Mathf.Sqrt(1f - Sq(d / r)) * 0.6f + 0.2f;
                            a = Color.Lerp(new Color(0.88f, 0.64f, 0.58f), new Color(0.76f, 0.50f, 0.46f), P(x * 3f + sd, y * 3f));
                            if (Mathf.Abs(P(x * 6f + sd, y * 6f) - 0.5f) < 0.022f) a = Color.Lerp(a, new Color(0.68f, 0.22f, 0.22f), 0.65f);
                            gloss = 1.4f; rough = 60f; film = 0.5f;
                        }
                        else if (Sq(x) + Sq(y) < 0.86f * 0.86f)
                        {
                            h = 0.02f; a = new Color(0.46f, 0.12f, 0.11f) * (0.8f + 0.3f * P(x * 5f, y * 5f));   // mesentery between loops
                            gloss = 0.6f; rough = 30f;
                        }
                        break;
                    }
                case "brain":
                    {
                        float f = Sq((x + 0.02f) / 0.95f) + Sq((y - 0.08f) / (y > 0.08f ? 0.72f : 0.55f));
                        float cb = Sq((x + 0.50f) / 0.36f) + Sq((y + 0.52f) / 0.24f);
                        bool stem = y < -0.35f && y > -0.92f && Mathf.Abs(x - (-0.15f + (y + 0.35f) * 0.1f)) < 0.11f;
                        if (f < 1f)
                        {
                            float w1 = P(x * 2.2f + sd, y * 2.2f), w2 = P(x * 5f + 12f, y * 5f + sd);
                            float g = Mathf.Pow(Mathf.Abs(Mathf.Sin((w1 * 9f + w2 * 3.5f) * Mathf.PI)), 0.45f);
                            h = Mathf.Sqrt(1f - f) * 0.8f + g * 0.07f;
                            a = Color.Lerp(new Color(0.42f, 0.10f, 0.12f), new Color(0.87f, 0.73f, 0.71f), Mathf.Pow(g, 0.65f));
                            if (SegDist(new Vector2(x, y), new Vector2(0.38f, -0.06f), new Vector2(-0.32f, 0.16f)) < 0.03f) { a = new Color(0.36f, 0.06f, 0.08f); h -= 0.1f; }   // lateral fissure
                            if (Mathf.Abs(P(x * 3f + sd + 5f, y * 3f) - 0.5f) < 0.014f) a = Color.Lerp(a, new Color(0.62f, 0.08f, 0.10f), 0.7f);
                            gloss = 1.3f; rough = 45f; film = 0.5f;
                        }
                        else if (cb < 1f)
                        {
                            float fol = Mathf.Abs(Mathf.Sin((y * 22f + P(x * 3f, y * 3f + sd) * 3f) * Mathf.PI));
                            h = Mathf.Sqrt(1f - cb) * 0.55f + fol * 0.04f;
                            a = Color.Lerp(new Color(0.48f, 0.18f, 0.18f), new Color(0.80f, 0.62f, 0.60f), fol);
                            gloss = 1.2f; rough = 45f;
                        }
                        else if (stem)
                        {
                            float sx = (x - (-0.15f + (y + 0.35f) * 0.1f)) / 0.11f;
                            h = Mathf.Sqrt(1f - sx * sx) * 0.35f; a = new Color(0.86f, 0.74f, 0.68f);
                            if (y < -0.85f) a = new Color(0.55f, 0.10f, 0.10f);   // torn end
                            gloss = 1f; rough = 40f;
                        }
                        break;
                    }
                case "eye":
                    {
                        // eyeball, nerve out the back
                        float ex = x - 0.2f, f = (ex * ex + y * y) / (0.42f * 0.42f);
                        float ny0 = Mathf.Sin((x + 0.2f) * 3f) * 0.05f;
                        float nd = Mathf.Abs(y - ny0) / 0.085f;
                        bool nerve = x < -0.1f && x > -0.95f && nd < 1f;
                        float mt = SegDist(new Vector2(x, y), new Vector2(-0.12f, 0.30f), new Vector2(-0.42f, 0.42f));
                        float mb = SegDist(new Vector2(x, y), new Vector2(-0.12f, -0.30f), new Vector2(-0.40f, -0.46f));
                        if (f < 1f)
                        {
                            h = Mathf.Sqrt(1f - f) * 0.9f;
                            float ang = Mathf.Atan2(y, ex), rr = Mathf.Sqrt(f);
                            a = Color.Lerp(new Color(0.96f, 0.93f, 0.88f), new Color(0.88f, 0.70f, 0.66f), Mathf.Clamp01(-ex / 0.42f) * 0.7f);
                            if (Mathf.Abs(P(x * 9f + sd, y * 9f) - 0.5f) < 0.025f && ex < 0.2f) a = Color.Lerp(a, new Color(0.75f, 0.10f, 0.12f), 0.65f);
                            float ia = Mathf.Abs(y) / 0.42f;
                            if (ex > 0f && ia < 0.62f)
                            {
                                float ir = ia / 0.62f;
                                float rad = Mathf.Abs(Mathf.Sin((Mathf.Atan2(y, ex) * 22f + P(ex * 8f, y * 8f) * 4f)));
                                Color iris = Color.Lerp(new Color(0.20f, 0.12f, 0.06f), new Color(0.42f, 0.28f, 0.14f), rad * 0.6f + (1f - ir) * 0.3f);
                                if (ir > 0.85f) iris *= 0.55f;                         // limbal ring
                                a = ex > 0.25f ? (ia < 0.25f ? new Color(0.02f, 0.01f, 0.01f) : iris) : Color.Lerp(a, iris, Mathf.Clamp01((ex - 0.12f) / 0.13f));
                                h += Mathf.Clamp01((ex - 0.2f) / 0.2f) * (1f - ir * ir) * 0.15f;   // cornea
                                gloss = 2.2f; rough = 140f; film = 0f;
                            }
                            else { gloss = 1.2f; rough = 55f; film = 0.25f; }
                            if (rr > 0.9f && ex < 0f) a = Color.Lerp(a, new Color(0.70f, 0.12f, 0.14f), 0.5f);   // torn conjunctiva rim
                        }
                        else if (nerve)
                        {
                            h = Mathf.Sqrt(1f - nd * nd) * 0.4f;
                            a = Color.Lerp(new Color(0.90f, 0.82f, 0.70f), new Color(0.80f, 0.66f, 0.56f), P(x * 6f + sd, y * 6f));
                            if (Mathf.Abs(y - ny0) < 0.012f) a = Color.Lerp(a, new Color(0.60f, 0.08f, 0.10f), 0.7f);   // central retinal vessels
                            if (x < -0.85f) a = new Color(0.55f, 0.08f, 0.10f);                                       // torn end
                            gloss = 1f; rough = 40f; film = 0.4f;
                        }
                        else if (mt < 0.06f || mb < 0.06f)
                        {
                            float m = Mathf.Min(mt, mb) / 0.06f;
                            h = Mathf.Sqrt(1f - m * m) * 0.3f;
                            a = Color.Lerp(new Color(0.45f, 0.04f, 0.06f), new Color(0.72f, 0.16f, 0.16f), P(x * 14f, y * 4f + sd));
                            gloss = 1f; rough = 30f;
                        }
                        else if (x < 0f && Sq(x + 0.25f) + Sq(y) < 0.33f * 0.33f && P(x * 7f + sd, y * 7f) > 0.48f)
                        {
                            h = 0.12f; a = new Color(0.95f, 0.82f, 0.48f) * (0.85f + 0.2f * P(x * 15f, y * 15f));   // orbital fat
                            gloss = 1.2f; rough = 45f;
                        }
                        break;
                    }
                default: // "gib": a lump of skin, fat and muscle blown out of a wound
                    {
                        float ang = Mathf.Atan2(y, x), rr = Mathf.Sqrt(x * x + y * y);
                        float edge = 0.55f + 0.32f * P(Mathf.Cos(ang) * 1.3f + sd, Mathf.Sin(ang) * 1.3f + sd);
                        if (rr >= edge) return -1f;
                        float f = rr / edge;
                        h = Mathf.Sqrt(1f - f * f) * 0.9f + (P(x * 4f + sd, y * 4f) - 0.5f) * 0.15f;
                        float side = (x * 0.3f + y) / edge;
                        if (side > 0.6f) { a = Color.Lerp(new Color(0.80f, 0.60f, 0.50f), new Color(0.70f, 0.50f, 0.42f), P(x * 9f, y * 9f)); gloss = 0.4f; rough = 20f; film = 0.4f; }
                        else if (side > 0.42f)
                        {
                            float f2, f1 = Worley(x * 6f + sd, y * 6f, out f2);
                            a = Color.Lerp(new Color(0.97f, 0.85f, 0.48f), new Color(0.80f, 0.42f, 0.30f), (1f - Mathf.Clamp01((f2 - f1) / 0.2f)) * 0.7f);
                            h += (0.5f - f1) * 0.08f; gloss = 1.2f; rough = 45f;
                        }
                        else
                        {
                            float fa = (y + (P(x * 1.5f + sd, y * 0.5f) - 0.5f) * 0.6f) * 9f;
                            float fasc = Mathf.Abs(Mathf.Sin(fa * Mathf.PI));
                            a = Color.Lerp(new Color(0.46f, 0.04f, 0.06f), new Color(0.78f, 0.16f, 0.16f), fasc * 0.7f + P(x * 12f, y * 3f) * 0.3f);
                            h += fasc * 0.04f; gloss = 1f; rough = 30f;
                        }
                        break;
                    }
            }
            return h;
        }

        static Sprite Build(string kind, int variant)
        {
            int N = kind == "intestines" || kind == "brain" ? 128 : kind == "gib" ? 64 : 96;
            float sd = variant * 37.3f + 11f;
            int T = N * N;
            var H = new float[T]; var A = new Color[T]; var G = new float[T]; var Rg = new float[T]; var F = new float[T];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int i = y * N + x;
                    float nx = (x + 0.5f) / N * 2f - 1f, ny = (y + 0.5f) / N * 2f - 1f;
                    H[i] = Sample(kind, nx, ny, sd, out A[i], out G[i], out Rg[i], out F[i]);
                }
            Vector3 Ld = new Vector3(-0.45f, 0.6f, 0.66f).normalized, Hv = (Ld + Vector3.forward).normalized;
            var px = new Color32[T];
            float k = N * 0.35f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int i = y * N + x;
                    float h = H[i];
                    if (h <= 0f) { px[i] = new Color32(0, 0, 0, 0); continue; }
                    float hl = x > 0 ? Mathf.Max(0f, H[i - 1]) : 0f, hr = x < N - 1 ? Mathf.Max(0f, H[i + 1]) : 0f;
                    float hd = y > 0 ? Mathf.Max(0f, H[i - N]) : 0f, hu = y < N - 1 ? Mathf.Max(0f, H[i + N]) : 0f;
                    Vector3 n = new Vector3(-(hr - hl) * 0.5f * k, -(hu - hd) * 0.5f * k, 1f).normalized;
                    float diff = Mathf.Max(0f, Vector3.Dot(n, Ld));
                    float ao = Mathf.Lerp(0.5f, 1f, Mathf.Sqrt(Mathf.Clamp01(h * 1.6f)));
                    Color b = A[i];
                    float nxp = x / (float)N * 6f, nyp = y / (float)N * 6f;
                    float bf = Mathf.Clamp01((P(nxp + sd + 40f, nyp + 3f) - 0.52f) * 2.2f) * F[i];
                    b = Color.Lerp(b, new Color(0.38f, 0.01f, 0.04f), bf * 0.8f);
                    Color lit = b * (0.24f + 0.85f * diff) * ao;
                    lit += new Color(b.r * 0.22f, b.g * 0.04f, b.b * 0.04f) * (1f - diff) * ao;
                    float spec = Mathf.Pow(Mathf.Max(0f, Vector3.Dot(n, Hv)), Rg[i] + bf * 40f) * (G[i] + bf * 0.8f);
                    spec *= 0.45f + 0.9f * P(nxp * 4f + sd, nyp * 4f);
                    lit += new Color(1f, 0.92f, 0.9f) * spec * 0.6f;
                    bool edge = hl <= 0f || hr <= 0f || hu <= 0f || hd <= 0f;
                    lit.a = edge ? 0.55f : 1f;
                    if (edge) lit *= 0.7f;
                    px[i] = lit;
                }
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px); tex.Apply(false);
            return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N, 0, SpriteMeshType.FullRect, Vector4.zero, true);
        }
    }
}
