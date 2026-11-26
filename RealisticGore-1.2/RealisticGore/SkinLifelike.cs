using UnityEngine;

namespace Mod
{
    public static partial class RealisticHuman
    {
        [System.ThreadStatic] static bool v2, fem;
        static float Fbm(float x, float y)
        {
            return Mathf.PerlinNoise(x, y) * 0.5f + Mathf.PerlinNoise(x * 2.03f + 17f, y * 2.03f + 5f) * 0.3f + Mathf.PerlinNoise(x * 4.1f + 3f, y * 4.1f + 29f) * 0.2f;
        }
        static float Band(float x, float c, float w) { float d = Mathf.Abs(x - c) / w; return d >= 1f ? 0f : 1f - d * d * (3f - 2f * d); }

        static Color SkinV2(Kind kind, float u, float v, float fx, float fy, int ix, int iy, float nx, float ny, float yw, float eu, float ev, bool hairy)
        {
            bool cloth = false;
            Color hairC = fem ? new Color(0.22f, 0.13f, 0.07f) : new Color(0.15f, 0.10f, 0.07f);
            if (fem) hairy = false;
            float nz = Mathf.Sqrt(Mathf.Max(0.02f, 1f - nx * nx - ny * ny * yw));
            float light = Mathf.Clamp01(nz * 0.72f + nx * 0.20f + ny * 0.14f * (yw + 0.3f) + 0.08f);
            {
                gu = 0f; gv = 0f; Relief(kind, u, v);
                float sx = gu * 0.05f, sy = gv * 0.05f * bw / Mathf.Max(1f, bh);
                light = Mathf.Clamp01(light - sx * 0.45f - sy * 0.65f);
            }
            float red = 0f, oil = 0.35f;

            // base skin
            float hue = Fbm(fx * 0.006f, fy * 0.006f) - 0.5f;
            float mott = Fbm(fx * 0.03f + 40f, fy * 0.03f) - 0.5f;
            Color s = new Color(0.84f, 0.65f, 0.54f);
            s += new Color(hue * 0.06f, -hue * 0.01f, -hue * 0.03f);
            s *= 1f + mott * 0.07f;
            float ph = Hash(ix, iy);
            s *= ph > 0.92f ? 0.94f : 0.985f + ph * 0.03f;
            if (Mathf.Abs(Mathf.PerlinNoise(fx * 0.07f, fy * 0.07f) - 0.5f) < 0.02f) s *= 0.97f;
            if (Mathf.PerlinNoise(fx * 0.09f + 7f, fy * 0.09f) > 0.80f && Mathf.PerlinNoise(fx * 0.004f + 9f, fy * 0.004f) > 0.55f)
                s = Color.Lerp(s, new Color(0.66f, 0.45f, 0.36f), 0.25f);                        // freckles, clustered

            switch (kind)
            {
                case Kind.Head:
                {
                    oil = 0.8f;
                    float ed = Ed(u, v, 0.40f, 0.50f, 0.10f, 0.16f);
                    bool ear = ed < 1f;
                    if (ear)
                    {
                        float rim = Smooth(0.62f, 0.85f, ed) * (1f - Smooth(0.92f, 1f, ed));        // helix rim
                        s *= 0.86f + rim * 0.16f;
                        if (ed < 0.45f) s *= 0.88f;                                                 // conch
                        if (Ed(u, v, 0.415f, 0.49f, 0.025f, 0.035f) < 1f) s *= 0.55f;             // canal
                        red += 0.6f;
                    }
                    // hair
                    float hn = (Fbm(fx * 0.03f, fy * 0.03f) - 0.5f) * 0.10f;
                    float hm = Mathf.Max(Smooth(0.78f, 0.83f, v - hn),
                        Mathf.Max(Smooth(0.32f, 0.27f, u + hn) * Smooth(0.22f, 0.28f, v), Smooth(0.54f, 0.49f, u + hn) * Smooth(0.64f, 0.69f, v - hn)));
                    if (fem)   // long hair
                        hm = Mathf.Max(Smooth(0.74f, 0.79f, v - hn), Mathf.Max(Smooth(0.47f, 0.40f, u + hn) * Smooth(0.02f, 0.08f, v),
                            Smooth(0.60f, 0.54f, u + hn) * Smooth(0.58f, 0.63f, v - hn)));
                    else if (ear) hm = 0f;
                    if (fem && hm > 0.3f) ear = false;
                    float strand = Mathf.PerlinNoise(fx * 0.14f, fy * 0.016f) * 0.6f + Mathf.PerlinNoise(fx * 0.05f + 11f, fy * 0.008f) * 0.4f;
                    float hcov = hm >= 0.95f ? 1f : Smooth(1f - hm, 1.05f - hm, strand);
                    if (hcov > 0.01f)
                    {
                        Color hc = hairC * (0.55f + strand * 0.9f);
                        hc += new Color(0.20f, 0.16f, 0.12f) * Band(v, 0.90f, 0.06f) * Smooth(0.5f, 0.7f, strand) * 0.5f;
                        s = Color.Lerp(s * 0.92f, hc, hcov);
                        if (hcov > 0.5f) cloth = true;
                    }
                    if (hcov < 0.5f && !ear)
                    {
                        // eye
                        float sd = Ed(u, v, eu, ev, 0.075f, 0.042f);
                        s *= 1f - 0.10f * Smooth(3.2f, 1.2f, sd);                                   // socket shadow
                        if (sd < 1f)
                        {
                            s = Color.Lerp(new Color(0.94f, 0.91f, 0.87f), new Color(0.88f, 0.70f, 0.66f), Smooth(0.5f, 1f, sd));
                            float id = Ed(u, v, eu + 0.028f, ev, 0.032f, 0.036f);
                            if (id < 1f)
                            {
                                float ang = Mathf.Atan2(v - ev, u - eu - 0.028f);
                                Color iris = Color.Lerp(new Color(0.22f, 0.14f, 0.08f), new Color(0.45f, 0.30f, 0.16f), Mathf.PerlinNoise(ang * 6f + 3f, id * 4f));
                                iris = Color.Lerp(iris, new Color(0.10f, 0.07f, 0.05f), Smooth(0.7f, 1f, id));   // limbal ring
                                s = id < 0.16f ? new Color(0.02f, 0.015f, 0.015f) : iris;
                            }
                            if (Ed(u, v, eu + 0.036f, ev + 0.012f, 0.008f, 0.009f) < 1f) s = new Color(0.97f, 0.97f, 0.95f);  // catchlight
                            if (v > ev + 0.030f) s *= 0.80f;                                        // lid shadow on the eyeball
                        }
                        else if (sd < (fem ? 1.6f : 1.35f) && v > ev) s = Color.Lerp(s, new Color(0.08f, 0.05f, 0.05f), fem ? 0.92f : 0.75f);   // lash line
                        else if (sd < 1.25f && v < ev) s = Color.Lerp(s, new Color(0.80f, 0.52f, 0.48f), 0.5f);    // wet lower lid
                        else if (sd > 1.9f && sd < 2.3f && v > ev) s *= 0.88f;                                     // lid crease
                        // brow
                        float bv = ev + 0.085f + (u - eu) * 0.15f;
                        float bt = Smooth(eu - 0.10f, eu - 0.02f, u) * Smooth(eu + 0.11f, eu + 0.06f, u);
                        if (bt > 0.05f && Mathf.Abs(v - bv) < (fem ? 0.014f : 0.022f) * (0.6f + bt * 0.6f))
                            s = Color.Lerp(s, hairC, (0.45f + Mathf.PerlinNoise(fx * 0.10f, fy * 0.03f) * 0.45f) * bt);
                        // nose
                        if (Ed(u, v, 0.95f, 0.45f, 0.03f, 0.02f) < 1f) s *= 0.45f;
                        float ala = Ed(u, v, 0.93f, 0.46f, 0.06f, 0.045f);
                        if (ala > 0.75f && ala < 1.05f && v < 0.48f) s *= 0.90f;
                        red += 0.35f * Smooth(1.5f, 0.3f, Ed(u, v, 0.98f, 0.50f, 0.05f, 0.06f));     // nose tip
                        red += (fem ? 0.55f : 0.30f) * Smooth(1.2f, 0.2f, Ed(u, v, 0.80f, 0.42f, 0.10f, 0.08f));     // cheek
                        float dv = v - 0.30f;
                        // stubble
                        if (!fem && u > 0.52f && v < 0.40f && Mathf.Abs(dv) > 0.04f && v > Key(JawK, 2, 1, u) - 0.02f)
                        {
                            float st = Smooth(0.52f, 0.62f, u) * Smooth(0.40f, 0.30f, v);
                            s = Color.Lerp(s, new Color(0.58f, 0.52f, 0.52f), 0.10f * st);
                            if (Hash(ix * 7 + 1, iy * 7) > 0.82f) s = Color.Lerp(s, hairC, 0.30f * st);
                        }
                        // lips
                        if (u > 0.84f)
                        {
                            if (Mathf.Abs(dv) < (fem ? 0.044f : 0.036f))
                            {
                                Color lc = fem ? (dv > 0 ? new Color(0.70f, 0.32f, 0.36f) : new Color(0.80f, 0.40f, 0.44f))
                                    : (dv > 0 ? new Color(0.62f, 0.34f, 0.33f) : new Color(0.72f, 0.42f, 0.40f));
                                if (Mathf.PerlinNoise(fx * 0.25f, fy * 0.02f) > 0.62f) lc *= 0.93f;  // lip lines
                                if (dv < -0.012f && dv > -0.024f) lc *= 1.08f;                        // lower lip highlight
                                s = Color.Lerp(s, lc, 0.75f * Smooth(0.84f, 0.88f, u));
                            }
                            if (Mathf.Abs(dv) < 0.006f) s *= 0.50f;                                   // mouth line
                            if (dv < -0.036f && dv > -0.06f) s *= 0.93f;                              // under lower lip
                            if (dv > 0.04f && dv < 0.11f && u > 0.92f) s *= 0.96f;                    // philtrum
                        }
                        if (v < 0.10f && v > -0.06f && u > 0.45f) s *= 0.85f + Mathf.Abs(v - 0.02f) * 1.8f;                            // under the jaw
                    }
                    break;
                }
                case Kind.Chest:
                    if (v > 0.83f && v < 0.90f && u > 0.45f) s *= v > 0.865f ? 1.05f : 0.93f;           // collarbone
                    { float pd = Ed(u, v, 0.84f, 0.60f, 0.30f, 0.26f); if (pd > 0.8f && pd < 1.1f && v < 0.6f) s *= 0.86f + Mathf.Abs(pd - 0.95f) / 0.15f * 0.14f; }
                    if (fem) { if ((v > 0.38f && v < 0.74f) || (v >= 0.74f && Mathf.Abs(u - 0.66f) < 0.05f)) cloth = true; }   // sports bra + strap
                    else if (Ed(u, v, 0.98f, 0.50f, 0.035f, 0.028f) < 1f) s = Color.Lerp(s, new Color(0.62f, 0.40f, 0.36f), 0.7f);   // nipple
                    if (u > 0.3f && u < 0.75f && v < 0.55f && Mathf.Sin(v * 40f) > 0.7f) s *= 0.975f;    // ribs through the side
                    break;
                case Kind.Belly:
                    { float nd = Ed(u, v, 0.90f, 0.30f, 0.035f, 0.05f); if (nd < 1f) s *= 0.45f + nd * 0.3f; else if (nd < 1.6f && v > 0.30f) s *= 0.93f; }
                    if (u > 0.72f) s *= 1f - 0.025f * Mathf.Max(0f, Mathf.Sin(v * 22f));                // faint abs
                    if (!fem && u > 0.85f && v < 0.28f && Hash(ix * 5, iy * 5 + 1) > 0.9f) s = Color.Lerp(s, hairC, 0.25f);
                    break;
                case Kind.Hips:
                    cloth = true; break;
                case Kind.Thigh:
                    if (v > (fem ? 0.88f : 0.70f)) cloth = true;
                    else if (v < 0.14f && u > 0.55f) red += 0.5f * Smooth(0.14f, 0.02f, v);           // knee
                    break;
                case Kind.Shin:
                    if (v > 0.84f && u > 0.55f) { red += 0.5f; if (Mathf.Sin(v * 120f) > 0.85f) s *= 0.96f; }   // knee + wrinkles
                    s *= 1f + 0.04f * Band(u, 0.82f, 0.07f) * Smooth(0.1f, 0.3f, v);                 // shinbone
                    s *= 1f - 0.05f * Band(u, 0.18f, 0.15f);                                         // calf shadow
                    if (Ed(u, v, 0.5f, 0.06f, 0.12f, 0.06f) < 1f) s *= 1.04f;                        // ankle bone
                    break;
                case Kind.Foot:
                    if (u < 0.25f) red += 0.4f;                                                      // heel
                    if (v < 0.12f) s *= 1.06f;                                                       // sole
                    if (v > 0.85f && Mathf.Sin(v * 140f) > 0.85f) s *= 0.95f;                        // ankle creases
                    break;
                case Kind.UpperArm:
                    if (v < 0.12f && u < 0.45f) { red += 0.4f; if (Mathf.Sin(v * 160f) > 0.8f) s *= 0.94f; }   // elbow
                    if (Band(v, 0.62f, 0.025f) > 0f && u > 0.5f) s *= 0.96f;                         // deltoid edge
                    break;
                case Kind.Forearm:
                case Kind.Hand:
                {
                    if (kind == Kind.Forearm && v > 0.88f && u < 0.45f) { red += 0.4f; if (Mathf.Sin(v * 160f) > 0.8f) s *= 0.94f; }
                    if (kind == Kind.Forearm && v > 0.24f && v < 0.3f && Mathf.Sin(v * 300f) > 0.8f) s *= 0.94f;   // wrist creases
                    if (kind == Kind.Forearm && v < 0.24f) { red += 0.35f * Band(v, 0.1f, 0.1f); if (v < 0.1f && u > 0.2f && u < 0.85f && Mathf.Abs(Mathf.Sin(u * 4f * Mathf.PI)) < 0.06f) s *= 0.9f; }   // hand: warmer, finger gaps
                    if (kind == Kind.Hand) { red += 0.5f * Band(v, 0.42f, 0.14f) * Smooth(0.5f, 0.8f, u); if (Band(v, 0.30f, 0.02f) > 0f) s *= 0.93f; }
                    float vn = Mathf.Abs(Mathf.PerlinNoise(fx * 0.008f, fy * 0.03f) - 0.5f);
                    if (vn < 0.02f) s = Color.Lerp(s, new Color(0.52f, 0.56f, 0.62f), 0.18f * (1f - vn / 0.02f));   // veins, soft
                    break;
                }
            }

            if (cloth && kind != Kind.Head)
            {
                // briefs
                float fold = Fbm(fx * 0.02f, fy * 0.08f);
                float diag = Mathf.PerlinNoise((fx + fy) * 0.03f, (fx - fy) * 0.006f);
                s = (fem ? new Color(0.10f, 0.10f, 0.11f) : new Color(0.13f, 0.15f, 0.20f)) * (0.80f + fold * 0.30f + (diag - 0.5f) * 0.15f);
                if (fem && kind == Kind.Chest)
                {
                    if (Mathf.Abs(v - 0.41f) < 0.03f) s = new Color(0.55f, 0.56f, 0.60f) * (0.88f + fold * 0.12f);   // underband
                    if (Mathf.Abs(v - 0.385f) < 0.006f || Mathf.Abs(v - 0.735f) < 0.006f) s *= 0.6f;            // edges
                }
                if (((ix + iy) & 3) == 0) s *= 0.95f;
                if (((ix - iy) & 3) == 0) s *= 0.97f;
                if (kind == Kind.Hips && !fem)
                {
                    if (v > 0.86f)
                    {
                        bool stripe = Mathf.Abs(v - 0.93f) < 0.012f;
                        s = stripe ? new Color(0.50f, 0.12f, 0.12f) : new Color(0.58f, 0.59f, 0.63f) * (0.88f + fold * 0.12f);
                        if (!stripe && (iy & 3) == 0) s *= 0.92f;
                    }
                    if (Mathf.Abs(v - 0.86f) < 0.008f) s *= 0.6f;
                    if (Mathf.Abs(u - 0.62f) < 0.006f && v < 0.86f && (iy % 6) < 3) s *= 1.25f;
                }
                if (kind == Kind.Thigh && !fem) { if (v < 0.74f) s *= 0.78f; if (Mathf.Abs(v - 0.745f) < 0.006f && (ix % 6) < 3) s *= 1.25f; }
            }
            else if (hairy)
            {
                // body hair
                int cx = ix / 5, cy = iy / 5;
                if (Hash(cx + 91, cy) > 0.6f)
                {
                    float hx = cx * 5 + Hash(cx, cy + 5) * 5, hy = cy * 5 + Hash(cx + 3, cy) * 5;
                    float along = (fx - hx) * 0.3f + (fy - hy) * -0.95f, across = (fx - hx) * 0.95f + (fy - hy) * 0.3f;
                    if (Mathf.Abs(across) < 0.22f && along > 0 && along < 4f) s = Color.Lerp(s, hairC, 0.16f);
                }
            }

            if (!cloth) s = Color.Lerp(s, new Color(0.86f, 0.50f, 0.46f), Mathf.Clamp01(red) * 0.3f);
            float ao = Smooth(0.70f, 1f, Mathf.Abs(nx)) * 0.18f;
            Color lit = s * (0.48f + 0.60f * light) * (1f - ao);
            if (!cloth)
            {
                float sh = 1f - Smooth(0f, 0.65f, light);
                lit = Color.Lerp(lit, lit * new Color(1.08f, 0.80f, 0.74f), sh);                     // warm subsurface shadow
                float oilM = Mathf.PerlinNoise(fx * 0.02f + 50f, fy * 0.02f) * oil;
                lit += new Color(1f, 0.96f, 0.92f) * Mathf.Pow(light, 12f) * (0.04f + 0.08f * oilM);
                lit += new Color(0.9f, 0.7f, 0.6f) * Smooth(0.85f, 1f, -nx) * 0.05f;                 // rim
            }
            else lit += Color.white * Mathf.Pow(light, 16f) * 0.03f;
            lit.a = 1f;
            return lit;
        }

