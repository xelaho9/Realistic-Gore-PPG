using UnityEngine;

namespace Mod
{
    public partial class Crater
    {
        bool IsHead { get { return L.kind == 0 && L.uw > 0f && L.uh < L.uw * 1.2f; } }

        // decap: spine stays in the torso, head slides off it
        void NeckRip(Vector2 at, Vector2 od, Rigidbody2D torso, Vector2 pat)
        {
            var rb = GetComponent<Rigidbody2D>();
            if (!rb) return;
            float scl = Mathf.Abs(transform.lossyScale.y);
            float headW = L.uw * L.S / ppuHi * scl;
            float neckR = StumpRad() * L.S / ppuHi * scl;
            Vector2 side = new Vector2(-od.y, od.x);

            var spine = SpawnSpine(pat, od, headW, torso, rb);
            if (!GoreSettings.Strands) return;

            Color skinCol = SkinAvg();
            if (spine)
            {
                // cord goes last
                Vector2 tip = spine.transform.position;
                Strand.Create(spine, rb, tip, at, Random.Range(0.04f, 0.1f), spine.mass * 900f, headW * 0.07f, new Color(0.93f, 0.88f, 0.76f), sr, Circ());
            }
            int n = Random.Range(2, 4);
            for (int k = 0; k < n; k++)   // nerves: thin pale threads
            {
                float s = Random.Range(-0.7f, 0.7f);
                Strand.Create(rb, torso, at + side * s * neckR, pat + side * s * neckR * 0.8f, Random.Range(0.1f, 0.35f),
                    rb.mass * Random.Range(30f, 70f), Random.Range(0.008f, 0.014f), new Color(0.92f, 0.86f, 0.70f), sr, Circ());
            }
            n = Random.Range(3, 6);
            for (int k = 0; k < n; k++)   // muscle and gristle
            {
                float s = Random.Range(-0.8f, 0.8f);
                Strand.Create(rb, torso, at + side * s * neckR, pat + side * s * neckR, Random.Range(0.04f, 0.22f),
                    rb.mass * Random.Range(40f, 120f), Random.Range(0.02f, 0.05f),
                    Color.Lerp(FleshHuman.Tissue, FleshHuman.TissueLight, Random.value * 0.5f), sr, Circ());
            }
            for (int k = -1; k <= 1; k += 2)   // a flap of skin down each side
            {
                float s = k * Random.Range(0.8f, 1f);
                var st = Strand.Create(rb, torso, at + side * s * neckR, pat + side * s * neckR, Random.Range(0.02f, 0.1f),
                    rb.mass * Random.Range(50f, 110f), Random.Range(0.035f, 0.06f), new Color(0.9f, 0.8f, 0.7f), sr, Circ());
                if (st) st.Tint(skinCol);
            }
            n = Random.Range(2, 4);
            for (int k = 0; k < n; k++)   // strings of clotting blood, weak, they go first
            {
                float s = Random.Range(-0.6f, 0.6f);
                var st = Strand.Create(rb, torso, at + side * s * neckR, pat + side * s * neckR, Random.Range(0.15f, 0.45f),
                    rb.mass * Random.Range(8f, 20f), Random.Range(0.008f, 0.018f), new Color(0.5f, 0.05f, 0.07f), sr, Circ());
                if (st) st.Tint(new Color(0.62f, 0.42f, 0.42f));
            }
        }

        CirculationBehaviour Circ() { return limb ? limb.CirculationBehaviour : null; }

        Color SkinAvg()
        {
            float r = 0f, g = 0f, b = 0f; int c = 0;
            for (int i = 0; i < L.skin.Length; i += 7) { var p = L.skin[i]; if (p.a < 200) continue; r += p.r; g += p.g; b += p.b; c++; }
            if (c == 0) return new Color(0.85f, 0.68f, 0.58f);
            return new Color(r / c / 255f * 1.15f, g / c / 255f * 1.15f, b / c / 255f * 1.15f, 1f);
        }

