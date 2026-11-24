using System.Collections.Generic;
using UnityEngine;

namespace Mod
{
    public partial class Crater
    {
        // drips
        class Bleeder { public Vector2 local; public float life, next; }
        class Drip { public Vector2 px; public float life, w; }
        readonly List<Bleeder> bleeders = new List<Bleeder>();
        readonly List<Drip> drips = new List<Drip>();
        void AddBleeder(Vector2 world, float life)
        {
            life *= GoreSettings.BleedTime; if (life <= 0f) return;
            if (bleeders.Count > 8) bleeders.RemoveAt(0);
            bleeders.Add(new Bleeder { local = transform.InverseTransformPoint(world), life = life });
        }

        void RunDrips(float dt)
        {
            for (int k = bleeders.Count - 1; k >= 0; k--)
            {
                var b = bleeders[k]; b.life -= dt; b.next -= dt;
                if (b.life <= 0) { bleeders.RemoveAt(k); continue; }
                if (b.next <= 0 && drips.Count < 14)
                {
                    b.next = Random.Range(0.6f, 2.2f);
                    Vector2 world = transform.TransformPoint(b.local + Random.insideUnitCircle * 0.01f);
                    drips.Add(new Drip { px = ToPx(world), life = Random.Range(1.5f, 4f), w = Random.Range(0.12f, 0.22f) * L.S });
                }
            }
            if (drips.Count == 0) return;
            Vector2 down = transform.InverseTransformDirection(Physics2D.gravity.normalized);
            down = new Vector2(down.x / transform.lossyScale.x, down.y / transform.lossyScale.y).normalized;
            float step = L.S * 3.5f * dt;   // px/frame
            for (int k = drips.Count - 1; k >= 0; k--)
            {
                var d = drips[k]; d.life -= dt;
                d.px += (down + new Vector2(Mathf.PerlinNoise(d.px.y * 0.05f, d.life) - 0.5f, 0) * 0.4f) * step;
                d.w = Mathf.Max(0.08f * L.S, d.w - dt * 0.03f * L.S);
                int cx = (int)d.px.x, cy = (int)d.px.y, r = Mathf.CeilToInt(d.w);
                bool alive = d.life > 0 && cx >= 0 && cy >= 0 && cx < L.w && cy < L.h && L.skin[cy * L.w + cx].a > 100;
                if (!alive) { drips.RemoveAt(k); continue; }
                for (int y = cy - r; y <= cy + r; y++)
                    for (int x = cx - r; x <= cx + r; x++)
                    {
                        if (x < 0 || y < 0 || x >= L.w || y >= L.h) continue;
                        float dd = ((x - d.px.x) * (x - d.px.x) + (y - d.px.y) * (y - d.px.y)) / (d.w * d.w);
                        if (dd > 1f) continue;
                        int i = y * L.w + x; if (cur[i].a == 0 || level[i] >= 4) continue;
                        Color wet = Color.Lerp(new Color(0.58f, 0.03f, 0.06f), new Color(0.45f, 0.01f, 0.04f), dd);
                        var col = Color.Lerp(cur[i], wet, 0.5f * (1f - dd * dd)); col.a = cur[i].a / 255f; cur[i] = col;
                    }
                Mark(cx - r, cy - r, cx + r, cy + r);
            }
        }

        // compound fracture
        bool fractured;
        void CheckFracture()
        {
            if (fractured || !(limb && limb.Broken)) return;
            string n = name.ToLower();
            if (limb && (n.Contains("body") || limb.HasBrain)) return;
            fractured = true;
            var bnd = sr.bounds;
            Vector2 mid = bnd.center;
            Vector2 side = transform.right * (Random.value < 0.5f ? 1f : -1f);
            Vector2 at = mid + side * Mathf.Min(bnd.extents.x, bnd.extents.y) * 0.5f;
            Wound(at, 2.6f, 3, false);
            AddBleeder(at, 40f);
            Spray(at, side, 30f, 8);
        }

        // blood loss
        float maxBlood, paleTick;
        void Pallor()
        {
            if (!limb) { if (ExtPale >= 0) sr.color = Color.Lerp(Color.white, new Color(0.80f, 0.84f, 0.92f), ExtPale); return; }
            var circ = limb.CirculationBehaviour; if (!circ) return;
            float b = circ.GetAmountOfBlood();
            if (b > maxBlood) maxBlood = b;
            if (maxBlood <= 0) return;
            float pale = 1f - Mathf.Clamp01(b / maxBlood);
            pale = Mathf.Clamp01(pale * 1.6f);
            if (!limb.IsConsideredAlive) pale = Mathf.Max(pale, 0.5f);
            pale *= 1f - Mathf.Clamp01(rot * 2f);
            sr.color = Color.Lerp(Color.white, new Color(0.80f, 0.84f, 0.92f), pale);
        }

