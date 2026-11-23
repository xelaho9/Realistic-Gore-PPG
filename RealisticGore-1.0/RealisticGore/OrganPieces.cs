using System.Collections.Generic;
using UnityEngine;

namespace Mod
{
    public class PieceShadow : MonoBehaviour
    {
        SpriteRenderer src, me;
        static readonly Vector3 off = new Vector3(0.018f, -0.026f, 0f);

        public static void Add(SpriteRenderer r)
        {
            if (!r) return;
            var g = new GameObject("Shadow");
            g.transform.SetParent(r.transform, false);
            var s = g.AddComponent<PieceShadow>();
            s.src = r;
            s.me = g.AddComponent<SpriteRenderer>();
            s.me.sprite = r.sprite; s.me.sharedMaterial = r.sharedMaterial;
            s.me.color = new Color(0f, 0f, 0f, 0.5f);
            s.me.sortingLayerID = r.sortingLayerID; s.me.sortingOrder = r.sortingOrder - 1;
            s.LateUpdate();
        }

        void LateUpdate()
        {
            if (!src) { Destroy(gameObject); return; }
            if (me.sprite != src.sprite) me.sprite = src.sprite;
            me.flipX = src.flipX; me.flipY = src.flipY;
            transform.position = src.transform.position + off;
            transform.rotation = src.transform.rotation;
            float a = 0.5f * src.color.a;
            if (Mathf.Abs(me.color.a - a) > 0.01f) me.color = new Color(0f, 0f, 0f, a);
        }
    }

    // spilled organ. hearts beat for a bit, then it all rots
    public class RealOrgan : MonoBehaviour
    {
        static readonly Queue<GameObject> all = new Queue<GameObject>();
        static Material mat;
        public string kind; public PersonBehaviour person; public CirculationBehaviour circ;
        SpriteRenderer sr; Rigidbody2D rb; Vector3 baseScale;
        float age, drip;

        public static RealOrgan Spawn(string kind, Vector2 at, float size, float angle, Rigidbody2D from, PhysicalBehaviour srcPhys, SpriteRenderer sortFrom,
            List<Collider2D> body, PersonBehaviour person, CirculationBehaviour circ, Vector2 vel, bool tether)
        {
            if (size < 0.012f) return null;
            while (all.Count >= Mathf.Max(1, GoreSettings.PieceCap)) { var old = all.Dequeue(); if (old) Destroy(old); }
            if (!mat) mat = new Material(Shader.Find("Sprites/Default"));
            var spr = OrganArt.Get(kind);
            var go = ModAPI.CreatePhysicalObject(kind == "gib" ? "flesh" : kind, spr);
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            go.transform.localScale = new Vector3(size, size, 1f);
            var r = go.GetComponent<SpriteRenderer>();
            if (r)
            {
                r.sprite = spr; r.sharedMaterial = mat;
                if (sortFrom) { r.sortingLayerID = sortFrom.sortingLayerID; r.sortingOrder = sortFrom.sortingOrder + 4; }
                PieceShadow.Add(r);
            }
            var ol = go.transform.Find("Outline"); if (ol) Destroy(ol.gameObject);
            var pb = go.GetComponent<PhysicalBehaviour>();
            if (pb && srcPhys) pb.Properties = srcPhys.Properties;
            foreach (var oc in go.GetComponents<Collider2D>()) DestroyImmediate(oc);
            var poly = go.AddComponent<PolygonCollider2D>();
            if (poly.pathCount == 0 || poly.GetTotalPointCount() < 3) { DestroyImmediate(poly); go.AddComponent<CircleCollider2D>().radius = 0.35f; }
            if (pb) pb.ResetColliderArray();

            var crb = go.GetComponent<Rigidbody2D>();
            float m = Mathf.Clamp(size * size * 8f, 0.03f, 1.5f);
            crb.mass = m;
            if (pb) { pb.TrueInitialMass = m; pb.InitialMass = m; }
            crb.velocity = vel; crb.angularVelocity = Random.Range(-120f, 120f);
            var mine = go.GetComponents<Collider2D>();
            if (body != null) foreach (var a in mine) foreach (var b in body) if (a && b) Physics2D.IgnoreCollision(a, b);

            var o = go.AddComponent<RealOrgan>();
            o.kind = kind; o.person = person; o.circ = circ; o.sr = r; o.rb = crb; o.baseScale = go.transform.localScale;

            if (tether && from && kind == "eye")
            {
                // optic nerve
                Vector2 back = go.transform.TransformPoint(new Vector3(-0.42f, 0f, 0f));
                Strand.Create(from, crb, at, back, size * 1.8f, 300f, size * 0.09f, new Color(0.90f, 0.80f, 0.68f), r, circ);
            }
            all.Enqueue(go);
            return o;
        }

        static float Sq(float v) { return v * v; }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;

            drip -= dt;
            if (drip <= 0f && age < (kind == "gib" ? 15f : 45f))
            {
                drip = Random.Range(0.25f, 0.7f) * (1f + age / 15f);
                Vector2 p = (Vector2)sr.bounds.center + new Vector2(Random.Range(-0.4f, 0.4f) * sr.bounds.extents.x, -sr.bounds.extents.y * 0.6f);
                Droplet.Create(p, new Vector2(Random.Range(-0.3f, 0.3f), -0.4f) + rb.velocity, sr, Random.Range(0.3f, 0.7f));
            }

            // rot
            if (age > 20f && sr)
            {
                float rot = Mathf.Clamp01((age - 20f) / 240f);
                sr.color = Color.Lerp(Color.white, new Color(0.45f, 0.40f, 0.30f), rot * 0.85f);
            }
        }

        void Shot(Shot s)
        {
            if (!sr) return;
            for (int k = 0; k < 10; k++)
                Droplet.Create(s.point, (-s.normal + Random.insideUnitCircle * 0.6f).normalized * Random.Range(2f, 6f), sr, Random.Range(0.4f, 0.9f));
        }
    }
}
