using System.Collections.Generic;
using UnityEngine;

namespace Mod
{
    // limb drawn as a bendy mesh so joints fold like skin. physics untouched
    public class SoftSkin : MonoBehaviour
    {
        class Link
        {
            public Transform other; public LimbBehaviour owner;   // owner = limb that has the joint
            public AnchoredJoint2D joint; public Rigidbody2D otherRb;
            public Vector2 A, d; public float band, strength;
            public Matrix4x4 rel; public bool has, ever;
            public float[] w, fade; public Crater oc;
        }

        SpriteRenderer sr; Sprite built; Crater cr;
        GameObject go; MeshFilter mf; MeshRenderer mr; Mesh mesh;
        Vector3[] rest, now; Color[] col; Color lastCol = new Color(-1, 0, 0, 0); int lastMask = -1;
        readonly List<Link> links = new List<Link>();
        MaterialPropertyBlock mpb;
        static Material mat;
        static readonly int MainTex = Shader.PropertyToID("_MainTex");

        public static void Setup(PersonBehaviour person)
        {
            if (!GoreSettings.SoftSkin || !person) return;
            var all = new List<LimbBehaviour>();
            foreach (var l in person.Limbs) if (l && l.GetComponent<Crater>()) all.Add(l);
            foreach (var l in all)
            {
                var s = l.GetComponent<SoftSkin>() ?? l.gameObject.AddComponent<SoftSkin>();
                s.links.Clear();
            }
            foreach (var l in all)
            {
                var j = l.Joint as AnchoredJoint2D;
                if (!j || !j.connectedBody) continue;
                var p = j.connectedBody.GetComponent<LimbBehaviour>();
                if (!p || !p.GetComponent<SoftSkin>()) continue;
                string n = l.name.ToLower();
                bool branch = n.Contains("upperarm") || n.Contains("upperleg");
                l.GetComponent<SoftSkin>().Add(l, j, j.anchor, p.transform, j.connectedBody, 1f);
                // torso can't follow both arms, so only the limb stretches
                p.GetComponent<SoftSkin>().Add(l, j, j.connectedAnchor, l.transform, l.GetComponent<Rigidbody2D>(), branch ? 0f : 1f);
            }
        }

        // cut-off piece
        public static void Chunk(Crater piece, Rigidbody2D prb, List<HingeJoint2D> joints)
        {
            if (!GoreSettings.SoftSkin || !piece) return;
            var ps = piece.GetComponent<SoftSkin>() ?? piece.gameObject.AddComponent<SoftSkin>();
            foreach (var nj in joints)
            {
                if (!nj) continue;
                var krb = nj.GetComponent<Rigidbody2D>();
                var ks = nj.GetComponent<SoftSkin>();
                // game thinks these are severed, they're still on the piece
                if (ks) ks.Add(null, nj, nj.anchor, piece.transform, prb, 1f);
                ps.Add(null, nj, nj.connectedAnchor, nj.transform, krb, 1f);
            }
        }

        void Add(LimbBehaviour owner, AnchoredJoint2D j, Vector2 anchorLocal, Transform other, Rigidbody2D otherRb, float strength)
        {
            links.Add(new Link { owner = owner, joint = j, A = anchorLocal, other = other, otherRb = otherRb, strength = strength, oc = other.GetComponent<Crater>() });
            built = null;   // rebuild weights
        }

        void Start()
        {
            sr = GetComponent<SpriteRenderer>(); cr = GetComponent<Crater>();
            var lb = GetComponent<LimbBehaviour>();
            if (lb && lb.SkinMaterialHandler && lb.SkinMaterialHandler.renderer) sr = lb.SkinMaterialHandler.renderer as SpriteRenderer ?? sr;
            if (!sr) { Destroy(this); return; }
            if (!mat) mat = new Material(Shader.Find("Sprites/Default"));
            go = new GameObject("SoftSkin");
            go.transform.SetParent(sr.transform, false);
            mf = go.AddComponent<MeshFilter>(); mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mesh = new Mesh(); mesh.MarkDynamic(); mf.sharedMesh = mesh;
            mpb = new MaterialPropertyBlock();
        }

        void OnDestroy()
        {
            if (go) Destroy(go); if (mesh) Destroy(mesh);
            if (sr) sr.forceRenderingOff = false;
        }

