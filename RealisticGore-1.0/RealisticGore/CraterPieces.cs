using System.Collections.Generic;
using UnityEngine;

namespace Mod
{
    public partial class Crater
    {
        // hanging flesh
        class Tag { public Vector2 local; public Vector2[] p, prev; public float seg; public LineRenderer lr; }
        readonly List<Tag> tags = new List<Tag>();
        readonly Queue<GameObject> wounds = new Queue<GameObject>();
        static Material mat;

        void SpawnTags(Vector2 point, Vector2 normal)
        {
            if (wounds.Count >= 5) { var old = wounds.Dequeue(); if (old) Destroy(old); }
            var w = new GameObject("Crater"); w.transform.SetParent(transform, false); wounds.Enqueue(w);
            int count = Random.Range(1, 4);
            for (int k = 0; k < count; k++)
            {
                bool nerve = k == 0 && Random.value < 0.4f;
                var t = new Tag();
                t.local = transform.InverseTransformPoint(point + Random.insideUnitCircle * 0.02f);
                t.seg = nerve ? Random.Range(0.01f, 0.017f) : Random.Range(0.007f, 0.014f);
                int pts = 6;
                t.p = new Vector2[pts]; t.prev = new Vector2[pts];
                Vector2 start = transform.TransformPoint(t.local);
                Vector2 dir = (-normal + Random.insideUnitCircle * 0.8f).normalized;
                for (int q = 0; q < pts; q++) t.p[q] = t.prev[q] = start + dir * t.seg * q;
                var g = new GameObject(nerve ? "Nerve" : "Flesh"); g.transform.SetParent(w.transform, false);
                t.lr = g.AddComponent<LineRenderer>();
                t.lr.material = mat; t.lr.useWorldSpace = true; t.lr.positionCount = pts; t.lr.numCapVertices = 3;
                if (nerve) { t.lr.startWidth = 0.003f; t.lr.endWidth = 0.0015f; t.lr.startColor = new Color(0.62f, 0.38f, 0.30f); t.lr.endColor = new Color(0.80f, 0.68f, 0.52f); }
                else { t.lr.startWidth = Random.Range(0.006f, 0.011f); t.lr.endWidth = 0.004f; t.lr.startColor = new Color(0.38f, 0.03f, 0.05f); t.lr.endColor = new Color(0.55f, 0.07f, 0.09f); }
                if (sr) { t.lr.sortingLayerID = sr.sortingLayerID; t.lr.sortingOrder = sr.sortingOrder + 1; }
                tags.Add(t);
            }
        }

        // loose bits fall, attached bits stay
        bool Op(int i) { return cur[i].a > 0 && level[i] != 7; }

        static int Nb(int i, int k, int W, int N)
        {
            if (k == 0) return i % W == 0 ? -1 : i - 1;
            if (k == 1) return i % W == W - 1 ? -1 : i + 1;
            if (k == 2) return i < W ? -1 : i - W;
            return i >= N - W ? -1 : i + W;
        }

        int CompNear(Vector2 p, int[] comp, float rad)
        {
            int W = L.w, Hh = L.h, best = 0; float bd = float.MaxValue;
            int cx = (int)p.x, cy = (int)p.y, r = Mathf.CeilToInt(rad), st = Mathf.Max(1, L.S / 4);
            for (int y = cy - r; y <= cy + r; y += st)
                for (int x = cx - r; x <= cx + r; x += st)
                {
                    if (x < 0 || y < 0 || x >= W || y >= Hh) continue;
                    int c = comp[y * W + x]; if (c <= 0) continue;
                    float d = (x - p.x) * (x - p.x) + (y - p.y) * (y - p.y);
                    if (d < bd) { bd = d; best = c; }
                }
            return best;
        }

        float splitCd;