        [System.ThreadStatic] static float gu, gv;
        static void Bump(float u, float v, float cu, float cv, float ru, float rv, float a)
        {
            float ex = (u - cu) / ru, ey = (v - cv) / rv, q = ex * ex + ey * ey;
            if (q > 9f) return;
            float h = a * Mathf.Exp(-2f * q);
            gu -= h * 4f * ex / ru; gv -= h * 4f * ey / rv;
        }
        // muscle relief
        static void Relief(Kind k, float u, float v)
        {
            switch (k)
            {
                case Kind.Head:
                    Bump(u, v, .45f, .78f, .35f, .25f, .6f);           // cranium
                    Bump(u, v, .86f, .68f, .12f, .05f, .5f);           // brow ridge
                    Bump(u, v, .80f, .50f, .10f, .07f, .6f);           // cheekbone
                    Bump(u, v, .95f, .18f, .07f, .07f, .5f);           // chin
                    Bump(u, v, .62f, .28f, .12f, .12f, .35f);          // jaw muscle
                    Bump(u, v, .48f, -.05f, .18f, .2f, .4f);           // neck muscle
                    break;
                case Kind.Chest:
                    if (!fem) Bump(u, v, .84f, .62f, .2f, .16f, 1f);   // pec
                    Bump(u, v, .18f, .70f, .16f, .2f, .7f);            // shoulder blade
                    Bump(u, v, .60f, .89f, .35f, .035f, .35f);         // collarbone
                    Bump(u, v, .62f, .35f, .14f, .2f, .3f);            // side ribs
                    break;
                case Kind.Belly:
                    for (int i = 0; i < 3; i++) Bump(u, v, .86f, .22f + i * .27f, .12f, .1f, .45f);   // abs
                    Bump(u, v, .58f, .45f, .16f, .35f, .35f);          // obliques
                    Bump(u, v, .14f, .5f, .12f, .45f, .5f);            // lower-back muscles
                    break;
                case Kind.Hips:
                    Bump(u, v, .16f, .4f, .26f, .3f, 1f);              // glute
                    Bump(u, v, .8f, .55f, .15f, .2f, .3f);             // hip bone
                    break;
                case Kind.Thigh:
                    Bump(u, v, .70f, .55f, .25f, .35f, 1f);            // quad
                    Bump(u, v, .78f, .20f, .14f, .12f, .5f);           // teardrop above the knee
                    Bump(u, v, .25f, .60f, .2f, .35f, .7f);            // hamstring
                    break;
                case Kind.Shin:
                    Bump(u, v, .25f, .68f, .25f, .22f, 1f);            // calf
                    Bump(u, v, .72f, .72f, .14f, .2f, .35f);           // shin muscle
                    Bump(u, v, .7f, .92f, .14f, .07f, .5f);            // kneecap
                    break;
                case Kind.UpperArm:
                    Bump(u, v, .5f, .86f, .42f, .18f, 1f);             // deltoid
                    Bump(u, v, .76f, .5f, .2f, .25f, .8f);             // bicep
                    Bump(u, v, .24f, .55f, .2f, .3f, .7f);             // tricep
                    break;
                case Kind.Forearm:
                    Bump(u, v, .55f, .76f, .35f, .18f, .8f);           // forearm muscle
                    Bump(u, v, .5f, .14f, .3f, .08f, .3f);             // knuckles / palm
                    break;
                case Kind.Foot:
                    Bump(u, v, .12f, .3f, .12f, .25f, .5f);            // heel
                    Bump(u, v, .45f, .8f, .2f, .15f, .4f);             // ankle
                    break;
            }
        }

