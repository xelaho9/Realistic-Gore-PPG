using UnityEngine;

namespace Mod
{
    public class Mod
    {
        // settings-menu prefabs only exist at the main menu, grab them while we can
        public static void OnLoad() { try { GoreSettingsUI.GrabPrefabs(); } catch { } }

        public static void Main()
        {
            // slow frame = game slows down a bit, not 3 s of physics catch-up
            if (Time.maximumDeltaTime > 0.1f) Time.maximumDeltaTime = 0.1f;
            try { GoreSettingsUI.Ensure(); } catch (System.Exception ex) { Debug.LogError("[RealisticGore] settings: " + ex); }
            ModAPI.OnItemSpawned += (s, e) => Apply(e.Instance);
            foreach (var p in Object.FindObjectsOfType<PersonBehaviour>())
                Apply(p.gameObject);
            ModAPI.Notify("Realistic Gore loaded");

            var human = ModAPI.FindSpawnable("Human");
            try { if (human) RealisticHuman.Prebuild(human.Prefab); } catch (System.Exception ex) { Debug.LogWarning("[RealisticGore] prebuild: " + ex.Message); }
            ModAPI.RegisterCategory("Realistic", "Humans from the Realistic Gore mod", ModAPI.LoadSprite("category.png"));
            ModAPI.Register(new Modification
            {
                OriginalItem = human,
                CategoryOverride = ModAPI.FindCategory("Realistic"),
                NameOverride = "Realistic Human (Old)",
                DescriptionOverride = "The first version of the body. Skin, muscle, bone and organs, all of which can end up on the floor.",
                ThumbnailOverride = ModAPI.LoadSprite("human_old.png"),
                AfterSpawn = inst => RealisticHuman.Apply(inst)
            });
            ModAPI.Register(new Modification
            {
                OriginalItem = human,
                CategoryOverride = ModAPI.FindCategory("Realistic"),
                NameOverride = "Realistic Human (New)",
                DescriptionOverride = "Same insides as the old one, nicer outside. Hair, stubble, proper face, better shading.",
                ThumbnailOverride = ModAPI.LoadSprite("human_new.png"),
                AfterSpawn = inst => RealisticHuman.Apply(inst, true)
            });
            ModAPI.Register(new Modification
            {
                OriginalItem = ModAPI.FindSpawnable("Brick"),
                CategoryOverride = ModAPI.FindCategory("Realistic"),
                NameOverride = "Gore Settings",
                DescriptionOverride = "Opens the gore settings. F8 does the same thing. Doesn't spawn anything.",
                ThumbnailOverride = ModAPI.LoadSprite("settings.png"),
                AfterSpawn = inst => { GoreSettingsUI.Open(); if (inst) { inst.SetActive(false); Object.Destroy(inst); } }
            });
            ModAPI.Register(new Modification
            {
                OriginalItem = human,
                CategoryOverride = ModAPI.FindCategory("Realistic"),
                NameOverride = "Realistic Human (Female)",
                DescriptionOverride = "Female version of the new body. Same insides.",
                ThumbnailOverride = ModAPI.LoadSprite("human_female.png"),
                AfterSpawn = inst => RealisticHuman.Apply(inst, true, true)
            });
            Bombs.Register();
        }

        static void Apply(GameObject go)
        {
            if (!go) return;
            foreach (var limb in go.GetComponentsInChildren<LimbBehaviour>())
                if (!limb.GetComponent<RealGore>())
                    limb.gameObject.AddComponent<RealGore>();
        }
    }

    public class RealGore : MonoBehaviour
    {
        static float BleedMult { get { return GoreSettings.BleedMult; } }
        static float DismemberMinDmg { get { return GoreSettings.DismemberMinDmg; } }
        static float FractureSpeed { get { return GoreSettings.FractureSpeed; } }
        static float CrushSpeed { get { return Mathf.Max(GoreSettings.CrushSpeed, GoreSettings.FractureSpeed + 1f); } }
        static float BurnSpeed { get { return GoreSettings.BurnSpeed; } }

