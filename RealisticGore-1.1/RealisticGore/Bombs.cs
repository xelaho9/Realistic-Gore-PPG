using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Mod
{
    public class BombSpec
    {
        public string Name, Desc, Sprite;
        public float Mass, Fuse, Boom, R, Vapor, Blow, Heat, Push, Dmg, Flash, Wave, Fire, Shake;
        public int Frag; public bool Suck, End, Cooks;
    }

    public static class Bombs
    {
        public static readonly BombSpec[] All =
        {
            new BombSpec { Name = "Phone Battery", Sprite = "bomb_0.png", Desc = "Lithium cell. Activate it, shoot it or heat it and it vents fire, then pops.",
                Mass = 0.05f, Fuse = 3f, Boom = 0.8f, R = 1.5f, Heat = 300f, Push = 2f, Dmg = 4f, Wave = 40f, Fire = 0.5f, Shake = 0.3f, Cooks = true },
            new BombSpec { Name = "Car Battery", Sprite = "bomb_1.png", Desc = "Lead-acid. Short it and the hydrogen inside goes up. Also blows if shot or set on fire.",
                Mass = 15f, Fuse = 2f, Boom = 2.5f, R = 3.5f, Blow = 0.8f, Heat = 150f, Push = 5f, Dmg = 15f, Wave = 50f, Fire = 1.2f, Shake = 1f, Cooks = true },
            new BombSpec { Name = "Pipe Bomb", Sprite = "bomb_2.png", Desc = "Steel pipe, two caps, bad intentions. Throws shrapnel everywhere.",
                Mass = 1.5f, Fuse = 4f, Boom = 5f, Frag = 96, R = 6f, Blow = 1.5f, Heat = 60f, Push = 10f, Dmg = 30f, Wave = 80f, Fire = 1.5f, Shake = 2f },
            new BombSpec { Name = "Dynamite Bundle", Sprite = "bomb_3.png", Desc = "Four sticks taped together. Mining equipment, technically.",
                Mass = 2f, Fuse = 5f, Boom = 8f, R = 9f, Blow = 2.5f, Heat = 80f, Push = 18f, Dmg = 50f, Wave = 100f, Fire = 3f, Shake = 3f },
            new BombSpec { Name = "C4 Brick", Sprite = "bomb_4.png", Desc = "Military plastic explosive with a detonator. Short fuse.",
                Mass = 1f, Fuse = 3f, Boom = 12f, R = 14f, Blow = 4f, Heat = 100f, Push = 28f, Dmg = 80f, Wave = 120f, Fire = 4f, Shake = 5f },
            new BombSpec { Name = "Thermobaric Bomb", Sprite = "bomb_5.png", Desc = "Sprays a fuel cloud, sucks the air in, then lights it. Everything nearby burns.",
                Mass = 200f, Fuse = 5f, Boom = 16f, R = 28f, Blow = 6f, Heat = 900f, Push = 35f, Dmg = 100f, Flash = 0.4f, Wave = 120f, Fire = 9f, Shake = 7f, Suck = true },
            new BombSpec { Name = "MOAB", Sprite = "bomb_6.png", Desc = "The biggest regular bomb there is. Flattens a whole area.",
                Mass = 400f, Fuse = 6f, Boom = 25f, R = 60f, Vapor = 3f, Blow = 14f, Heat = 600f, Push = 60f, Dmg = 200f, Flash = 0.6f, Wave = 150f, Fire = 16f, Shake = 10f },
            new BombSpec { Name = "Tactical Nuke", Sprite = "bomb_7.png", Desc = "Small nuclear warhead. Flash, fireball, mushroom cloud, nothing left near the middle.",
                Mass = 300f, Fuse = 8f, Boom = 30f, R = 200f, Vapor = 18f, Blow = 50f, Heat = 3000f, Push = 90f, Dmg = 400f, Flash = 2.5f, Wave = 160f, Fire = 45f, Shake = 15f },
            new BombSpec { Name = "Tsar Bomba", Sprite = "bomb_8.png", Desc = "Biggest bomb ever set off. Covers basically the whole map.",
                Mass = 600f, Fuse = 10f, Boom = 30f, R = 1200f, Vapor = 120f, Blow = 300f, Heat = 6000f, Push = 140f, Dmg = 1000f, Flash = 5f, Wave = 300f, Fire = 160f, Shake = 25f },
            new BombSpec { Name = "Planet Cracker", Sprite = "bomb_9.png", Desc = "Ends the world. Every object on the map turns to ash. Save first.",
                Mass = 50f, Fuse = 10f, Boom = 30f, R = 1e9f, Heat = 9000f, Push = 200f, Dmg = 5000f, Flash = 7f, Wave = 500f, Fire = 400f, Shake = 40f, End = true },
        };

        static bool buffed;
        public static void Register()
        {
            ModAPI.RegisterCategory("Realistic Bombs", "Ten bombs, from a phone battery to the end of the world", ModAPI.LoadSprite("bombs.png"));
            var brick = ModAPI.FindSpawnable("Brick");
            if (!buffed)
            {
                buffed = true;
                // power pass: everything hits harder, small ones included
                foreach (var b in All)
                {
                    b.Boom = Mathf.Min(b.Boom * 2f, 40f);
                    if (!b.End) b.R *= 2f;
                    b.Blow = Mathf.Max(b.Blow * 2.5f, 0.6f);
                    b.Push *= 1.8f; b.Dmg *= 3f; b.Heat *= 1.5f; b.Vapor *= 2f;
                    b.Fire *= 1.4f; b.Wave *= 1.3f; b.Shake *= 1.3f;
                }
            }
            for (int i = 0; i < All.Length; i++)
            {
                int k = i; var s = All[i];
                ModAPI.Register(new Modification
                {
                    OriginalItem = brick,
                    CategoryOverride = ModAPI.FindCategory("Realistic Bombs"),
                    NameOverride = s.Name,
                    NameToOrderByOverride = "RG Bomb " + k.ToString("00"),
                    DescriptionOverride = s.Desc + " Activate to arm (" + s.Fuse.ToString("0") + "s fuse).",
                    ThumbnailOverride = ModAPI.LoadSprite(s.Sprite, 3f),
                    AfterSpawn = inst => Setup(inst, k)
                });
            }
        }

        static void Setup(GameObject inst, int k)
        {
            if (!inst) return;
            var s = All[k];
            var sr = inst.GetComponent<SpriteRenderer>();
            if (sr) sr.sprite = ModAPI.LoadSprite(s.Sprite, 3f);
            Utils.FixColliders(inst);
            var rb = inst.GetComponent<Rigidbody2D>();
            if (rb) rb.mass = s.Mass;
            inst.AddComponent<Bomb>().Tier = k;
        }
    }

    public class Bomb : MonoBehaviour
    {
        public int Tier;
        bool armed, gone; float fuseAt, noteCd;
        SpriteRenderer sr; PhysicalBehaviour pb;
        BombSpec S { get { return Bombs.All[Tier]; } }

        void Start() { sr = GetComponent<SpriteRenderer>(); pb = GetComponent<PhysicalBehaviour>(); }

        public void Use(ActivationPropagation a) { Arm(S.Fuse, true); }

        void Shot(Shot s) { if (Tier <= 4) Arm(Random.Range(0.02f, 0.15f), false); }

        public void Chain() { Arm(Random.Range(0.05f, 0.35f), false); }

        void Arm(float fuse, bool say)
        {
            if (gone) return;
            if (armed) { fuseAt = Mathf.Min(fuseAt, Time.time + fuse); return; }
            armed = true; fuseAt = Time.time + fuse;
            if (say && Time.time > noteCd) { noteCd = Time.time + 1f; ModAPI.Notify(S.Name + " armed: " + fuse.ToString("0") + "s"); }
        }

        void FixedUpdate()
        {
            // batteries cook off when hot or burning
            if (!armed && !gone && S.Cooks && pb && (pb.OnFire || pb.Temperature > 140f)) Arm(Random.Range(0.8f, 2f), false);
        }

        void Update()
        {
            if (!armed || gone) return;
            float left = fuseAt - Time.time;
            if (sr)
            {
                float rate = left < 2f ? 10f : 3f;
                sr.color = Mathf.Repeat(Time.time * rate, 1f) < 0.5f ? new Color(1f, 0.45f, 0.45f) : Color.white;
            }
            if (S.Cooks && pb && Random.value < 0.3f) ModAPI.CreateParticleEffect("Smoke", transform.position);
            if (left <= 0f) Detonate();
        }

        void Detonate()
        {
            gone = true;
            var go = new GameObject("RG_Blast");
            var b = go.AddComponent<Blast>();
            b.S = S; b.C = transform.position; b.Self = gameObject;
            Destroy(gameObject);
        }
    }

    public class Blast : MonoBehaviour
    {
        public BombSpec S; public Vector2 C; public GameObject Self;
        int running;

        IEnumerator Start()
        {
            if (S.Suck)
            {
                // pull the air in first
                Pull();
                yield return new WaitForSeconds(0.35f);
            }
            Vanilla();
            if (S.Flash > 0f) Run(FlashFx(S.Flash, S.End ? Color.white : (S.Flash < 1f ? new Color(1f, 0.75f, 0.4f) : new Color(1f, 0.97f, 0.9f))));
            if (S.Fire > 0f) Run(Fireball());
            try { var cs = Object.FindObjectOfType<CameraShakeBehaviour>(); if (cs) cs.Shake(S.Shake, C, Mathf.Min(S.R * 3f, 5000f)); } catch { }
            if (S.End) ModAPI.Notify("The world has ended.");
            Run(Sweep());
            while (running > 0) yield return null;
            Destroy(gameObject);
        }

        void Run(IEnumerator e) { StartCoroutine(Track(e)); }
        IEnumerator Track(IEnumerator e) { running++; while (true) { bool more; try { more = e.MoveNext(); } catch (System.Exception x) { Debug.LogWarning("[RealisticGore] blast: " + x.Message); more = false; } if (!more) break; yield return e.Current; } running--; }

        void Vanilla()
        {
            var ex = new ExplosionCreator.ExplosionParameters();
            ex.Position = C;
            ex.Range = S.Boom;
            ex.CreateParticlesAndSound = true;
            ex.EffectSize = S.Boom > 6f ? ExplosionCreator.EffectSize.Large : S.Boom > 2f ? ExplosionCreator.EffectSize.Medium : ExplosionCreator.EffectSize.Small;
            ex.FragmentationRayCount = (uint)Mathf.Clamp(S.Frag > 0 ? S.Frag : S.Boom * 6f, 16f, 128f);
            ex.FragmentForce = Mathf.Clamp(S.Boom * 1.5f, 4f, 40f);
            ex.DismemberChance = S.Frag > 0 ? 0.35f : 0.2f;
            ex.BallisticShrapnelCount = S.Frag > 0 ? S.Frag / 3 : 0;
            if (S.Frag > 0) ExplosionCreator.CreateFragmentationExplosion(ex);
            else ExplosionCreator.Explode(ex);

            // big ones: extra blasts around the core for the look
            if (S.Fire >= 9f)
            {
                int n = Mathf.Clamp((int)(S.Fire / 3f), 3, 14);
                for (int i = 0; i < n; i++)
                {
                    var e2 = new ExplosionCreator.ExplosionParameters();
                    e2.Position = C + Random.insideUnitCircle * Mathf.Min(S.Fire * 0.4f, 60f);
                    e2.Range = 6f; e2.CreateParticlesAndSound = true; e2.EffectSize = ExplosionCreator.EffectSize.Large;
                    e2.FragmentationRayCount = 16; e2.FragmentForce = 6f;
                    ExplosionCreator.Explode(e2);
                }
            }
        }

        void Pull()
        {
            foreach (var p in Object.FindObjectsOfType<PhysicalBehaviour>())
            {
                if (!p || p.gameObject == Self) continue;
                Vector2 to = C - (Vector2)p.transform.position; float d = to.magnitude;
                if (d > S.R || d < 0.01f) continue;
                var rb = p.GetComponent<Rigidbody2D>();
                if (rb && rb.bodyType == RigidbodyType2D.Dynamic) rb.velocity += to / d * S.Push * 0.35f * (1f - d / S.R);
            }
        }

        IEnumerator Sweep()
        {
            var list = new List<KeyValuePair<float, PhysicalBehaviour>>();
            foreach (var p in Object.FindObjectsOfType<PhysicalBehaviour>())
            {
                if (!p || p.gameObject == Self) continue;
                float d = Vector2.Distance(p.transform.position, C);
                if (d <= S.R) list.Add(new KeyValuePair<float, PhysicalBehaviour>(d, p));
            }
            list.Sort((a, b) => a.Key.CompareTo(b.Key));
            float rad = 0f; int i = 0;
            while (i < list.Count)
            {
                rad += S.Wave * Time.deltaTime;
                int done = 0;
                while (i < list.Count && list[i].Key <= rad && done < 80)
                {
                    try { Hit(list[i].Value, list[i].Key); } catch { }
                    i++; done++;
                }
                yield return null;
            }
        }

        void Hit(PhysicalBehaviour p, float d)
        {
            if (!p || p.isDisintegrated) return;
            float f = S.End ? 1f : Mathf.Clamp01(1f - d / S.R);
            Vector2 pos = p.transform.position;
            Vector2 dir = pos - C;
            dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Random.insideUnitCircle.normalized;
            dir = (dir + Vector2.up * 0.35f).normalized;

            var bomb = p.GetComponent<Bomb>();
            if (bomb) bomb.Chain();

            var limb = p.GetComponent<LimbBehaviour>();

            // core: gone to ash
            if (S.End || d < S.Vapor)
            {
                p.Temperature += S.Heat;
                if (p.Disintegratable) { p.Disintegrate(); return; }
                if (limb) { limb.Crush(); return; }
            }

            // heat + fire
            float heat = S.Heat * f * f;
            p.Temperature += heat;
            if (heat > 120f && Random.value < 0.4f + f) p.Ignite(true);

            // shove
            var rb = p.GetComponent<Rigidbody2D>();
            if (rb && rb.bodyType == RigidbodyType2D.Dynamic)
            {
                rb.velocity += dir * S.Push * f * Random.Range(0.8f, 1.2f);
                rb.angularVelocity += Random.Range(-1f, 1f) * S.Push * f * 20f;
            }

            if (!limb || limb.IsAndroid) return;
            if (limb.SkinMaterialHandler && heat > 60f) limb.SkinMaterialHandler.AddDamagePoint(DamageType.Burn, pos, Mathf.Clamp(heat / 400f, 0.3f, 1.5f));
            limb.Damage(S.Dmg * f);
            var per = limb.Person;
            if (per) per.Consciousness = Mathf.Min(per.Consciousness, Mathf.Clamp01(1.2f - f * 2f));

            // close in: torn apart by the blast
            if (d < S.Blow && Random.value < 0.35f + 0.6f * (1f - d / Mathf.Max(0.01f, S.Blow)))
            {
                var g = limb.GetComponent<RealGore>();
                if (g) g.BlastHit(pos - dir * 0.05f, dir, 300f + f * 700f);
                else if (!limb.IsDismembered) limb.Slice();
            }
        }

        // white-out over the whole screen
        IEnumerator FlashFx(float secs, Color col)
        {
            var go = new GameObject("RG_Flash");
            var cv = go.AddComponent<Canvas>();
            cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 32000;
            var ig = new GameObject("img", typeof(RectTransform)).AddComponent<Image>();
            ig.transform.SetParent(go.transform, false);
            ig.raycastTarget = false;
            var rt = ig.rectTransform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            float hold = Mathf.Min(1.2f, secs * 0.25f), t = 0f;
            while (t < secs)
            {
                t += Time.unscaledDeltaTime;
                float a = t < hold ? 1f : 1f - (t - hold) / (secs - hold);
                col.a = Mathf.Clamp01(a); ig.color = col;
                yield return null;
            }
            Destroy(go);
        }

        static Sprite ball;
        static Sprite Ball()
        {
            if (ball) return ball;
            const int N = 64;
            var tx = new Texture2D(N, N, TextureFormat.RGBA32, false);
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float n = Mathf.PerlinNoise(x * 0.18f, y * 0.18f) * 0.25f;
                    float a = Mathf.Clamp01((1f - r - n * 0.6f) * 2.5f);
                    byte v = (byte)(255 * Mathf.Clamp01(1f - n));
                    px[y * N + x] = new Color32(v, v, v, (byte)(a * 255));
                }
            tx.SetPixels32(px); tx.Apply(); tx.wrapMode = TextureWrapMode.Clamp;
            ball = Sprite.Create(tx, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
            return ball;
        }

        static SpriteRenderer Puff(string n, int order)
        {
            var sr = new GameObject(n).AddComponent<SpriteRenderer>();
            sr.sprite = Ball(); sr.sortingOrder = order;
            return sr;
        }

        // fireball that rises into a cap, with a stem and a ground ring for the big ones
        IEnumerator Fireball()
        {
            float size = S.Fire;
            bool mush = size >= 16f;
            float life = Mathf.Clamp(size * 0.15f, 1f, 14f);
            var cap = Puff("RG_Fireball", 900);
            SpriteRenderer stem = mush ? Puff("RG_Stem", 899) : null;
            SpriteRenderer ring = mush ? Puff("RG_Ring", 898) : null;
            float t = 0f;
            while (t < life)
            {
                t += Time.deltaTime;
                float k = t / life, grow = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / (life * 0.15f)), 3f);
                Color c = k < 0.08f ? Color.Lerp(Color.white, new Color(1f, 0.8f, 0.3f), k / 0.08f)
                        : k < 0.35f ? Color.Lerp(new Color(1f, 0.8f, 0.3f), new Color(0.85f, 0.25f, 0.05f), (k - 0.08f) / 0.27f)
                        : Color.Lerp(new Color(0.85f, 0.25f, 0.05f), new Color(0.25f, 0.22f, 0.2f), (k - 0.35f) / 0.65f);
                c.a = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
                float rise = mush ? size * 1.2f * Mathf.SmoothStep(0f, 1f, k * 1.6f) : size * 0.3f * k;
                cap.transform.position = C + Vector2.up * rise;
                cap.transform.localScale = new Vector3(size * grow * (mush ? 1f + k * 0.6f : 1f), size * grow * (mush ? 0.7f + k * 0.2f : 1f), 1f);
                cap.color = c;
                if (stem)
                {
                    stem.transform.position = C + Vector2.up * rise * 0.5f;
                    stem.transform.localScale = new Vector3(size * 0.28f * grow, Mathf.Max(0.01f, rise) + size * 0.2f, 1f);
                    Color sc = c * 0.85f; sc.a = c.a * 0.95f; stem.color = sc;
                }
                if (ring)
                {
                    ring.transform.position = C;
                    ring.transform.localScale = new Vector3(size * (0.6f + k * 2.5f), size * 0.12f, 1f);
                    Color rc = new Color(0.55f, 0.45f, 0.35f, (1f - k) * 0.8f); ring.color = rc;
                }
                yield return null;
            }
            Destroy(cap.gameObject);
            if (stem) Destroy(stem.gameObject);
            if (ring) Destroy(ring.gameObject);
        }
    }
}
