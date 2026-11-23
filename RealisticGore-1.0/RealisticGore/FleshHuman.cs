using System.Collections.Generic;
using UnityEngine;

namespace Mod
{
    public static class FleshHuman
    {
        public static void Setup(GameObject root)
        {
            if (!root) return;
            foreach (var limb in root.GetComponentsInChildren<LimbBehaviour>())
            {
                var rg = limb.GetComponent<RealGore>();
                if (!rg) rg = limb.gameObject.AddComponent<RealGore>();
                rg.Strong = true;
                // organs stay painted in
            }
        }

        public static readonly Color Tissue = new Color(0.62f, 0.10f, 0.12f);
        public static readonly Color TissueLight = new Color(0.82f, 0.45f, 0.42f);

    }

    // flesh strand, snaps under force
    public class Strand : MonoBehaviour
    {
        Rigidbody2D a, b; Vector2 la, lb; DistanceJoint2D joint; LineRenderer lr;
        CirculationBehaviour circ; float maxLen;

        public static Strand Create(Rigidbody2D a, Rigidbody2D b, Vector2 worldA, Vector2 worldB, float slack,
            float breakForce, float width, Color col, Renderer sortFrom, CirculationBehaviour circ)
        {
            if (!a || !b) return null;
            var go = new GameObject("FleshStrand");
            var s = go.AddComponent<Strand>();
            s.a = a; s.b = b; s.circ = circ;
            s.la = a.transform.InverseTransformPoint(worldA);
            s.lb = b.transform.InverseTransformPoint(worldB);
            var j = a.gameObject.AddComponent<DistanceJoint2D>();
            j.connectedBody = b;
            j.autoConfigureConnectedAnchor = false; j.autoConfigureDistance = false;
            j.anchor = s.la; j.connectedAnchor = s.lb;
            j.distance = Vector2.Distance(worldA, worldB) + slack;
            j.maxDistanceOnly = true; j.enableCollision = false;
            j.breakForce = breakForce;
            s.joint = j; s.maxLen = j.distance;

            bool pale = col.r + col.g + col.b > 1.5f;
            if (!texRed) { texRed = MakeTex(false); texPale = MakeTex(true); }
            if (!matRed)
            {
                matRed = new Material(Shader.Find("Sprites/Default")) { mainTexture = texRed };
                matPale = new Material(Shader.Find("Sprites/Default")) { mainTexture = texPale };
            }
            s.lr = go.AddComponent<LineRenderer>();
            s.lr.sharedMaterial = pale ? matPale : matRed;
            s.lr.textureMode = LineTextureMode.Stretch;
            s.lr.positionCount = Pts; s.lr.useWorldSpace = true;
            s.lr.widthMultiplier = width * 0.85f;
            float tint = Random.Range(0.82f, 1.05f);
            s.lr.startColor = s.lr.endColor = new Color(tint, tint, tint, 1f);
            s.lr.numCapVertices = 0;
            s.sortFrom = sortFrom as SpriteRenderer;
            s.drip = Random.Range(0.3f, 1.2f);
            s.SetNeck(0.6f);
            if (sortFrom) { s.lr.sortingLayerID = sortFrom.sortingLayerID; s.lr.sortingOrder = sortFrom.sortingOrder - 1; }
            return s;
        }

        const int Pts = 12;
        static Texture2D texRed, texPale;
        static Material matRed, matPale;
        SpriteRenderer sortFrom; float neck = -1f, drip, age;

