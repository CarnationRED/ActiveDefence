using Jundroo.SimplePlanes.ModTools.Interfaces.Parts.Modifiers;
using System;
using UnityEngine;

namespace CarnationRED.ActiveDefence
{
    public struct GunInfo : IWeaponEnhanced
    {
        IModifierScript script;
        float fireDelayCached;
        float fireDelay;
        float velocity;
        float maxRange;
        Transform muzzle;
        float gun_MinTimeBetweenRounds;
        float gun_TimeBetweenBursts;
        int gun_BurstCount;

        public IModifierScript Script => script;
        public float FireDelayCached => fireDelayCached;
        public float FireDelay
        {
            get => (float)Reflections.GunScript.InvokeMethod("get_FireDelay", script, null);
            set => Reflections.GunScript.InvokeMethod("set_FireDelay", script, new object[] { value });
        }
        /// <summary>
        /// Bullet fires forward
        /// </summary>
        public Transform Muzzle => muzzle;
        public bool IsArmed => (bool)Reflections.GunScript.InvokeMethod("get_IsArmed", script, null);

        public float MuzzleVelocity => velocity;

        public bool Gravity => false;

        public float MaxRange => maxRange;

        public string Name => "Gun";

        public GunInfo(IModifierScript script)
        {
            this.script = script;
            object gun = Reflections.GunScript.InvokeMethod("get_Gun", script, null);
            gun_TimeBetweenBursts = (float)Reflections.Gun.InvokeMethod("get_TimeBetweenBursts", gun, null);
            gun_MinTimeBetweenRounds = (float)Reflections.Gun.InvokeMethod("get_MinTimeBetweenRounds", gun, null);
            gun_BurstCount = (int)Reflections.Gun.InvokeMethod("get_BurstCount", gun, null);

            muzzle = (Transform)Reflections.GunScript.GetField("_bulletStartPoint", script);
            fireDelay = fireDelayCached = (float)Reflections.GunScript.InvokeMethod("get_FireDelay", script, null);
            velocity = fireDelayCached = (float)Reflections.Gun.InvokeMethod("get_MuzzleVelocity", gun, null);
            maxRange = velocity * (float)Reflections.Gun.InvokeMethod("get_Lifetime", gun, null);
        }
        public bool Equals(IWeaponEnhanced other)
        {
            if (!(other is GunInfo o)) return false;
            if (o.script == null)
                return script == null;
            return o.script == script;
        }
        static GunInfo()
        {
            var gs = Reflections.GunScript;
            gs.RegisterField("_barrelSpinSpeed");
            gs.RegisterField("_burstTimer");
            gs.RegisterField("_fireTimer");
            gs.RegisterField("_fireEffectsTimer");
            gs.RegisterField("_burstCount");
        }
        public void Fire()
        {
            if (ActiveDefenceTurretScript.p_SilentGod)
                Reflections.GunScript.InvokeMethod("FireGun", script, null);
            else
            {
                var gs = Reflections.GunScript;
                gs.SetField("_barrelSpinSpeed", script, 1000f);
                if ((float)gs.GetField("_burstTimer", script) <= 0f)
                {
                    if ((float)gs.GetField("_fireTimer", script) <= 0f)
                    {
                        gs.SetField("_fireEffectsTimer", script, gun_MinTimeBetweenRounds * 1.5f);
                        gs.InvokeMethod("FireGun", script, null);
                        gs.SetField("_burstCount", script, (int)gs.GetField("_burstCount", script) + 1);
                        gs.SetField("_fireTimer", script, gun_MinTimeBetweenRounds);
                    }
                    if ((int)gs.GetField("_burstCount", script) >= gun_BurstCount)
                    {
                        gs.SetField("_burstCount", script, 0);
                        gs.SetField("_burstTimer", script, gun_TimeBetweenBursts);
                        return;
                    }
                }
            }
        }
        internal struct Comparer : System.Collections.Generic.IEqualityComparer<GunInfo>
        {
            public bool Equals(GunInfo x, GunInfo y) => x.script == y.script;

            public int GetHashCode(GunInfo obj) => obj.script.GetHashCode();
        }
    }
}