        void Build(Sprite s)
        {
            built = s;
            var b = s.bounds;   // local space of the renderer
            bool tall = b.size.y >= b.size.x;
            int nx = tall ? 7 : 18, ny = tall ? 18 : 7;
            rest = new Vector3[nx * ny]; now = new Vector3[rest.Length]; col = new Color[rest.Length];
            var uv = new Vector2[rest.Length];
            var r = s.textureRect; float tw = s.texture.width, th = s.texture.height;
            for (int y = 0; y < ny; y++)
                for (int x = 0; x < nx; x++)
                {
                    float fx = x / (nx - 1f), fy = y / (ny - 1f);
                    rest[y * nx + x] = new Vector3(Mathf.Lerp(b.min.x, b.max.x, fx), Mathf.Lerp(b.min.y, b.max.y, fy), 0f);
                    uv[y * nx + x] = new Vector2((r.x + fx * r.width) / tw, (r.y + fy * r.height) / th);
                }
            var tri = new int[(nx - 1) * (ny - 1) * 6]; int t = 0;
            for (int y = 0; y < ny - 1; y++)
                for (int x = 0; x < nx - 1; x++)
                {
                    int i = y * nx + x;
                    tri[t++] = i; tri[t++] = i + nx; tri[t++] = i + 1;
                    tri[t++] = i + 1; tri[t++] = i + nx; tri[t++] = i + nx + 1;
                }
            mesh.Clear();
            mesh.vertices = rest; mesh.uv = uv; mesh.triangles = tri;
            lastCol = new Color(-1, 0, 0, 0); lastMask = -1;

            // 0 inside, .5 at the joint, 1 past it
            Vector2 c = b.center;
            float thick = Mathf.Min(b.size.x, b.size.y);
            var srT = sr.transform; var me = transform;
            foreach (var L in links)
            {
                Vector2 A = srT == me ? L.A : (Vector2)srT.InverseTransformPoint(me.TransformPoint(L.A));
                Vector2 d = A - c; d = d.sqrMagnitude > 1e-8f ? d.normalized : Vector2.down;
                L.d = d; L.band = thick * 0.55f; L.has = false;
                L.w = new float[rest.Length];
                for (int i = 0; i < rest.Length; i++)
                {
                    Vector2 p = rest[i];
                    float tt = Vector2.Dot(p - A, d) / L.band;
                    float w = Mathf.Clamp01((tt + 1f) * 0.5f); w = w * w * (3f - 2f * w);
                    float side = Mathf.Abs(Vector2.Dot(p - A, new Vector2(-d.y, d.x))) / (thick * 1.3f);
                    w *= Mathf.Clamp01(1.5f - side);
                    L.w[i] = w * L.strength;
                }
                // top part melts into the one under it, no hard edge
                L.fade = null;
                var os = L.oc ? L.oc.Sr : null;
                if (os && os.sortingLayerID == sr.sortingLayerID && os.sortingOrder < sr.sortingOrder)
                {
                    L.fade = new float[rest.Length];
                    for (int i = 0; i < rest.Length; i++)
                    {
                        float f = 1f;
                        if (L.w[i] > 0.35f && L.oc.Covers(srT.TransformPoint(rest[i])))
                            f = 1f - Mathf.Clamp01((L.w[i] - 0.35f) / 0.5f);
                        L.fade[i] = f * f * (3f - 2f * f);
                    }
                }
                L.A = A;
            }
        }

        public void Joined(List<Crater> o)
        {
            foreach (var L in links) if (L.has && L.oc && Alive(L) && !o.Contains(L.oc)) o.Add(L.oc);
        }

        bool Alive(Link L)
        {
            var j = L.joint;
            if (!j || !j.enabled || !L.other || !L.otherRb) return false;
            if (L.owner && L.owner.IsDismembered) return false;
            if (j.connectedBody == null) return false;
            return true;
        }

        void LateUpdate()
        {
            if (!mesh || !sr) return;
            var s = sr.sprite;
            if (!s) { mr.enabled = false; sr.forceRenderingOff = false; return; }
            if (s != built) Build(s);
            var srT = sr.transform;

            float tot = 0f;
            for (int k = 0; k < links.Count; k++)
            {
                var L = links[k];
                if (!Alive(L)) { L.has = false; continue; }
                // rest shape = straight joint
                var h = L.joint as HingeJoint2D;
                bool straight = h ? Mathf.Abs(h.jointAngle) < 4f : !L.has;
                if (straight || !L.has)
                {
                    if (straight || L.rel == default(Matrix4x4)) { L.rel = L.other.worldToLocalMatrix * srT.localToWorldMatrix; L.has = true; }
                }
                if (L.has) { tot += 1f; L.ever = true; }
            }

            if (tot == 0f) { System.Array.Copy(rest, now, rest.Length); }
            else
            {
                var toMe = srT.worldToLocalMatrix;
                for (int i = 0; i < rest.Length; i++) now[i] = rest[i];
                // capped so it never overshoots
                for (int i = 0; i < rest.Length; i++)
                {
                    Vector3 p = rest[i], acc = Vector3.zero; float ws = 0f;
                    for (int k = 0; k < links.Count; k++)
                    {
                        var L = links[k]; if (!L.has || L.w == null) continue;
                        float w = L.w[i]; if (w <= 0f) continue;
                        ws += w;
                    }
                    if (ws <= 0f) continue;
                    float sc = ws > 1f ? 1f / ws : 1f;
                    for (int k = 0; k < links.Count; k++)
                    {
                        var L = links[k]; if (!L.has || L.w == null) continue;
                        float w = L.w[i] * sc; if (w <= 0f) continue;
                        Vector3 q = toMe.MultiplyPoint3x4(L.other.localToWorldMatrix.MultiplyPoint3x4(L.rel.MultiplyPoint3x4(p)));
                        acc += (q - p) * w;
                    }
                    now[i] = p + acc;
                }
            }
            mesh.vertices = now; mesh.RecalculateBounds();

            Color c = sr.color;
            int mask = 0;
            for (int k = 0; k < links.Count && k < 30; k++) if ((links[k].has || links[k].ever) && links[k].fade != null) mask |= 1 << k;
            if (c != lastCol || mask != lastMask)
            {
                for (int i = 0; i < col.Length; i++)
                {
                    float a = 1f;
                    for (int k = 0; k < links.Count && k < 30; k++) if ((mask & (1 << k)) != 0) a = Mathf.Min(a, links[k].fade[i]);
                    col[i] = new Color(c.r, c.g, c.b, c.a * a);
                }
                mesh.colors = col; lastCol = c; lastMask = mask;
            }
            mr.GetPropertyBlock(mpb); mpb.SetTexture(MainTex, s.texture); mr.SetPropertyBlock(mpb);
            mr.sortingLayerID = sr.sortingLayerID; mr.sortingOrder = sr.sortingOrder;
            mr.enabled = sr.enabled && sr.gameObject.activeInHierarchy;
            go.transform.localPosition = Vector3.zero;
            sr.forceRenderingOff = true;
        }
    }
}