        LimbBehaviour limb;
        CirculationBehaviour circ;
        SkinMaterialHandler skin;
        PhysicalBehaviour phys;
        SpriteRenderer sr;
        bool isTorso, isHead;
        float burnTick, burnDepth; Crater crater;
        public bool Strong;                   // set on Flesh Human
        public float BurnDepth { get { return burnDepth; } }
        public void HealBurn(float a) { burnDepth = Mathf.Max(0f, burnDepth - a); }

        void Start()
        {
            limb = GetComponent<LimbBehaviour>();
            if (!limb) { Destroy(this); return; }
            circ = limb.CirculationBehaviour;
            skin = limb.SkinMaterialHandler;
            phys = limb.PhysicalBehaviour;
            sr = GetComponent<SpriteRenderer>();

            string n = name.ToLower();
            isTorso = n.Contains("body");
            isHead = limb.HasBrain;
            if (isHead && !limb.IsAndroid && !GetComponent<Acting>()) gameObject.AddComponent<Acting>();

            if (circ) baseBleed = circ.BloodLossRateMultiplier;
            baseShot = limb.ShotDamageMultiplier; baseImpact = limb.ImpactDamageMultiplier; baseBreak = limb.BreakingThreshold;
            ApplyTuning();

            // pull hard enough and limbs come off (short grace after spawn)
            if (!limb.IsAndroid) Invoke("SetRipForce", 1.5f);
        }

        float baseBleed = 1f, baseShot = 1f, baseImpact = 1f, baseBreak = 1f, baseRip = -1f; int tuned = -1; bool ripSet;
        void ApplyTuning()
        {
            tuned = GoreSettings.Version;
            if (!limb) return;
            if (circ) circ.BloodLossRateMultiplier = baseBleed * GoreSettings.BleedMult;
            limb.ShotDamageMultiplier = baseShot * GoreSettings.ShotDmgMult;
            limb.ImpactDamageMultiplier = baseImpact * GoreSettings.ImpactDmgMult;
            limb.BreakingThreshold = baseBreak * GoreSettings.BoneBreakEase;
            if (ripSet) SetRipForce();
        }

        void SetRipForce()
        {
            if (!limb || !limb.HasJoint || !limb.Joint || limb.IsDismembered) return;
            if (!ripSet) { ripSet = true; baseRip = limb.Joint.breakForce; }
            var rb = GetComponent<Rigidbody2D>();
            float mass = rb ? rb.mass : 1f;
            float f = GoreSettings.RipForce * mass * ((isTorso || isHead) ? GoreSettings.TorsoRipMult : 1f) * (Strong ? 0.7f : 1f) / Mathf.Max(0.1f, GoreSettings.RipEase);
            limb.Joint.breakForce = (float.IsInfinity(baseRip) || baseRip > f) ? f : baseRip;
        }

        void Update()
        {
            if (!skin || !phys || !sr || limb.IsAndroid) return;
            burnTick -= Time.deltaTime;
            if (burnTick > 0) return;
            burnTick = 0.2f;
            if (tuned != GoreSettings.Version) ApplyTuning();

            if (!crater) crater = GetComponent<Crater>();
            if (crater && !GoreSettings.HideVanillaBlood && origDecal != null && limb.BloodDecal == RealBlood.None) limb.BloodDecal = origDecal;
            // hide vanilla blood streams
            if (crater && GoreSettings.HideVanillaBlood)
            {
                if (limb.BloodDecal != RealBlood.None) { if (origDecal == null) origDecal = limb.BloodDecal; limb.BloodDecal = RealBlood.None; }   // no game stains smeared on everything
                if (circ) foreach (var g in circ.BleedingParticles) RealBlood.HideBleeder(g);
                if (transform.childCount != lastKids)
                {
                    lastKids = transform.childCount;
                    foreach (var bp in GetComponentsInChildren<BleedingParticleBehaviour>(true)) RealBlood.HideBleeder(bp.gameObject);
                }
            }

            bool hot = phys.OnFire || phys.Temperature > 150f;
            if (!hot) return;
            float heat = phys.OnFire ? 1f : Mathf.Clamp01((phys.Temperature - 150f) / 400f);
            burnDepth = Mathf.Min(burnDepth + 0.04f * heat * BurnSpeed * (Strong ? 2f : 1f), 3f);

            var b = sr.bounds;
            if (!crater) crater = GetComponent<Crater>();
            // Crater wipes damage points anyway
            int spots = (crater && crater.Ready) ? 0 : 1 + (int)burnDepth;
            for (int i = 0; i < spots; i++)
            {
                Vector2 p = new Vector2(Random.Range(b.min.x, b.max.x), Random.Range(b.min.y, b.max.y));
                skin.AddDamagePoint(DamageType.Burn, p, Mathf.Clamp(0.3f + burnDepth * 0.4f, 0.3f, 1.5f));
            }
            if (burnDepth > 1.5f && circ && Random.value < 0.15f)
                RealBlood.Bleed(circ, b.center, Random.insideUnitCircle, 0.4f);
        }

