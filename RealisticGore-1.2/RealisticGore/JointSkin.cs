using UnityEngine;

namespace Mod
{
    // fills the wedge gap at bent joints
    public class JointSkin : MonoBehaviour
    {
        const int M = 24, K = 12, PerSide = 2 * K + 3;
        Crater me, par; LimbBehaviour limb; Rigidbody2D parRb;
        GameObject go; MeshRenderer mr; Mesh mesh;
        Color[] ramp = new Color[M + 1]; float[] rampW = new float[M + 1];
        float tick; bool ok, hasPar;
        float cP, cM, pP, pM;
        Color cIn, cMid, cOut;
        Vector3[] v = new Vector3[PerSide * 2]; Color[] vc = new Color[PerSide * 2];
        static Material mat;

        void Start()
        {
            me = GetComponent<Crater>(); limb = GetComponent<LimbBehaviour>();
            if (!me || !limb) { Destroy(this); return; }
            go = new GameObject("JointSkin");
            go.AddComponent<MeshFilter>().sharedMesh = mesh = new Mesh();
            mesh.MarkDynamic();
            mr = go.AddComponent<MeshRenderer>();
            if (!mat) mat = new Material(Shader.Find("Sprites/Default"));
            mr.sharedMaterial = mat;
            mesh.vertices = v; mesh.colors = vc;
            var tri = new int[2 * 9 * K]; int t = 0;
            for (int s = 0; s < 2; s++)
            {
                int b = s * PerSide;
                for (int k = 0; k < K; k++)
                {
                    int m0 = b + 1 + k, m1 = b + 2 + k, r0 = b + K + 2 + k, r1 = b + K + 3 + k;
                    tri[t++] = b; tri[t++] = m0; tri[t++] = m1;
                    tri[t++] = m0; tri[t++] = r0; tri[t++] = r1;
                    tri[t++] = m0; tri[t++] = r1; tri[t++] = m1;
                }
            }
            mesh.triangles = tri;
            mr.enabled = false;
            tick = Random.Range(0.05f, 0.3f);
        }

        void OnDestroy() { if (go) Destroy(go); if (mesh) Destroy(mesh); }

        void LateUpdate()
        {
            if (!mr) return;
            var j = limb ? limb.Joint : null;
            if (!j || !j.enabled || limb.IsDismembered || !j.connectedBody || !me.Ready) { mr.enabled = false; ok = false; return; }
            if (j.connectedBody != parRb) { parRb = j.connectedBody; par = parRb.GetComponent<Crater>(); tick = 0f; }
            Vector2 P = transform.TransformPoint(j.anchor);
            tick -= Time.deltaTime;
            if (tick <= 0f) { tick = 0.5f; ok = Resample(P); }
            mr.enabled = ok;
            if (!ok) return;

            Vector2 ac = me.AcrossDir, ap = ac;
            float p1 = cP, p2 = cM;
            if (hasPar)
            {
                ap = par.AcrossDir; p1 = pP; p2 = pM;
                if (Vector2.Dot(ap, ac) < 0f) { ap = -ap; p1 = pM; p2 = pP; }
            }
            float z = transform.position.z;
            Side(0, P, ac * cP, ap * p1, z);
            Side(1, P, -ac * cM, -ap * p2, z);
            mesh.vertices = v; mesh.colors = vc; mesh.RecalculateBounds();

            var a = me.Sr; var bs = hasPar ? par.Sr : null;
            mr.sortingLayerID = a.sortingLayerID;
            int o = a.sortingOrder;
            if (bs && bs.sortingLayerID == a.sortingLayerID) o = Mathf.Min(o, bs.sortingOrder);
            mr.sortingOrder = o - 1;
        }