        float spineLen;
        // head rides the spine on a slider
        Rigidbody2D SpawnSpine(Vector2 pat, Vector2 od, float headW, Rigidbody2D torso, Rigidbody2D head)
        {
            if (!torso) return null;
            spineLen = headW * 1.5f;
            float exposed = headW * 0.6f;
            var tx = SpineTex();
            float ppu = tx.height / spineLen;
            var spr = Sprite.Create(tx, new Rect(0, 0, tx.width, tx.height), new Vector2(0.5f, 1f), ppu, 0, SpriteMeshType.FullRect, Vector4.zero, true);

            var go = ModAPI.CreatePhysicalObject("spine", spr);
            go.transform.rotation = Quaternion.FromToRotation(Vector3.down, od);
            go.transform.localScale = Vector3.one;
            go.transform.position = pat - od * exposed;
            var tsr = torso.GetComponent<SpriteRenderer>(); if (!tsr) tsr = sr;
            var r = go.GetComponent<SpriteRenderer>();
            if (r) { r.sprite = spr; r.sharedMaterial = mat; r.sortingLayerID = tsr.sortingLayerID; r.sortingOrder = Mathf.Min(tsr.sortingOrder, sr.sortingOrder) - 1; PieceShadow.Add(r); }
            var ol = go.transform.Find("Outline"); if (ol) Destroy(ol.gameObject);
            var pb = go.GetComponent<PhysicalBehaviour>();
            if (pb && phys) pb.Properties = phys.Properties;
            foreach (var oc in go.GetComponents<Collider2D>()) DestroyImmediate(oc);
            var bc = go.AddComponent<BoxCollider2D>();
            bc.size = new Vector2(spineLen * 0.2f, exposed); bc.offset = new Vector2(0f, -exposed * 0.5f);
            if (pb) pb.ResetColliderArray();

            var crb = go.GetComponent<Rigidbody2D>();
            float m = Mathf.Max(0.03f, head.mass * 0.12f);
            crb.mass = m;
            if (pb) { pb.TrueInitialMass = m; pb.InitialMass = m; }
            crb.velocity = torso.GetPointVelocity(pat); crb.angularVelocity = torso.angularVelocity;
            foreach (var a in go.GetComponents<Collider2D>()) foreach (var b in BodyCols()) if (a && b) Physics2D.IgnoreCollision(a, b);

            var hj = go.AddComponent<HingeJoint2D>();
            hj.anchor = new Vector2(0f, -spineLen * 0.75f);
            hj.connectedBody = torso;
            hj.enableCollision = false;
            hj.limits = new JointAngleLimits2D { min = -10f, max = 10f };
            hj.useLimits = true;

            var sj = go.AddComponent<SliderJoint2D>();
            sj.connectedBody = head;
            sj.enableCollision = false;
            sj.autoConfigureAngle = false; sj.angle = 90f;
            sj.anchor = Vector2.zero;
            sj.limits = new JointTranslationLimits2D { min = 0f, max = exposed };
            sj.useLimits = true;
            sj.breakForce = head.mass * 400f;

            var d = go.AddComponent<SpineDrip>();
            d.root = torso; d.slide = sj; d.exposed = exposed; d.sortFrom = tsr;
            Spray(pat, -od, 30f, 10);
            return crb;
        }