        void Split()
        {
            int W = L.w, Hh = L.h, N = W * Hh;
            if (fComp == null || fComp.Length != N) { fComp = new int[N]; fQueue = new int[N]; fSolid = new bool[N]; }
            int[] comp = fComp, q = fQueue; bool[] solid = fSolid;
            System.Array.Clear(comp, 0, N);
            // thin skin bridges don't hold, bone does
            int t = Mathf.Max(1, Mathf.RoundToInt(L.S * 0.3f));
            for (int y = 0; y < Hh; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    if (!Op(i)) { solid[i] = false; continue; }
                    if (level[i] >= 4 && level[i] <= 5 && IsBone(L.bone[i])) { solid[i] = true; continue; }
                    solid[i] = x >= t && y >= t && x < W - t && y < Hh - t && Op(i - t) && Op(i + t) && Op(i - t * W) && Op(i + t * W);
                }

            var sizes = new List<int> { 0 };
            int id = 0;
            for (int s0 = 0; s0 < N; s0++)
            {
                if (!solid[s0] || comp[s0] != 0) continue;
                id++; int head = 0, tail = 0; q[tail++] = s0; comp[s0] = id;
                while (head < tail)
                {
                    int i = q[head++];
                    for (int k = 0; k < 4; k++)
                    {
                        int j = Nb(i, k, W, N);
                        if (j < 0 || !solid[j] || comp[j] != 0) continue;
                        comp[j] = id; q[tail++] = j;
                    }
                }
                sizes.Add(tail);
            }
            if (id <= 1) return;

            // main piece keeps the joint, else biggest
            var rb = GetComponent<Rigidbody2D>();
            int main = 0;
            foreach (var j in GetComponents<AnchoredJoint2D>())
            {
                if (!j || !j.enabled || !j.connectedBody) continue;
                Vector2 ap = ToPx(transform.TransformPoint(j.anchor));
                int c = CompNear(ap, comp, L.S * 4f);
                if (c == 0) c = CompNear(ap, comp, Mathf.Max(L.w, L.h));
                if (c > 0 && (main == 0 || sizes[c] > sizes[main])) main = c;
            }
            if (main == 0) for (int c = 1; c <= id; c++) if (main == 0 || sizes[c] > sizes[main]) main = c;

            int hd = 0, tl = 0;
            for (int i = 0; i < N; i++) if (comp[i] > 0) q[tl++] = i;
            while (hd < tl)
            {
                int i = q[hd++], c = comp[i];
                for (int k = 0; k < 4; k++)
                {
                    int j = Nb(i, k, W, N);
                    if (j < 0 || comp[j] != 0 || cur[j].a == 0) continue;
                    comp[j] = c; q[tl++] = j;
                }
            }

            int n1 = id + 1;
            int[] mnx = new int[n1], mny = new int[n1], mxx = new int[n1], mxy = new int[n1], cnt = new int[n1];
            bool[] cutEdge = new bool[n1];
            for (int c = 0; c < n1; c++) { mnx[c] = mny[c] = int.MaxValue; mxx[c] = mxy[c] = -1; }
            int total = 0;
            for (int y = 0; y < Hh; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    if (cur[i].a == 0) continue;
                    int c = comp[i];
                    if (c == 0) { cur[i] = new Color32(0, 0, 0, 0); level[i] = 6; continue; }   // loose specks
                    total++; cnt[c]++;
                    if (level[i] >= 4) cutEdge[c] = true;
                    if (x < mnx[c]) mnx[c] = x; if (x > mxx[c]) mxx[c] = x;
                    if (y < mny[c]) mny[c] = y; if (y > mxy[c]) mxy[c] = y;
                }

            var order = new List<int>();
            for (int c = 1; c <= id; c++) if (c != main && cnt[c] > 0 && cutEdge[c]) order.Add(c);
            order.Sort((a, b) => cnt[b].CompareTo(cnt[a]));
            int minA = 24 * L.S * L.S;   // crumbs just vanish

            BodyCols();
            List<AnchoredJoint2D> kids = null;
            bool made = false;
            bool[] erase = null;
            for (int o = 0; o < order.Count; o++)
            {
                int c = order[o];
                // flesh alone can't drop half a limb
                if (cnt[c] > total * 0.3f && !cutBone && !(limb && limb.IsDismembered)) continue;
                if (o >= GoreSettings.ChunkCap && cnt[c] >= minA) continue;   // over the cap, leave it on
                if (cnt[c] < minA)
                {
                    if (erase == null) erase = new bool[n1];
                    erase[c] = true;
                    continue;
                }
                if (kids == null)
                {
                    kids = new List<AnchoredJoint2D>();
                    if (rb) foreach (var j in FindObjectsOfType<AnchoredJoint2D>()) if (j && j.connectedBody == rb && j.gameObject != gameObject) kids.Add(j);
                }
                MakeChunk(c, mnx[c], mny[c], mxx[c], mxy[c], cnt[c] / (float)Mathf.Max(1, total), kids, rb);
                made = true;
            }
            if (erase != null)
                for (int i = 0; i < N; i++) { int c = comp[i]; if (c > 0 && erase[c] && cur[i].a > 0) { cur[i] = new Color32(0, 0, 0, 0); level[i] = 6; } }
            if (made) ShrinkCollider(mnx[main], mny[main], mxx[main], mxy[main]);
            dirty = true;
        }

