using System.Collections.Generic;
using UnityEngine;

namespace Mod
{
    // idle life: wave, wander, and get scared of weapons nearby (lives on the head)
    public class Acting : MonoBehaviour
    {
        LimbBehaviour head; PersonBehaviour per; Crater cr;
        Rigidbody2D ua, la, torso;
        readonly HashSet<Rigidbody2D> own = new HashSet<Rigidbody2D>();
        static readonly Collider2D[] hits = new Collider2D[64];

        int act;                 // 0 idle, 1 wave, 2 walk, 3 flee
        float actEnd, nextAct, scanCd, fear, walkWant, map = 1f, mapT, waveSeed;
        Vector2 threat; bool threatSeen;

        void Start()
        {
            head = GetComponent<LimbBehaviour>();
            per = head ? head.Person : null;
            if (!per || head.IsAndroid) { Destroy(this); return; }
            cr = GetComponent<Crater>();
            foreach (var l in per.Limbs)
            {
                if (!l) continue;
                var rb = l.GetComponent<Rigidbody2D>(); if (!rb) continue;
                own.Add(rb);
                if (l.name == "UpperArmFront") ua = rb;
                else if (l.name == "LowerArmFront") la = rb;
                else if (l.name == "UpperBody") torso = rb;
            }
            nextAct = Time.time + Random.Range(3f, 8f);
            scanCd = Random.Range(0f, 0.3f);
            waveSeed = Random.value * 10f;
        }

        bool Calm()
        {
            if (!GoreSettings.Acting || !per || !head || head.IsDismembered || per.Braindead || !per.IsAlive()) return false;
            if (per.Consciousness < 0.9f || per.PainLevel > 0.4f || per.ShockLevel > 0.5f || per.OverridePoseIndex != -1) return false;
            return torso && Vector2.Angle(torso.transform.up, Vector2.up) < 35f;
        }

        bool LegsOk()
        {
            foreach (var l in per.Limbs)
                if (l && (l.IsDismembered || (l.Broken && l.name.Contains("Leg")))) return false;
            return per.IsTouchingFloor;
        }

        static bool IsWeapon(Collider2D c)
        {
            var go = c.attachedRigidbody ? c.attachedRigidbody.gameObject : c.gameObject;
            if (go.GetComponent<LimbBehaviour>()) return false;
            if (go.GetComponent<FirearmBehaviour>() || go.GetComponent<ChainsawBehaviour>() || go.GetComponent<EnergySwordBehaviour>()
                || go.GetComponent<RocketLauncherBehaviour>() || go.GetComponent<GenericScifiWeapon40Behaviour>() || go.GetComponent<ExplosiveBehaviour>()
                || go.GetComponent<StickyGrenadeBehaviour>() || go.GetComponent<SpikeGrenadeBehaviour>()) return true;
            var pb = go.GetComponent<PhysicalBehaviour>();
            return pb && pb.Properties && pb.Properties.Sharp;
        }

        bool HeldBySelf(Collider2D c)
        {
            var rb = c.attachedRigidbody; if (!rb) return false;
            foreach (var j in rb.GetComponents<Joint2D>()) if (j && j.connectedBody && own.Contains(j.connectedBody)) return true;
            return false;
        }

        void Scan()
        {
            threatSeen = false;
            Vector2 me = head.transform.position; float best = float.MaxValue;
            int n = Physics2D.OverlapCircleNonAlloc(me, 7f, hits);
            for (int i = 0; i < n; i++)
            {
                var c = hits[i];
                if (!c || (c.attachedRigidbody && own.Contains(c.attachedRigidbody)) || !IsWeapon(c) || HeldBySelf(c)) continue;
                float d = ((Vector2)c.bounds.center - me).sqrMagnitude;
                if (d < best) { best = d; threat = c.bounds.center; threatSeen = true; }
            }
        }

        void Update()
        {
            if (!head || !per) return;
            if ((scanCd -= Time.deltaTime) <= 0f) { scanCd = 0.3f; if (GoreSettings.Acting) Scan(); else threatSeen = false; }
            bool alive = per.IsAlive() && !per.Braindead;
            fear = Mathf.MoveTowards(fear, threatSeen && alive ? 1f : 0f, Time.deltaTime * (threatSeen ? 3f : 0.4f));
            if (cr) cr.Fear = fear;

            bool calm = Calm();
            if (!calm) { act = 0; return; }

            float now = Time.time;
            if (fear > 0.5f && threatSeen)
            {
                // back away from it
                if (act != 3) { act = 3; mapT = 0f; }
                walkWant = threat.x > head.transform.position.x ? -1f : 1f;
                actEnd = now + 1.5f;
            }
            else if (act != 0 && now > actEnd) { act = 0; nextAct = now + Random.Range(5f, 12f); }
            else if (act == 0 && now > nextAct)
            {
                float r = Random.value;
                if (r < 0.35f && ua && la) { act = 1; actEnd = now + Random.Range(2f, 3.5f); }
                else if (r < 0.8f) { act = 2; mapT = 0f; walkWant = Random.value < 0.5f ? -1f : 1f; actEnd = now + Random.Range(2f, 5f); }
                else nextAct = now + Random.Range(4f, 9f);
            }

            if ((act == 2 || act == 3) && LegsOk())
            {
                per.DesiredWalkingDirection = walkWant * map;
                // learn which sign moves which way for this guy
                mapT += Time.deltaTime;
                if (mapT > 0.8f && torso)
                {
                    float vx = torso.velocity.x;
                    if (Mathf.Abs(vx) > 0.25f && Mathf.Sign(vx) != walkWant) map = -map;
                    mapT = 0f;
                }
            }
        }

        void FixedUpdate()
        {
            if (act != 1 || !ua || !la || !torso) return;
            float f = Mathf.Sign(torso.transform.lossyScale.x);
            float t = Time.time * 9f + waveSeed;
            float baseR = torso.rotation;
            Drive(ua, baseR + f * 155f);
            Drive(la, baseR + f * (160f + 28f * Mathf.Sin(t)));
        }

        static void Drive(Rigidbody2D rb, float target)
        {
            float err = Mathf.DeltaAngle(rb.rotation, target);
            rb.angularVelocity = Mathf.Lerp(rb.angularVelocity, Mathf.Clamp(err * 12f, -720f, 720f), 0.6f);
        }

        void OnDestroy() { if (cr) cr.Fear = 0f; }
    }
}
