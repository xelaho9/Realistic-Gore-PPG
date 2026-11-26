using UnityEngine;

namespace Mod
{
    // custom outline per limb (front = +u, up = +v)
    public static partial class RealisticHuman
    {
        // rows: v, back u, front u
        // ends stay full width so neighbours meet
        static readonly float[] ChestK = { 0f, 0f, 1f, .3f, -.01f, 1.01f, .48f, -.03f, 1.0f, .65f, -.05f, 1.07f, .82f, -.03f, 1.03f, .93f, 0f, 1f, 1f, .03f, .97f };
        static readonly float[] BellyK = { 0f, 0f, 1f, .3f, .04f, 1.03f, .7f, .03f, .99f, 1f, 0f, 1f };
        static readonly float[] HipsK = { 0f, .03f, .97f, .35f, -.08f, 1f, .6f, -.05f, 1f, .85f, -.01f, 1f, 1f, 0f, 1f };
        static readonly float[] ThighK = { 0f, 0f, 1f, .2f, .02f, 1f, .55f, -.03f, 1.05f, .8f, -.02f, 1.02f, 1f, 0f, 1f };
        static readonly float[] ShinK = { 0f, .1f, .9f, .25f, .08f, .92f, .5f, -.04f, .96f, .7f, -.09f, .98f, .9f, 0f, 1f, 1f, 0f, 1f };
        static readonly float[] UpperArmK = { 0f, 0f, 1f, .45f, -.04f, 1.05f, .8f, -.02f, 1.03f, 1f, 0f, 1f };
        static readonly float[] ForearmK = { 0f, .05f, .95f, .12f, 0f, 1f, .26f, .1f, .9f, .45f, .05f, .95f, .8f, -.01f, 1.01f, 1f, 0f, 1f };
        static readonly float[] HandK = { 0f, .04f, .96f, 1f, 0f, 1f };
        static readonly float[] FaceK = { .12f, .9f, .17f, .96f, .24f, .94f, .29f, .985f, .32f, .98f, .38f, .97f, .42f, 1.04f, .5f, .99f, .58f, .94f, .68f, .97f, .8f, .92f };
        static readonly float[] JawK = { .3f, .3f, .55f, .22f, .75f, .14f, .92f, .12f };
        static readonly float[] FootTop = { 0f, .8f, .15f, 1.04f, .45f, 1.04f, .7f, .62f, 1f, .5f };
        static readonly float[] FootBot = { 0f, .05f, .3f, 0f, .65f, .08f, .8f, 0f, 1f, .02f };

        static float Key(float[] k, int stride, int col, float x)
        {
            int n = k.Length / stride;
            if (x <= k[0]) return k[col];
            for (int i = 1; i < n; i++)
            {
                float x1 = k[i * stride];
                if (x > x1) continue;
                float x0 = k[(i - 1) * stride], t = (x - x0) / (x1 - x0);
                t = t * t * (3f - 2f * t);
                return Mathf.Lerp(k[(i - 1) * stride + col], k[i * stride + col], t);
            }
            return k[(n - 1) * stride + col];
        }

        static float Box(float dx, float dy, float r)
        {
            float qx = r - dx, qy = r - dy;
            if (qx > 0f && qy > 0f && dx > -r && dy > -r) return r - Mathf.Sqrt(qx * qx + qy * qy);
            return Mathf.Min(dx, dy);
        }

        // -1 = unknown part, keep vanilla
        static float Shape(Kind k, float u, float v)
        {
            float d;
            switch (k)
            {
                case Kind.Head: d = Head(u, v); break;
                case Kind.Foot: d = Foot(u, v); break;
                case Kind.Chest: d = Limb(ChestK, u, v, -.08f, 1f, .2f, 1.6f); if (fem) d = Mathf.Max(d, Ell(u, v, .86f, .5f, .2f, .24f)); break;
                case Kind.Belly: d = Limb(BellyK, u, v, -.08f, 1.08f, .2f, .2f); break;
                case Kind.Hips: d = Limb(HipsK, u, v, -.03f, 1.08f, 1.2f, .2f); if (fem) d = Mathf.Max(d, Ell(u, v, .2f, .3f, .28f, .3f)); break;
                case Kind.Thigh: d = Limb(ThighK, u, v, -.08f, 1.08f, .2f, .2f); break;
                case Kind.Shin: d = Limb(ShinK, u, v, -.08f, 1.08f, .2f, .2f); break;
                case Kind.UpperArm: d = Limb(UpperArmK, u, v, -.08f, 1.06f, .2f, 1.4f); break;
                case Kind.Forearm: d = SMax(Limb(ForearmK, u, v, -.06f, 1.08f, 1.2f, .2f), Ell(u, v, .97f, .16f, .14f, .09f), 1f); break;   // + thumb
                case Kind.Hand: d = Limb(HandK, u, v, 0f, 1.08f, 1f, .2f); break;
                default: return -1f;
            }
            return Mathf.Clamp01(d * S / 1.6f + 0.5f);
        }

        static float Ell(float u, float v, float cu, float cv, float ru, float rv)
        {
            float ex = (u - cu) / ru, ey = (v - cv) / rv;
            return (1f - Mathf.Sqrt(ex * ex + ey * ey)) * Mathf.Min(ru * bw, rv * bh);
        }

        static float Limb(float[] keys, float u, float v, float vb, float vt, float rBot, float rTop)
        {
            float vc = Mathf.Clamp01(v);
            float back = Key(keys, 3, 1, vc), front = Key(keys, 3, 2, vc);
            float dx = Mathf.Min((u - back) * bw, (front - u) * bw);
            float dy = Mathf.Min((v - vb) * bh, (vt - v) * bh);
            return Box(dx, dy, v < 0.5f ? rBot : rTop);
        }

        [System.ThreadStatic] static float neckBot;   // set at the top of Build
        static float Head(float u, float v)
        {
            float skull = Ell(u, v, .5f, .6f, .5f, .42f);
            float face = Mathf.Min(Mathf.Min((Key(FaceK, 2, 1, v) - u) * bw, (u - .4f) * bw),
                                   Mathf.Min((v - Key(JawK, 2, 1, u)) * bh, (.85f - v) * bh));
            float t = Mathf.Clamp01((.35f - v) / .5f);
            float nb = Mathf.Lerp(.3f, .22f, t), nf = Mathf.Lerp(.64f, .74f, t);
            float neck = Mathf.Min(Mathf.Min((u - nb) * bw, (nf - u) * bw), Mathf.Min((v + neckBot) * bh, (.45f - v) * bh));
            return SMax(SMax(skull, face, 1.2f), neck, 1.2f);
        }
        static float SMax(float a, float b, float k)
        {
            float h = Mathf.Max(k - Mathf.Abs(a - b), 0f) / k;
            return Mathf.Max(a, b) + h * h * k * 0.25f;
        }

        static float Foot(float u, float v)
        {
            float top = Key(FootTop, 2, 1, u), bot = Key(FootBot, 2, 1, u);
            float dx = Mathf.Min((u + .02f) * bw, (1.02f - u) * bw);
            float dy = Mathf.Min((v - bot) * bh, (top - v) * bh);
            return Box(dx, dy, u < .5f ? 1.4f : .9f);
        }
    }
}
