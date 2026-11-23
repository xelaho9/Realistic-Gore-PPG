using System.Collections.Generic;
using UnityEngine;

namespace Mod
{
    public static class GoreSettings
    {
        // blood
        public static float BleedMult = 1.75f;
        public static float BleedTime = 1f;
        public static float InternalBleed = 1f;
        public static bool HideVanillaBlood = true;
        public static bool Droplets = true;
        public static float DropletMult = 1f;
        public static bool Rills = true;
        public static int RillMax = 8;
        // damage
        public static float ShotDmgMult = 1.4f;
        public static float ImpactDmgMult = 1.3f;
        public static float BoneBreakEase = 0.8f;
        public static float FractureSpeed = 20f;
        public static float CrushSpeed = 45f;
        public static float DismemberMinDmg = 55f;
        public static float RipForce = 900f;
        public static float TorsoRipMult = 3f;
        public static float RipEase = 10f;
        public static float ShockMult = 1f;
        // wounds
        public static float HolePower = 1f;
        public static float HoleSize = 1f;
        public static float BoneSnapDmg = 60f;
        public static float TunnelDmg = 35f;
        public static float ImpactWound = 20f;
        public static float ImpactBoneSnap = 60f;
        public static bool StabWounds = true;
        public static bool RaggedEdges = true;
        public static bool Bruises = true;
        public static bool Strands = true;
        public static bool SoftSkin = true;
        public static bool Powder = true;
        public static bool Mist = true;
        // pieces + organs
        public static bool CutPieces = true;
        public static int ChunkCap = 3;
        public static int PieceCap = 40;
        public static bool EyePop = true;
        // body
        public static float TexDetail = 2.5f;
        public static float BurnSpeed = 1f;
        public static bool Rot = true;
        public static float RotStart = 15f;
        public static float RotTime = 300f;
        public static bool Eyes = true;
        public static bool Heal = true;
        public static float HealSpeed = 1f;
        public static bool Scars = true;

        public static int Version;   // bumped on change

        public class Entry
        {
            public string sec, key, label, tip, unit; public float min, max, def; public int kind; public bool restart;
            public System.Func<float> get; public System.Action<float> set;
            public bool IsBool { get { return kind == 1; } }
            public bool IsInt { get { return kind == 2; } }
            public float Get() { return get(); }
            public void Set(float v) { if (IsBool) set(v > 0.5f ? 1f : 0f); else set(Mathf.Clamp(v, min, max)); }
            public bool Changed { get { return Mathf.Abs(Get() - def) > 0.0005f; } }
        }

        public static readonly string[] Sections = { "BLOOD", "DAMAGE", "WOUNDS", "PIECES", "BODY" };
        public static readonly List<Entry> All = new List<Entry>();

        static void A(string sec, string key, string label, float min, float max, string unit, string tip, System.Func<float> g, System.Action<float> st, int kind, bool restart = false)
        {
            var e = new Entry { sec = sec, key = key, label = label, min = min, max = max, unit = unit, tip = tip, get = g, set = st, kind = kind, restart = restart };
            e.def = e.Get();
            All.Add(e);
        }
        static void B(string sec, string key, string label, string tip, System.Func<float> g, System.Action<float> st, int kind) { A(sec, key, label, 0f, 1f, "", tip, g, st, kind); }