        // rot
        float rot, deadFor, rotTick = -1f; int rotPass; bool isChunk;
        void RunRot(float dt)
        {
            if (!limb && !isChunk) return;
            if (limb && limb.IsConsideredAlive) { deadFor = 0f; return; }
            if (!GoreSettings.Rot) return;
            deadFor += dt;
            if (deadFor < (isChunk ? Mathf.Min(6f, GoreSettings.RotStart) : GoreSettings.RotStart)) return;
            rot = Mathf.Min(1f, rot + dt / Mathf.Max(1f, GoreSettings.RotTime));
            if (rotTick < 0f) rotTick = Random.Range(0f, 1.2f);
            rotTick -= dt; if (rotTick > 0f) return;
            rotTick = 1.2f;
            int stride = Mathf.Max(7, Mathf.RoundToInt(7f * L.S * L.S));
            rotPass = (rotPass + 1) % stride;
            float r = rot, inv = 1f / L.S;
            int n = cur.Length;
            if (L.rOk == null || L.rOk.Length != n)
            { L.rG = new ushort[n]; L.rV = new ushort[n]; L.rB = new ushort[n]; L.rSl = new ushort[n]; L.rF1 = new ushort[n]; L.rOk = new byte[n]; }
            if (rotPass >= L.h) return;
            int lastY = rotPass;
            for (int y = rotPass; y < L.h; lastY = y, y += stride)
                for (int x = 0; x < L.w; x++)
                {
                    int i = y * L.w + x;
                    if (cur[i].a == 0) continue;
                    byte lv = level[i];
                    float ox = x * inv, oy = y * inv;
                    Color c = cur[i], tgt; float k;
                    if (lv == 0)
                    {
                        if (L.rOk[i] == 0) CacheRot(i, ox, oy);
                        float f1 = L.rF1[i] == 65535 ? -1f : L.rF1[i] * (2f / 65534f);
                        tgt = RotSkin(L.skin[i], L.rG[i] / 65535f, L.rV[i] / 65535f, L.rB[i] * (1.5f / 65535f) - 0.25f, f1, r);
                        // skin slip
                        float slip = L.rSl[i] * (1.5f / 65535f) - 0.25f;
                        if (r > 0.78f && slip > 1.15f - (r - 0.78f) * 2.2f)
                        {
                            Color fl = Color.Lerp((Color)L.flesh[i], new Color(0.30f, 0.12f, 0.08f), 0.55f) * (0.75f + 0.25f * Mathf.PerlinNoise(ox * 1.5f, oy * 1.5f));
                            fl.a = c.a; cur[i] = fl; level[i] = 2; continue;
                        }
                        k = 0.3f;
                    }
                    else if ((lv == 5 || lv == 4) && IsBone(L.bone[i])) { tgt = c * 0.55f + new Color(0.40f, 0.33f, 0.20f) * 0.45f; k = 0.05f * r; }
                    else { tgt = Color.Lerp(c, new Color(0.20f, 0.07f, 0.05f), 0.5f + 0.5f * r); k = 0.06f * r; }   // wounds dry out and blacken
                    Color o = Color.Lerp(c, tgt, k); o.a = c.a; cur[i] = o;
                }
            Mark(0, rotPass, L.w - 1, lastY);
        }

        // bruises (needs a heartbeat)
        class Bruise { public Vector2 c; public float R, age, str, sx; }
        readonly List<Bruise> bruises = new List<Bruise>(); byte[] brS; float bruiseTick;
        void BruiseAt(Vector2 cPx, float radPx, float str)
        {
            if (!limb || !limb.IsConsideredAlive || !GoreSettings.Bruises) return;
            radPx = Mathf.Min(radPx, 3.5f * L.S);
            foreach (var b in bruises)
                if ((b.c - cPx).magnitude < b.R * 0.6f) { b.R = Mathf.Max(b.R, radPx); b.str = Mathf.Min(1f, b.str + str * 0.4f); return; }
            if (bruises.Count >= 6) bruises.RemoveAt(0);
            bruises.Add(new Bruise { c = cPx, R = radPx, str = Mathf.Clamp01(str), sx = Random.value * 500f });
        }
        void RunBruises(float dt)
        {
            if (bruises.Count == 0) return;
            bruiseTick -= dt; if (bruiseTick > 0f) return;
            bruiseTick = Random.Range(0.45f, 0.6f);
            if (!limb.IsConsideredAlive) { bruises.Clear(); return; }
            if (brS == null) brS = new byte[cur.Length];
            float inv = 1f / L.S;
            for (int k = bruises.Count - 1; k >= 0; k--)
            {
                var b = bruises[k];
                b.age += 0.5f;
                float grow = Mathf.Clamp01(b.age / 10f), purple = Mathf.Clamp01((b.age - 2f) / 18f);
                float Rb = b.R * (0.7f + 0.5f * grow); bool ch = false;
                int x0 = Mathf.Max(0, (int)(b.c.x - Rb)), x1 = Mathf.Min(L.w - 1, (int)(b.c.x + Rb));
                int y0 = Mathf.Max(0, (int)(b.c.y - Rb)), y1 = Mathf.Min(L.h - 1, (int)(b.c.y + Rb));
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        int i = y * L.w + x;
                        if (level[i] != 0 || L.skin[i].a == 0) continue;
                        float ox = x * inv, oy = y * inv;
                        float dx = x - b.c.x, dy = y - b.c.y;
                        float d = Mathf.Sqrt(dx * dx + dy * dy) / Rb + (Mathf.PerlinNoise(ox * 0.9f + b.sx, oy * 0.9f) - 0.5f) * 0.5f;
                        if (d >= 1f) continue;
                        float s = (1f - d * d) * b.str * grow * (0.6f + 0.4f * Mathf.PerlinNoise(ox * 2.3f, oy * 2.3f + b.sx));
                        byte sb = (byte)(Mathf.Clamp01(s) * 255f);
                        if (sb < brS[i]) continue;
                        brS[i] = sb;
                        Color tint = Color.Lerp(new Color(1f, 0.62f, 0.60f),
                            Color.Lerp(new Color(0.62f, 0.44f, 0.62f), new Color(0.42f, 0.24f, 0.38f), Mathf.Clamp01(1f - d) * 0.6f), purple);
                        Color o = (Color)L.skin[i] * Color.Lerp(Color.white, tint, Mathf.Clamp01(s) * 0.85f);
                        o.a = L.skin[i].a / 255f; cur[i] = o; ch = true;
                    }
                if (ch) Mark(x0, y0, x1, y1);
                if (b.age > 24f) bruises.RemoveAt(k);
            }
        }

