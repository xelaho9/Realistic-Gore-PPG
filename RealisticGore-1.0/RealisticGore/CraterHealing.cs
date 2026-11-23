using UnityEngine;

namespace Mod
{
    public partial class Crater
    {
        // healing, ~10 min at 1x
        Color32[] hFrom; ushort[] hT; byte[] hKind, scarS, scarB, hFill, lost;
        float healTick = -1f, ringAcc, brokenFor, lungFor; int healRow, healPass; bool healBusy = true, healFound, healWas;
        int hx0 = int.MaxValue, hy0 = int.MaxValue, hx1 = -1, hy1 = -1;
        const int HealStride = 4;
        static float HealTotal { get { return 600f / Mathf.Max(0.05f, GoreSettings.HealSpeed); } }
        void MarkLost(int i) { if (lost == null || lost.Length != cur.Length) lost = new byte[cur.Length]; lost[i] = 1; }

        void RunHeal(float dt)
        {
            if (!GoreSettings.Heal || !limb || isChunk || limb.IsAndroid || !limb.IsConsideredAlive) return;
            float H = HealTotal;
            HealBody(dt, H);
            if (phys.OnFire || phys.Temperature > 150f) return;
            if (healTick < 0f) healTick = Random.Range(0f, 0.5f);
            healTick -= dt; if (healTick > 0f) return;
            healTick = Random.Range(0.45f, 0.55f);
            int N = cur.Length, W = L.w;
            if (hT == null || hT.Length != N)
            {
                hFrom = new Color32[N]; hT = new ushort[N]; hKind = new byte[N]; scarS = new byte[N]; scarB = new byte[N]; hFill = new byte[N];
                if (lost == null || lost.Length != N) lost = new byte[N];
            }
            float step = 0.5f * HealStride;
            bool ch = false, bruising = bruises.Count > 0, dism = limb.IsDismembered;
            for (int y = healRow; y < L.h; y += HealStride)
            {
                int xs = healBusy ? 1 : 4;
                for (int x = (healBusy ? 0 : (y / HealStride) & 3); x < W; x += xs)
                {
                    int i = y * W + x;
                    if (lost[i] != 0) continue;
                    Color32 sk = L.skin[i];
                    if (sk.a == 0)
                    {
                        if (level[i] >= 2 && cur[i].a > 0) { healFound = true; if (!healBusy) { healBusy = true; continue; } cur[i] = new Color32(0, 0, 0, 0); level[i] = 6; ch = true; }
                        continue;
                    }
                    byte lv = level[i];
                    if (lv == 6)
                    {
                        if (dism) continue;   // stumps don't grow back
                        healFound = true; if (!healBusy) { healBusy = true; continue; }
                        if (x < hx0) hx0 = x; if (x > hx1) hx1 = x; if (y < hy0) hy0 = y; if (y > hy1) hy1 = y;
                        continue;
                    }
                    if (hKind[i] == 0)
                    {
                        if (lv == 0 && burn[i] <= 0f)
                        {
                            if (scarS[i] > 0) { if (GoreSettings.Scars && Same(cur[i], hFrom[i])) continue; }
                            else if (Same(cur[i], sk)) continue;
                            if (bruising && brS != null && brS[i] > 0) continue;
                        }
                        healFound = true; if (!healBusy) { healBusy = true; continue; }
                        Begin(i);
                    }
                    else
                    {
                        healFound = true; if (!healBusy) { healBusy = true; continue; }
                        if (!Same(cur[i], Eval(i, hT[i] / 65535f))) Begin(i);
                    }
                    int k = hKind[i];
                    float dur = H * (k == 3 ? 0.75f : k == 2 ? 0.6f : 0.25f);
                    float t = Mathf.Min(1f, hT[i] / 65535f + step / dur);
                    if (t >= 1f) { Finish(i); ch = true; continue; }
                    hT[i] = (ushort)(t * 65535f);
                    float tq = hT[i] / 65535f;
                    if (k == 3) { if (tq >= 0.7f) { level[i] = 1; burn[i] = 0f; } else if (tq >= 0.35f) { level[i] = 2; burn[i] = 0f; } }
                    else if (k == 2 && tq >= 0.5f) { level[i] = 1; burn[i] = 0f; }
                    cur[i] = Eval(i, tq); ch = true;
                }
            }
            healRow++;
            if (healRow >= HealStride)
            {
                healRow = 0;
                if (!healFound && healBusy) { healBusy = false; if (healWas) HealDone(); healWas = false; }
                else if (healFound) healWas = true;
                healFound = false;
            }
            // close holes from the edge in
            if (hx1 >= 0 && !dism)
            {
                ringAcc += 0.5f * 60f / (0.25f * H);
                int rings = Mathf.Min(6, (int)ringAcc); ringAcc -= rings;
                for (int r = 0; r < rings && hx1 >= 0; r++) if (Ring()) ch = true;
                if (hx1 < 0)
                {
                    bleeders.Clear(); spurts.Clear();
                    var circ = limb.CirculationBehaviour; if (circ) circ.HealBleeding();
                }
            }
            if (ch) dirty = true;
        }