        static Texture2D MakeTex(bool pale)
        {
            int W = 128, Hh = 32;
            var px = new Color32[W * Hh];
            float sd = pale ? 31f : 7f;
            for (int y = 0; y < Hh; y++)
                for (int x = 0; x < W; x++)
                {
                    float v = (y + 0.5f) / Hh * 2f - 1f;
                    float r = 0.95f - Mathf.PerlinNoise(x * 0.09f + sd, 3.3f) * 0.3f - Mathf.PerlinNoise(x * 0.4f, sd) * 0.12f;
                    float av = Mathf.Abs(v);
                    float alpha = Mathf.Clamp01((r - av) * 9f);
                    if (alpha <= 0f) { px[y * W + x] = new Color32(0, 0, 0, 0); continue; }
                    float vn = Mathf.Clamp(v / r, -1f, 1f);
                    float sh = Mathf.Sqrt(1f - vn * vn);
                    float warp = Mathf.PerlinNoise(x * 0.02f + sd, y * 0.08f) * 5f;
                    float fib = Mathf.Abs(Mathf.Sin((vn * 3.2f + warp) * Mathf.PI));
                    float n1 = Mathf.PerlinNoise(x * 0.06f + sd, y * 0.25f), n2 = Mathf.PerlinNoise(x * 0.03f + 50f, y * 0.12f + sd);
                    Color c;
                    if (pale)
                    {
                        c = Color.Lerp(new Color(0.70f, 0.44f, 0.40f), new Color(0.90f, 0.70f, 0.64f), fib * 0.5f + n1 * 0.5f);
                        if (Mathf.Abs(Mathf.PerlinNoise(x * 0.05f, y * 0.2f + sd) - 0.5f) < 0.03f) c = Color.Lerp(c, new Color(0.55f, 0.08f, 0.10f), 0.6f);
                    }
                    else
                    {
                        c = Color.Lerp(new Color(0.36f, 0.02f, 0.04f), new Color(0.74f, 0.14f, 0.15f), fib * 0.55f + n1 * 0.45f);
                        c = Color.Lerp(c, new Color(0.84f, 0.62f, 0.56f), Mathf.Clamp01((n2 - 0.62f) * 4f) * 0.6f);   // fascia sheets
                    }
                    c = Color.Lerp(c, new Color(0.24f, 0.0f, 0.02f), Mathf.Clamp01((Mathf.PerlinNoise(x * 0.05f + 90f, y * 0.1f + sd) - 0.55f) * 3f) * 0.8f);
                    Color lit = c * (0.30f + 0.80f * sh);
                    lit += new Color(c.r * 0.2f, 0f, 0f) * (1f - sh);
                    float spec = Mathf.Exp(-Mathf.Pow((vn + 0.4f) / 0.16f, 2f)) * (0.3f + 0.9f * Mathf.PerlinNoise(x * 0.25f + sd, y * 0.5f));
                    lit += new Color(1f, 0.9f, 0.88f) * spec * 0.35f;
                    lit.a = alpha;
                    px[y * W + x] = lit;
                }
            var t = new Texture2D(W, Hh, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            t.SetPixels32(px); t.Apply(false);
            return t;
        }

        public void Tint(Color c) { if (lr) lr.startColor = lr.endColor = c; }

        void SetNeck(float m)
        {
            if (Mathf.Abs(m - neck) < 0.03f) return;
            neck = m;
            lr.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.15f, Mathf.Lerp(m, 1f, 0.45f)),
                new Keyframe(0.5f, m), new Keyframe(0.85f, Mathf.Lerp(m, 1f, 0.4f)), new Keyframe(1f, 0.9f));
        }

        void LateUpdate()
        {
            if (!joint || !a || !b) { Snap(); return; }
            Vector3 pa = a.transform.TransformPoint(la), pb = b.transform.TransformPoint(lb);
            float d = Vector2.Distance(pa, pb);
            if (d > maxLen * 1.8f + 0.12f) { Destroy(joint); Snap(); return; }
            // sags when slack, thins when stretched
            Vector3 ctl = (pa + pb) * 0.5f + Vector3.down * Mathf.Max(0, maxLen - d) * 0.9f;
            for (int k = 0; k < Pts; k++)
            {
                float t = k / (float)(Pts - 1), it = 1f - t;
                lr.SetPosition(k, it * it * pa + 2f * it * t * ctl + t * t * pb);
            }
            float stretch = Mathf.Clamp01((d - maxLen * 0.5f) / (maxLen * 1.5f + 0.01f));
            SetNeck(Mathf.Lerp(0.62f, 0.2f, stretch));

            // drip
            age += Time.deltaTime; drip -= Time.deltaTime;
            if (drip <= 0f && age < 25f && sortFrom)
            {
                drip = Random.Range(0.5f, 1.4f) * (1f + age / 10f);
                Vector3 low = lr.GetPosition(0);
                for (int k = 1; k < Pts; k++) { var p = lr.GetPosition(k); if (p.y < low.y) low = p; }
                Droplet.Create(low, new Vector2(Random.Range(-0.2f, 0.2f), -0.3f), sortFrom, Random.Range(0.4f, 0.8f));
            }
        }

        void Snap()
        {
            if (circ && lr) RealBlood.Bleed(circ, lr.GetPosition(Pts / 2), Random.insideUnitCircle, 0.4f);
            if (lr && sortFrom)
                for (int k = 0; k < 5; k++)
                    Droplet.Create(lr.GetPosition(Pts / 2), Random.insideUnitCircle * 2.5f, sortFrom, Random.Range(0.4f, 0.8f));
            Destroy(gameObject);
        }
    }

}