        // neck vertebrae
        static Texture2D spineTex;
        static Texture2D SpineTex()
        {
            if (spineTex) return spineTex;
            int TW = 40, TH = 160;
            var px = new Color32[TW * TH];
            float sd = Random.value * 300f, tear0 = 0.84f, segL = tear0 / 5f;
            for (int y = 0; y < TH; y++)
                for (int x = 0; x < TW; x++)
                {
                    float t = 1f - (y + 0.5f) / TH, u = (x + 0.5f) / TW * 2f - 1f, au = Mathf.Abs(u);
                    float tear = tear0 + (Mathf.PerlinNoise(x * 0.3f + sd, 1.7f) - 0.5f) * 0.1f;
                    float blot = Mathf.Clamp01((Mathf.PerlinNoise(x * 0.12f + 40f, t * 9f + sd) - 0.5f) * 3f) * (0.4f + t * 0.6f);
                    float fib = Mathf.Abs(Mathf.Sin((u * 5f + Mathf.PerlinNoise(t * 4f, sd) * 3f) * Mathf.PI));
                    Color flesh = Color.Lerp(new Color(0.38f, 0.02f, 0.05f), new Color(0.72f, 0.14f, 0.15f), fib * 0.6f + Mathf.PerlinNoise(x * 0.2f, t * 30f + sd) * 0.4f);
                    Color c; float sh;
                    if (t > tear)
                    {
                        float cordEnd = 0.97f + (Mathf.PerlinNoise(sd, x * 0.5f) - 0.5f) * 0.04f;
                        if (au < 0.17f && t < cordEnd)
                        {
                            sh = Mathf.Sqrt(1f - au / 0.17f * au / 0.17f);
                            c = Color.Lerp(new Color(0.80f, 0.70f, 0.58f), new Color(0.95f, 0.90f, 0.80f), sh);
                            if (Mathf.Abs(Mathf.PerlinNoise(x * 0.4f, t * 20f + sd) - 0.5f) < 0.04f) c = Color.Lerp(c, new Color(0.6f, 0.1f, 0.1f), 0.6f);
                        }
                        else if (au < 0.75f && t < tear + 0.08f && Mathf.PerlinNoise(x * 0.35f + sd, t * 12f) > 0.45f) { c = flesh; sh = 0.8f; }
                        else { px[y * TW + x] = new Color32(0, 0, 0, 0); continue; }
                    }
                    else
                    {
                        float f = (t / segL) % 1f;
                        float wb = f > 0.8f ? 0.40f : 0.47f - 0.06f * Mathf.Sin(f / 0.8f * Mathf.PI);
                        if (f > 0.18f && f < 0.62f) wb = Mathf.Max(wb, 0.47f + 0.2f * Mathf.Sin((f - 0.18f) / 0.44f * Mathf.PI));   // transverse processes
                        if (au < wb)
                        {
                            sh = Mathf.Sqrt(Mathf.Max(0f, 1f - (u / wb) * (u / wb)));
                            float n = Mathf.PerlinNoise(x * 0.25f + sd, t * 40f);
                            c = f > 0.8f ? Color.Lerp(new Color(0.74f, 0.52f, 0.50f), new Color(0.90f, 0.76f, 0.72f), n)
                                         : Color.Lerp(new Color(0.76f, 0.68f, 0.54f), new Color(0.94f, 0.89f, 0.78f), n);
                            if (f > 0.76f && f < 0.82f) c *= 0.7f;   // joint line
                            c = Color.Lerp(c, flesh, Mathf.Clamp01((Mathf.PerlinNoise(x * 0.15f, t * 10f + sd) - 0.62f) * 4f));   // shreds stuck to the bone
                        }
                        else if (au < 0.95f && Mathf.PerlinNoise(x * 0.2f + sd, t * 6f) > 0.5f + au * 0.1f) { c = flesh; sh = 1f - (au - 0.4f); }
                        else { px[y * TW + x] = new Color32(0, 0, 0, 0); continue; }
                    }
                    c = Color.Lerp(c, new Color(0.26f, 0f, 0.02f), blot * 0.75f);
                    Color lit = c * (0.45f + 0.6f * sh);
                    lit.a = 1f;
                    px[y * TW + x] = lit;
                }
            spineTex = new Texture2D(TW, TH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            spineTex.SetPixels32(px); spineTex.Apply(false);
            return spineTex;
        }
    }

    public class SpineDrip : MonoBehaviour
    {
        public Rigidbody2D root; public SliderJoint2D slide; public float exposed; public SpriteRenderer sortFrom;
        float age, next;
        void FixedUpdate()
        {
            if (slide && (!slide.connectedBody || slide.jointTranslation >= exposed * 0.92f)) Destroy(slide);
        }
        void Update()
        {
            if (!root) { Destroy(gameObject); return; }
            age += Time.deltaTime; next -= Time.deltaTime;
            if (next > 0f || age > 30f || !sortFrom) return;
            next = Random.Range(0.25f, 0.7f) * (1f + age / 8f);
            Droplet.Create(transform.position, new Vector2(Random.Range(-0.2f, 0.2f), -0.4f), sortFrom, Random.Range(0.5f, 0.9f));
        }
    }
}