        void MakeChunk(int id, int bx0, int by0, int bx1, int by1, float frac, List<AnchoredJoint2D> kids, Rigidbody2D rb)
        {
            int W = L.w, cw = bx1 - bx0 + 1, ch = by1 - by0 + 1, n = cw * ch;
            var sub = new RealisticHuman.Layers { w = cw, h = ch, S = L.S, pad = 0, skin = new Color32[n], flesh = new Color32[n], bone = new Color32[n] };
            var c2 = new Color32[n]; var l2 = new byte[n];
            for (int y = 0; y < ch; y++)
                for (int x = 0; x < cw; x++)
                {
                    int i = (y + by0) * W + x + bx0, k = y * cw + x;
                    if (fComp[i] != id || cur[i].a == 0) continue;
                    sub.skin[k] = L.skin[i]; sub.flesh[k] = L.flesh[i]; sub.bone[k] = L.bone[i];
                    c2[k] = cur[i]; l2[k] = level[i];
                    cur[i] = new Color32(0, 0, 0, 0); level[i] = 6; MarkLost(i);
                }
            var tx = new Texture2D(cw, ch, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            tx.SetPixels32(c2); tx.Apply(false);
            Vector2 pv = pivotPx - new Vector2(bx0, by0);
            var spr = Sprite.Create(tx, new Rect(0, 0, cw, ch), new Vector2(pv.x / cw, pv.y / ch), ppuHi, 0, SpriteMeshType.FullRect, Vector4.zero, true);

            var go = ModAPI.CreatePhysicalObject(name + " piece", spr);
            // real pose, or it mirrors facing left
            Vector2 rx = transform.TransformVector(Vector3.right), uy = transform.TransformVector(Vector3.up);
            float sx = rx.magnitude * ((rx.x * uy.y - rx.y * uy.x) < 0f ? -1f : 1f);
            Vector2 dx = rx / sx;
            go.transform.position = transform.position;
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dx.y, dx.x) * Mathf.Rad2Deg);
            go.transform.localScale = new Vector3(sx, uy.magnitude, 1f);
            var r = go.GetComponent<SpriteRenderer>();
            if (r) { r.sprite = spr; r.sharedMaterial = mat; r.sortingLayerID = sr.sortingLayerID; r.sortingOrder = sr.sortingOrder + 2; r.color = sr.color; }
            var ol = go.transform.Find("Outline"); if (ol) Destroy(ol.gameObject);
            var pb = go.GetComponent<PhysicalBehaviour>();
            if (pb && phys) pb.Properties = phys.Properties;

            foreach (var oc in go.GetComponents<Collider2D>()) DestroyImmediate(oc);
            var poly = go.AddComponent<PolygonCollider2D>();
            Vector2 lo = (new Vector2(bx0, by0) - pivotPx) / ppuHi, hi = (new Vector2(bx1 + 1, by1 + 1) - pivotPx) / ppuHi;
            if (poly.pathCount == 0 || poly.GetTotalPointCount() < 3)
            {
                DestroyImmediate(poly);
                var bc = go.AddComponent<BoxCollider2D>();
                bc.offset = (lo + hi) * 0.5f; bc.size = hi - lo;
            }
            if (pb) pb.ResetColliderArray();

            var crb = go.GetComponent<Rigidbody2D>();
            float m = Mathf.Max(0.02f, (rb ? rb.mass : 1f) * frac);
            crb.mass = m;
            if (pb) { pb.TrueInitialMass = m; pb.InitialMass = m; }
            Vector2 mid = go.transform.TransformPoint((lo + hi) * 0.5f);
            if (rb) { crb.velocity = rb.GetPointVelocity(mid); crb.angularVelocity = rb.angularVelocity; }
            Vector2 away = mid - (Vector2)sr.bounds.center;
            if (away.sqrMagnitude < 1e-6f) away = Random.insideUnitCircle;
            crb.velocity += away.normalized * Random.Range(0.8f, 1.5f);
            crb.angularVelocity += Random.Range(-90f, 90f);
            if (r) PieceShadow.Add(r);

            var mine = go.GetComponents<Collider2D>();
            foreach (var a in mine) foreach (var b in bodyCols) if (a && b) Physics2D.IgnoreCollision(a, b);

            var cr = go.AddComponent<Crater>();
            cr.InitChunk(sub, c2, l2, tx, r, ppuHi, pv, Vert ? 1 : 2);
            cr.bodyCols = new List<Collider2D>(bodyCols); cr.bodyCols.AddRange(mine);
            cr.AddBleed(mid, 15f);