        bool Ring()
        {
            int W = L.w, Hh = L.h;
            healPass++; byte pass = (byte)(1 + healPass % 250);
            int nx0 = int.MaxValue, ny0 = int.MaxValue, nx1 = -1, ny1 = -1; bool ch = false;
            for (int y = hy0; y <= hy1; y++)
                for (int x = hx0; x <= hx1; x++)
                {
                    int i = y * W + x;
                    if (level[i] != 6 || L.skin[i].a == 0 || lost[i] != 0) continue;
                    bool edge = false;
                    if (x > 0) edge = Solid(i - 1, pass);
                    if (!edge && x < W - 1) edge = Solid(i + 1, pass);
                    if (!edge && y > 0) edge = Solid(i - W, pass);
                    if (!edge && y < Hh - 1) edge = Solid(i + W, pass);
                    if (!edge)
                    {
                        if (x < nx0) nx0 = x; if (x > nx1) nx1 = x; if (y < ny0) ny0 = y; if (y > ny1) ny1 = y;
                        continue;
                    }
                    Color32 g = Gran(i); g.a = L.skin[i].a;
                    cur[i] = g; hFrom[i] = g; level[i] = 3; hKind[i] = 3; hT[i] = 0; hFill[i] = pass; burn[i] = 0f;
                    if (GoreSettings.Scars) scarS[i] = 255;
                    ch = true;
                }
            hx0 = nx0; hy0 = ny0; hx1 = nx1; hy1 = ny1;
            return ch;
        }

        bool Solid(int n, byte pass) { return level[n] != 6 && cur[n].a > 0 && hFill[n] != pass; }

        void Begin(int i)
        {
            byte lv = level[i]; float b = burn[i];
            int kind = ((lv >= 3 && lv != 6) || b >= 2f) ? 3 : (lv == 2 || b >= 1f) ? 2 : 1;
            if (kind >= 2 && GoreSettings.Scars)
            {
                byte s = (byte)(kind == 3 ? 255 : 150);
                if (s > scarS[i]) scarS[i] = s;
                if (b >= 1f) scarB[i] = 1;
            }
            hKind[i] = (byte)kind; hT[i] = 0; hFrom[i] = cur[i];
        }

        void Finish(int i)
        {
            Color32 t = Target(i);
            cur[i] = t; hFrom[i] = t;
            level[i] = 0; burn[i] = 0f; hKind[i] = 0; hT[i] = 0;
            if (!GoreSettings.Scars) scarS[i] = 0;
            if (brS != null && i < brS.Length) brS[i] = 0;
            if (runCov != null && i < runCov.Length) runCov[i] = 0;
            if (wallT != null && i < wallT.Length) wallT[i] = 255;
        }

        static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        Color32 Eval(int i, float t)
        {
            int k = hKind[i]; Color c;
            if (k == 3)
            {
                if (t < 0.35f) c = Color.Lerp(hFrom[i], Gran(i), Ease(t / 0.35f));
                else if (t < 0.7f) c = Color.Lerp(Gran(i), Pink(i), Ease((t - 0.35f) / 0.35f));
                else c = Color.Lerp(Pink(i), Target(i), Ease((t - 0.7f) / 0.3f));
            }
            else if (k == 2)
            {
                if (t < 0.5f) c = Color.Lerp(hFrom[i], Pink(i), Ease(t / 0.5f));
                else c = Color.Lerp(Pink(i), Target(i), Ease((t - 0.5f) / 0.5f));
            }
            else c = Color.Lerp(hFrom[i], Target(i), Ease(t));
            Color32 o = c; o.a = L.skin[i].a; return o;
        }