        void Side(int s, Vector2 P, Vector2 e1, Vector2 e2, float z)
        {
            int b = s * PerSide;
            float a1 = Mathf.Atan2(e1.y, e1.x) * Mathf.Rad2Deg;
            float a2 = Mathf.Atan2(e2.y, e2.x) * Mathf.Rad2Deg;
            float da = Mathf.DeltaAngle(a1, a2);
            float r1 = e1.magnitude, r2 = e2.magnitude;
            float bulge = 0.08f * Mathf.Clamp01(Mathf.Abs(da) / 90f);
            v[b] = new Vector3(P.x, P.y, z); vc[b] = cIn;
            for (int k = 0; k <= K; k++)
            {
                float f = k / (float)K;
                float ang = (a1 + da * f) * Mathf.Deg2Rad;
                float r = Mathf.Lerp(r1, r2, f) * (1f + bulge * Mathf.Sin(f * Mathf.PI));
                Vector2 d = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                Vector2 m = P + d * r * 0.6f, o = P + d * r;
                v[b + 1 + k] = new Vector3(m.x, m.y, z); vc[b + 1 + k] = cMid;
                v[b + K + 2 + k] = new Vector3(o.x, o.y, z); vc[b + K + 2 + k] = cOut;
            }
        }

        bool Spans(Crater c, Vector2 P, out float sPlus, out float sMinus)
        {
            sPlus = sMinus = 0f;
            Vector2 a = c.AcrossDir, lng = new Vector2(-a.y, a.x);
            if (Vector2.Dot((Vector2)c.Sr.bounds.center - P, lng) < 0f) lng = -lng;
            float step = c.PxWorld * 2f; Color32 col; Vector2 Q = P; bool found = false;
            for (int k = 0; k < 160; k++)
            {
                Q = P + lng * (step * k);
                if (c.Probe(Q, out col) >= 0) { found = true; break; }
            }
            if (!found) return false;
            sPlus = Edge(c, Q, a, step); sMinus = Edge(c, Q, -a, step);
            if (sPlus + sMinus < step * 4f) return false;
            AddRamp(c, Q, a, sPlus); AddRamp(c, Q, -a, sMinus);
            return true;
        }

        static float Edge(Crater c, Vector2 Q, Vector2 dir, float step)
        {
            Color32 col;
            for (int k = 1; k < 1200; k++)
                if (c.Probe(Q + dir * (step * k), out col) < 0) return step * k;   // holes (6) count as inside
            return step * 1200;
        }

        void AddRamp(Crater c, Vector2 Q, Vector2 dir, float span)
        {
            Color32 col;
            for (int k = 0; k <= M; k++)
            {
                int lv = c.Probe(Q + dir * (span * Mathf.Min(k / (float)M, 0.95f)), out col);
                if (lv < 0 || lv >= 3) continue;   // skin only, not wound flesh
                ramp[k] += (Color)col; rampW[k] += 1f;
            }
        }

        bool Resample(Vector2 P)
        {
            for (int k = 0; k <= M; k++) { ramp[k] = Color.clear; rampW[k] = 0f; }
            if (!Spans(me, P, out cP, out cM)) return false;
            hasPar = par && par.Ready && Spans(par, P, out pP, out pM);
            if (hasPar)
            {
                // narrower part sets the size
                float c1 = Mathf.Min(cP, pP * 1.15f), p1 = Mathf.Min(pP, cP * 1.15f);
                float c2 = Mathf.Min(cM, pM * 1.15f), p2 = Mathf.Min(pM, cM * 1.15f);
                cP = c1; pP = p1; cM = c2; pM = p2;
            }
            Color last = Color.clear; bool have = false;
            for (int k = 0; k <= M; k++)
            {
                if (rampW[k] > 0f) { ramp[k] /= rampW[k]; last = ramp[k]; have = true; }
                else if (have) ramp[k] = last;
            }
            if (!have) return false;
            Color first = last; for (int k = 0; k <= M; k++) if (rampW[k] > 0f) { first = ramp[k]; break; }
            for (int k = 0; k <= M && rampW[k] <= 0f; k++) ramp[k] = first;
            cIn = ramp[0]; cMid = ramp[(int)(M * 0.6f)]; cOut = ramp[(int)(M * 0.93f)];
            cIn.a = cMid.a = cOut.a = 1f;
            return true;
        }
    }
}
