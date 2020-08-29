using Assets.Game.Weapons;
using Jundroo.SimplePlanes.ModTools.Interfaces.Parts.Modifiers;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarnationRED.ActiveDefence
{
    public class CannonInfo : IWeaponEnhanced
    {
        IModifierScript script;
        float fireDelayCached;
        float fireDelay;
        float velocity;
        Transform muzzle;
        AudioSource audio;
        ParticleSystem ps;
        float lastFireTime;

        public IModifierScript Script => script;
        public float FireDelayCached => fireDelayCached;
        public float FireDelay
        {
            get => (float)Reflections.CannonScript.InvokeMethod("get_FiringDelay", script, null);
            set => Reflections.CannonScript.InvokeMethod("set_FiringDelay", script, new object[] { value });
        }
        public Transform Muzzle => muzzle;

        public bool IsArmed => (bool)Reflections.CannonScript.InvokeMethod("get_IsArmed", script, null);

        public float MuzzleVelocity => velocity;

        public bool Gravity => true;

        public float MaxRange => Mathf.Infinity;

        public float LastFireTime
        {
            get => lastFireTime; set
            {
                lastFireTime = value;
            }
        }

        public string Name => "Cannon";

        public CannonInfo(IModifierScript script)
        {
            this.script = script;
            audio = (AudioSource)Reflections.CannonScript.GetField("_launchSound", script);
            ps = (ParticleSystem)Reflections.CannonScript.GetField("_particleSystem", script);
            muzzle = (Transform)Reflections.CannonScript.GetField("_muzzleTip", script);
            fireDelay = fireDelayCached = (float)Reflections.CannonScript.InvokeMethod("get_FiringDelay", script, null);
            velocity = (float)Reflections.CannonScript.InvokeMethod("get_ProjectileVelocity", script, null);
            lastFireTime = 0;
        }

        public bool Equals(IWeaponEnhanced other)
        {
            if (!(other is CannonInfo o)) return false;
            if (o.script == null)
                return script == null;
            return o.script == script;
        }

        public void Fire()
        {
            if (Time.time - LastFireTime > fireDelay)
            {
                var cs = Reflections.CannonScript;
                if ((bool)cs.InvokeMethod("get_CanFire", script, null))
                {
                    LastFireTime = Time.time;
                    cs.InvokeMethod("Fire", script, null);
                    ps.Play();
                    audio.pitch = Time.timeScale;
                    audio.Play();
                }
            }
        }

        internal struct Comparer : IEqualityComparer<CannonInfo>
        {
            public bool Equals(CannonInfo x, CannonInfo y) => x.script == y.script;

            public int GetHashCode(CannonInfo obj) => obj.script.GetHashCode();
        }
    }
}