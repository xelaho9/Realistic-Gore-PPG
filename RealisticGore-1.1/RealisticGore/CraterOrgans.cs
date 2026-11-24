using System.Collections.Generic;
using UnityEngine;

namespace Mod
{
    public partial class Crater
    {
        Vector2 PxToWorld(Vector2 px) { return transform.TransformPoint((px - pivotPx) / ppuHi); }

        List<Collider2D> BodyCols()
        {
            if (bodyCols == null)
            {
                bodyCols = new List<Collider2D>();
                if (limb && limb.Person) bodyCols.AddRange(limb.Person.GetComponentsInChildren<Collider2D>());
                bodyCols.AddRange(GetComponents<Collider2D>());
            }
            return bodyCols;
        }

        int runsMade;

        // head bursts: brain, eyes and chunks fly out, blood everywhere
        public void PopPieces(Vector2 hit, Vector2 dir)
        {
            if (!limb) return;
            var rb = GetComponent<Rigidbody2D>();
            float scl = Mathf.Abs(transform.lossyScale.y);
            float headW = Ready && L.uw > 0f ? L.uw * L.S / ppuHi * scl : 0.4f * scl;
            Vector2 c = sr ? (Vector2)sr.bounds.center : (Vector2)transform.position;
            Vector2 baseV = rb ? rb.velocity * 0.5f : Vector2.zero;
            var cols = BodyCols(); var circ = limb.CirculationBehaviour;
            System.Func<float, Vector2> fly = sp => baseV + (dir * 0.6f + Random.insideUnitCircle + Vector2.up * 0.5f).normalized * sp;
            for (int i = 0; i < 2; i++)
                RealOrgan.Spawn("eye", c + Random.insideUnitCircle * headW * 0.2f, 0.17f * headW / 0.4f, Random.Range(0f, 360f), rb, phys, sr, cols, null, circ, fly(Random.Range(5f, 10f)), false);
            int gibs = Random.Range(6, 10);
            for (int i = 0; i < gibs; i++)
                RealOrgan.Spawn("gib", c + Random.insideUnitCircle * headW * 0.3f, headW * Random.Range(0.18f, 0.35f), Random.Range(0f, 360f), rb, phys, sr, cols, null, circ, fly(Random.Range(4f, 12f)), false);
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f + Random.Range(-0.4f, 0.4f);
                Spray(c, new Vector2(Mathf.Cos(a), Mathf.Sin(a)), 500f, 14);
            }
            Spray(c, dir, 900f, 30);
        }

