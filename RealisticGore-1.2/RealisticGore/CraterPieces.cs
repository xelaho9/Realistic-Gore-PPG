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

        // split works on 8px cells; only cells along edges/cuts go per-pixel
        static int[] sPix, sQ, sCell, sCnt; static byte[] sFl;   // shared by all limbs, split is synchronous
        int cg = 1, cgw = 1;
        int CompAt(int x, int y)
        {
            int ci = (y / cg) * cgw + x / cg; byte f = sFl[ci];
            if ((f & 8) != 0) return sCell[ci];
            if ((f & 1) == 0) return 0;
            return sPix[y * L.w + x];
        }
        int SolidAt(int x, int y) { int ci = (y / cg) * cgw + x / cg; return (sFl[ci] & 8) != 0 ? sCell[ci] : 0; }

        int CompNear(Vector2 p, float rad)
        {
            int W = L.w, Hh = L.h, best = 0; float bd = float.MaxValue;
            int cx = (int)p.x, cy = (int)p.y, r = Mathf.CeilToInt(rad), st = Mathf.Max(1, L.S / 4);
            for (int y = cy - r; y <= cy + r; y += st)
                for (int x = cx - r; x <= cx + r; x += st)
                {
                    if (x < 0 || y < 0 || x >= W || y >= Hh) continue;
                    int c = CompAt(x, y); if (c <= 0) continue;
                    float d = (x - p.x) * (x - p.x) + (y - p.y) * (y - p.y);
                    if (d < bd) { bd = d; best = c; }
                }
            return best;
        }

        float splitCd;
        int ex0, ey0, ex1, ey1;
        void Ex(int x0, int y0, int x1, int y1) { if (x0 < ex0) ex0 = x0; if (y0 < ey0) ey0 = y0; if (x1 > ex1) ex1 = x1; if (y1 > ey1) ey1 = y1; }

        void Split()
        {
            int W = L.w, Hh = L.h, N = W * Hh;
            int g = cg = Mathf.Max(2, L.S / 8), gw = cgw = (W + g - 1) / g, gh = (Hh + g - 1) / g, M = gw * gh;
            if (sPix == null || sPix.Length < N) { sPix = new int[N]; sQ = new int[N]; }
            if (sCell == null || sCell.Length < M) { sCell = new int[M]; sCnt = new int[M]; sFl = new byte[M]; }
            int[] pix = sPix, q = sQ, cell = sCell, ccnt = sCnt; byte[] fl = sFl;
            // cell flags: 1 any alpha, 2 all opaque, 4 has cut edge, 8 solid
            for (int c = 0; c < M; c++) { cell[c] = 0; ccnt[c] = 0; fl[c] = 2; }
            // whole-limb scan, split by cell rows across cores
            var px = cur; var lvl = level;
            System.Action<int, int> pass = delegate(int a, int b)
            {
                for (int y = a * g, ye = Mathf.Min(Hh, (b + 1) * g); y < ye; y++)
                {
                    int rowC = (y / g) * gw, i = y * W;
                    for (int x = 0; x < W; x++, i++)
                    {
                        int ci = rowC + x / g;
                        if (px[i].a == 0) { fl[ci] &= 0xFD; continue; }
                        byte lv = lvl[i], f = (byte)(fl[ci] | 1);
                        if (lv == 7) f &= 0xFD; else if (lv >= 4) f |= 4;
                        fl[ci] = f; ccnt[ci]++;
                    }
                }
            };
            if (gh >= 4 && Threads()) new Bands(pass, 0, gh - 1, 2).Done(-1); else pass(0, gh - 1);
            // thin skin bridges don't hold, bone does
            int t = Mathf.Max(1, Mathf.RoundToInt(L.S * 0.3f));
            for (int cy = 0; cy < gh; cy++)
                for (int cx = 0; cx < gw; cx++)
                {
                    int ci = cy * gw + cx; if ((fl[ci] & 3) != 3) continue;
                    int x = Mathf.Min(W - 1, cx * g + g / 2), y = Mathf.Min(Hh - 1, cy * g + g / 2), i = y * W + x;
                    bool so = (level[i] >= 4 && level[i] <= 5 && IsBone(L.bone[i]))
                        || (x >= t && y >= t && x < W - t && y < Hh - t && Op(i - t) && Op(i + t) && Op(i - t * W) && Op(i + t * W));
                    if (so) fl[ci] |= 8;
                }

            var sizes = new List<int> { 0 };
            int id = 0;
            for (int s0 = 0; s0 < M; s0++)
            {
                if ((fl[s0] & 8) == 0 || cell[s0] != 0) continue;
                id++; int head = 0, tail = 0; q[tail++] = s0; cell[s0] = id;
                while (head < tail)
                {
                    int c = q[head++], cx = c % gw;
                    for (int k = 0; k < 4; k++)
                    {
                        int j = k == 0 ? (cx > 0 ? c - 1 : -1) : k == 1 ? (cx < gw - 1 ? c + 1 : -1) : k == 2 ? c - gw : c + gw;
                        if (j < 0 || j >= M || (fl[j] & 8) == 0 || cell[j] != 0) continue;
                        cell[j] = id; q[tail++] = j;
                    }
                }
                sizes.Add(tail);
            }
            if (id <= 1) return;

            // edge cells start empty
            for (int cy = 0; cy < gh; cy++)
                for (int cx = 0; cx < gw; cx++)
                {
                    int ci = cy * gw + cx; if ((fl[ci] & 9) != 1) continue;
                    int x0 = cx * g, xw = Mathf.Min(W, x0 + g) - x0, y1 = Mathf.Min(Hh, cy * g + g);
                    for (int y = cy * g; y < y1; y++) System.Array.Clear(pix, y * W + x0, xw);
                }

            // main piece keeps the joint, else biggest
            var rb = GetComponent<Rigidbody2D>();
            int main = 0;
            foreach (var j in GetComponents<AnchoredJoint2D>())
            {
                if (!j || !j.enabled || !j.connectedBody) continue;
                Vector2 ap = ToPx(transform.TransformPoint(j.anchor));
                int c = CompNear(ap, L.S * 4f);
                if (c == 0) c = CompNear(ap, Mathf.Max(L.w, L.h));
                if (c > 0 && (main == 0 || sizes[c] > sizes[main])) main = c;
            }
            if (main == 0) for (int c = 1; c <= id; c++) if (main == 0 || sizes[c] > sizes[main]) main = c;

            // grow pieces into edge pixels
            int hd = 0, tl = 0;
            for (int cy = 0; cy < gh; cy++)
                for (int cx = 0; cx < gw; cx++)
                {
                    int ci = cy * gw + cx; if ((fl[ci] & 9) != 1) continue;
                    int x1 = Mathf.Min(W, cx * g + g), y1 = Mathf.Min(Hh, cy * g + g);
                    for (int y = cy * g; y < y1; y++)
                        for (int x = cx * g; x < x1; x++)
                        {
                            int i = y * W + x; if (cur[i].a == 0) continue;
                            int c = 0;
                            if (x > 0) c = SolidAt(x - 1, y);
                            if (c == 0 && x < W - 1) c = SolidAt(x + 1, y);
                            if (c == 0 && y > 0) c = SolidAt(x, y - 1);
                            if (c == 0 && y < Hh - 1) c = SolidAt(x, y + 1);
                            if (c != 0) { pix[i] = c; q[tl++] = i; }
                        }
                }
            while (hd < tl)
            {
                int i = q[hd++], c = pix[i], x = i % W, y = i / W;
                for (int k = 0; k < 4; k++)
                {
                    int jx = k == 0 ? x - 1 : k == 1 ? x + 1 : x, jy = k == 2 ? y - 1 : k == 3 ? y + 1 : y;
                    if (jx < 0 || jy < 0 || jx >= W || jy >= Hh) continue;
                    int j = jy * W + jx;
                    if ((fl[(jy / g) * gw + jx / g] & 8) != 0 || cur[j].a == 0 || pix[j] != 0) continue;
                    pix[j] = c; q[tl++] = j;
                }
            }

            int n1 = id + 1;
            int[] mnx = new int[n1], mny = new int[n1], mxx = new int[n1], mxy = new int[n1], cnt = new int[n1];
            bool[] cutEdge = new bool[n1];
            for (int c = 0; c < n1; c++) { mnx[c] = mny[c] = int.MaxValue; mxx[c] = mxy[c] = -1; }
            int total = 0;
            ex0 = ey0 = int.MaxValue; ex1 = ey1 = -1;
            for (int cy = 0; cy < gh; cy++)
                for (int cx = 0; cx < gw; cx++)
                {
                    int ci = cy * gw + cx; byte f = fl[ci]; if ((f & 1) == 0) continue;
                    int x0 = cx * g, y0 = cy * g, x1 = Mathf.Min(W, x0 + g) - 1, y1 = Mathf.Min(Hh, y0 + g) - 1;
                    if ((f & 8) != 0)
                    {
                        int c = cell[ci];
                        total += ccnt[ci]; cnt[c] += ccnt[ci]; if ((f & 4) != 0) cutEdge[c] = true;
                        if (x0 < mnx[c]) mnx[c] = x0; if (x1 > mxx[c]) mxx[c] = x1;
                        if (y0 < mny[c]) mny[c] = y0; if (y1 > mxy[c]) mxy[c] = y1;
                        continue;
                    }
                    for (int y = y0; y <= y1; y++)
                        for (int x = x0; x <= x1; x++)
                        {
                            int i = y * W + x;
                            if (cur[i].a == 0) continue;
                            int c = pix[i];
                            if (c == 0) { cur[i] = new Color32(0, 0, 0, 0); level[i] = 6; Ex(x, y, x, y); continue; }   // loose specks
                            total++; cnt[c]++;
                            if (level[i] >= 4) cutEdge[c] = true;
                            if (x < mnx[c]) mnx[c] = x; if (x > mxx[c]) mxx[c] = x;
                            if (y < mny[c]) mny[c] = y; if (y > mxy[c]) mxy[c] = y;
                        }
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
                Ex(mnx[c], mny[c], mxx[c], mxy[c]);
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
                for (int cy = 0; cy < gh; cy++)
                    for (int cx = 0; cx < gw; cx++)
                    {
                        int ci = cy * gw + cx; byte f = fl[ci]; if ((f & 1) == 0) continue;
                        bool so = (f & 8) != 0; if (so && !erase[cell[ci]]) continue;
                        int x1 = Mathf.Min(W, cx * g + g), y1 = Mathf.Min(Hh, cy * g + g);
                        for (int y = cy * g; y < y1; y++)
                            for (int x = cx * g; x < x1; x++)
                            {
                                int i = y * W + x; if (cur[i].a == 0) continue;
                                if (so || (pix[i] > 0 && erase[pix[i]])) { cur[i] = new Color32(0, 0, 0, 0); level[i] = 6; }
                            }
                    }
            if (made) ShrinkCollider(mnx[main], mny[main], mxx[main], mxy[main]);
            if (ex1 >= 0) Mark(ex0, ey0, ex1, ey1);
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
                    if (cur[i].a == 0 || CompAt(x + bx0, y + by0) != id) continue;
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
            Hullify(poly);
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
                if (CompNear(ToPx(transform.TransformPoint(j.connectedAnchor)), L.S * 3f) != id) continue;
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

        // shared per-frame upload cap so a pile of bodies can't stall one frame
        static int upFrame = -1, upPx;
        static bool UpOk(int px)
        {
            if (Time.frameCount != upFrame) { upFrame = Time.frameCount; upPx = 0; }
            if (upPx > 0 && upPx + px > 1500000) return false;
            upPx += px; return true;
        }

        // dirty rect: only what changed goes to the GPU
        int ux0 = int.MaxValue, uy0 = int.MaxValue, ux1 = -1, uy1 = -1;
        int pbx0 = int.MaxValue, pby0 = int.MaxValue, pbx1 = -1, pby1 = -1;   // pending for healing
        static bool markSlow; bool soon; float lastUp;
        void Mark(int x0, int y0, int x1, int y1)
        {
            if (x0 < 0) x0 = 0; if (y0 < 0) y0 = 0; if (x1 >= L.w) x1 = L.w - 1; if (y1 >= L.h) y1 = L.h - 1;
            if (x1 < x0 || y1 < y0) return;
            dirty = true; if (!markSlow) soon = true;
            if (x0 < ux0) ux0 = x0; if (y0 < uy0) uy0 = y0; if (x1 > ux1) ux1 = x1; if (y1 > uy1) uy1 = y1;
            if (x0 < pbx0) pbx0 = x0; if (y0 < pby0) pby0 = y0; if (x1 > pbx1) pbx1 = x1; if (y1 > pby1) pby1 = y1;
        }
        void MarkR(Vector2 c, float r) { int rr = Mathf.CeilToInt(r) + 1, cx = Mathf.FloorToInt(c.x), cy = Mathf.FloorToInt(c.y); Mark(cx - rr, cy - rr, cx + rr, cy + rr); }

        static int copyOk = -1;
        static bool CopyOk
        {
            get
            {
                if (copyOk < 0) copyOk = (SystemInfo.copyTextureSupport & UnityEngine.Rendering.CopyTextureSupport.Basic) != 0 ? 1 : 0;
                return copyOk == 1;
            }
        }
        Texture2D stg; Color32[] blk; float stgIdle;

        void Upload(bool body)
        {
            if (body && ux1 < 0) { ux0 = uy0 = 0; ux1 = L.w - 1; uy1 = L.h - 1; }   // dirty with no rect = everything
            bool eye = eyeDirty;
            if (body && eyeOn && eyeBlk != null && ux0 < ebx + ebw && ux1 >= ebx && uy0 < eby + ebh && uy1 >= eby) eye = true;
            if (!body && !eye) return;
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1;
            if (body) { x0 = ux0; y0 = uy0; x1 = ux1; y1 = uy1; }
            if (eye)
            {
                PaintEye();
                if (eyeBlk != null) { x0 = Mathf.Min(x0, ebx); y0 = Mathf.Min(y0, eby); x1 = Mathf.Max(x1, ebx + ebw - 1); y1 = Mathf.Max(y1, eby + ebh - 1); }
            }
            if (x1 < 0) { eyeDirty = false; return; }
            int rw = x1 - x0 + 1, rh = y1 - y0 + 1;
            int bw = Mathf.Min(L.w, (rw + 63) & ~63), bh = Mathf.Min(L.h, (rh + 63) & ~63);
            bool part = ownTex && CopyOk && bw * bh * 2 < cur.Length;
            if (part && stg && stg.width >= bw && stg.height >= bh && stg.width * stg.height <= 2 * bw * bh) { bw = stg.width; bh = stg.height; }
            if (!UpOk(part ? bw * bh : cur.Length)) return;   // over this frame's budget, next frame
            bool done = false;
            if (part)
            {
                try
                {
                    if (!stg || stg.width != bw || stg.height != bh)
                    {
                        if (stg) Destroy(stg);
                        stg = new Texture2D(bw, bh, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                        blk = new Color32[bw * bh];
                    }
                    for (int r = 0; r < rh; r++) System.Array.Copy(cur, (y0 + r) * L.w + x0, blk, r * bw, rw);
                    if (eye && eyeBlk != null)
                        for (int r = 0; r < ebh; r++) System.Array.Copy(eyeBlk, r * ebw, blk, (eby + r - y0) * bw + ebx - x0, ebw);
                    stg.SetPixels32(blk); stg.Apply(false);
                    Graphics.CopyTexture(stg, 0, 0, 0, 0, rw, rh, tex, 0, 0, x0, y0);
                    stgIdle = 0f; done = true;
                }
                catch (System.Exception e) { copyOk = 0; Debug.LogWarning("[RealisticGore] partial upload off: " + e.Message); }
            }
            if (!done)
            {
                bool promote = !ownTex;
                if (promote)
                {
                    tex = new Texture2D(L.w, L.h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                    ownTex = true;
                }
                tex.SetPixels32(cur);
                if (eyeBlk != null && (eyeOn || eye)) { if (!eye) PaintEye(); tex.SetPixels32(ebx, eby, ebw, ebh, eyeBlk); }
                tex.Apply(false);
                if (promote && sr) sr.sprite = Sprite.Create(tex, new Rect(0, 0, L.w, L.h), sprPiv, ppuHi, 0, SpriteMeshType.FullRect);
                body = true;   // everything went up
            }
            eyeDirty = false;
            if (body) { dirty = false; soon = false; lastUp = Time.time; ux0 = uy0 = int.MaxValue; ux1 = uy1 = -1; upTick = Random.Range(0.07f, 0.09f); midHold = 0f; }
        }

        void LateUpdate()
        {
            // vanilla wound shader off
            if (skinH && skinH.currentDamagePointCount > 0) { skinH.ClearAllDamage(); skinH.currentDamagePointCount = 0; }
            if (Carving) { double t0 = Now(); PumpCarves(); Spend(t0); }
            splitCd -= Time.deltaTime;
            if (needSplit && Ready && splitCd <= 0f && !Carving)
            {
                needSplit = false; splitCd = 0.12f;
                if (GoreSettings.CutPieces) { double t0 = Now(); try { Split(); } catch (System.Exception e) { Debug.LogWarning("[RealisticGore] split: " + e); } Spend(t0); }
            }
            upTick -= Time.deltaTime;
            midHold = carveCur != null ? midHold + Time.deltaTime : 0f;
            bool body = dirty && upTick <= 0 && (soon || Time.time - lastUp > 0.25f) && (carveCur == null || midHold > 0.25f);
            if (tex && (body || eyeDirty)) { double t0 = Now(); Upload(body); Spend(t0); }
            if (stg) { stgIdle += Time.deltaTime; if (stgIdle > 3f) { Destroy(stg); stg = null; blk = null; } }

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
