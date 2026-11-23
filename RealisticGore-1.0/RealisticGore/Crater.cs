using System.Collections.Generic;
using UnityEngine;

namespace Mod
{
    public partial class Crater : MonoBehaviour
    {
        RealisticHuman.Layers L;
        Color32[] cur; byte[] level; float[] burn;
        Texture2D tex; bool dirty;
        float ppuHi; Vector2 pivotPx;
        SpriteRenderer sr; SkinMaterialHandler skinH; LimbBehaviour limb; PhysicalBehaviour phys; RealGore rg;
        float burnTick, upTick;
        public bool Ready { get { return tex; } }
        bool needSplit, cutBone; int vertMode; public List<Collider2D> bodyCols;
        int[] fComp, fQueue; bool[] fSolid;
        bool Vert { get { return vertMode == 0 ? L.h >= L.w : vertMode == 1; } }

        public void InitChunk(RealisticHuman.Layers layers, Color32[] c, byte[] lv, Texture2D t, SpriteRenderer r, float ppu, Vector2 pivot, int vMode)
        {
            L = layers; sr = r; phys = GetComponent<PhysicalBehaviour>();
            cur = c; level = lv; burn = new float[c.Length]; tex = t;
            ppuHi = ppu; pivotPx = pivot; vertMode = vMode; ExtPale = 0.3f; isChunk = true;
            if (!mat) mat = new Material(Shader.Find("Sprites/Default"));
        }