        // rot noise cached per pixel
        void CacheRot(int i, float ox, float oy)
        {
            float g = Mathf.PerlinNoise(ox * 0.07f + 3f, oy * 0.07f + 17f);
            float vn = Mathf.Abs(Mathf.PerlinNoise(ox * 0.35f + 9f, oy * 0.35f + 41f) - 0.5f) + Mathf.Abs(Mathf.PerlinNoise(ox * 0.9f + 1f, oy * 0.9f) - 0.5f) * 0.4f;
            float b = Mathf.PerlinNoise(ox * 0.05f + 50f, oy * 0.05f + 5f);
            float sl = Mathf.PerlinNoise(ox * 0.18f + 71f, oy * 0.18f + 13f);
            L.rG[i] = (ushort)(Mathf.Clamp01((g - 0.42f) * 3f) * 65535f + 0.5f);
            L.rV[i] = (ushort)(Mathf.Clamp01(1f - vn / 0.055f) * 65535f + 0.5f);
            L.rB[i] = (ushort)(Mathf.Clamp01((b + 0.25f) / 1.5f) * 65535f + 0.5f);
            L.rSl[i] = (ushort)(Mathf.Clamp01((sl + 0.25f) / 1.5f) * 65535f + 0.5f);
            if (Mathf.PerlinNoise(ox * 0.1f + 90f, oy * 0.1f) > 0.5f)
            {
                float f2, f1 = Worley(ox * 0.45f + 13f, oy * 0.45f + 7f, out f2);
                L.rF1[i] = (ushort)(Mathf.Clamp01(f1 / 2f) * 65534f + 0.5f);
            }
            else L.rF1[i] = 65535;
            L.rOk[i] = 1;
        }

        static Color RotSkin(Color32 s32, float gF, float vF, float b, float f1, float r)
        {
            Color s = s32;
            float lum = s.r * 0.3f + s.g * 0.59f + s.b * 0.11f;
            float rel = lum / 0.62f;
            Color o = Color.Lerp(s, new Color(0.74f, 0.71f, 0.68f) * rel, Mathf.Clamp01(r / 0.15f) * 0.55f);
            o = Color.Lerp(o, new Color(0.47f, 0.54f, 0.33f) * rel, Mathf.Clamp01((r - 0.12f) / 0.25f) * gF * 0.75f);
            o = Color.Lerp(o, new Color(0.22f, 0.23f, 0.20f), Mathf.Clamp01((r - 0.3f) / 0.2f) * vF * 0.8f);
            o = Color.Lerp(o, new Color(0.33f, 0.20f, 0.18f) * (0.7f + 0.4f * rel), Mathf.Clamp01((r - 0.48f) / 0.3f) * (0.55f + 0.45f * b));
            // blisters
            if (r > 0.6f && f1 >= 0f)
            {
                float bl = Mathf.Clamp01(1f - f1 / (0.16f + (r - 0.6f) * 0.25f));
                if (bl > 0f)
                {
                    Color bc = Color.Lerp(new Color(0.62f, 0.55f, 0.36f), new Color(0.86f, 0.82f, 0.62f), bl);
                    if (bl > 0.82f) bc += new Color(0.25f, 0.25f, 0.22f);
                    o = Color.Lerp(o, bc, Mathf.Clamp01(bl * 2f));
                }
            }
            o.a = s.a;
            return o;
        }

    }
}