        static readonly Color BoneC = new Color(0.91f, 0.87f, 0.77f), Deep = new Color(0.36f, 0.04f, 0.06f),
            Nerve = new Color(0.95f, 0.90f, 0.66f), Cord = new Color(0.96f, 0.93f, 0.80f);

        // spine
        static bool Spine(float u, float v, int rh, out Color c)
        {
            c = Deep;
            float d = Mathf.Abs(u - 0.3f);
            if (d > 0.11f) return false;
            float seg = (v * rh / (float)(S * 6)) % 1f;
            if (d < 0.025f) { c = Cord; return true; }
            if (seg < 0.16f) { c = new Color(0.78f, 0.58f, 0.55f); return true; }
            c = BoneC * (0.82f + (1f - d / 0.11f) * 0.2f);
            return true;
        }

        static Color Inner(Kind k, float u, float v, float cu, int px, int py, int rh, float eu, float ev)
        {
            Color c;
            switch (k)
            {
                case Kind.Head:
                {
                    float dx = (u - 0.42f) * 2f, dy = (v - 0.62f) * 2.3f;
                    if (dx * dx + dy * dy < 0.55f)
                    {
                        float g = Mathf.Abs(Mathf.Sin(Mathf.PerlinNoise(px * 0.035f, py * 0.035f) * 22f));
                        return g < 0.12f ? new Color(0.55f, 0.25f, 0.30f) : new Color(0.88f, 0.66f, 0.67f) * (0.75f + g * 0.3f);
                    }
                    if (Ed(u, v, eu, ev, 0.09f, 0.07f) < 1f) return new Color(0.12f, 0.06f, 0.06f);            // eye socket
                    if (u > 0.6f && v < 0.36f && Mathf.Abs(((u - 0.6f) * 22f) % 1f - 0.5f) < 0.38f && Mathf.Abs(v - 0.27f) < 0.05f) return new Color(0.95f, 0.93f, 0.85f); // teeth
                    if (v < 0.25f && Spine(u, v, rh, out c)) return c;
                    return BoneC * (0.85f + Mathf.PerlinNoise(px * 0.04f, py * 0.04f) * 0.2f);                // skull
                }
                case Kind.Chest:
                {
                    if (Spine(u, v, rh, out c)) return c;
                    float rib = (v * 6.5f + cu * cu * 3f) % 1f;
                    if (u > 0.42f && u < 0.97f && rib < 0.42f) return BoneC * (0.8f + Mathf.Sin(rib / 0.42f * Mathf.PI) * 0.22f);
                    float hx = (u - 0.66f) / 0.16f, hy = (v - 0.42f) / 0.22f;
                    if (hx * hx + hy * hy < 1f)
                    {
                        float vein = Mathf.Abs(Mathf.Sin(Mathf.PerlinNoise(px * 0.05f, py * 0.05f) * 15f));
                        return vein < 0.1f ? new Color(0.85f, 0.75f, 0.45f) : new Color(0.50f, 0.04f, 0.08f) * (0.85f + (1 - hx * hx) * 0.2f);
                    }
                    return Color.Lerp(new Color(0.78f, 0.42f, 0.47f), new Color(0.93f, 0.62f, 0.64f), Mathf.PerlinNoise(px * 0.15f, py * 0.15f));
                }
                case Kind.Belly:
                {
                    if (Spine(u, v, rh, out c)) return c;
                    if (v > 0.5f) return new Color(0.40f, 0.08f, 0.07f) * (0.85f + Mathf.PerlinNoise(px * 0.02f, py * 0.02f) * 0.25f); // liver
                    if (u < 0.3f) return new Color(0.55f, 0.12f, 0.15f);                                       // kidney
                    float rug = Mathf.Abs(Mathf.Sin(Mathf.PerlinNoise(px * 0.04f, py * 0.04f) * 18f));
                    return new Color(0.84f, 0.56f, 0.50f) * (0.8f + rug * 0.22f);                             // stomach
                }
                case Kind.Hips:
                {
                    if (v > 0.35f && Spine(u, v, rh, out c)) return c;
                    if (v < 0.4f && (u < 0.42f || u > 0.75f)) return BoneC * (0.8f + (0.4f - v) * 0.5f);      // pelvis
                    float loop = Mathf.Abs(Mathf.Sin(Mathf.PerlinNoise(px * 0.03f, py * 0.03f) * 16f));
                    return loop < 0.18f ? new Color(0.50f, 0.20f, 0.22f) : new Color(0.89f, 0.60f, 0.56f) * (0.78f + loop * 0.25f);
                }
            }
            if (cu < 0.17f)
            {
                if (cu < 0.06f && v > 0.18f && v < 0.82f) return new Color(0.72f, 0.32f, 0.26f);
                return BoneC * (0.78f + (1f - cu / 0.17f) * 0.25f);
            }
            if (Mathf.Abs(cu - 0.26f) < 0.018f) return Nerve;
            if (Mathf.Abs(cu - 0.32f) < 0.025f) return new Color(0.32f, 0.02f, 0.06f);
            return Color.Lerp(new Color(0.40f, 0.05f, 0.07f), new Color(0.62f, 0.11f, 0.12f), Mathf.PerlinNoise(px * 0.09f, py * 0.012f));
        }
    }
}