        public void Init(RealisticHuman.Layers layers, Sprite orig, SpriteRenderer r)
        {
            L = layers; sr = r;
            limb = GetComponent<LimbBehaviour>(); skinH = limb ? limb.SkinMaterialHandler : null;
            phys = limb ? limb.PhysicalBehaviour : GetComponent<PhysicalBehaviour>(); rg = GetComponent<RealGore>();
            cur = (Color32[])L.skin.Clone(); level = new byte[cur.Length]; burn = new float[cur.Length];
            tex = new Texture2D(L.w, L.h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(cur); tex.Apply(false);
            ppuHi = orig.pixelsPerUnit * L.S;
            pivotPx = (orig.pivot + Vector2.one * L.pad) * L.S;
            r.sprite = Sprite.Create(tex, new Rect(0, 0, L.w, L.h), new Vector2((orig.pivot.x + L.pad) / (orig.rect.width + 2 * L.pad), (orig.pivot.y + L.pad) / (orig.rect.height + 2 * L.pad)), ppuHi, 0, SpriteMeshType.FullRect);
            if (!mat) mat = new Material(Shader.Find("Sprites/Default"));
            if (limb)
            {
                var gs = GetComponent<GoreStringBehaviour>(); if (gs) { gs.DestroyJoint(); gs.enabled = false; }
                var jn = limb.Joint;
                if (jn) { jAnchor = jn.anchor; jBody = jn.connectedBody; jConn = jn.connectedAnchor; }
                wasDism = limb.IsDismembered;
            }
        }

        // torn-off ends get a wound too
        public bool skipStump; bool wasDism; Vector2 jAnchor, jConn; Rigidbody2D jBody;
        float StumpRad() { float m = L.uw > 0 ? Mathf.Min(L.uw, L.uh) : Mathf.Min(L.w, L.h) / (float)L.S; return Mathf.Clamp(m * 0.34f, 0.8f, 3.2f); }
        public void StumpAt(Vector2 world, Vector2 outDir, bool pumping, bool longShard)
        {
            if (!Ready) return;
            float R = StumpRad(), pxW = L.S / ppuHi * Mathf.Abs(transform.lossyScale.y);
            bool nm = noMirror; noMirror = true;
            try { StumpCarves(world, outDir, R, pxW); } finally { noMirror = nm; }
            BoneShard(world, outDir, R * pxW, longShard);
            AddBleeder(world, 45f);
            Spray(world, outDir, 45f, 16);
            AddSpurt(world, outDir, pumping ? 14f : 3f, pumping);
        }
        void StumpCarves(Vector2 world, Vector2 outDir, float R, float pxW)
        {
            Carve(world, R, true, false, true);
            Carve(world - outDir * R * 0.35f * pxW, R * 0.7f, true, false, false);
            // ragged notches so it doesn't look cut
            Vector2 side = new Vector2(-outDir.y, outDir.x);
            int bites = Random.Range(3, 6);
            for (int k = 0; k < bites; k++)
            {
                float s = Random.Range(-1f, 1f);
                Carve(world + (side * s * R * 0.95f - outDir * Random.Range(0.1f, 0.6f) * R) * pxW, R * Random.Range(0.3f, 0.55f), true, false, false);
            }
        }
        void Stump()
        {
            Vector2 at = transform.TransformPoint(jAnchor);
            Vector2 c = sr.bounds.center;
            Vector2 od = at - c; od = od.sqrMagnitude > 1e-8f ? od.normalized : (Vector2)transform.up;
            bool mine = Random.value < 0.5f;
            StumpAt(at, od, false, mine);
            if (jBody)
            {
                var pc = jBody.GetComponent<Crater>();
                Vector2 pat = jBody.transform.TransformPoint(jConn);
                bool head = IsHead;
                if (pc) pc.StumpAt(pat, -od, true, !mine && !head);   // spine left with the head
                if (head && !(limb && limb.IsAndroid)) NeckRip(at, od, jBody, pat);
            }
        }

        // bone sticking out of the stump
        void BoneShard(Vector2 world, Vector2 outDir, float rWorld, bool longShard)
        {
            if (limb && limb.IsAndroid) return;
            if (L.kind == 0 && L.uw > 0 && L.uh > 0 && L.uh < L.uw * 1.2f) return;
            // follow the limb, not the tear
            Vector2 axis = (Vert ? transform.up : transform.right); axis.Normalize();
            Vector2 mid = sr.bounds.center;
            float along = Vector2.Dot(world - mid, axis);
            if (along < 0f) { axis = -axis; along = -along; }
            outDir = axis;
            world = mid + axis * along;
            float limbW = rWorld / 0.34f;
            float boneW = limbW * Random.Range(0.36f, 0.44f);
            float inside = rWorld * 1.3f;
            float stick = limbW * (longShard ? Random.Range(0.3f, 0.5f) : Random.Range(0.08f, 0.16f));
            float len = inside + stick;
            float tipFrac = Mathf.Clamp01((inside + stick * (longShard ? 0.15f : 0.3f)) / len);

            int TW = 128, TH = 32;
            var px = new Color32[TW * TH];
            float sd = Random.value * 500f;
            float uTop = Random.Range(tipFrac, 1f), uBot = Random.Range(tipFrac, 1f);
            if (longShard && Mathf.Abs(uTop - uBot) < 0.25f) { if (uTop > uBot) uBot = Mathf.Max(tipFrac, uTop - 0.35f); else uTop = Mathf.Max(tipFrac, uBot - 0.35f); }
            Vector3 Ld = new Vector3(-0.3f, 0.75f, 0.6f).normalized;
            for (int y = 0; y < TH; y++)
                for (int x = 0; x < TW; x++)
                {
                    float u = x / (float)(TW - 1), v = (y + 0.5f) / TH * 2f - 1f;
                    float t = (v + 1f) * 0.5f;
                    float end = Mathf.Lerp(uBot, uTop, t);
                    end += (Mathf.PerlinNoise(t * 9f + sd, sd) - 0.5f) * 0.12f + Mathf.Abs(Mathf.Sin(t * 23f + sd)) * 0.035f;
                    float halfW = 1f - Mathf.Clamp01((u - tipFrac) / Mathf.Max(0.05f, 1f - tipFrac)) * 0.25f;
                    if (Mathf.Abs(v) > halfW * (0.94f + Mathf.PerlinNoise(u * 20f + sd, v * 3f) * 0.06f)) continue;
                    if (u > end) continue;
                    float n = v / halfW;
                    Vector3 nn = new Vector3(0f, n, Mathf.Sqrt(Mathf.Max(0f, 1f - n * n))).normalized;
                    float diff = Mathf.Max(0f, Vector3.Dot(nn, Ld));
                    Color bc = Color.Lerp(new Color(0.74f, 0.66f, 0.52f), new Color(0.88f, 0.82f, 0.70f), Mathf.PerlinNoise(u * 30f + sd, v * 4f));
                    float fromEnd = end - u;
                    if (fromEnd < 0.06f)
                    {
                        if (Mathf.Abs(v) < halfW * 0.55f) bc = Color.Lerp(new Color(0.42f, 0.04f, 0.05f), new Color(0.70f, 0.20f, 0.16f), Mathf.PerlinNoise(x * 0.6f + sd, y * 0.6f));
                        else bc *= 0.82f;
                        diff = 0.55f + diff * 0.3f;
                    }
                    float wet = Mathf.Clamp01(1f - (u - tipFrac + 0.1f) / 0.35f) * 0.95f;
                    wet = Mathf.Max(wet, Mathf.Clamp01((Mathf.PerlinNoise(u * 5f + sd, v * 2.5f + sd) - 0.42f) * 3f) * 0.8f);
                    wet = Mathf.Max(wet, Mathf.Clamp01((Mathf.PerlinNoise(u * 40f + sd, v * 1.2f) - 0.6f) * 4f) * 0.6f);
                    bc = Color.Lerp(bc, new Color(0.88f, 0.62f, 0.55f), Mathf.Clamp01(0.25f - fromEnd) * 0.6f);
                    bc = Color.Lerp(bc, new Color(0.45f, 0.02f, 0.04f), wet);
                    Color c = bc * (0.35f + 0.75f * diff);
                    c += new Color(1f, 0.95f, 0.9f) * Mathf.Pow(Mathf.Max(0f, Vector3.Dot(nn, (Ld + Vector3.forward).normalized)), 30f) * (0.2f + wet * 0.35f);
                    float edge = Mathf.Clamp01(Mathf.Min((halfW - Mathf.Abs(v)) * 6f, fromEnd * 40f));
                    c *= Mathf.Lerp(0.45f, 1f, edge);
                    c.a = 1f;
                    px[y * TW + x] = c;
                }
            var tx = new Texture2D(TW, TH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            tx.SetPixels32(px); tx.Apply(false);

            // local space = mirroring for free
            var go = new GameObject("BoneShard");
            go.transform.SetParent(transform, false);
            Vector2 root = world - outDir * inside;
            Vector2 dl = transform.InverseTransformVector(outDir * len);
            float lenL = dl.magnitude;
            float wL = boneW / Mathf.Max(1e-4f, Mathf.Abs(transform.lossyScale.y));
            float ppu = TW / Mathf.Max(1e-4f, lenL);
            var spr = Sprite.Create(tx, new Rect(0, 0, TW, TH), new Vector2(0f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
            go.transform.localPosition = (Vector3)(Vector2)transform.InverseTransformPoint(root);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dl.y, dl.x) * Mathf.Rad2Deg + Random.Range(-7f, 7f));
            go.transform.localScale = new Vector3(1f, wL / (TH / ppu), 1f);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = spr; r.sharedMaterial = mat ? mat : sr.sharedMaterial;
            r.sortingLayerID = sr.sortingLayerID; r.sortingOrder = sr.sortingOrder - 1;
            r.color = Color.white;
        }

        // arterial spurts
        class Spurt { public Vector2 local, dirL; public float life, max; public bool pump; }
        readonly List<Spurt> spurts = new List<Spurt>();
        float pulse;
        void AddSpurt(Vector2 world, Vector2 dir, float life, bool pump)
        {
            if (spurts.Count > 3) spurts.RemoveAt(0);
            spurts.Add(new Spurt { local = transform.InverseTransformPoint(world), dirL = transform.InverseTransformDirection(dir), life = life, max = life, pump = pump });
        }
        void RunSpurts(float dt)
        {
            if (spurts.Count == 0) return;
            bool beating = limb && limb.IsConsideredAlive;
            float prev = pulse; pulse += dt * 1.25f;
            bool beat = Mathf.Floor(pulse) != Mathf.Floor(prev);
            float ph = pulse % 1f;
            for (int k = spurts.Count - 1; k >= 0; k--)
            {
                var s = spurts[k]; s.life -= dt;
                if (s.life <= 0) { spurts.RemoveAt(k); continue; }
                float str = s.life / s.max;
                Vector2 p = transform.TransformPoint(s.local), d = ((Vector2)transform.TransformDirection(s.dirL)).normalized;
                if (s.pump && beating)
                {
                    if (ph < 0.15f)
                        for (int q = 0; q < 3; q++)
                            Droplet.Create(p, (d + Random.insideUnitCircle * 0.12f).normalized * Random.Range(3.5f, 6.5f) * (0.4f + 0.6f * str), sr, Random.Range(0.5f, 1.1f));
                    if (beat && limb.CirculationBehaviour) RealBlood.Bleed(limb.CirculationBehaviour, p, d, 0.6f * str);
                }
                else if (Random.value < dt * 10f * str)
                    Droplet.Create(p, (d * 0.6f + Random.insideUnitCircle * 0.4f) * Random.Range(0.4f, 1.5f), sr);
            }
        }

        public float ExtPale = -1f;
        public void AddBleed(Vector2 world, float life) { AddBleeder(world, life); }

        Vector2 ToPx(Vector2 world)
        {
            Vector2 l = transform.InverseTransformPoint(world);
            return l * ppuHi + pivotPx;
        }

        public Vector2 AcrossDir { get { return Vert ? (Vector2)transform.right : (Vector2)transform.up; } }
        public float PxWorld { get { return Mathf.Abs(transform.lossyScale.x) / Mathf.Max(1f, ppuHi); } }
        public SpriteRenderer Sr { get { return sr; } }
        // -1 = outside the body, 6 = see-through wound, else the paint level
        public int Probe(Vector2 world, out Color32 col)
        {
            col = new Color32(0, 0, 0, 0);
            if (!Ready) return -1;
            Vector2 p = ToPx(world); int x = (int)p.x, y = (int)p.y;
            if (x < 0 || y < 0 || x >= L.w || y >= L.h) return -1;
            int i = y * L.w + x; col = cur[i];
            if (level[i] == 6) return 6;
            if (cur[i].a < 100) return -1;
            return level[i];
        }

        static readonly Color Hole = new Color(0.16f, 0.01f, 0.02f), Blood = new Color(0.42f, 0.0f, 0.03f);

        // depth: 0 bruise, 1 skin torn, 2 flesh, 3 to the bone
        public void Wound(Vector2 world, float radiusOrigPx, int depth, bool hole)
        {
            if (!Ready) return;
            Vector2 c = ToPx(world);
            float R = radiusOrigPx * L.S;
            float reach = R * 1.4f;
            int x0 = Mathf.Max(0, (int)(c.x - reach)), x1 = Mathf.Min(L.w - 1, (int)(c.x + reach));
            int y0 = Mathf.Max(0, (int)(c.y - reach)), y1 = Mathf.Min(L.h - 1, (int)(c.y + reach));
            float nf = 1.6f / L.S; float sx = Random.value * 500f, sy = Random.value * 500f;
            if (depth >= 2) { Crater3D(c, R, depth, hole, x0, x1, y0, y1, nf, sx, sy); return; }
            float tBone = depth >= 3 ? 0.42f : -1f, tFlesh = depth >= 2 ? 0.78f : -1f, tEdge = depth >= 1 ? 1f : -1f;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    int i = y * L.w + x;
                    if (cur[i].a == 0) continue;
                    float d = Vector2.Distance(new Vector2(x, y), c) / R;
                    d += (Mathf.PerlinNoise(x * nf + sx, y * nf + sy) - 0.5f) * 0.55f;
                    if (d > 1.35f) continue;
                    byte lv; Color col;
                    if (hole && d < 0.16f) { lv = 5; col = Hole; }
                    else if (d < tBone) { lv = 4; col = Color.Lerp(L.bone[i], Blood, 0.25f + d * 0.4f); }
                    else if (d < tFlesh) { lv = 3; col = Color.Lerp(L.flesh[i], Blood, 0.15f + (d - 0.4f) * 0.3f); }
                    else if (d < tEdge) { lv = 2; col = Color.Lerp(L.skin[i], new Color(0.58f, 0.16f, 0.14f), 0.65f); }
                    else continue;
                    if (lv < level[i]) continue;
                    col.a = L.skin[i].a / 255f;
                    cur[i] = col; level[i] = lv;
                }
            BruiseAt(c, R * 1.35f, depth == 0 ? 0.55f : 0.8f);
            if (depth >= 1)
                for (int k = 0; k < (int)(R * 1.5f); k++)
                {
                    Vector2 p = c + Random.insideUnitCircle * R * 1.7f;
                    int x = (int)p.x, y = (int)p.y;
                    if (x < 0 || y < 0 || x >= L.w || y >= L.h) continue;
                    int i = y * L.w + x; if (cur[i].a == 0 || level[i] > 2) continue;
                    var col = Color.Lerp(cur[i], Blood, 0.7f); col.a = cur[i].a / 255f; cur[i] = col;
                }
            dirty = true;
        }

    }
}
