using UnityEngine;

namespace Mod
{
    public class Mod
    {
        // settings-menu prefabs only exist at the main menu, grab them while we can
        public static void OnLoad() { try { GoreSettingsUI.GrabPrefabs(); } catch { } }

        public static void Main()
        {
            try { GoreSettingsUI.Ensure(); } catch (System.Exception ex) { Debug.LogError("[RealisticGore] settings: " + ex); }
            ModAPI.OnItemSpawned += (s, e) => Apply(e.Instance);
            foreach (var p in Object.FindObjectsOfType<PersonBehaviour>())
                Apply(p.gameObject);
            ModAPI.Notify("Realistic Gore loaded");

            var human = ModAPI.FindSpawnable("Human");
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

            if (!isTorso && limb.HasJoint && !limb.IsDismembered && limb.Joint && dmg >= DismemberMinDmg)
            {
                float chance = (dmg - DismemberMinDmg) / 150f;
                if (Random.value < chance)
                    limb.Joint.breakForce = 0f;
            }
        }

        int lastKids = -1; DecalDescriptor origDecal;
        System.Collections.IEnumerator HideImpactsSoon(Vector2 at) { yield return null; RealBlood.HideImpacts(at); }

        void OnCollisionEnter2D(Collision2D col)
        {
            if (!limb || limb.IsAndroid || limb.ImmuneToDamage) return;
            float speed = col.relativeVelocity.magnitude;

            // road rash
            if (speed > 12f && skin && col.contactCount > 0)
                skin.AddDamagePoint(DamageType.Blunt, col.GetContact(0).point, Mathf.Clamp((speed - 12f) / 25f, 0.2f, 1f));

            if (speed < FractureSpeed) return;

            if (speed >= CrushSpeed && !isTorso)
            {
                limb.Crush();
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