            // hand etc goes with it
            var hung = new List<HingeJoint2D>();
            foreach (var j in kids)
            {
                if (!j || j.connectedBody != rb) continue;
                if (CompNear(ToPx(transform.TransformPoint(j.connectedAnchor)), fComp, L.S * 3f) != id) continue;
                var nj = j.gameObject.AddComponent<HingeJoint2D>();
                nj.autoConfigureConnectedAnchor = false;
                nj.anchor = j.anchor;
                nj.connectedBody = crb;
                nj.connectedAnchor = j.connectedAnchor;
                nj.enableCollision = false;
                hung.Add(nj);
                var oh = j as HingeJoint2D;
                if (oh && oh.useLimits)
                {
                    float ja = oh.jointAngle; var lim = oh.limits;
                    nj.limits = new JointAngleLimits2D { min = Mathf.Min(0f, lim.min - ja), max = Mathf.Max(0f, lim.max - ja) };
                    nj.useLimits = true;
                }
                var kc = j.GetComponents<Collider2D>();
                foreach (var a in mine) foreach (var b in kc) if (a && b) Physics2D.IgnoreCollision(a, b);
                var kcr = j.GetComponent<Crater>(); if (kcr) kcr.skipStump = true;
                j.gameObject.SendMessage("OnJointBreak2D", j, SendMessageOptions.DontRequireReceiver);
                Destroy(j);
            }
            Spray(mid, Physics2D.gravity.normalized, 25f, 8);
            try { SoftSkin.Chunk(cr, crb, hung); } catch (System.Exception e) { Debug.LogWarning("[RealisticGore] chunk skin: " + e); }
        }

        void ShrinkCollider(int bx0, int by0, int bx1, int by1)
        {
            if (bx1 < 0) return;
            Vector2 a = (new Vector2(bx0, by0) - pivotPx) / ppuHi, b = (new Vector2(bx1 + 1, by1 + 1) - pivotPx) / ppuHi;
            foreach (var bc in GetComponents<BoxCollider2D>())
            {
                Vector2 n0 = Vector2.Max(bc.offset - bc.size * 0.5f, a), n1 = Vector2.Min(bc.offset + bc.size * 0.5f, b);
                if (n1.x - n0.x < 0.01f || n1.y - n0.y < 0.01f) continue;
                bc.offset = (n0 + n1) * 0.5f; bc.size = n1 - n0;
            }
            foreach (var cc in GetComponents<CapsuleCollider2D>())
            {
                Vector2 n0 = Vector2.Max(cc.offset - cc.size * 0.5f, a), n1 = Vector2.Min(cc.offset + cc.size * 0.5f, b);
                if (n1.x - n0.x < 0.01f || n1.y - n0.y < 0.01f) continue;
                cc.offset = (n0 + n1) * 0.5f; cc.size = n1 - n0;
            }
        }

        void LateUpdate()
        {
            // vanilla wound shader off
            if (skinH && skinH.currentDamagePointCount > 0) { skinH.ClearAllDamage(); skinH.currentDamagePointCount = 0; }
            if (Carving) PumpCarves();
            splitCd -= Time.deltaTime;
            if (needSplit && Ready && splitCd <= 0f && !Carving)
            {
                needSplit = false; splitCd = 0.12f;
                if (GoreSettings.CutPieces) try { Split(); } catch (System.Exception e) { Debug.LogWarning("[RealisticGore] split: " + e); }
            }
            upTick -= Time.deltaTime;
            midHold = carveCur != null ? midHold + Time.deltaTime : 0f;
            bool full = dirty && tex && upTick <= 0 && (carveCur == null || midHold > 0.25f);
            if (full) { tex.SetPixels32(cur); dirty = false; upTick = Random.Range(0.07f, 0.09f); midHold = 0f; }
            if (tex && (eyeDirty || (full && eyeOn)))
            {
                eyeDirty = false; PaintEye();
                if (eyeBlk != null) { tex.SetPixels32(ebx, eby, ebw, ebh, eyeBlk); full = true; }
            }
            if (full) tex.Apply(false);

            if (tags.Count == 0) return;
            float dt = Time.deltaTime; Vector2 grav = Physics2D.gravity * dt * dt;
            for (int k = tags.Count - 1; k >= 0; k--)
            {
                var t = tags[k];
                if (!t.lr) { tags.RemoveAt(k); continue; }
                t.p[0] = transform.TransformPoint(t.local);
                for (int q = 1; q < t.p.Length; q++) { var c = t.p[q]; t.p[q] += (t.p[q] - t.prev[q]) * 0.96f + grav; t.prev[q] = c; }
                for (int it = 0; it < 3; it++)
                    for (int q = 1; q < t.p.Length; q++)
                    {
                        Vector2 d = t.p[q] - t.p[q - 1]; float len = d.magnitude;
                        if (len < 1e-5f) continue;
                        Vector2 fix = d * (1f - t.seg / len);
                        if (q == 1) t.p[q] -= fix; else { t.p[q] -= fix * 0.5f; t.p[q - 1] += fix * 0.5f; }
                    }
                for (int q = 0; q < t.p.Length; q++) t.lr.SetPosition(q, t.p[q]);
            }
        }

    }
}