        static bool built;
        public static void Init()
        {
            if (built) return;
            built = true;
            A("BLOOD", "BleedMult", "Blood loss rate", 0f, 6f, "x", "how fast wounds drain the body", () => BleedMult, v => BleedMult = v, 0);
            A("BLOOD", "BleedTime", "Wound bleed time", 0f, 4f, "x", "how long each wound keeps dripping", () => BleedTime, v => BleedTime = v, 0);
            A("BLOOD", "InternalBleed", "Internal bleeding", 0f, 6f, "x", "torso + head shots bleed inside", () => InternalBleed, v => InternalBleed = v, 0);
            B("BLOOD", "HideVanillaBlood", "Hide game blood", "no vanilla streams or stains on realistic bodies", () => HideVanillaBlood ? 1f : 0f, v => HideVanillaBlood = v > 0.5f, 1);
            B("BLOOD", "Droplets", "Blood spray", "droplets thrown out of shot wounds", () => Droplets ? 1f : 0f, v => Droplets = v > 0.5f, 1);
            A("BLOOD", "DropletMult", "Spray amount", 0f, 4f, "x", "droplets per wound", () => DropletMult, v => DropletMult = v, 0);
            B("BLOOD", "Rills", "Blood trickles", "blood running down the skin from wounds", () => Rills ? 1f : 0f, v => Rills = v > 0.5f, 1);
            A("BLOOD", "RillMax", "Trickles per part", 0f, 40f, "", "cap per body part", () => RillMax, v => RillMax = Mathf.RoundToInt(v), 2);

            A("DAMAGE", "ShotDmgMult", "Bullet damage", 0f, 6f, "x", "damage the game deals per shot", () => ShotDmgMult, v => ShotDmgMult = v, 0);
            A("DAMAGE", "ImpactDmgMult", "Impact damage", 0f, 6f, "x", "damage from hits, falls, throws", () => ImpactDmgMult, v => ImpactDmgMult = v, 0);
            A("DAMAGE", "BoneBreakEase", "Bone strength", 0.1f, 4f, "x", "lower = bones break easier", () => BoneBreakEase, v => BoneBreakEase = v, 0);
            A("DAMAGE", "FractureSpeed", "Fracture speed", 5f, 120f, "m/s", "impact speed that can snap a bone", () => FractureSpeed, v => FractureSpeed = v, 0);
            A("DAMAGE", "CrushSpeed", "Crush speed", 10f, 200f, "m/s", "impact speed that pulps a limb", () => CrushSpeed, v => CrushSpeed = v, 0);
            A("DAMAGE", "DismemberMinDmg", "Shot dismember", 0f, 400f, "dmg", "shot damage where limbs can come off", () => DismemberMinDmg, v => DismemberMinDmg = v, 0);
            A("DAMAGE", "RipForce", "Tear-off force", 100f, 6000f, "", "pull needed to rip a limb off", () => RipForce, v => RipForce = v, 0);
            A("DAMAGE", "RipEase", "Limb rip ease", 1f, 30f, "x", "how much easier limbs, head and torso pull apart", () => RipEase, v => RipEase = v, 0);
            A("DAMAGE", "TorsoRipMult", "Torso / neck toughness", 1f, 12f, "x", "how much harder the head and torso tear", () => TorsoRipMult, v => TorsoRipMult = v, 0);
            A("DAMAGE", "ShockMult", "Shock from wounds", 0f, 5f, "x", "pain shock + adrenaline per shot", () => ShockMult, v => ShockMult = v, 0);

            A("WOUNDS", "HolePower", "Hole damage", 0.1f, 5f, "x", "how hard bullets tear (feeds every threshold below)", () => HolePower, v => HolePower = v, 0);
            A("WOUNDS", "HoleSize", "Hole size", 0.2f, 3f, "x", "entry + exit hole radius", () => HoleSize, v => HoleSize = v, 0);
            A("WOUNDS", "BoneSnapDmg", "Bone snap on shot", 5f, 300f, "", "hole damage that shatters bone (default 60 = dmg 300)", () => BoneSnapDmg, v => BoneSnapDmg = v, 0);
            A("WOUNDS", "TunnelDmg", "Through-channel", 5f, 200f, "", "hole damage that tears a channel all the way through", () => TunnelDmg, v => TunnelDmg = v, 0);
            A("WOUNDS", "ImpactWound", "Impact burst force", 8f, 120f, "", "hit force that splits flesh open", () => ImpactWound, v => ImpactWound = v, 0);
            A("WOUNDS", "ImpactBoneSnap", "Impact bone snap", 10f, 250f, "", "hit force that breaks the bone in the wound", () => ImpactBoneSnap, v => ImpactBoneSnap = v, 0);
            B("WOUNDS", "StabWounds", "Stab cuts", "knives open real slits", () => StabWounds ? 1f : 0f, v => StabWounds = v > 0.5f, 1);
            B("WOUNDS", "RaggedEdges", "Torn outline", "wounds at the edge of a limb bite into its outline", () => RaggedEdges ? 1f : 0f, v => RaggedEdges = v > 0.5f, 1);
            B("WOUNDS", "Bruises", "Bruising", "skin reddens then goes purple around hits", () => Bruises ? 1f : 0f, v => Bruises = v > 0.5f, 1);
            B("WOUNDS", "Powder", "Powder burns", "close shots leave soot and powder stipple round the entry hole", () => Powder ? 1f : 0f, v => Powder = v > 0.5f, 1);
            B("WOUNDS", "Mist", "Blood mist", "exit wounds spray fine blood specks on the skin around them", () => Mist ? 1f : 0f, v => Mist = v > 0.5f, 1);
            B("WOUNDS", "SoftSkin", "Stretchy skin", "skin bends and stretches across joints instead of stiff overlapping parts (new spawns)", () => SoftSkin ? 1f : 0f, v => SoftSkin = v > 0.5f, 1);
            B("WOUNDS", "Strands", "Flesh strands", "stretchy tissue hanging across big holes", () => Strands ? 1f : 0f, v => Strands = v > 0.5f, 1);

            B("PIECES", "CutPieces", "Cut-off pieces", "shot-away chunks fall off as their own objects", () => CutPieces ? 1f : 0f, v => CutPieces = v > 0.5f, 1);
            A("PIECES", "ChunkCap", "Chunks per part", 0f, 12f, "", "most pieces one part can lose", () => ChunkCap, v => ChunkCap = Mathf.RoundToInt(v), 2);
            A("PIECES", "PieceCap", "Loose piece limit", 5f, 250f, "", "oldest loose organs vanish past this", () => PieceCap, v => PieceCap = Mathf.RoundToInt(v), 2);
            B("PIECES", "EyePop", "Eye pop", "head shots near the eye blow it out", () => EyePop ? 1f : 0f, v => EyePop = v > 0.5f, 1);

            A("BODY", "TexDetail2", "Texture detail", 0.5f, 2.5f, "x", "sharper skin + wounds, heavier. bodies already out need a map restart", () => TexDetail, v => TexDetail = v, 0, true);
            A("BODY", "BurnSpeed", "Burn speed", 0f, 6f, "x", "how fast fire eats to the bone", () => BurnSpeed, v => BurnSpeed = v, 0);
            B("BODY", "Rot", "Decay", "dead skin greys, greens and darkens", () => Rot ? 1f : 0f, v => Rot = v > 0.5f, 1);
            A("BODY", "RotStart", "Decay starts after", 0f, 600f, "s", "", () => RotStart, v => RotStart = v, 0);
            A("BODY", "RotTime", "Full decay over", 10f, 3600f, "s", "", () => RotTime, v => RotTime = v, 0);
            B("BODY", "Heal", "Wound healing", "living bodies close holes, peel burns and mend bones", () => Heal ? 1f : 0f, v => Heal = v > 0.5f, 1);
            A("BODY", "HealSpeed", "Healing speed", 0.1f, 20f, "x", "1x = about 10 min to fully heal", () => HealSpeed, v => HealSpeed = v, 0);
            B("BODY", "Scars", "Leave scars", "healed wounds and burns stay as pale scars", () => Scars ? 1f : 0f, v => Scars = v > 0.5f, 1);
            B("BODY", "Eyes", "Living eyes", "blinking, glancing, rolled back when out or dead", () => Eyes ? 1f : 0f, v => Eyes = v > 0.5f, 1);
            Load();
        }

        const string Pref = "RealisticGore.settings";
        public static void Load()
        {
            string s = PlayerPrefs.GetString(Pref, "");
            if (string.IsNullOrEmpty(s)) return;
            foreach (var part in s.Split(';'))
            {
                int eq = part.IndexOf('='); if (eq <= 0) continue;
                string k = part.Substring(0, eq); float v;
                if (!float.TryParse(part.Substring(eq + 1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) continue;
                foreach (var e in All) if (e.key == k) { e.Set(v); break; }
            }
            Version++;
        }
        public static void Save()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var e in All)
                if (e.Changed) sb.Append(e.key).Append('=').Append(e.Get().ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(';');
            PlayerPrefs.SetString(Pref, sb.ToString());
            PlayerPrefs.Save();
        }
        public static void Reset(string sec)
        {
            foreach (var e in All) if (sec == null || e.sec == sec) e.Set(e.def);
            Version++;
        }
    }
}
