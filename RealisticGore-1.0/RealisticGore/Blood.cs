using System.Collections.Generic;
using UnityEngine;

namespace Mod
{
    // blood drop, arcs and fades
    public class Droplet : MonoBehaviour
    {
        static Material mat;
        static int alive;
        Vector2 pos, vel; float life, max; LineRenderer lr;

        public static DecalDescriptor Decal;
        float age;
        public static void Create(Vector2 at, Vector2 vel, SpriteRenderer sortFrom) { Create(at, vel, sortFrom, 0f); }
        public static void Create(Vector2 at, Vector2 vel, SpriteRenderer sortFrom, float life)
        {
            if (alive > 250) return;
            if (!mat) mat = new Material(Shader.Find("Sprites/Default")) { mainTexture = DropTex() };
            var g = new GameObject("BloodDrop");
            var d = g.AddComponent<Droplet>();
            d.pos = at; d.vel = vel; d.max = d.life = life > 0 ? life : Random.Range(0.35f, 0.9f);
            d.lr = g.AddComponent<LineRenderer>();
            d.lr.sharedMaterial = mat; d.lr.textureMode = LineTextureMode.Stretch;
            d.lr.useWorldSpace = true; d.lr.positionCount = 2; d.lr.numCapVertices = 0;
            d.w = Random.value < 0.15f ? Random.Range(0.018f, 0.028f) : Random.Range(0.007f, 0.016f);
            d.lr.startWidth = d.lr.endWidth = d.w;
            d.lr.startColor = d.lr.endColor = Color.white;
            if (sortFrom) { d.lr.sortingLayerID = sortFrom.sortingLayerID; d.lr.sortingOrder = sortFrom.sortingOrder + 3; }
            alive++;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            life -= dt;
            if (life <= 0) { Destroy(gameObject); return; }
            vel += Physics2D.gravity * dt; vel *= 0.985f;
            Vector2 prev = pos; pos += vel * dt; age += dt;
            if (age > 0.1f)
            {
                var hit = Physics2D.Linecast(prev, pos);
                if (hit.collider)
                {
                    Destroy(gameObject); return;
                }
            }
            // tail stretches with speed
            float sp = vel.magnitude;
            Vector2 dir = sp > 1e-3f ? vel / sp : Vector2.down;
            float len = Mathf.Max(sp * dt * 2.5f, w * 1.6f);
            lr.SetPosition(0, pos); lr.SetPosition(1, pos - dir * len);
            float a = Mathf.Clamp01(life / max * 2f);
            lr.startColor = lr.endColor = new Color(1f, 1f, 1f, a);
        }

        float w;
        static Texture2D dropTex;
        static Texture2D DropTex()
        {
            if (dropTex) return dropTex;
            int W = 64, Hh = 16;
            var px = new Color32[W * Hh];
            for (int y = 0; y < Hh; y++)
                for (int x = 0; x < W; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f - Hh * 0.5f;
                    float hw = fx < 7f ? Mathf.Sqrt(Mathf.Max(0f, 49f - (fx - 7f) * (fx - 7f))) : 7f * Mathf.Pow(1f - (fx - 7f) / (W - 7f), 1.6f);
                    float av = Mathf.Abs(fy);
                    float al = Mathf.Clamp01((hw - av) * 1.5f);
                    if (al <= 0f) { px[y * W + x] = new Color32(0, 0, 0, 0); continue; }
                    float sh = Mathf.Sqrt(Mathf.Max(0f, 1f - (av / Mathf.Max(hw, 0.5f)) * (av / Mathf.Max(hw, 0.5f))));
                    Color c = Color.Lerp(new Color(0.62f, 0.04f, 0.07f), new Color(0.26f, 0.0f, 0.02f), sh * Mathf.Clamp01(hw / 5f));
                    float hx = fx - 5f, hy = fy - 2.5f;
                    c += new Color(1f, 0.9f, 0.9f) * Mathf.Exp(-(hx * hx + hy * hy) / 2.2f) * 0.75f;
                    c.a = al * Mathf.Lerp(0.75f, 1f, Mathf.Clamp01(hw / 4f));
                    px[y * W + x] = c;
                }
            dropTex = new Texture2D(W, Hh, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            dropTex.SetPixels32(px); dropTex.Apply(false);
            return dropTex;
        }

        void OnDestroy() { alive--; }
    }

    // kill vanilla blood on our bodies, ours replaces it
    public static class RealBlood
    {
        static DecalDescriptor none;
        // 1px clear stain so the game has something to paint
        public static DecalDescriptor None
        {
            get
            {
                if (none) return none;
                var t = new Texture2D(1, 1, TextureFormat.RGBA32, false); t.SetPixel(0, 0, new Color(0, 0, 0, 0)); t.Apply(false);
                none = ScriptableObject.CreateInstance<DecalDescriptor>();
                none.name = "NoBlood"; none.Color = new Color(0, 0, 0, 0); none.IgnoreRadius = 1000f;
                none.Sprites = new[] { Sprite.Create(t, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f) };
                return none;
            }
        }

        static readonly HashSet<int> done = new HashSet<int>();
        // hidden particles still cost physics
        public static void HideBleeder(GameObject g)
        {
            if (!g) return;
            foreach (var ps in g.GetComponentsInChildren<ParticleSystem>(true))
            {
                var em = ps.emission;
                if (em.enabled) { em.enabled = false; ps.Clear(); }
            }
            if (!done.Add(g.GetInstanceID())) return;
            foreach (var r in g.GetComponentsInChildren<ParticleSystemRenderer>(true)) r.enabled = false;
            var b = g.GetComponent<BleedingParticleBehaviour>();
            if (b && b.BloodSplatController) b.BloodSplatController.Decal = None;
        }

        public static void Bleed(CirculationBehaviour circ, Vector2 p, Vector2 d, float i)
        {
            if (!circ) return;
            if (GoreSettings.HideVanillaBlood && circ.GetComponent<Crater>()) return;
            circ.CreateBleedingParticle(p, d, i, false);
        }

        public static void HideImpacts(Vector2 near)
        {
            foreach (var bi in Object.FindObjectsOfType<BloodImpactBehaviour>())
            {
                if (((Vector2)bi.transform.position - near).sqrMagnitude > 1f) continue;
                foreach (var r in bi.GetComponentsInChildren<ParticleSystemRenderer>()) r.enabled = false;
                foreach (var ps in bi.GetComponentsInChildren<ParticleSystem>()) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                foreach (var f in bi.GetComponentsInChildren<FancyBloodSplatController>()) f.Decal = None;
                bi.StartCoroutine(ShowLater(bi));
            }
        }
        // impacts are pooled, normal humans want them back
        static System.Collections.IEnumerator ShowLater(BloodImpactBehaviour bi)
        {
            yield return new WaitForSeconds(3f);
            if (!bi) yield break;
            foreach (var r in bi.GetComponentsInChildren<ParticleSystemRenderer>(true)) r.enabled = true;
        }
    }
}