        void OnJointBreak2D(Joint2D j)
        {
            try { TorsoBreak(j); } catch (System.Exception e) { Debug.LogWarning("[RealisticGore] torso split: " + e.Message); }
            if (!Strong || !limb || j != limb.Joint || !j.connectedBody) return;
            var rb = GetComponent<Rigidbody2D>();
            if (!rb) return;
            var aj = j as AnchoredJoint2D;
            Vector2 at = aj ? (Vector2)transform.TransformPoint(aj.anchor) : (Vector2)transform.position;
            int n = Random.Range(2, 5);
            for (int i = 0; i < n; i++)
            {
                Vector2 off = Random.insideUnitCircle * 0.06f;
                Strand.Create(rb, j.connectedBody, at + off, at + off * 0.5f, Random.Range(0.08f, 0.3f),
                    rb.mass * Random.Range(40f, 120f), Random.Range(0.015f, 0.04f),
                    Color.Lerp(FleshHuman.Tissue, FleshHuman.TissueLight, Random.value * 0.5f), sr, circ);
            }
        }

        // PPG sends Shot to every component on the limb
        void Shot(Shot shot)
        {
            if (!limb || !circ || limb.IsAndroid) return;
            float dmg = shot.damage;

            if (skin) skin.AddDamagePoint(DamageType.Blunt, shot.point, Mathf.Clamp(dmg / 60f, 0.3f, 1.2f));

            // exit spray
            int drops = Mathf.Clamp((int)(dmg / 15f), 1, 6);
            for (int i = 0; i < drops; i++)
            {
                Vector2 dir = (-shot.normal + Random.insideUnitCircle * 0.35f).normalized;
                RealBlood.Bleed(circ, shot.point, dir, Mathf.Clamp(dmg / 40f, 0.4f, 2f));
            }

            if (!crater) crater = GetComponent<Crater>();
            if (crater && GoreSettings.HideVanillaBlood) StartCoroutine(HideImpactsSoon(shot.point));

            if (isTorso || isHead)
                circ.InternalBleedingIntensity += dmg * 0.003f * GoreSettings.InternalBleed;

            var p = limb.Person;
            if (p)
            {
                p.ShockLevel += dmg * 0.01f * GoreSettings.ShockMult;
                p.AdrenalineLevel += dmg * 0.005f * GoreSettings.ShockMult;
            }

            if (GoreSettings.Blowout && dmg >= GoreSettings.BlowoutDmg) { Blowout(shot.point, -shot.normal, dmg); return; }

            if (!isTorso && limb.HasJoint && !limb.IsDismembered && limb.Joint && dmg >= DismemberMinDmg)
            {
                float chance = (dmg - DismemberMinDmg) / 150f;
                if (Random.value < chance)
                    limb.Joint.breakForce = 0f;
            }
        }

