using System.Collections.Generic;
using UnityEngine;

namespace Mod
{
    public static partial class RealisticHuman
    {
        public class Layers { public Color32[] skin, flesh, bone; public int w, h, S, pad; public float ux0, uy0, uw, uh, eu, ev; public int kind = -1; public Texture2D shared;
            public float mu = 0.95f, mv = 0.26f;   // head: front of the jaw (mouth line), in sprite-bounds uv
            // noise caches
            public float[] bN; public byte[] bCk;                 // burn edge noise, crack (0 = not yet, 1 = no, 2 = yes)
            public ushort[] rG, rV, rB, rSl, rF1; public byte[] rOk;   // rot terms, packed
        }
        static readonly Dictionary<string, Layers> cache = new Dictionary<string, Layers>();
        static readonly Dictionary<Texture, Color[]> readCache = new Dictionary<Texture, Color[]>();

        public static void Apply(GameObject root) { Apply(root, false); }
        // real = lifelike skin (SkinV2)
        public static void Apply(GameObject root, bool real) { Apply(root, real, false); }
        public static void Apply(GameObject root, bool real, bool female)
        {
            if (female) real = true;
            if (!root) return;
            var person = root.GetComponent<PersonBehaviour>();
            if (!person) person = root.GetComponentInChildren<PersonBehaviour>();
            if (!person) return;
            FleshHuman.Setup(root);
            foreach (var l in person.Limbs)
            {
                if (!l) continue;
                try
                {
                    var r = l.SkinMaterialHandler ? l.SkinMaterialHandler.renderer : l.GetComponent<SpriteRenderer>();
                    if (!r || !r.sprite) continue;
                    var orig = r.sprite;
                    string key = Key(orig, l.name, real, female);
                    Layers L;
                    lock (buildLock)   // waits for the background build of this limb if it's mid-way
                        if (!cache.TryGetValue(key, out L))
                        {
                            var all = ReadAll(orig.texture);
                            L = BuildLocked(all, orig.rect, orig.texture.width, l.name, l.HasBrain, real, female);
                            cache[key] = L;
                        }
                    if (L == null) continue;
                    var c = l.GetComponent<Crater>() ?? l.gameObject.AddComponent<Crater>();
                    c.Init(L, orig, r);
                }
                catch (System.Exception e) { Debug.LogError("[RealisticGore] " + l.name + ": " + e); }
            }
            try { SoftSkin.Setup(person); } catch (System.Exception e) { Debug.LogError("[RealisticGore] skin: " + e); }
        }

        static readonly object buildLock = new object();
        static string Key(Sprite orig, string limb, bool real, bool female)
        {
            return GoreSettings.TexDetail.ToString("0.###") + "|" + (female ? "f:" : "") + (real ? "v2:" : "") + orig.texture.GetInstanceID() + ":" + orig.rect + ":" + limb;
        }
        static Layers BuildLocked(Color[] all, Rect rect, int tw, string limb, bool brain, bool real, bool female)
        {
            v2 = real; fem = female;
            try { return Build(all, rect, tw, limb.ToLower().Replace(" ", ""), brain); } finally { v2 = false; fem = false; }
        }

        // build every limb texture in the background at load so the first spawn doesn't freeze
        public static void Prebuild(GameObject prefab)
        {
            if (!prefab) return;
            var person = prefab.GetComponentInChildren<PersonBehaviour>(true);
            var limbs = person && person.Limbs != null && person.Limbs.Length > 0 ? person.Limbs : prefab.GetComponentsInChildren<LimbBehaviour>(true);
            var jobs = new List<System.Action>();
            bool[,] vars = { { true, false }, { true, true }, { false, false } };   // new, female, old
            for (int vi = 0; vi < 3; vi++)
                foreach (var l in limbs)
                {
                    if (!l) continue;
                    var r = l.GetComponent<SpriteRenderer>();
                    if (!r || !r.sprite) continue;
                    var orig = r.sprite;
                    bool real = vars[vi, 0], female = vars[vi, 1];
                    string key = Key(orig, l.name, real, female), name = l.name; bool brain = l.HasBrain;
                    var all = ReadAll(orig.texture); var rect = orig.rect; int tw = orig.texture.width;
                    jobs.Add(() =>
                    {
                        lock (buildLock)
                        {
                            if (cache.ContainsKey(key)) return;
                            cache[key] = BuildLocked(all, rect, tw, name, brain, real, female);
                        }
                    });
                }
            if (jobs.Count == 0) return;
            var th = new System.Threading.Thread(() =>
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                try { foreach (var j in jobs) j(); Debug.Log("[RealisticGore] prebuilt " + jobs.Count + " limb textures in " + sw.ElapsedMilliseconds + " ms"); }
                catch (System.Exception e) { Debug.LogWarning("[RealisticGore] prebuild stopped, spawns build on demand: " + e.Message); }
            });
            th.IsBackground = true; th.Priority = System.Threading.ThreadPriority.BelowNormal;
            th.Start();
        }

