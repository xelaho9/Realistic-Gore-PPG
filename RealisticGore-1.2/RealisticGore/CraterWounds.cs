using System.Collections.Generic;
using UnityEngine;

namespace Mod
{
    public partial class Crater
    {
        // crater
        static float Worley(float x, float y, out float f2)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float f1 = 9f; f2 = 9f;
            for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    int cx = ix + i, cy = iy + j;
                    float px = cx + RealisticHuman.Hash(cx, cy), py = cy + RealisticHuman.Hash(cx + 71, cy - 13);
                    float d = (px - x) * (px - x) + (py - y) * (py - y);
                    if (d < f1) { f2 = f1; f1 = d; } else if (d < f2) f2 = d;
                }
            f2 = Mathf.Sqrt(f2); return Mathf.Sqrt(f1);
        }
        static float Ridge(float x, float y)
        {
            float f2, f1 = Worley(x, y, out f2);
            float a = 1f - Mathf.Clamp01((f2 - f1) / 0.32f);
            float g2, g1 = Worley(x * 2.1f + 17f, y * 2.1f + 5f, out g2);
            float b = 1f - Mathf.Clamp01((g2 - g1) / 0.3f);
            return Mathf.Clamp01(a * 0.75f + b * 0.4f - f1 * 0.25f);
        }

        // worley + cell id
        static float Chunks(float x, float y, out float f2, out float id)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float f1 = 9f; f2 = 9f; id = 0f;
            for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    int cx = ix + i, cy = iy + j;
                    float px = cx + RealisticHuman.Hash(cx, cy), py = cy + RealisticHuman.Hash(cx + 71, cy - 13);
                    float d = (px - x) * (px - x) + (py - y) * (py - y);
                    if (d < f1) { f2 = f1; f1 = d; id = RealisticHuman.Hash(cx - 29, cy + 41); } else if (d < f2) f2 = d;
                }
            f2 = Mathf.Sqrt(f2); return Mathf.Sqrt(f1);
        }

        static float MeatH(float x, float y, out float id, out float crev)
        {
            float wx = x + (Mathf.PerlinNoise(x * 0.45f + 3f, y * 0.45f) - 0.5f) * 1.6f;
            float wy = y + (Mathf.PerlinNoise(x * 0.45f, y * 0.45f + 7f) - 0.5f) * 1.6f;
            float f2, f1 = Chunks(wx, wy, out f2, out id);
            crev = (1f - Mathf.Clamp01((f2 - f1) / 0.1f)) * Mathf.Clamp01(Mathf.PerlinNoise(x * 0.6f + 13f, y * 0.6f) * 2.2f - 0.6f);
            float g2, gid, g1 = Chunks(wx * 2.3f + 11f, wy * 2.3f + 3f, out g2, out gid);
            return 1f - f1 * f1 * 0.9f + (0.5f - g1 * g1) * 0.35f - crev * 0.6f;
        }

        void Crater3D(Vector2 c, float R, int depth, bool hole, int x0, int x1, int y0, int y1, float nf, float sx, float sy)
        {
            Vector2 up2 = transform.InverseTransformVector(Vector2.up); if (up2.sqrMagnitude < 1e-8f) up2 = Vector2.up; up2.Normalize();
            Vector2 rt = new Vector2(up2.y, -up2.x);
            Vector3 Ld = new Vector3(rt.x * 0.3f + up2.x * 0.75f, rt.y * 0.3f + up2.y * 0.75f, 0.6f).normalized;
            Vector3 H = (Ld + Vector3.forward).normalized;
            float cell = Mathf.Max(R * 0.2f, 3.5f);
            float ox = Random.value * 300f, oy = Random.value * 300f;
            Color pit = new Color(0.36f, 0.03f, 0.045f), wall = new Color(0.74f, 0.09f, 0.11f), hot = new Color(0.90f, 0.22f, 0.22f);
            ParallelRows(delegate(int ya, int yb)
            {
                for (int y = ya; y <= yb; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        int i = y * L.w + x;
                        if (cur[i].a == 0) continue;
                        Vector2 off = new Vector2(x - c.x, y - c.y);
                        float dr = off.magnitude / R;
                        float d = dr + (Mathf.PerlinNoise(x * nf + sx, y * nf + sy) - 0.5f) * 0.42f
                                     + (Mathf.PerlinNoise(x * nf * 4f + sy, y * nf * 4f + sx) - 0.5f) * 0.12f;
                        if (d > 1.35f) continue;
                        Vector2 dir = dr > 1e-4f ? off / (dr * R) : Vector2.zero;
                        byte lv; Color col;
                        if (d < 0.9f)
                        {
                            float tx = (x + ox) / cell, ty = (y + oy) / cell, e = 0.35f;
                            float r0 = Ridge(tx, ty);
                            float gx = Ridge(tx + e, ty) - Ridge(tx - e, ty), gy = Ridge(tx, ty + e) - Ridge(tx, ty - e);
                            float slope = Mathf.Lerp(0.15f, 1.1f, Mathf.Clamp01(d / 0.9f));
                            Vector3 n = new Vector3(-dir.x * slope - gx * 0.7f, -dir.y * slope - gy * 0.7f, 1f).normalized;
                            float diff = Mathf.Max(0f, Vector3.Dot(n, Ld));
                            float var = Mathf.PerlinNoise(tx * 0.23f + 3f, ty * 0.23f);
                            Color baseC = Color.Lerp(pit, wall, Mathf.Pow(r0, 0.8f));
                            baseC = Color.Lerp(baseC, hot, Mathf.Clamp01((r0 - 0.65f) * 2.5f) * var);
                            if (L.flesh != null) baseC = Color.Lerp(baseC, (Color)L.flesh[i] * 0.8f, 0.12f);
                            if (depth >= 3 && d < 0.4f && L.bone != null)
                                baseC = Color.Lerp(baseC, (Color)L.bone[i], Mathf.Clamp01((0.4f - d) / 0.15f) * 0.6f);
                            float ao = Mathf.Lerp(0.8f, 1f, Mathf.Clamp01(d / 0.8f));
                            col = baseC * ((0.5f + 0.7f * diff) * ao);
                            float spec = Mathf.Pow(Mathf.Max(0f, Vector3.Dot(n, H)), 34f) * (0.3f + 0.7f * r0);
                            col += new Color(1f, 0.86f, 0.86f) * spec * 0.85f;
                            if (RealisticHuman.Hash(x * 3 + 1, y * 5 + 2) > 0.95f && diff > 0.35f) col += new Color(0.38f, 0.33f, 0.33f);   // wet glints
                            if (hole && dr < 0.15f) col = Color.Lerp(col, Hole * 0.6f, Mathf.Clamp01((0.15f - dr) / 0.06f));
                            lv = (byte)(depth >= 3 ? 4 : 3);
                        }
                        else if (d < 1.04f)
                        {
                            float t = (d - 0.9f) / 0.14f;
                            Vector3 n = new Vector3(dir.x * (t - 0.4f) * 1.2f, dir.y * (t - 0.4f) * 1.2f, 1f).normalized;
                            float diff = Mathf.Max(0f, Vector3.Dot(n, Ld));
                            float bl = Mathf.PerlinNoise(x * nf * 3f + sx, y * nf * 3f) ;
                            Color rc = Color.Lerp(Color.Lerp(new Color(0.62f, 0.12f, 0.11f), (Color)L.skin[i], Mathf.SmoothStep(0f, 1f, t)), Blood, (1f - t) * (0.2f + bl * 0.3f));
                            col = rc * (0.45f + 0.75f * diff) + new Color(1f, 0.9f, 0.9f) * Mathf.Pow(Mathf.Max(0f, Vector3.Dot(n, H)), 24f) * 0.5f;
                            lv = 2;
                        }
                        else
                        {
                            if (level[i] > 1) continue;
                            if (d > 1.2f) continue;
                            float k = Mathf.Clamp01((1.2f - d) / 0.16f);
                            col = Color.Lerp(cur[i], new Color(0.50f, 0.05f, 0.07f), k * 0.4f);                  // blood smear around it
                            lv = 1;
                        }
                        if (lv < level[i]) continue;
                        col.a = L.skin[i].a / 255f;
                        cur[i] = col; level[i] = lv;
                    }
            }, y0, y1);
            for (int k = 0; k < (int)(R * 1.5f); k++)
            {
                Vector2 p = c + Random.insideUnitCircle * R * 1.8f;
                int x = (int)p.x, y = (int)p.y;
                if (x < 0 || y < 0 || x >= L.w || y >= L.h) continue;
                int i = y * L.w + x; if (cur[i].a == 0 || level[i] > 2) continue;
                var col = Color.Lerp(cur[i], Blood, 0.75f); col.a = cur[i].a / 255f; cur[i] = col;
            }
            Mark(x0, y0, x1, y1); MarkR(c, R * 1.8f);
        }

        public void Burn(Vector2 world, float amount)
        {
            if (!Ready) return;
            Vector2 c = ToPx(world);
            float R = Random.Range(2.5f, 4.5f) * L.S;
            int x0 = Mathf.Max(0, (int)(c.x - R)), x1 = Mathf.Min(L.w - 1, (int)(c.x + R));
            int y0 = Mathf.Max(0, (int)(c.y - R)), y1 = Mathf.Min(L.h - 1, (int)(c.y + R));
            float nf = 1.2f / L.S;
            if (L.bN == null || L.bN.Length != cur.Length) { L.bN = new float[cur.Length]; L.bCk = new byte[cur.Length]; }
            float[] bN = L.bN; byte[] bCk = L.bCk;
            float invR = 1f / R, lim = 1.25f * R; lim *= lim;
            Color32 charC = (Color32)new Color(0.07f, 0.05f, 0.04f), crackC = (Color32)new Color(0.5f, 0.08f, 0.05f);
            bool changed = false;
            for (int y = y0; y <= y1; y++)
            {
                float dy = y - c.y;
                for (int x = x0; x <= x1; x++)
                {
                    int i = y * L.w + x;
                    if (cur[i].a == 0 || level[i] >= 3) continue;
                    float dx = x - c.x, dd = dx * dx + dy * dy;
                    if (dd > lim) continue;
                    if (bCk[i] == 0) { bN[i] = Mathf.PerlinNoise(x * nf, y * nf); bCk[i] = (byte)(Mathf.PerlinNoise(x * nf * 3f, y * nf * 3f) > 0.68f ? 2 : 1); }
                    float d = Mathf.Sqrt(dd) * invR + (bN[i] - 0.5f) * 0.5f;
                    if (d > 1f) continue;
                    Color32 o;
                    if (burn[i] >= 3f)
                    {
                        // charred is final unless painted over
                        o = bCk[i] == 2 ? crackC : charC; o.a = L.skin[i].a;
                    }
                    else
                    {
                        burn[i] = Mathf.Min(3f, burn[i] + amount * (1f - d));
                        float b = burn[i]; Color col;
                        if (b < 1f) col = Color.Lerp(L.skin[i], new Color(0.62f, 0.30f, 0.20f), b);                        // red, blistered
                        else if (b < 2f) col = Color.Lerp(new Color(0.62f, 0.30f, 0.20f), (Color)L.flesh[i] * 0.75f, b - 1f);       // skin gone, raw flesh
                        else col = Color.Lerp((Color)L.flesh[i] * 0.75f, bCk[i] == 2 ? new Color(0.5f, 0.08f, 0.05f) : new Color(0.07f, 0.05f, 0.04f), b - 2f); // charred
                        col.a = L.skin[i].a / 255f; o = col;
                    }
                    Color32 p = cur[i];
                    if (p.r != o.r || p.g != o.g || p.b != o.b || p.a != o.a) { cur[i] = o; changed = true; }
                }
            }
            if (changed) Mark(x0, y0, x1, y1);
        }

        void Shot(Shot shot)
        {
            if (!Ready || (limb && limb.IsAndroid)) return;
            float dmg = shot.damage;
            float hd = dmg / 5f * GoreSettings.HolePower;                                   // holes are 5x weaker than the gun's damage
            float entryR = Mathf.Clamp(0.15f + hd / 100f, 0.15f, 0.6f) * GoreSettings.HoleSize;
            float exitR = Mathf.Clamp(0.35f + hd / 40f, 0.35f, 1.4f) * GoreSettings.HoleSize;
            bool snap = hd > GoreSettings.BoneSnapDmg;
            Vector2 travel = -shot.normal;
            Vector2 exitPt = shot.point;
            var bnd = sr.bounds; float span = Mathf.Min(bnd.size.x, bnd.size.y) * 0.9f;
            for (int k = 0; k < 12; k++) { var p = exitPt + travel * (span / 12f); if (!InBody(p)) break; exitPt = p; }

            // entry
            Wound(shot.point, entryR * 2.5f, 1, false);
            powderNext = GoreSettings.Powder && Random.value < 0.4f;   // close range shot
            Carve(shot.point, entryR, snap, true, false);
            powderNext = false;
            AddBleeder(shot.point, 25f);
            if (GoreSettings.EyePop) TryEye(shot.point, exitPt, dmg);

            if ((exitPt - shot.point).sqrMagnitude > 0.0004f)
            {
                // big rounds go straight through
                if (hd >= GoreSettings.TunnelDmg) Tunnel(shot.point, exitPt, Mathf.Min(exitR * 0.5f, 0.7f), snap);
                mistNext = GoreSettings.Mist;
                Carve(exitPt, exitR, snap);
                mistNext = false;
                AddBleeder(exitPt, 25f);
                Spray(exitPt, travel, dmg, 16);
            }
            Spray(shot.point, shot.normal, dmg * 0.4f, 6);
        }

        // torn clean through (torso cut in half / blowout)
        public void Rip(Vector2 p, Vector2 into)
        {
            if (!Ready) return;
            into = into.sqrMagnitude > 1e-8f ? into.normalized : Vector2.zero;
            float u = L.S / ppuHi * Mathf.Abs(transform.lossyScale.y);
            mistNext = GoreSettings.Mist;
            Carve(p + into * u * 0.5f, 1.7f, true, false, true);
            mistNext = false;
            Carve(p + into * u * 1.4f, 1.2f, true, false, false);
            Carve(p, 1.4f, true, false, false);
            AddBleeder(p, 90f);
            Spray(p, -into, 400f, 20);
            needSplit = true;
        }

        // blown apart: cut across the limb a few times so it falls to bits
        public void Shred(Vector2 hit, Vector2 dir)
        {
            if (!Ready) return;
            bool v = Vert; int len = v ? L.h : L.w, wid = v ? L.w : L.h;
            int cuts = len > wid * 2 ? 3 : 2;
            for (int k = 0; k < cuts; k++)
            {
                float a = (k + 0.5f + Random.Range(-0.2f, 0.2f)) / cuts * len, j = Random.Range(-0.2f, 0.2f) * wid;
                Vector2 p0 = v ? new Vector2(-2f, a - j) : new Vector2(a - j, -2f);
                Vector2 p1 = v ? new Vector2(wid + 2f, a + j) : new Vector2(a + j, wid + 2f);
                Tunnel(PxToWorld(p0), PxToWorld(p1), 1.1f, true);
            }
            mistNext = GoreSettings.Mist;
            Carve(hit, 2.2f, true, false, true);
            mistNext = false;
            AddBleeder(hit, 90f);
            Spray(hit, dir, 600f, 24);
            needSplit = true;
        }

        // actual holes
        static bool IsBone(Color32 c)
        {
            if (c.r > 150 && c.g > 140 && c.b > 115 && c.r - c.g < 32 && c.r - c.b < 80) return true;
            return c.r > 172 && c.r < 197 && c.g > 72 && c.g < 92 && c.b > 56 && c.b < 76;   // marrow core of limb bones
        }

        Vector2 PxDown()
        {
            Vector2 down = transform.InverseTransformDirection(Physics2D.gravity.normalized);
            Vector3 ls = transform.lossyScale;
            down = new Vector2(down.x * Mathf.Sign(ls.x), down.y * Mathf.Sign(ls.y));
            return down.sqrMagnitude > 1e-8f ? down.normalized : Vector2.down;
        }

        // bone shaft
        Color BoneShade(int x, int y, bool vert, Vector3 Ld, Vector3 H, bool brokenEnd)
        {
            int a = 0, b = 0, lim = Mathf.Max(6, L.S * 10);
            int dx = vert ? 1 : 0, dy = vert ? 0 : 1;
            while (a < lim) { int xx = x - dx * (a + 1), yy = y - dy * (a + 1); if (xx < 0 || yy < 0 || xx >= L.w || yy >= L.h || !IsBone(L.bone[yy * L.w + xx])) break; a++; }
            while (b < lim) { int xx = x + dx * (b + 1), yy = y + dy * (b + 1); if (xx < 0 || yy < 0 || xx >= L.w || yy >= L.h || !IsBone(L.bone[yy * L.w + xx])) break; b++; }
            float u = (a - b) / (float)Mathf.Max(1, a + b);
            float nz = Mathf.Sqrt(Mathf.Max(0f, 1f - u * u));
            Vector3 n = vert ? new Vector3(u, 0f, nz) : new Vector3(0f, u, nz);
            float diff = Mathf.Max(0f, Vector3.Dot(n, Ld));
            Color ivory = new Color(0.90f, 0.85f, 0.74f);
            float bx = x / (float)L.S, by = y / (float)L.S;
            float ves = vert ? Mathf.PerlinNoise(bx * 3f, by * 0.5f) : Mathf.PerlinNoise(bx * 0.5f, by * 3f);
            Color col = Color.Lerp(ivory, new Color(0.86f, 0.60f, 0.54f), 0.2f + ves * 0.3f);
            float spec = 1f;
            if (brokenEnd)
            {
                if (Mathf.Abs(u) < 0.62f)
                {
                    float sp = Mathf.PerlinNoise(bx * 4f + 11f, by * 4f + 3f);
                    col = sp > 0.64f ? new Color(0.84f, 0.62f, 0.34f) : Color.Lerp(new Color(0.30f, 0.02f, 0.03f), new Color(0.55f, 0.08f, 0.07f), sp);
                    spec = 1.6f;
                }
                else col = new Color(0.94f, 0.91f, 0.84f);
            }
            float bl = Mathf.PerlinNoise(bx * 1.2f + 31f, by * 1.2f + 7f) + Mathf.PerlinNoise(bx * 4f + 3f, by * 4f + 17f) * 0.25f;
            col = Color.Lerp(col, Blood, Mathf.Clamp01(bl - 0.42f) * 1.4f);
            col *= 0.86f + RealisticHuman.Hash(x * 3 + 1, y * 5 + 7) * 0.18f;
            Color lit = col * (0.3f + 0.8f * diff) + new Color(col.r * 0.12f, 0f, 0f) * (1f - diff);
            lit += Color.white * Mathf.Pow(Mathf.Max(0f, Vector3.Dot(n, H)), 40f) * 0.45f * spec;
            lit.a = 1f;
            return lit;
        }

        // head
        bool InBrain(int x, int y)
        {
            float u = ((x + 0.5f) / L.S - L.pad - L.ux0) / L.uw, v = ((y + 0.5f) / L.S - L.pad - L.uy0) / L.uh;
            float dx = (u - 0.42f) * 2f, dy = (v - 0.62f) * 2.3f;
            return dx * dx + dy * dy < 0.55f;
        }

        static float Gyri(float ox, float oy)
        {
            float w1 = Mathf.PerlinNoise(ox * 0.22f + 3.1f, oy * 0.22f + 8.7f);
            float w2 = Mathf.PerlinNoise(ox * 0.5f + 12.4f, oy * 0.5f + 1.3f);
            float f = Mathf.Abs(Mathf.Sin((w1 * 9f + w2 * 3.5f) * Mathf.PI));
            return Mathf.Pow(f, 0.45f);
        }

        // skull edge
        Color SkullShade(int x, int y, Vector3 Ld, Vector3 H, float tb, Vector2 dir, float ox, float oy)
        {
            Vector3 n = new Vector3(-dir.x * 0.55f, -dir.y * 0.55f, 1f).normalized;
            float pore = Mathf.PerlinNoise(ox * 3.2f + 5f, oy * 3.2f + 9f);
            Color col;
            float spec;
            if (tb > 0.72f || tb < 0.22f)
            {
                col = Color.Lerp(new Color(0.93f, 0.89f, 0.80f), new Color(0.84f, 0.76f, 0.64f), pore * 0.6f);
                if (tb < 0.22f) col *= 0.88f;
                spec = 0.8f;
            }
            else
            {
                col = pore > 0.58f ? new Color(0.88f, 0.74f, 0.58f) : Color.Lerp(new Color(0.42f, 0.04f, 0.05f), new Color(0.70f, 0.20f, 0.16f), pore / 0.58f);
                n = (n + new Vector3((pore - 0.5f) * 0.8f, (Mathf.PerlinNoise(ox * 3.2f + 40f, oy * 3.2f) - 0.5f) * 0.8f, 0f)).normalized;
                spec = 1.5f;
            }
            float bl = Mathf.PerlinNoise(ox * 0.9f + 21f, oy * 0.9f + 4f);
            col = Color.Lerp(col, new Color(0.40f, 0.02f, 0.04f), Mathf.Clamp01(bl - 0.38f) * 1.4f);
            col *= 0.86f + RealisticHuman.Hash(x * 3 + 1, y * 5 + 7) * 0.18f;
            float diff = Mathf.Max(0f, Vector3.Dot(n, Ld));
            Color lit = col * (0.32f + 0.78f * diff);
            lit += Color.white * Mathf.Pow(Mathf.Max(0f, Vector3.Dot(n, H)), 36f) * 0.35f * spec;
            lit.a = 1f;
            return lit;
        }

        // strands across the hole
        void Strands(Vector2 c, float R, int count)
        {
            if (R < 3f || !GoreSettings.Strands) return;
            Vector2 down = PxDown();
            for (int s = 0; s < count; s++)
            {
                float a0 = Random.value * Mathf.PI * 2f, a1 = a0 + Mathf.PI + Random.Range(-1f, 1f);
                Vector2 p0 = c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * R * 1.12f;
                Vector2 p2 = c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * R * 1.12f;
                bool torn = Random.value < 0.45f;
                if (torn) p2 = p0 + (p2 - p0) * Random.Range(0.35f, 0.6f) + down * R * 0.55f;
                Vector2 p1 = (p0 + p2) * 0.5f + down * R * Random.Range(0.25f, 0.6f);
                bool nerve = Random.value < 0.25f;
                float w0 = (nerve ? 0.06f : Random.Range(0.07f, 0.14f)) * L.S;
                int steps = Mathf.CeilToInt(R * 4f);
                // only if it spans the hole, else it's a blob
                int open = 0;
                for (int k = 0; k <= steps; k++)
                {
                    float t = k / (float)steps, it = 1f - t;
                    Vector2 p = it * it * p0 + 2f * it * t * p1 + t * t * p2;
                    int qx = (int)p.x, qy = (int)p.y;
                    if (qx >= 0 && qy >= 0 && qx < L.w && qy < L.h && level[qy * L.w + qx] == 6) open++;
                }
                if (open < steps * 0.55f) continue;
                for (int k = 0; k <= steps; k++)
                {
                    float t = k / (float)steps, it = 1f - t;
                    Vector2 p = it * it * p0 + 2f * it * t * p1 + t * t * p2;
                    float w = Mathf.Max(0.6f, w0 * (torn ? Mathf.Lerp(1f, 0.3f, t) : 0.7f + 0.6f * Mathf.Abs(t - 0.5f)));
                    int r = Mathf.CeilToInt(w);
                    for (int y = (int)p.y - r; y <= (int)p.y + r; y++)
                        for (int x = (int)p.x - r; x <= (int)p.x + r; x++)
                        {
                            if (x < 0 || y < 0 || x >= L.w || y >= L.h) continue;
                            int i = y * L.w + x; if (level[i] != 6 || L.skin[i].a < 100) continue;
                            float dd = Vector2.Distance(new Vector2(x, y), p) / w;
                            if (dd > 1f) continue;
                            float sh = Mathf.Sqrt(1f - dd * dd);
                            Color baseC = nerve ? Color.Lerp(new Color(0.70f, 0.50f, 0.42f), new Color(0.36f, 0.03f, 0.04f), Mathf.Clamp01(Mathf.PerlinNoise(x * 1.5f / L.S + 9f, y * 1.5f / L.S) * 1.3f - 0.1f))
                                : Color.Lerp(new Color(0.38f, 0.02f, 0.04f), new Color(0.74f, 0.15f, 0.15f), Mathf.PerlinNoise(x * 2f / L.S, y * 2f / L.S));
                            if (!nerve)
                            {
                                float fib = Mathf.PerlinNoise(x * 6f / L.S + 17f, y * 6f / L.S);
                                baseC = Color.Lerp(baseC, new Color(0.84f, 0.60f, 0.55f), Mathf.Clamp01((fib - 0.66f) * 4f) * 0.6f);   // fascia fibres
                                baseC = Color.Lerp(baseC, new Color(0.22f, 0.0f, 0.02f), Mathf.Clamp01((0.3f - fib) * 4f) * 0.6f);    // clotted blood
                            }
                            Color col = baseC * (0.3f + 0.8f * sh) + Color.white * Mathf.Pow(sh, 14f) * 0.4f;
                            if (sh > 0.7f && RealisticHuman.Hash(x * 3 + 5, y * 7 + 2) > 0.9f) col += new Color(0.35f, 0.3f, 0.3f);   // wet beads
                            col.a = 1f; cur[i] = col; level[i] = 7;   // 7 = strand: never holds a piece on
                        }
                }
            }
            MarkR(c, R * 1.8f + L.S * 0.2f + 2f);
        }

        Vector3 LocalLight()
        {
            Vector2 up2 = transform.InverseTransformVector(Vector2.up); if (up2.sqrMagnitude < 1e-8f) up2 = Vector2.up; up2.Normalize();
            Vector2 rt = new Vector2(up2.y, -up2.x);
            return new Vector3(rt.x * 0.3f + up2.x * 0.75f, rt.y * 0.3f + up2.y * 0.75f, 0.6f).normalized;
        }

        public void Tunnel(Vector2 a, Vector2 b, float radOrig, bool snap)
        {
            if (!Ready) return;
            float step = radOrig * L.S * 0.6f / ppuHi;
            int n = Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(a, b) / Mathf.Max(step, 1e-4f)), 1, 28);
            for (int k = 0; k <= n; k++) Carve(Vector2.Lerp(a, b, k / (float)n), radOrig * Random.Range(0.85f, 1.15f), snap, false, false);
            int sN = Random.Range(0, 2);
            for (int k = 0; k < sN; k++) Strands(ToPx(Vector2.Lerp(a, b, Random.Range(0.2f, 0.8f))), radOrig * L.S, 1);
        }

        // eat the outline too
        bool Outside(int x, int y) { return x < 0 || y < 0 || x >= L.w || y >= L.h || L.skin[y * L.w + x].a == 0; }
        // scan is read-only so it runs on worker threads, apply stays here
        sealed class Rg { public Vector2 c; public float R, zone, maxB, f1, f2, sx, sy; public int x0, x1, y0, y1, bw; public byte[] m; }
        byte[] rgM;
        static readonly int[] rdx = { 1, -1, 0, 0, 1, 1, -1, -1 }, rdy = { 0, 0, 1, -1, 1, -1, 1, -1 };
        Rg RaggedPrep(Vector2 c, float R, float wall)
        {
            var g = new Rg { c = c, R = R, zone = R + wall * 0.85f, maxB = Mathf.Clamp(R * 0.45f, 3f, L.S * 0.7f) };
            g.x0 = Mathf.Max(0, (int)(c.x - g.zone)); g.x1 = Mathf.Min(L.w - 1, (int)(c.x + g.zone));
            g.y0 = Mathf.Max(0, (int)(c.y - g.zone)); g.y1 = Mathf.Min(L.h - 1, (int)(c.y + g.zone));
            if (g.x1 < g.x0 || g.y1 < g.y0) return null;
            g.f1 = 2.2f / Mathf.Max(R, 2f); g.f2 = g.f1 * 4.5f; g.sx = Random.value * 400f; g.sy = Random.value * 400f;
            g.bw = g.x1 - g.x0 + 1; int mn = g.bw * (g.y1 - g.y0 + 1);
            if (rgM == null || rgM.Length < mn) rgM = new byte[mn]; else System.Array.Clear(rgM, 0, mn);
            g.m = rgM;
            return g;
        }
        void RaggedRows(Rg g, int a, int b)
        {
            int W = L.w, H = L.h; var skin = L.skin; float zone = g.zone, R = g.R, z2 = zone * zone;
            for (int y = a; y <= b; y++)
                for (int x = g.x0; x <= g.x1; x++)
                {
                    int i = y * W + x; byte lv = level[i];
                    if (cur[i].a == 0 || (lv != 3 && lv != 4 && lv != 2)) continue;
                    float ex = x - g.c.x, ey = y - g.c.y, d2 = ex * ex + ey * ey;
                    if (d2 > z2) continue;
                    float dd = Mathf.Sqrt(d2);
                    float taper = 1f - Mathf.Clamp01((dd - R * 0.6f) / Mathf.Max(1f, zone - R * 0.6f));
                    float bmax = g.maxB * taper * 1.25f;
                    if (bmax < 1f) continue;   // noise can't push it past 1
                    float n = Mathf.PerlinNoise(x * g.f1 + g.sx, y * g.f1 + g.sy) * 0.7f + Mathf.PerlinNoise(x * g.f2 + g.sy, y * g.f2 + g.sx) * 0.3f;
                    float bite = g.maxB * taper * (0.15f + n * 1.1f);
                    if (bite < 1f) continue;
                    int lim = Mathf.CeilToInt(bite); bool hit = false;
                    for (int r = 1; r <= lim && !hit; r++)
                    {
                        int rd = Mathf.RoundToInt(r * 0.7071f);
                        for (int k = 0; k < 8; k++)
                        {
                            if (k >= 4 && r * 0.7071f > bite) break;
                            if (k < 4 && r > bite) continue;
                            int s = k < 4 ? r : rd, ox = x + rdx[k] * s, oy = y + rdy[k] * s;
                            if (ox < 0 || oy < 0 || ox >= W || oy >= H || skin[oy * W + ox].a == 0) { hit = true; break; }
                        }
                    }
                    if (hit) g.m[(y - g.y0) * g.bw + x - g.x0] = 1;
                }
        }
        void RaggedApply(Rg g)
        {
            bool any = false;
            for (int y = g.y0; y <= g.y1; y++)
                for (int x = g.x0; x <= g.x1; x++)
                    if (g.m[(y - g.y0) * g.bw + x - g.x0] != 0) { int i = y * L.w + x; cur[i] = new Color32(0, 0, 0, 0); level[i] = 6; any = true; }
            if (!any) return;
            for (int pass = 0; pass < 2; pass++)
                for (int y = g.y0; y <= g.y1; y++)
                    for (int x = g.x0; x <= g.x1; x++)
                    {
                        int i = y * L.w + x;
                        if (cur[i].a == 0 || level[i] == 5 || level[i] == 7 || level[i] == 0) continue;
                        int nb = 0;
                        if (x > 0 && cur[i - 1].a > 0) nb++;
                        if (x < L.w - 1 && cur[i + 1].a > 0) nb++;
                        if (y > 0 && cur[i - L.w].a > 0) nb++;
                        if (y < L.h - 1 && cur[i + L.w].a > 0) nb++;
                        if (nb <= 1) { cur[i] = new Color32(0, 0, 0, 0); level[i] = 6; }
                    }
        }

        byte[] wallT;   // stops overlapping carves banding
        bool powderNext, mistNext;
        float[] mhB, midB, mcrB;

        // carving is slow, few ms per frame, one queue for everyone
        readonly Queue<System.Collections.IEnumerator> carveQ = new Queue<System.Collections.IEnumerator>();
        System.Collections.IEnumerator carveCur; float midHold;
        static float freshT = -9f; static int budFrame = -1; static float budT0;
        static float BudgetLeft()
        {
            if (Time.frameCount != budFrame) { budFrame = Time.frameCount; budT0 = Time.realtimeSinceStartup; }
            return (Time.time - freshT < 0.6f ? 0.006f : 0.003f) - (Time.realtimeSinceStartup - budT0);
        }
        static bool OverBudget() { return BudgetLeft() < 0f; }
        static int BudgetMs() { return Mathf.Max(0, (int)(BudgetLeft() * 1000f)); }

        sealed class Cv
        {
            public Vector2 c, down, across; public Vector3 Ld, H;
            public float R, wall, smear, speckR, g, nf, sx, sy, inv, cs, rin, rout, farD, speckD, stip, mist;
            public int x0, x1, y0, y1, bw; public bool vert, head, snap, entry;
        }

        sealed class Bands
        {
            // workers and the main thread both grab bands, so a blocking wait
            // never sits behind other carves' work in the thread pool queue
            int left, next; readonly int n, y0, rows; readonly System.Action<int, int> f; System.Exception err;
            readonly System.Threading.ManualResetEvent ev = new System.Threading.ManualResetEvent(false);
            public Bands(System.Action<int, int> f, int y0, int y1, int per = 16)
            {
                this.f = f; this.y0 = y0; rows = y1 - y0 + 1;
                n = Mathf.Clamp(Mathf.Min(SystemInfo.processorCount, rows / per), 1, 16);
                left = n;
                for (int b = 0; b < n; b++) System.Threading.ThreadPool.QueueUserWorkItem(delegate { while (RunOne()) { } });
            }
            bool RunOne()
            {
                int b = System.Threading.Interlocked.Increment(ref next) - 1;
                if (b >= n) return false;
                int a = y0 + rows * b / n, e = y0 + rows * (b + 1) / n - 1;
                try { f(a, e); } catch (System.Exception x) { err = x; }
                if (System.Threading.Interlocked.Decrement(ref left) == 0) ev.Set();
                return true;
            }
            // true once every band is done; -1 = help out, then wait
            public bool Done(int ms)
            {
                if (ms < 0) while (RunOne()) { }
                if (!ev.WaitOne(ms)) return false;
                ev.Close();
                if (err != null) throw new System.Exception("carve worker failed", err);
                return true;
            }
        }

        // set while a wound job is collecting its pixel rows to run later
        static List<KeyValuePair<System.Action<int, int>, Vector2Int>> deferRows;
        static void ParallelRows(System.Action<int, int> f, int y0, int y1)
        {
            if (deferRows != null) { deferRows.Add(new KeyValuePair<System.Action<int, int>, Vector2Int>(f, new Vector2Int(y0, y1))); return; }
            if (y1 - y0 < 32 || !Threads()) { f(y0, y1); return; }
            new Bands(f, y0, y1).Done(-1);
        }

        // if unity noise won't run off-thread, fall back to slices
        static int thr;
        static bool Threads()
        {
            if (thr != 0) return thr > 0;
            thr = -1;
            try
            {
                float want = Mathf.PerlinNoise(3.7f, 1.3f), got = -1f;
                var t = new Bands(delegate(int a, int b) { got = Mathf.PerlinNoise(3.7f, 1.3f); }, 0, 0);
                if (t.Done(1000) && got == want) thr = 1;
            }
            catch (System.Exception e) { Debug.LogWarning("[RealisticGore] no worker threads, carving on the main thread: " + e.Message); }
            return thr > 0;
        }
        // impact wound as a queued job: rows run on workers over a few frames, main thread never waits
        System.Collections.IEnumerator WoundJob(Vector2 world, float r, int depth)
        {
            var rows = new List<KeyValuePair<System.Action<int, int>, Vector2Int>>();
            deferRows = rows;
            try { Wound(world, r, depth, false); } finally { deferRows = null; }
            foreach (var it in rows)
            {
                int a = it.Value.x, b = it.Value.y;
                if (Threads()) { var bd = new Bands(it.Key, a, b, 16); while (!bd.Done(0)) yield return null; }
                else it.Key(a, b);
                Mark(0, a, L.w - 1, b);
            }
        }
        public bool Carving { get { return carveCur != null || carveQ.Count > 0; } }
        public void PumpCarves()
        {
            while (true)
            {
                if (carveCur == null) { if (carveQ.Count == 0) return; carveCur = carveQ.Dequeue(); }
                bool more;
                try { more = carveCur.MoveNext(); }
                catch (System.Exception e) { Debug.LogWarning("[RealisticGore] carve: " + e); more = false; }
                if (more) return;            // out of time this frame
                carveCur = null;
                if (OverBudget()) return;
            }
        }

        public void Carve(Vector2 world, float radOrig, bool snap, bool entry = false, bool fx = true)
        {
            if (!Ready) return;
            freshT = Time.time;
            if (!noMirror) Mirror(world, radOrig, snap, entry);
            carveQ.Enqueue(CarveJob(world, radOrig, snap, entry, fx, powderNext ? Random.Range(0.6f, 1f) : 0f, mistNext ? Random.Range(0.5f, 1f) : 0f));
            if (!OverBudget()) { double t0 = Now(); PumpCarves(); Spend(t0); }
        }

        // one convex shape, max 10 points: sprite outlines split into dozens of physics shapes
        internal static void Hullify(PolygonCollider2D poly)
        {
            if (!poly || poly.pathCount == 0) return;
            var pts = new List<Vector2>();
            for (int i = 0; i < poly.pathCount; i++) pts.AddRange(poly.GetPath(i));
            if (pts.Count < 3) return;
            pts.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            var h = new List<Vector2>();
            for (int pass = 0; pass < 2; pass++)
            {
                int start = h.Count;
                for (int k = 0; k < pts.Count; k++)
                {
                    var p = pass == 0 ? pts[k] : pts[pts.Count - 1 - k];
                    while (h.Count >= start + 2 && Cross(h[h.Count - 2], h[h.Count - 1], p) <= 0f) h.RemoveAt(h.Count - 1);
                    h.Add(p);
                }
                h.RemoveAt(h.Count - 1);
            }
            while (h.Count > 10)
            {
                int best = 0; float ba = float.MaxValue;
                for (int k = 0; k < h.Count; k++)
                {
                    float a = Mathf.Abs(Cross(h[(k + h.Count - 1) % h.Count], h[k], h[(k + 1) % h.Count]));
                    if (a < ba) { ba = a; best = k; }
                }
                h.RemoveAt(best);
            }
            if (h.Count < 3) return;
            poly.pathCount = 1; poly.SetPath(0, h.ToArray());
        }
        static float Cross(Vector2 o, Vector2 a, Vector2 b) { return (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x); }
        // mod ms already spent this frame; extras wait when it's high
        static double frameMs; static int frameF = -1;
        internal static bool Over(double ms) { return Time.frameCount == frameF && frameMs > ms; }
        internal static double Now() { return Time.realtimeSinceStartupAsDouble; }
        static void Spend(double t0)
        {
            if (Time.frameCount != frameF) { frameF = Time.frameCount; frameMs = 0; }
            frameMs += (Now() - t0) * 1000.0;
        }

        System.Collections.IEnumerator CarveJob(Vector2 world, float radOrig, bool snap, bool entry, bool fx, float stip, float mist)
        {
            if (!Ready) yield break;

            if (wallT == null || wallT.Length != cur.Length) { wallT = new byte[cur.Length]; for (int q = 0; q < wallT.Length; q++) wallT[q] = 255; }
            var cv = new Cv();
            Vector2 c = cv.c = ToPx(world);
            float S = L.S;
            float R = cv.R = Mathf.Max(1.5f, radOrig * S);
            float wall = cv.wall = entry ? Mathf.Max(0.3f * S, R * 0.6f) : Mathf.Max(0.4f * S, R * 0.75f);
            cv.smear = Mathf.Max(0.3f * S, R * 0.42f);
            // soot near entry, mist near exit
            cv.speckR = (stip > 0f ? R * 3.2f + 1.2f * S : R * 2.4f + S) * (stip > 0f || mist > 0f ? 1f : 0f);
            cv.g = Mathf.Max(1.5f, S * (stip > 0f ? 0.13f : 0.09f));
            float reach = Mathf.Max(R * 1.35f + wall + cv.smear * 2.5f, R + wall + 2f) + cv.speckR;
            cv.x0 = Mathf.Max(0, (int)(c.x - reach)); cv.x1 = Mathf.Min(L.w - 1, (int)(c.x + reach));
            cv.y0 = Mathf.Max(0, (int)(c.y - reach)); cv.y1 = Mathf.Min(L.h - 1, (int)(c.y + reach));
            Mark(cv.x0, cv.y0, cv.x1, cv.y1);
            cv.Ld = LocalLight(); cv.H = (cv.Ld + Vector3.forward).normalized;
            cv.down = PxDown();
            cv.vert = Vert;
            cv.across = cv.vert ? new Vector2(1f, 0f) : new Vector2(0f, 1f);
            cv.nf = 1.5f / Mathf.Max(R, 1f); cv.sx = Random.value * 500f; cv.sy = Random.value * 500f;
            cv.inv = 1f / S;
            cv.head = L.kind == 0 && L.uw > 0f;
            cv.bw = cv.x1 - cv.x0 + 1; int bh = cv.y1 - cv.y0 + 1;
            cv.cs = Mathf.Max(2.5f, Mathf.Min(R * 0.4f, S));
            cv.snap = snap; cv.entry = entry; cv.stip = stip; cv.mist = mist;
            cv.rin = Mathf.Max(0f, R * 0.5f - 2f); cv.rout = R * 1.5f + wall + 2f;
            cv.farD = R + wall + cv.smear * 2.5f + R * 0.48f; cv.speckD = cv.speckR > 0f ? R + wall + cv.speckR : -1f;
            if (cv.bw > 0 && bh > 0)
            {
                if (mhB == null || mhB.Length < cv.bw * bh) { mhB = new float[cv.bw * bh]; midB = new float[cv.bw * bh]; mcrB = new float[cv.bw * bh]; }
                if (Threads())
                {
                    // per-pixel only, safe to thread
                    var p = new Bands(delegate(int a, int b) { MeatRows(cv, a, b); }, cv.y0, cv.y1);
                    while (!p.Done(0)) yield return null;   // poll, never block the frame
                    p = new Bands(delegate(int a, int b) { PaintRows(cv, a, b); }, cv.y0, cv.y1);
                    while (!p.Done(0)) yield return null;   // poll, never block the frame
                }
                else
                {
                    for (int y = cv.y0; y <= cv.y1; y++) { if (OverBudget()) yield return null; MeatRows(cv, y, y); }
                    for (int y = cv.y0; y <= cv.y1; y++) { if (OverBudget()) yield return null; PaintRows(cv, y, y); }
                }
            }
            if (OverBudget()) yield return null;

            if (GoreSettings.RaggedEdges)
            {
                var rg = RaggedPrep(c, R, wall);
                if (rg != null)
                {
                    if (rg.y1 - rg.y0 >= 8 && Threads())
                    {
                        var pr = new Bands(delegate(int a, int b) { RaggedRows(rg, a, b); }, rg.y0, rg.y1, 4);

                        while (!pr.Done(0)) yield return null;
                    }
                    else RaggedRows(rg, rg.y0, rg.y1);
                    RaggedApply(rg);
                }
            }

            if (fx || entry) BruiseAt(c, (R + wall) * 1.3f, 0.45f);

            if (fx)
            {
                Strands(c, R, R > 6f ? Random.Range(0, 3) : (Random.value < 0.3f ? 1 : 0));
                Runs(c, R + wall, R > 6f ? Random.Range(1, 4) : Random.Range(0, 2));
            }
            Mark(cv.x0, cv.y0, cv.x1, cv.y1); needSplit = true;
        }

        void MeatRows(Cv cv, int ya, int yb)
        {
            Vector2 c = cv.c; float rin = cv.rin, rout = cv.rout, cs = cv.cs, sx = cv.sx, sy = cv.sy;
            int x0 = cv.x0, x1 = cv.x1, y0 = cv.y0, bw = cv.bw; bool vert = cv.vert;
            for (int y = ya; y <= yb; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    int k = (y - y0) * bw + (x - x0);
                    float dx = x - c.x, dy = y - c.y, dd2 = dx * dx + dy * dy;
                    if (dd2 < rin * rin || dd2 > rout * rout) { mhB[k] = float.NaN; continue; }
                    int pi = y * L.w + x;
                    if (cur[pi].a == 0) { mhB[k] = float.NaN; continue; }
                    float hx = vert ? x : y, hy = vert ? y : x, id, crev;
                    mhB[k] = MeatH(hx / cs + sx, hy / (cs * 1.5f) + sy, out id, out crev);
                    midB[k] = id; mcrB[k] = crev;
                }
            }
        }

        void PaintRows(Cv cv, int ya, int yb)
        {
            Vector2 c = cv.c, down = cv.down, across = cv.across; Vector3 Ld = cv.Ld, H = cv.H;
            float R = cv.R, wall = cv.wall, smear = cv.smear, speckR = cv.speckR, g = cv.g, nf = cv.nf, sx = cv.sx, sy = cv.sy, inv = cv.inv, cs = cv.cs;
            float farD = cv.farD, speckD = cv.speckD, stip = cv.stip, mist = cv.mist, S = L.S;
            int x0 = cv.x0, x1 = cv.x1, y0 = cv.y0, y1 = cv.y1, bw = cv.bw; bool vert = cv.vert, head = cv.head, snap = cv.snap, entry = cv.entry;
            for (int y = ya; y <= yb; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    int i = y * L.w + x;
                    if (cur[i].a == 0 || level[i] == 6) continue;
                    Vector2 off = new Vector2(x - c.x, y - c.y);
                    float dist = off.magnitude;
                    if (dist > farD && dist > speckD) continue;
                    float d = dist + (Mathf.PerlinNoise(x * nf + sx, y * nf + sy) - 0.5f) * R * 0.7f
                                   + (Mathf.PerlinNoise(x * nf * 4f + sy, y * nf * 4f + sx) - 0.5f) * R * 0.25f;
                    Vector2 dir = dist > 1e-3f ? off / dist : Vector2.zero;
                    float low = Mathf.Clamp01(Vector2.Dot(dir, down));
                    bool bone = IsBone(L.bone[i]);
                    float ax = (vert ? x : y) * inv, al = (vert ? y : x) * inv, ox = x * inv, oy = y * inv;   // across / along the limb, in original px
                    Color col; byte lv;
                    bool brainPx = head && InBrain(x, y);
                    if (d < R)
                    {
                        if (head && (bone || brainPx))
                        {
                            // skull shatters
                            float jag = Mathf.PerlinNoise(ox * 1.8f + sy, oy * 1.8f + sx);
                            if (!snap && d > R * (0.84f + jag * 0.14f) && jag > 0.42f)
                            {
                                col = SkullShade(x, y, Ld, H, Mathf.Clamp01((d - R * 0.84f) / (R * 0.16f)), dir, ox, oy);
                                lv = 5;
                                goto put;
                            }
                            if (bone) cutBone = true;
                            cur[i] = new Color32(0, 0, 0, 0); level[i] = 6; continue;
                        }
                        if (bone && (!snap || d > R * 0.55f))
                        {
                            col = BoneShade(x, y, vert, Ld, H, snap && d < R * 0.78f);
                            lv = 5;
                        }
                        else { if (bone) cutBone = true; cur[i] = new Color32(0, 0, 0, 0); level[i] = 6; continue; }
                    }
                    else if (d < R + wall)
                    {
                        if (level[i] >= 5) continue;
                        float t = (d - R) / wall;                                                     // 0 hole edge .. 1 skin
                        byte wtb = (byte)(Mathf.Clamp01(t) * 254f);
                        if (level[i] == 4 && wallT[i] < wtb) continue;
                        wallT[i] = wtb;
                        float tw = t + (Mathf.PerlinNoise(ox * 0.8f + sx, oy * 0.8f + sy) - 0.5f) * 0.2f;
                        float slope = Mathf.Lerp(1.7f, 0.25f, t);
                        Vector2 tilt = -dir * slope;
                        Color baseC; float gloss, rough, dome = 1f;
                        if (head && tw <= 0.84f && (brainPx ? tw > 0.62f : bone))
                        {
                            // scalp, skull, brain
                            float tb = brainPx ? (tw - 0.62f) / 0.22f : Mathf.Clamp01(tw / 0.84f);
                            col = SkullShade(x, y, Ld, H, tb, dir, ox, oy) * Mathf.Lerp(0.6f, 1f, Mathf.Sqrt(t));
                            lv = 4;
                            goto put;
                        }
                        if (bone && !head)
                        {
                            col = BoneShade(x, y, vert, Ld, H, false) * Mathf.Lerp(0.5f, 1f, Mathf.Sqrt(t));
                            lv = 4;
                            goto put;
                        }
                        if (brainPx && tw <= 0.62f)
                        {
                            // cortex
                            float g0 = Gyri(ox, oy), gxp = Gyri(ox + 0.25f, oy), gyp = Gyri(ox, oy + 0.25f);
                            baseC = Color.Lerp(new Color(0.40f, 0.07f, 0.09f), new Color(0.86f, 0.70f, 0.68f), Mathf.Pow(g0, 0.65f));
                            float ves = Mathf.Abs(Mathf.PerlinNoise(ox * 0.6f + 7f, oy * 0.6f + 2f) - 0.5f);
                            if (ves < 0.02f) baseC = Color.Lerp(baseC, new Color(0.55f, 0.04f, 0.07f), 0.7f);
                            tilt += new Vector2(g0 - gxp, g0 - gyp) * 3.2f;
                            dome = 0.9f + g0 * 0.15f;
                            gloss = 1.2f; rough = 42f;
                        }
                        else if (tw > 0.86f)
                        {
                            float k = Mathf.SmoothStep(0f, 1f, (tw - 0.86f) / 0.14f);
                            Color raw = entry ? new Color(0.30f, 0.06f, 0.05f) : new Color(0.62f, 0.12f, 0.11f);
                            baseC = Color.Lerp(raw, (Color)L.skin[i], k);
                            tilt = dir * (k - 0.35f) * 1.6f;
                            gloss = 0.35f; rough = 40f;
                        }
                        else
                        {
                            // torn meat
                            float hx = vert ? x : y, hy = vert ? y : x;
                            float mx = hx / cs + sx, my = hy / (cs * 1.5f) + sy;
                            int kk = (y - y0) * bw + (x - x0);
                            float h0 = mhB[kk], id = midB[kk], crev = mcrB[kk];
                            if (float.IsNaN(h0)) { float id2, cr2; h0 = MeatH(mx, my, out id2, out cr2); id = id2; crev = cr2; }
                            int kA = vert ? (x < x1 ? kk + 1 : -1) : (y < y1 ? kk + bw : -1);
                            int kB = vert ? (y < y1 ? kk + bw : -1) : (x < x1 ? kk + 1 : -1);
                            float hA = kA >= 0 && !float.IsNaN(mhB[kA]) ? mhB[kA] : h0;
                            float hB = kB >= 0 && !float.IsNaN(mhB[kB]) ? mhB[kB] : h0;
                            Vector2 along = vert ? new Vector2(0f, 1f) : new Vector2(1f, 0f);
                            tilt += (across * (h0 - hA) + along * (h0 - hB)) * 0.66f * cs;
                            float grain = Mathf.PerlinNoise(hx * 0.7f + sy, hy * 0.06f + sx);
                            float var = Mathf.PerlinNoise(mx * 0.35f + 5f, my * 0.35f + 9f);
                            float grit = RealisticHuman.Hash(x * 3 + 7, y * 3 + 1);
                            Color m = Color.Lerp(new Color(0.26f, 0.02f, 0.045f), new Color(0.58f, 0.05f, 0.07f), Mathf.Clamp01(h0));
                            m = Color.Lerp(m, new Color(0.84f, 0.08f, 0.07f), Mathf.Clamp01((h0 - 0.55f) * 2.2f) * var);
                            m *= 0.82f + grain * 0.36f;
                            if (var > 0.55f) m *= 0.8f + grit * 0.4f;
                            if (tw > 0.7f && id > 0.72f)
                            {
                                Color fatC = Color.Lerp(new Color(0.86f, 0.70f, 0.42f), new Color(0.68f, 0.22f, 0.16f), Mathf.PerlinNoise(mx * 1.3f, my * 1.3f));
                                m = Color.Lerp(m, fatC, Mathf.Clamp01((tw - 0.7f) / 0.08f) * Mathf.Clamp01(h0));
                            }
                            baseC = Color.Lerp(m, new Color(0.06f, 0.0f, 0.01f), crev * 0.7f);
                            dome = 1f - crev * 0.4f;
                            gloss = 1.4f; rough = 80f;
                        }
                        tilt += new Vector2(RealisticHuman.Hash(x * 7 + 3, y * 5 + 1) - 0.5f, RealisticHuman.Hash(x * 5 + 9, y * 7 + 4) - 0.5f) * 0.55f;
                        Vector3 n = new Vector3(tilt.x, tilt.y, 1f).normalized;
                        float diff = Mathf.Max(0f, Vector3.Dot(n, Ld));
                        float ao = Mathf.Lerp(0.18f, 1f, Mathf.Sqrt(t));
                        Color lit = baseC * (0.3f + 0.85f * diff) * ao * dome;
                        lit += new Color(baseC.r * 0.2f, baseC.g * 0.03f, baseC.b * 0.03f) * (1f - diff) * ao;
                        // blood film, pools low
                        float film = Mathf.Clamp01((1f - t) * 0.5f + low * (1f - t) * 0.7f + (Mathf.PerlinNoise(ox * 1.2f + sy, oy * 1.2f + sx) - 0.45f) * 0.5f);
                        Color bc = Color.Lerp(new Color(0.72f, 0.04f, 0.05f), new Color(0.24f, 0.0f, 0.02f), Mathf.PerlinNoise(ox * 2f + sx, oy * 2f));
                        lit = Color.Lerp(lit, bc * (0.5f + 0.6f * diff) * ao, film * 0.6f);
                        float spec = Mathf.Pow(Mathf.Max(0f, Vector3.Dot(n, H)), rough + film * 60f) * (gloss + film * 0.9f);
                        col = lit + new Color(1f, 0.92f, 0.9f) * spec * 0.7f;
                        lv = 4;
                    }
                    else
                    {
                        float sm = smear * (1f + 1.5f * low);
                        if (level[i] > 1) continue;
                        float soot = stip > 0f ? Mathf.Clamp01(1f - (dist - R - wall) / (R * 1.6f + 0.5f * S)) * stip : 0f;
                        if (d >= R + wall + sm)
                        {
                            if (speckR <= 0f || dist > R + wall + speckR) continue;
                            float far = Mathf.Clamp01((dist - R - wall) / speckR);
                            int gx = Mathf.FloorToInt(x / g), gy = Mathf.FloorToInt(y / g);
                            float hs = RealisticHuman.Hash(gx * 13 + 7, gy * 29 + 3);
                            float dens = stip > 0f ? stip * (1f - far * far) * 0.55f : mist * (1f - far) * (1f - far) * 0.4f;
                            Color basePx = cur[i];
                            if (soot > 0f) basePx = Color.Lerp(basePx, new Color(0.14f, 0.12f, 0.11f), soot * soot * 0.7f * Mathf.PerlinNoise(ox * 1.4f + sx, oy * 1.4f + sy) * 1.4f);
                            if (hs < 1f - dens)
                            {
                                if (soot <= 0.01f) continue;
                                col = basePx; lv = 1; goto put;
                            }
                            float cxp = (gx + 0.2f + RealisticHuman.Hash(gx * 5 + 1, gy * 3 + 9) * 0.6f) * g;
                            float cyp = (gy + 0.2f + RealisticHuman.Hash(gx * 7 + 4, gy * 11 + 2) * 0.6f) * g;
                            float rad = g * (0.18f + RealisticHuman.Hash(gx * 3 + 8, gy * 17 + 5) * 0.3f) * (1f - far * 0.5f);
                            float dd = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cxp, cyp));
                            if (stip > 0f)
                            {
                                if (dd < rad) col = new Color(0.08f, 0.06f, 0.05f);
                                else if (dd < rad * 2.1f) col = Color.Lerp(basePx, new Color(0.46f, 0.12f, 0.08f), 0.75f * (1f - (dd - rad) / (rad * 1.1f)));
                                else if (soot > 0.01f) col = basePx; else continue;
                            }
                            else
                            {
                                if (dd >= rad * 1.3f) continue;
                                float e2 = Mathf.Clamp01((rad * 1.3f - dd) / (rad * 0.5f));
                                col = Color.Lerp(basePx, Color.Lerp(new Color(0.55f, 0.02f, 0.04f), new Color(0.30f, 0.0f, 0.02f), hs), e2 * 0.95f);
                                if (dd < rad * 0.35f && hs > 0.8f) col += new Color(0.3f, 0.25f, 0.25f);
                            }
                            lv = 1; goto put;
                        }
                        float k = 1f - (d - R - wall) / sm;
                        float cov = Mathf.PerlinNoise(ox * 0.7f + sx, oy * 0.7f + sy);
                        float a = Mathf.Clamp01((k * 1.4f - (1f - cov) * 0.6f) * 2.5f);
                        if (a <= 0f) continue;
                        Color bl = Color.Lerp(new Color(0.62f, 0.05f, 0.07f), new Color(0.24f, 0.0f, 0.02f), Mathf.Clamp01(a * 0.7f + cov * 0.5f));
                        col = Color.Lerp(cur[i], bl, Mathf.Clamp01(a * 1.15f) * 0.9f);
                        if (soot > 0f) col = Color.Lerp(col, new Color(0.10f, 0.08f, 0.07f), soot * 0.45f);
                        float gl = RealisticHuman.Hash(x * 5 + 2, y * 3 + 8) * Mathf.PerlinNoise(x * 0.35f + sy, y * 0.35f + sx);
                        if (gl > 0.55f) col += new Color(1f, 0.9f, 0.88f) * (gl - 0.55f) * 1.6f * a;
                        lv = 1;
                    }
                put:
                    if (lv < level[i]) continue;
                    col.a = L.skin[i].a / 255f;
                    cur[i] = col; level[i] = lv;
                }
            }
        }

        // hole at a joint goes through both parts
        static bool noMirror; static readonly List<Crater> joinedTmp = new List<Crater>();
        void Mirror(Vector2 world, float radOrig, bool snap, bool entry)
        {
            var ss = GetComponent<SoftSkin>(); if (!ss) return;
            joinedTmp.Clear(); ss.Joined(joinedTmp);
            if (joinedTmp.Count == 0) return;
            var list = joinedTmp.ToArray();
            noMirror = true;
            try { foreach (var o in list) if (o && o.Ready && o.Covers(world)) o.Carve(world, radOrig, snap, entry, false); }
            finally { noMirror = false; }
        }
        public bool Covers(Vector2 world) { return Ready && InBody(world); }

        bool InBody(Vector2 world)
        {
            var px = ToPx(world); int x = (int)px.x, y = (int)px.y;
            return x >= 0 && y >= 0 && x < L.w && y < L.h && L.skin[y * L.w + x].a > 128;
        }

        void Spray(Vector2 at, Vector2 dir, float dmg, int n)
        {
            if (!GoreSettings.Droplets || GoreSettings.DropletMult <= 0f) return;
            n = Mathf.Clamp((int)(n * Mathf.Clamp(dmg / 30f, 0.5f, 2f) * GoreSettings.DropletMult), 1, 80);
            for (int k = 0; k < n; k++)
                Droplet.Create(at, (dir + Random.insideUnitCircle * 0.45f).normalized * Random.Range(1.5f, 6f) * Mathf.Clamp(dmg / 25f, 0.6f, 2f), sr);
        }
    }
}