        // huge hit: limbs pop, torso gets cut in two at the nearest torso joint
        float blowCd;
        public void BlastHit(Vector2 p, Vector2 dir, float power) { if (GoreSettings.Blowout) Blowout(p, dir, power); else if (!isTorso && !limb.IsDismembered) limb.Slice(); }
        void Blowout(Vector2 p, Vector2 dir, float power)
        {
            if (!limb || limb.IsAndroid) return;
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.down;
            Burst(p, dir, Mathf.Clamp((int)(power / 25f), 10, 24));
            stopCrush = true;   // the game's own crush just deletes the limb
            if (!crater) crater = GetComponent<Crater>();
            var per = limb.Person;
            if (isHead && GoreSettings.HeadPop) { HeadPop(p, dir); return; }
            if (!isTorso)
            {
                if (blown) return;
                if (!crater || !crater.enabled || !crater.Ready) { limb.Crush(); return; }
                blown = true;
                if (limb.HasJoint && limb.Joint && !limb.IsDismembered)
                {
                    var aj = limb.Joint as AnchoredJoint2D;
                    Vector2 ap = aj ? (Vector2)transform.TransformPoint(aj.anchor) : (Vector2)transform.position;
                    var prb = limb.Joint.connectedBody;
                    limb.Joint.breakForce = 0f;
                    var pg = prb ? prb.GetComponent<RealGore>() : null;
                    if (pg) HalfEnd(pg, ap);
                }
                crater.Shred(p, dir);
                if (isHead && per) { per.Braindead = true; per.Consciousness = 0f; }
                var rb = GetComponent<Rigidbody2D>();
                if (rb) rb.AddForce(dir * rb.mass * 6f, ForceMode2D.Impulse);
                return;
            }

            blowCd = Time.time + 0.5f;
            if (crater && crater.enabled) crater.Rip(p, dir);
            Sever(p);
        }

        bool popped; float squeeze, squeezeT;
        public void HeadPop(Vector2 p, Vector2 dir)
        {
            if (popped || !limb || limb.IsAndroid) return;
            popped = true; blown = true;
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.up;
            if (!crater) crater = GetComponent<Crater>();
            var per = limb.Person;
            if (per) { per.Braindead = true; per.Consciousness = 0f; }
            try { if (crater) crater.PopPieces(p, dir); } catch { }
            Burst(p, dir, 30);
            for (int i = 0; i < 4; i++) Burst(p, Random.insideUnitCircle.normalized, 8);
            // neck stump on the body
            if (limb.HasJoint && limb.Joint && !limb.IsDismembered)
            {
                var aj = limb.Joint as AnchoredJoint2D;
                Vector2 ap = aj ? (Vector2)transform.TransformPoint(aj.anchor) : (Vector2)transform.position;
                var prb = limb.Joint.connectedBody;
                var pg = prb ? prb.GetComponent<RealGore>() : null;
                if (pg) HalfEnd(pg, ap);
            }
            stopCrush = false; limb.Crush();   // game removes the head
        }

        // squeezed under something heavy
        void OnCollisionStay2D(Collision2D col) { StayIn(col); }
        void StayIn(Collision2D col)
        {
            if (!isHead || popped || !GoreSettings.HeadPop || !limb || limb.IsAndroid || limb.ImmuneToDamage) return;
            int n = col.contactCount; float imp = 0f;
            for (int i = 0; i < n; i++) imp += col.GetContact(i).normalImpulse;
            squeeze += imp / Mathf.Max(Time.fixedDeltaTime, 1e-4f);
        }
        void FixedUpdate()
        {
            if (!isHead || popped) return;
            if (squeeze > GoreSettings.HeadPopKg * 9.81f) squeezeT += Time.fixedDeltaTime; else squeezeT = Mathf.Max(0f, squeezeT - Time.fixedDeltaTime);
            squeeze = 0f;
            if (squeezeT > 0.12f) HeadPop(transform.position, Random.insideUnitCircle.normalized);
        }

        bool stopCrush, blown;
        void LateUpdate()
        {
            // kill the game's pending CrushNextFrame (same frame as the shot)
            if (stopCrush) { stopCrush = false; if (limb) limb.StopAllCoroutines(); }
        }

        // any torso-to-torso joint giving way (shot, rip, impact): gore both ends, and that's fatal
        void TorsoBreak(Joint2D j)
        {
            if (!limb || limb.IsAndroid || !isTorso || !j || j != limb.Joint) return;
            var other = j.connectedBody ? j.connectedBody.GetComponent<LimbBehaviour>() : null;
            if (!other || !other.name.ToLower().Contains("body")) return;
            var aj = j as AnchoredJoint2D;
            Vector2 p = aj ? (Vector2)transform.TransformPoint(aj.anchor) : (Vector2)transform.position;
            HalfEnd(this, p);
            var og = other.GetComponent<RealGore>();
            if (og) HalfEnd(og, p);
            var per = limb.Person;
            if (per) { per.Braindead = true; per.Consciousness = 0f; }
        }