        // close shots pop the eye out
        bool eyeOut;
        void TryEye(Vector2 a, Vector2 b, float dmg)
        {
            if (eyeOut || L.kind != 0 || L.uw <= 0f || !limb || limb.IsAndroid) return;
            Vector2 c = new Vector2((L.eu * L.uw + L.ux0 + L.pad) * L.S, (L.ev * L.uh + L.uy0 + L.pad) * L.S);
            float rx = 0.075f * L.uw * L.S, ry = 0.042f * L.uh * L.S;
            Vector2 pa = ToPx(a), pb = ToPx(b), ab = pb - pa;
            float t = Mathf.Clamp01(Vector2.Dot(c - pa, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
            float d = Vector2.Distance(c, pa + ab * t);
            float reach = rx * (1.6f + Mathf.Clamp01(dmg / 200f) * 1.4f);
            if (d > reach) return;
            if (d > rx && Random.value > 0.75f) return;
            eyeOut = true;

            // empty socket
            Vector3 Ld = LocalLight(), Hh = (Ld + Vector3.forward).normalized;
            float ex = rx * 1.2f, ey = ry * 1.7f, sx = Random.value * 300f;
            for (int y = (int)(c.y - ey) - 2; y <= (int)(c.y + ey) + 2; y++)
                for (int x = (int)(c.x - ex) - 2; x <= (int)(c.x + ex) + 2; x++)
                {
                    if (x < 0 || y < 0 || x >= L.w || y >= L.h) continue;
                    int i = y * L.w + x; if (level[i] == 6 || cur[i].a == 0) continue;
                    float ox = (x - c.x) / ex, oy = (y - c.y) / ey;
                    float e = Mathf.Sqrt(ox * ox + oy * oy) + (Mathf.PerlinNoise(x * 0.3f / L.S * 4f + sx, y * 0.3f / L.S * 4f) - 0.5f) * 0.25f;
                    if (e >= 1f) continue;
                    Color bc = e > 0.78f ? Color.Lerp(new Color(0.78f, 0.36f, 0.36f), new Color(0.55f, 0.08f, 0.10f), Mathf.PerlinNoise(x * 0.5f + sx, y * 0.5f))
                                         : Color.Lerp(new Color(0.08f, 0.0f, 0.01f), new Color(0.45f, 0.04f, 0.06f), Mathf.Pow(e / 0.78f, 2f));
                    if (e < 0.5f && Mathf.PerlinNoise(x * 0.25f + sx, y * 0.25f) > 0.62f) bc = Color.Lerp(bc, new Color(0.85f, 0.75f, 0.62f), 0.5f);   // torn nerve sheath / fat
                    Vector3 n = new Vector3(ox, oy, 0.8f).normalized;   // concave bowl
                    float diff = Mathf.Max(0f, Vector3.Dot(n, Ld));
                    Color lit = bc * (0.35f + 0.8f * diff);
                    lit += new Color(1f, 0.9f, 0.9f) * Mathf.Pow(Mathf.Max(0f, Vector3.Dot(n, Hh)), 50f) * 0.5f * Mathf.PerlinNoise(x * 0.8f + sx, y * 0.8f);
                    lit.a = L.skin[i].a / 255f;
                    cur[i] = lit; level[i] = 4;
                }
            Runs(c + Vector2.down * 0f, ry * 1.5f, Random.Range(2, 4));
            Mark((int)(c.x - ex) - 3, (int)(c.y - ey) - 3, (int)(c.x + ex) + 3, (int)(c.y + ey) + 3);

            float scl = Mathf.Abs(transform.lossyScale.y);
            float headW = L.uw * L.S / ppuHi * scl;
            float size = 0.17f * headW / 0.4f;
            Vector2 at = PxToWorld(c);
            Vector2 face = (Vector2)transform.right * Mathf.Sign(transform.lossyScale.x);
            Vector2 travel = (b - a).sqrMagnitude > 1e-6f ? (b - a).normalized : face;
            var rb = GetComponent<Rigidbody2D>();
            Vector2 vel = (rb ? rb.GetPointVelocity(at) : Vector2.zero) + (face * 0.6f + travel * 0.8f + Vector2.up * 0.4f).normalized * Random.Range(2f, 4f);
            float ang = Mathf.Atan2(face.y, face.x) * Mathf.Rad2Deg;
            RealOrgan.Spawn("eye", at + face * size * 0.15f, size, ang, rb, phys, sr, BodyCols(), null, limb.CirculationBehaviour, vel, true);
            Spray(at, face, 30f, 10);
            AddBleeder(at, 15f);
        }

        // trickles follow gravity every tick
        class Rill { public Vector2 p; public float w, len, done, wob; }
        List<Rill> rills; byte[] runCov;
        void Runs(Vector2 c, float R, int count)
        {
            if (!GoreSettings.Rills) return;
            count = Mathf.Min(count, GoreSettings.RillMax - runsMade);   // any more looks like stripes
            if (count <= 0) return;
            runsMade += count;
            if (rills == null) rills = new List<Rill>();
            if (runCov == null || runCov.Length != cur.Length) runCov = new byte[cur.Length];
            Vector2 down = PxDown(), side = new Vector2(down.y, -down.x);
            float S = L.S;
            for (int k = 0; k < count; k++)
                rills.Add(new Rill
                {
                    p = c + (down + side * Random.Range(-0.8f, 0.8f)).normalized * R * 0.9f,
                    len = R * Random.Range(0.5f, 1.6f) + S * Random.Range(1f, 3.5f),
                    w = Random.Range(0.28f, 0.5f) * S,
                    wob = Random.value * 100f
                });
        }

        void RunRills(float dt)
        {
            if (rills == null || rills.Count == 0) return;
            float S = L.S;
            Vector2 down = PxDown(), side = new Vector2(down.y, -down.x);
            Vector3 Ld = LocalLight();
            float lightSide = Mathf.Sign(Vector2.Dot(new Vector2(Ld.x, Ld.y), side) + 1e-4f);
            for (int k = rills.Count - 1; k >= 0; k--)
            {
                var r = rills[k];
                float speed = S * Mathf.Lerp(5f, 1.2f, r.done / r.len);
                float move = speed * dt;
                int steps = Mathf.Max(1, Mathf.CeilToInt(move * 2f));
                bool stop = false;
                for (int s = 0; s < steps && !stop; s++)
                {
                    r.done += move / steps;
                    float t = r.done / r.len;
                    Vector2 wig = side * (Mathf.PerlinNoise(r.wob, r.done / S * 0.6f) - 0.5f) * 0.25f;
                    r.p += (down + wig).normalized * (move / steps);
                    float ww = Mathf.Max(0.6f, r.w * Mathf.Lerp(1f, 0.65f, t));
                    if (!Dab(r.p, ww, side, lightSide)) stop = true;   // ran off the body
                    if (t >= 1f) stop = true;
                }
                if (stop)
                {
                    Dab(r.p, Mathf.Max(0.8f, r.w * 0.95f), side, lightSide);   // little bead where it stopped
                    rills.RemoveAt(k);
                }
            }
        }

        bool Dab(Vector2 q, float ww, Vector2 side, float lightSide)
        {
            int cx = (int)q.x, cy = (int)q.y;
            if (cx < 0 || cy < 0 || cx >= L.w || cy >= L.h) return false;
            if (cur[cy * L.w + cx].a == 0 || L.skin[cy * L.w + cx].a < 100) return false;
            int rr = Mathf.CeilToInt(ww);
            for (int y = cy - rr; y <= cy + rr; y++)
                for (int x = cx - rr; x <= cx + rr; x++)
                {
                    if (x < 0 || y < 0 || x >= L.w || y >= L.h) continue;
                    int i = y * L.w + x;
                    if (level[i] > 1 || cur[i].a == 0 || L.skin[i].a < 100) continue;
                    Vector2 o = new Vector2(x, y) - q;
                    float dd = o.magnitude / ww;
                    if (dd > 1f) continue;
                    float sh = Mathf.Sqrt(1f - dd * dd);
                    byte sb = (byte)(sh * 255f);
                    if (runCov[i] >= sb) continue;
                    // relative to the skin so dabs don't stack
                    Color under = level[i] == 0 ? (Color)cur[i] : (Color)L.skin[i];
                    if (level[i] == 1 && runCov[i] == 0) under = cur[i];
                    runCov[i] = sb;
                    float sd = Vector2.Dot(o, side) / ww * lightSide;
                    Color bl = Color.Lerp(new Color(0.50f, 0.03f, 0.05f), new Color(0.30f, 0.0f, 0.02f), sh);
                    Color col = Color.Lerp(under, bl, Mathf.Clamp01(sh * 2.4f) * 0.92f);
                    float hl = Mathf.Exp(-(sd - 0.4f) * (sd - 0.4f) * 18f) * sh;
                    col += new Color(0.9f, 0.6f, 0.6f) * hl * 0.12f;
                    col.a = cur[i].a / 255f;
                    cur[i] = col; level[i] = 1;
                }
            Mark(cx - rr, cy - rr, cx + rr, cy + rr);
            return true;
        }

        void Stabbed(Stabbing s)
        {
            if (!Ready) return;
            Wound(s.point, 1.3f, 3, true);
            if (GoreSettings.StabWounds) Carve(s.point, 0.35f, false, true, false);
            AddBleeder(s.point, 20f);
        }

        void OnCollisionEnter2D(Collision2D col)
        {
            if (!Ready || col.contactCount == 0 || !limb) return;
            float v = col.relativeVelocity.magnitude;
            if (v < 7f) return;
            int depth = v > 30f ? 3 : v > 20f ? 2 : v > 12f ? 1 : 0;
            Vector2 p = col.GetContact(0).point;
            Wound(p, Mathf.Clamp(v / 7f, 1.2f, 5f), depth, false);

            // big hits split the skin, corners sooner
            if (v < Mathf.Min(15f, GoreSettings.ImpactWound) || Time.time < impactCd) return;
            if (col.collider && BodyCols().Contains(col.collider)) return;
            bool point = col.contactCount == 1;
            float force = v * (point ? 1.35f : 1f);
            float iw = GoreSettings.ImpactWound;
            if (force < iw) return;
            if (carveQ.Count > 3) return;   // queue's backed up
            impactCd = Time.time + 0.2f;
            float rad = Mathf.Clamp((force - iw) / 25f, 0f, 1f) * 0.9f + 0.3f;   // orig px; 0.3 = split skin, ~1.2 = burst open
            Vector2 into = (Vector2)sr.bounds.center - p; into = into.sqrMagnitude > 1e-8f ? into.normalized : Vector2.zero;
            Carve(p + into * rad * L.S / ppuHi * Mathf.Abs(transform.lossyScale.y) * 0.4f, rad, force > GoreSettings.ImpactBoneSnap, false, true);
            if (force > iw + 12f) Carve(p, rad * 0.7f, false, false, false);   // ragged second bite
            AddBleeder(p, 10f + force * 0.6f);
            needSplit = true;
        }
        float impactCd;

        // eyes: blink, look around, roll back when out
        // drawn into the head texture
        Color32[] eyeBlk; int ebx, eby, ebw, ebh; bool eyeOn, eyeDirty; int eyeKey = -1, eyeDryQ;
        float eLid, lidS, rollS, gazeS, gazeT, pupS = 0.18f, lowS, blinkT = -1f, blinkDur, blinkCd = 2f, sacCd = 1f, flutT;
        float deadT, deadLid, deadRoll, deadGaze, eyeSeed; Color eyeK = Color.white;

        void RunEyes(float dt)
        {
            if (L.kind != 0 || L.uw <= 0f || !limb || limb.IsAndroid) return;
            if (eyeOut || !GoreSettings.Eyes) { if (eyeOn) { eyeOn = false; eyeDirty = true; } return; }
            var p = limb.Person; if (!p) return;
            if (eyeSeed == 0f) eyeSeed = Random.Range(1f, 500f);
            float t = Time.time + eyeSeed;
            float tl = 0f, tr = 0f, tp = 0.18f, tlo = 0f, sl = 9f, srl = 3f, sg = 3f;
            bool canBlink = false;
            if (!p.IsAlive())
            {
                if (deadT <= 0f) { deadLid = Random.Range(0.28f, 0.6f); deadRoll = Random.Range(0.4f, 0.8f); deadGaze = Random.Range(-0.35f, 0.25f); }
                deadT += dt;
                tl = deadLid; tr = deadRoll; tlo = 0.1f; gazeT = deadGaze;
                tp = Mathf.Lerp(0.26f, 0.46f, deadT / 4f);                        // pupils blow wide
                if (deadT < 1.2f) tl = Mathf.Max(tl, 0.75f * (1f - deadT / 1.2f)); // lids sag, then relax half open
                sl = 1.5f; srl = 1.2f; sg = 1f;
            }
            else
            {
                deadT = 0f;
                float ko = Mathf.Max(0.3f, 0.8f - Mathf.Clamp01(p.AdrenalineLevel));   // same as vanilla ragdoll
                if (p.SeizureTime > 0f)
                {
                    tl = 0.25f + 0.5f * Mathf.PerlinNoise(t * 9f, 1.3f);
                    tr = 0.5f + 0.35f * Mathf.PerlinNoise(t * 6f, 4.1f);
                    gazeT = (Mathf.PerlinNoise(t * 11f, 7.7f) - 0.5f) * 1.1f;
                    tp = 0.3f; sl = 30f; srl = 20f; sg = 25f;
                }
                else if (p.Consciousness < ko)
                {
                    // knocked out
                    tl = 0.7f + 0.15f * Mathf.PerlinNoise(t * 0.35f, 2.2f);
                    tr = 0.6f + 0.15f * Mathf.PerlinNoise(t * 0.25f, 5.5f);
                    gazeT = (Mathf.PerlinNoise(t * 0.3f, 9.1f) - 0.5f) * 0.6f;
                    tp = 0.28f; tlo = 0.08f; sl = 3f; srl = 2f; sg = 2f;
                    if (flutT > 0f) { flutT -= dt; tl = Mathf.Clamp01(0.72f + Mathf.Sin(t * 38f) * 0.2f); sl = 40f; }
                    else if (Random.value < dt * 0.12f) flutT = Random.Range(0.3f, 0.8f);
                }
                else
                {
                    float grog = Mathf.Clamp01((0.95f - p.Consciousness) / Mathf.Max(0.05f, 0.95f - ko));
                    float pain = Mathf.Clamp01(Mathf.Max(p.PainLevel, p.ShockLevel * 0.7f));
                    tl = grog * 0.5f + pain * 0.22f;                                   // heavy lids / squint
                    tr = grog * 0.22f; tlo = pain * 0.12f;
                    tp = 0.18f + Mathf.Clamp01(p.ShockLevel) * 0.08f;
                    canBlink = true; sg = grog > 0.3f ? 4f : 30f;                       // quick glances, slow drift when groggy
                    if ((sacCd -= dt) <= 0f) { sacCd = Random.Range(0.8f, 3.5f) * (1f + grog * 2f); gazeT = Random.Range(-0.3f, 0.22f) * (1f - grog * 0.5f); }
                    if (blinkT < 0f && (blinkCd -= dt) <= 0f)
                    {
                        blinkT = 0f; blinkDur = 0.2f + grog * 0.45f;
                        blinkCd = Random.value < 0.18f ? 0.3f : Random.Range(2f, 6f) * (1f - pain * 0.6f);
                    }
                    if (Fear > 0.05f)
                    {
                        // wide eyes, tiny pupils, darting
                        tl = Mathf.Min(tl, 0.05f * (1f - Fear)); tlo = 0f; tr = 0f;
                        tp = Mathf.Lerp(tp, 0.09f, Fear); sg = 40f; sl = 20f;
                        if (sacCd > 0.4f) sacCd = Random.Range(0.15f, 0.4f);
                        if (blinkCd < 1f) blinkCd += 1.5f * Fear;
                    }
                }
            }
            lidS = Mathf.Lerp(lidS, tl, 1f - Mathf.Exp(-sl * dt));
            rollS = Mathf.Lerp(rollS, tr, 1f - Mathf.Exp(-srl * dt));
            gazeS = Mathf.Lerp(gazeS, gazeT, 1f - Mathf.Exp(-sg * dt));
            pupS = Mathf.Lerp(pupS, tp, 1f - Mathf.Exp(-2f * dt));
            lowS = Mathf.Lerp(lowS, tlo, 1f - Mathf.Exp(-3f * dt));
            float lid = lidS;
            if (blinkT >= 0f)
            {
                if (!canBlink) blinkT = -1f;
                else
                {
                    blinkT += dt; float u = blinkT / blinkDur;
                    float b = u < 0.35f ? u / 0.35f : (u < 0.45f ? 1f : 1f - (u - 0.45f) / 0.55f);
                    if (u >= 1f) blinkT = -1f; else lid = Mathf.Max(lid, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(b)));
                }
            }
            eLid = lid;
            bool on = eLid > 0.01f || rollS > 0.01f || Mathf.Abs(gazeS) > 0.02f || Mathf.Abs(pupS - 0.18f) > 0.01f || lowS > 0.01f;
            int key = on ? ((int)(eLid * 80f) | ((int)(rollS * 50f) << 7) | ((int)((gazeS + 1f) * 25f) << 13) | ((int)(pupS * 60f) << 19) | ((int)(lowS * 40f) << 25)) : -1;
            int dq = (int)(Mathf.Clamp01(deadT / 90f) * 10f);
            if (key != eyeKey || on != eyeOn || dq != eyeDryQ) { eyeKey = key; eyeOn = on; eyeDryQ = dq; eyeDirty = true; }
        }

        void PaintEye()
        {
            Vector2 c = new Vector2((L.eu * L.uw + L.ux0 + L.pad) * L.S, (L.ev * L.uh + L.uy0 + L.pad) * L.S);
            float rx = 0.075f * L.uw * L.S, ry = 0.042f * L.uh * L.S;
            float iu = c.x + 0.028f * L.uw * L.S, irx = 0.032f * L.uw * L.S, iry = 0.036f * L.uh * L.S;
            Color scl = new Color(0.93f, 0.90f, 0.86f);
            if (eyeBlk == null)
            {
                ebx = Mathf.Max(0, (int)(c.x - rx) - 2); eby = Mathf.Max(0, (int)(c.y - ry) - 2);
                ebw = Mathf.Min(L.w, (int)(c.x + rx) + 3) - ebx; ebh = Mathf.Min(L.h, (int)(c.y + ry) + 3) - eby;
                if (ebw <= 0 || ebh <= 0) { eyeOut = true; return; }
                eyeBlk = new Color32[ebw * ebh];
                // match the head bake's lighting
                float kr = 0f, kg = 0f, kb = 0f; int n = 0;
                for (int y = eby; y < eby + ebh; y++)
                    for (int x = ebx; x < ebx + ebw; x++)
                    {
                        float ox = (x - c.x) / rx, oy = (y - c.y) / ry, sd = ox * ox + oy * oy;
                        float ix = (x - iu) / irx, iy = (y - c.y) / iry;
                        if (sd > 0.85f || ix * ix + iy * iy < 1.4f) continue;
                        Color bs = scl * (0.9f + (1f - sd) * 0.1f); Color32 bk = L.skin[y * L.w + x];
                        kr += bk.r / 255f / bs.r; kg += bk.g / 255f / bs.g; kb += bk.b / 255f / bs.b; n++;
                    }
                eyeK = n > 0 ? new Color(kr / n, kg / n, kb / n) : Color.white;
            }
            float lr = 0f, lg = 0f, lb = 0f; int ln = 0;
            int ly = Mathf.Clamp((int)(c.y + ry * 1.15f), 0, L.h - 1);
            for (int x = (int)(c.x - rx * 0.45f); x <= (int)(c.x + rx * 0.45f); x++)
            {
                if (x < 0 || x >= L.w) continue;
                int i = ly * L.w + x; Color32 q = (level[i] == 0 && cur[i].a > 0) ? cur[i] : L.skin[i];
                lr += q.r; lg += q.g; lb += q.b; ln++;
            }
            Color lidC = ln > 0 ? new Color(lr / ln / 255f, lg / ln / 255f, lb / ln / 255f) : new Color(0.6f, 0.45f, 0.4f);
            Color lash = new Color(0.10f, 0.07f, 0.06f);
            float icx = iu + gazeS * irx, icy = c.y + rollS * ry * 1.3f;
            float dryA = Mathf.Clamp01(deadT / 90f), lw = Mathf.Max(1.5f, ry * 0.14f), shade = Mathf.Clamp01(eLid * 5f);

            for (int y = 0; y < ebh; y++)
                for (int x = 0; x < ebw; x++)
                {
                    int gx = x + ebx, gy = y + eby, i = gy * L.w + gx, k = y * ebw + x;
                    Color32 cc = cur[i]; eyeBlk[k] = cc;
                    if (!eyeOn || level[i] != 0 || cc.a == 0) continue;
                    float ox = (gx - c.x) / rx, oy = (gy - c.y) / ry, sd = ox * ox + oy * oy;
                    if (sd >= 1f) continue;
                    float hw = Mathf.Sqrt(1f - ox * ox);
                    float dTop = (hw * (1f - 2f * eLid) - oy) * ry;    // px below the upper lid edge
                    float dBot = (oy + hw * (1f - 2f * lowS)) * ry;    // px above the lower lid edge

                    // eyeball
                    Color s = Color.Lerp(scl * (0.9f + (1f - sd) * 0.1f), new Color(0.80f, 0.75f, 0.66f), dryA);   // dead eyes dull and dry
                    float dx = (gx - icx) / irx, dy = (gy - icy) / iry, id = dx * dx + dy * dy;
                    if (id < 1f) s = id < pupS ? new Color(0.03f, 0.02f, 0.02f) : new Color(0.32f, 0.20f, 0.11f) * (0.8f + id * 0.3f);
                    if (id < 0.05f && gy > icy) s = Color.Lerp(s, Color.white * 0.95f, 1f - dryA * 0.85f);
                    s = new Color(s.r * eyeK.r, s.g * eyeK.g, s.b * eyeK.b);
                    if (dTop < 3f) s *= 1f - shade * 0.3f * (1f - Mathf.Clamp01(dTop / 3f));   // lid shadow on the ball

                    // upper lid
                    if (eLid > 0.005f)
                    {
                        float cov = Mathf.Clamp01(0.5f - dTop);
                        if (cov > 0f)
                        {
                            Color lc = lidC * (0.9f + 0.1f * hw);
                            float e = -dTop; if (e < lw) lc = Color.Lerp(lc, lash, 0.75f * (1f - Mathf.Clamp01(e / lw)));
                            s = Color.Lerp(s, lc, cov);
                        }
                    }
                    if (lowS > 0.005f)
                    {
                        float cov = Mathf.Clamp01(0.5f - dBot);
                        if (cov > 0f)
                        {
                            Color lc = lidC * 0.95f;
                            float e = -dBot; if (e < lw * 0.6f) lc = Color.Lerp(lc, lash, 0.45f * (1f - Mathf.Clamp01(e / (lw * 0.6f))));
                            s = Color.Lerp(s, lc, cov);
                        }
                    }
                    Color32 o = s; o.a = cc.a; eyeBlk[k] = o;
                }
        }

        void Update()
        {
            if (!Ready || !phys || (limb && limb.IsAndroid)) return;
            if (limb && !wasDism && limb.IsDismembered)
            {
                wasDism = true;
                if (!skipStump) { try { Stump(); } catch (System.Exception e) { Debug.LogWarning("[RealisticGore] stump: " + e); } }
            }
            RunDrips(Time.deltaTime);
            RunSpurts(Time.deltaTime);
            RunRot(Time.deltaTime);
            RunBruises(Time.deltaTime); RunRills(Time.deltaTime);
            try { RunHeal(Time.deltaTime); } catch (System.Exception e) { Debug.LogWarning("[RealisticGore] heal: " + e); }
            try { RunEyes(Time.deltaTime); } catch (System.Exception e) { Debug.LogWarning("[RealisticGore] eyes: " + e); eyeOut = true; }
            CheckFracture();
            paleTick -= Time.deltaTime;
            if (paleTick <= 0) { paleTick = 0.5f; Pallor(); }
            burnTick -= Time.deltaTime;
            if (burnTick > 0) return;
            burnTick = Random.Range(0.17f, 0.23f);
            if (!(phys.OnFire || phys.Temperature > 150f)) return;
            float heat = phys.OnFire ? 1f : Mathf.Clamp01((phys.Temperature - 150f) / 400f);
            var bnd = sr.bounds;
            for (int k = 0; k < 2; k++)
                Burn(new Vector2(Random.Range(bnd.min.x, bnd.max.x), Random.Range(bnd.min.y, bnd.max.y)), 0.18f * heat);
        }

    }
}