        Color Gran(int i)
        {
            int x = i % L.w, y = i / L.w; float f = 1.5f / L.S;
            float n = 0.6f * Mathf.PerlinNoise(x * f + 31f, y * f + 7f) + 0.4f * RealisticHuman.Hash(x, y);
            return Color.Lerp(new Color(0.55f, 0.10f, 0.10f), new Color(0.80f, 0.28f, 0.26f), n);
        }

        static float Lum(Color c) { return c.r * 0.3f + c.g * 0.59f + c.b * 0.11f; }
        static Color ToLum(Color c, float lum) { float l = Lum(c); if (l <= 0.001f) return c; Color o = c * (lum / l); o.a = 1f; return o; }

        // new skin
        Color Pink(int i)
        {
            Color sk = L.skin[i];
            return Color.Lerp(ToLum(new Color(0.92f, 0.56f, 0.54f), Lum(sk)), sk, 0.25f);
        }

        Color32 Target(int i)
        {
            Color32 sk = L.skin[i];
            if (!GoreSettings.Scars || scarS == null || scarS[i] == 0) return sk;
            float s = scarS[i] / 255f; Color c = sk; float lum = Lum(c);
            int x = i % L.w, y = i / L.w;
            if (scarB[i] != 0)
            {
                float f = 0.9f / L.S, m = Mathf.PerlinNoise(x * f + 11f, y * f + 53f);
                Color mot = Color.Lerp(ToLum(new Color(0.98f, 0.80f, 0.76f), lum * 1.08f), ToLum(new Color(0.66f, 0.40f, 0.36f), lum * 0.82f), Mathf.Clamp01((m - 0.3f) * 1.6f));
                c = Color.Lerp(c, mot, 0.62f * s);
            }
            else c = Color.Lerp(c, ToLum(new Color(0.95f, 0.76f, 0.72f), lum * 1.06f), 0.42f * s);
            c *= 0.97f + 0.06f * RealisticHuman.Hash(x, y);
            Color32 o = c; o.a = sk.a; return o;
        }

        static bool Same(Color32 a, Color32 b)
        {
            return Mathf.Abs(a.r - b.r) <= 4 && Mathf.Abs(a.g - b.g) <= 4 && Mathf.Abs(a.b - b.b) <= 4 && Mathf.Abs(a.a - b.a) <= 4;
        }

        void HealBody(float dt, float H)
        {
            if (limb.Health < limb.InitialHealth) limb.Health = Mathf.Min(limb.InitialHealth, limb.Health + limb.InitialHealth * dt / (0.8f * H));
            if (limb.Broken) { brokenFor += dt; if (brokenFor > 0.6f * H) { limb.HealBone(); brokenFor = 0f; fractured = false; } } else brokenFor = 0f;
            if (limb.LungsPunctured) { lungFor += dt; if (lungFor > 0.5f * H) { limb.LungsPunctured = false; lungFor = 0f; } } else lungFor = 0f;
            var circ = limb.CirculationBehaviour;
            if (circ && circ.InternalBleedingIntensity > 0f) circ.InternalBleedingIntensity = Mathf.Max(0f, circ.InternalBleedingIntensity - dt / (0.3f * H));
            if (!phys.OnFire && phys.BurnProgress > 0f) phys.BurnProgress = Mathf.Max(0f, phys.BurnProgress - dt / (0.8f * H));
            if (rg && !phys.OnFire) rg.HealBurn(dt * 3f / (0.8f * H));
        }

        // fully healed, clean up
        void HealDone()
        {
            foreach (var t in tags) if (t.lr) Destroy(t.lr.gameObject);
            tags.Clear();
            while (wounds.Count > 0) { var w = wounds.Dequeue(); if (w) Destroy(w); }
            if (!limb.IsDismembered)
                for (int c = transform.childCount - 1; c >= 0; c--)
                {
                    var tr = transform.GetChild(c);
                    if (tr.name == "BoneShard") Destroy(tr.gameObject);
                }
            bleeders.Clear(); spurts.Clear();
            var circ = limb.CirculationBehaviour; if (circ) circ.HealBleeding();
            limb.BruiseCount = 0;
        }

        void OnDestroy() { if (tex) Destroy(tex); }
    }
}