        static void HalfEnd(RealGore g, Vector2 p)
        {
            if (!g.crater) g.crater = g.GetComponent<Crater>();
            Vector2 into = (Vector2)g.sr.bounds.center - p;
            if (g.crater && g.crater.enabled) g.crater.Rip(p, into);
            g.Burst(p, -into.normalized, 14);
            if (g.circ) g.circ.InternalBleedingIntensity += 2f;
        }

        void Burst(Vector2 p, Vector2 dir, int n)
        {
            if (!circ) return;
            for (int i = 0; i < n; i++)
            {
                Vector2 d = (dir * 0.8f + Random.insideUnitCircle).normalized;
                RealBlood.Bleed(circ, p + Random.insideUnitCircle * 0.05f, d, Random.Range(1.2f, 2.6f));
            }
        }

        void Sever(Vector2 p)
        {
            var rb = GetComponent<Rigidbody2D>();
            var per = limb.Person;
            LimbBehaviour best = null, any = null; float bd = float.MaxValue, ad = float.MaxValue;
            System.Action<LimbBehaviour> consider = l =>
            {
                if (!l || !l.HasJoint || !l.Joint || l.IsDismembered || !l.Joint.enabled) return;
                bool mine = l == limb;
                if (!mine && (!rb || l.Joint.connectedBody != rb)) return;
                var other = mine ? (l.Joint.connectedBody ? l.Joint.connectedBody.GetComponent<LimbBehaviour>() : null) : l;
                float d = ((Vector2)l.transform.TransformPoint(l.Joint.anchor) - p).sqrMagnitude;
                if (d < ad) { ad = d; any = l; }
                if (other && other.name.ToLower().Contains("body") && d < bd) { bd = d; best = l; }
            };
            consider(limb);
            if (per && per.Limbs != null) foreach (var l in per.Limbs) if (l != limb) consider(l);
            var cut = best ? best : any;
            if (cut) cut.Joint.breakForce = 0f;
        }

        int lastKids = -1; DecalDescriptor origDecal;
        System.Collections.IEnumerator HideImpactsSoon(Vector2 at) { yield return null; RealBlood.HideImpacts(at); }

        void OnCollisionEnter2D(Collision2D col) { HitIn(col); }
        void HitIn(Collision2D col)
        {
            if (!limb || limb.IsAndroid || limb.ImmuneToDamage) return;
            float speed = col.relativeVelocity.magnitude;

            // road rash
            if (speed > 12f && skin && col.contactCount > 0)
                skin.AddDamagePoint(DamageType.Blunt, col.GetContact(0).point, Mathf.Clamp((speed - 12f) / 25f, 0.2f, 1f));

            if (isHead && GoreSettings.HeadPop && speed >= CrushSpeed * 0.8f && col.contactCount > 0)
            {
                HeadPop(col.GetContact(0).point, -col.relativeVelocity.normalized);
                return;
            }
            if (speed < FractureSpeed) return;

            if (speed >= CrushSpeed && !isTorso && col.contactCount > 0)
            {
                Blowout(col.GetContact(0).point, -col.relativeVelocity.normalized, speed * 5f);
                return;
            }
            if (isTorso && GoreSettings.Blowout && speed >= GoreSettings.BlowoutSpeed && col.contactCount > 0 && Time.time > blowCd)
            {
                Blowout(col.GetContact(0).point, -col.relativeVelocity.normalized, speed * 5f);
                return;
            }

            float chance = (speed - FractureSpeed) / (CrushSpeed - FractureSpeed);
            if (!limb.Broken && Random.value < chance)
            {
                limb.BreakBone();
                if (circ && Random.value < 0.4f && col.contactCount > 0)
                {
                    var c = col.GetContact(0);
                    RealBlood.Bleed(circ, c.point, c.normal, 0.8f);
                }
            }
        }
    }
}