        static Color[] ReadAll(Texture src)
        {
            Color[] px;
            if (readCache.TryGetValue(src, out px)) return px;
            var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var t = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
            t.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0); t.Apply();
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
            px = t.GetPixels(); Object.Destroy(t);
            readCache[src] = px; return px;
        }

        public static float Hash(int x, int y)
        {
            unchecked { int h = x * 374761393 + y * 668265263; h = (h ^ (h >> 13)) * 1274126177; return ((h ^ (h >> 16)) & 0xffff) / 65535f; }
        }
        static float Smooth(float a, float b, float x) { x = Mathf.Clamp01((x - a) / (b - a)); return x * x * (3 - 2 * x); }

        static Color[] sp; static int W, H; static int S;
        static Color P(int x, int y) { if (x < 0 || y < 0 || x >= W || y >= H) return Color.clear; return sp[y * W + x]; }
        static Color Bil(float fx, float fy)
        {
            fx -= 0.5f; fy -= 0.5f;
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0, ty = fy - y0;
            return Color.Lerp(Color.Lerp(P(x0, y0), P(x0 + 1, y0), tx), Color.Lerp(P(x0, y0 + 1), P(x0 + 1, y0 + 1), tx), ty);
        }

        static float bw, bh;
        static float E(float u, float v, float cu, float cv, float rx, float ry)
        {
            float dx = (u - cu) * bw / rx, dy = (v - cv) * bh / ry;
            return Smooth(1.1f, 0.9f, dx * dx + dy * dy);
        }
        static float Ed(float u, float v, float cu, float cv, float rx, float ry)
        {
            float dx = (u - cu) / rx, dy = (v - cv) / ry; return dx * dx + dy * dy;
        }

        enum Kind { Head, Chest, Belly, Hips, Thigh, Shin, Foot, UpperArm, Forearm, Hand, Other }
        static Kind KindOf(string n, bool head)
        {
            if (head) return Kind.Head;
            if (n.Contains("upperbody")) return Kind.Chest;
            if (n.Contains("middlebody")) return Kind.Belly;
            if (n.Contains("lowerbody")) return Kind.Hips;
            if (n.Contains("upperleg")) return Kind.Thigh;
            if (n.Contains("lowerleg")) return Kind.Shin;
            if (n.Contains("foot")) return Kind.Foot;
            if (n.Contains("upperarm")) return Kind.UpperArm;
            if (n.Contains("lowerarm")) return Kind.Forearm;
            if (n.Contains("hand")) return Kind.Hand;
            return Kind.Other;
        }

        static Layers Build(Color[] all, Rect rect, int tw, string n, bool headFlag)
        {
            W = (int)rect.width; H = (int)rect.height;
            int ox = (int)rect.x, oy = (int)rect.y;
            sp = new Color[W * H];
            int bx0 = W, bx1 = 0, by0 = H, by1 = 0, cnt = 0; float sum = 0;
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
                {
                    var c = all[(oy + y) * tw + ox + x]; sp[y * W + x] = c;
                    if (c.a > 0.5f) { cnt++; sum += c.grayscale; if (x < bx0) bx0 = x; if (x > bx1) bx1 = x; if (y < by0) by0 = y; if (y > by1) by1 = y; }
                }
            if (cnt < 4) return null;
            float avg = sum / cnt;
            bw = bx1 - bx0 + 1; bh = by1 - by0 + 1;
            Kind kind = KindOf(n, headFlag);

            float eu = 0.78f, ev = 0.62f, mouthU = 0.98f;

            int pad = kind == Kind.Head ? 9 : 3;
            neckBot = Mathf.Min(0.32f, (by0 + pad - 0.7f) / bh);
            int PW = W + pad * 2, PH = H + pad * 2;
            S = Mathf.Clamp((int)(Mathf.Sqrt(250000f / (PW * PH)) * GoreSettings.TexDetail), 6, 64);
            int hw = PW * S, hh = PH * S;
            float k16 = 16f / S;
            int seed = (n.GetHashCode() & 1023) * 41;

            // shape mask
            var A = new float[hw * hh]; var U = new float[hw * hh]; var V = new float[hw * hh];
            for (int y = 0; y < hh; y++)
                for (int x = 0; x < hw; x++)
                {
                    float px = (x + 0.5f) / S - pad, py = (y + 0.5f) / S - pad;
                    float u = (px - bx0) / bw, v = (py - by0) / bh;
                    float a = Shape(kind, u, v);
                    if (a < 0f) a = Smooth(0.3f, 0.6f, Bil(px, py).a);
                    else if (kind == Kind.Head && v < 0f) a *= Smooth(-neckBot, -neckBot * 0.25f, v);
                    int i = y * hw + x; A[i] = a; U[i] = u; V[i] = v;
                }

            var NX = new float[hw * hh]; var NY = new float[hw * hh];
            for (int y = 0; y < hh; y++)
            {
                int x = 0;
                while (x < hw)
                {
                    if (A[y * hw + x] < 0.5f) { x++; continue; }
                    int s0 = x; while (x < hw && A[y * hw + x] >= 0.5f) x++;
                    float len = x - s0;
                    for (int q = s0; q < x; q++) NX[y * hw + q] = ((q - s0 + 0.5f) / len) * 2f - 1f;
                }
            }
            for (int x = 0; x < hw; x++)
            {
                int y = 0;
                while (y < hh)
                {
                    if (A[y * hw + x] < 0.5f) { y++; continue; }
                    int s0 = y; while (y < hh && A[y * hw + x] >= 0.5f) y++;
                    float len = y - s0;
                    for (int q = s0; q < y; q++) NY[q * hw + x] = ((q - s0 + 0.5f) / len) * 2f - 1f;
                }
            }
            float yw = kind == Kind.Head ? 0.7f : (kind == Kind.Foot || kind == Kind.Hand) ? 0.5f : 0f;
            if (yw == 0f) System.Array.Clear(NY, 0, NY.Length);
            if (kind == Kind.Head) for (int i = 0; i < NY.Length; i++) NY[i] *= Smooth(0.02f, 0.25f, V[i]);

            var L = new Layers { w = hw, h = hh, S = S, pad = pad, skin = new Color32[hw * hh], flesh = new Color32[hw * hh], bone = new Color32[hw * hh] };
            L.ux0 = bx0; L.uy0 = by0; L.uw = bw; L.uh = bh; L.kind = (int)kind; L.eu = eu; L.ev = ev; L.mu = mouthU;
            Color hairC = new Color(0.17f, 0.11f, 0.075f);
            bool hairy = kind == Kind.Thigh || kind == Kind.Shin || kind == Kind.Forearm || kind == Kind.UpperArm;

            for (int y = 0; y < hh; y++)
                for (int x = 0; x < hw; x++)
                {
                    int i = y * hw + x;
                    float a = A[i]; if (a < 0.02f) continue;
                    float u = U[i], v = V[i];
                    float fx = x * k16 + seed, fy = y * k16;
                    int ix = (int)fx, iy = (int)fy;
                    float nx = NX[i], ny = NY[i];
                    float nz = Mathf.Sqrt(Mathf.Max(0.02f, 1f - nx * nx - ny * ny * yw));
                    float light = Mathf.Clamp01(nz * 0.78f + nx * 0.16f + ny * 0.10f * (yw + 0.3f) + 0.08f);
                    float pore = Hash(ix, iy) - 0.5f;

                    if (v2)
                    {
                        Color l2 = SkinV2(kind, u, v, fx, fy, ix, iy, nx, ny, yw, eu, ev, hairy);
                        l2.a = a; L.skin[i] = l2;
                    }
                    else
                    {
                    // skin
                    float tone = Mathf.PerlinNoise(fx * 0.012f, fy * 0.012f) - 0.5f;
                    Color s = new Color(0.87f, 0.67f, 0.55f);
                    s += new Color(tone * 0.08f, tone * 0.06f, tone * 0.05f) + new Color(pore, pore * 0.9f, pore * 0.8f) * 0.03f;
                    if (Mathf.PerlinNoise(fx * 0.05f + 7, fy * 0.05f) > 0.8f) s = Color.Lerp(s, new Color(0.72f, 0.48f, 0.40f), 0.22f); // freckles / moles
                    bool cloth = false;

                    switch (kind)
                    {
                        case Kind.Head:
                        {
                            // ear
                            float ed = Ed(u, v, 0.40f, 0.50f, 0.10f, 0.16f);
                            bool ear = ed < 1f;
                            if (ear)
                            {
                                s *= ed > 0.65f ? 0.80f : (ed < 0.3f ? 0.78f : 0.93f);
                                s = Color.Lerp(s, new Color(0.85f, 0.55f, 0.50f), 0.2f);
                            }
                            // hair
                            float hn = (Mathf.PerlinNoise(fx * 0.03f, fy * 0.03f) - 0.5f) * 0.10f;
                            bool hair = !ear && ((v > 0.80f + hn) || (u < 0.30f + hn && v > 0.27f) || (u < 0.52f + hn && v > 0.66f + hn));
                            if (hair)
                            {
                                float strand = Mathf.PerlinNoise(fx * 0.03f, fy * 0.45f);
                                s = hairC * (0.7f + strand * 0.7f) + new Color(pore, pore, pore) * 0.04f;
                                cloth = true;
                            }
                            else
                            {
                                // eye
                                float sd = Ed(u, v, eu, ev, 0.075f, 0.042f);
                                if (sd < 1f)
                                {
                                    s = new Color(0.93f, 0.90f, 0.86f) * (0.9f + (1f - sd) * 0.1f);
                                    float id = Ed(u, v, eu + 0.028f, ev, 0.032f, 0.036f);
                                    if (id < 1f) s = id < 0.18f ? new Color(0.03f, 0.02f, 0.02f) : new Color(0.32f, 0.20f, 0.11f) * (0.8f + id * 0.3f);
                                    if (id < 0.05f && v > ev) s = Color.white * 0.95f;                                // catchlight
                                }
                                else if (sd < 1.7f && v > ev) s *= 0.80f;                                             // upper lid
                                else if (sd < 1.5f && v < ev) s *= 0.92f;                                             // lower lid
                                if (v > ev + 0.075f && v < ev + 0.11f && Mathf.Abs(u - eu) < 0.09f)                   // eyebrow
                                    s = Color.Lerp(s, hairC, 0.85f * (0.6f + Hash(ix, iy) * 0.4f));
                                if (Ed(u, v, 0.95f, 0.45f, 0.03f, 0.02f) < 1f) s *= 0.55f;                            // nostril
                                if (u > 0.84f && Mathf.Abs(v - 0.30f) < 0.035f) s = Color.Lerp(s, new Color(0.70f, 0.38f, 0.36f), 0.7f); // lips
                                if (u > 0.84f && Mathf.Abs(v - 0.30f) < 0.007f) s *= 0.55f;                           // mouth line
                                if (u > 0.55f && v < 0.36f) s = Color.Lerp(s, new Color(0.55f, 0.45f, 0.42f), Hash(ix * 3, iy * 3) > 0.7f ? 0.25f : 0.06f); // stubble
                            }
                            break;
                        }
                        case Kind.Chest:
                            if (Mathf.Abs(v - 0.88f) < 0.025f && u > 0.45f) s *= 1.05f;                              // collarbone
                            { float pd = Ed(u, v, 0.84f, 0.60f, 0.30f, 0.26f); if (pd > 0.85f && pd < 1.05f && v < 0.6f) s *= 0.88f; }
                            break;
                        case Kind.Belly:
                            if (Ed(u, v, 0.90f, 0.30f, 0.035f, 0.05f) < 1f) s *= 0.55f;                              // navel
                            break;
                        case Kind.Hips:
                            cloth = true; break;
                        case Kind.Thigh:
                            if (v > 0.70f) cloth = true;
                            break;
                        case Kind.Forearm:
                        case Kind.Hand:
                            { float vn = Mathf.Abs(Mathf.PerlinNoise(fx * 0.008f, fy * 0.03f) - 0.5f); if (vn < 0.015f) s = Color.Lerp(s, new Color(0.50f, 0.55f, 0.62f), 0.25f); }
                            break;
                        case Kind.Foot:
                            if (v < 0.12f) s *= 0.9f;
                            break;
                    }

                    if (cloth && kind != Kind.Head)
                    {
                        // briefs
                        float fold = Mathf.PerlinNoise(fx * 0.025f, fy * 0.12f);
                        s = new Color(0.14f, 0.16f, 0.22f) * (0.85f + fold * 0.3f) + new Color(pore, pore, pore) * 0.04f;
                        if (((ix + iy) & 3) == 0) s *= 0.96f;                                                     // weave
                        if (kind == Kind.Hips && v > 0.86f) s = (Mathf.Abs(v - 0.93f) < 0.015f) ? new Color(0.55f, 0.12f, 0.12f) : new Color(0.62f, 0.63f, 0.67f) * (0.9f + fold * 0.15f); // waistband
                        if (kind == Kind.Thigh && v < 0.74f) s *= 0.8f;                                           // hem
                    }
                    else if (hairy)
                    {
                        int cx = ix / 7, cy = iy / 7;
                        if (Hash(cx + 91, cy) > 0.55f)
                        {
                            float hx = cx * 7 + Hash(cx, cy + 5) * 7, hy = cy * 7 + Hash(cx + 3, cy) * 7;
                            float along = (fx - hx) * 0.45f + (fy - hy) * -0.89f, across = (fx - hx) * 0.89f + (fy - hy) * 0.45f;
                            if (Mathf.Abs(across) < 0.35f && along > 0 && along < 5f) s = Color.Lerp(s, hairC, 0.22f);
                        }
                    }

                    // light
                    Color lit = s * (0.50f + 0.58f * light);
                    lit = Color.Lerp(lit * new Color(1f, 0.84f, 0.80f), lit, Smooth(0f, 0.6f, light));
                    if (!cloth) lit += new Color(1f, 0.95f, 0.9f) * Mathf.Pow(light, 10f) * 0.07f;
                    lit.a = a; L.skin[i] = lit;
                    }

                    // flesh
                    float edge = Mathf.Abs(nx);
                    float fib = Mathf.PerlinNoise(fx * 0.09f, fy * 0.011f);
                    Color m = Color.Lerp(new Color(0.42f, 0.05f, 0.07f), new Color(0.72f, 0.14f, 0.15f), fib);
                    if (fib > 0.74f) m = Color.Lerp(m, new Color(0.88f, 0.78f, 0.74f), (fib - 0.74f) * 2.5f);
                    Color fatc = new Color(0.93f, 0.82f, 0.52f) * (0.9f + Hash(ix + 9, iy) * 0.12f);
                    Color f = Color.Lerp(m, fatc, Smooth(0.72f, 0.9f, edge));
                    f *= 0.75f + 0.3f * light;
                    f.a = a; L.flesh[i] = f;

                    // deep layer
                    Color b = (u < 0f || u > 1f || v < 0f || v > 1f) ? m * 0.85f : Inner(kind, u, v, Mathf.Abs(u - 0.5f), ix, iy, hh, eu, ev);
                    b *= 0.78f + 0.25f * light;
                    b.a = a; L.bone[i] = b;
                }
            sp = null;
            return L;
        }

    }
}
