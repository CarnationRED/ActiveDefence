using Assets.Game;
using Assets.Game.Weapons;
using Jundroo.SimplePlanes.ModTools;
using Jundroo.SimplePlanes.ModTools.Interfaces;
using Jundroo.SimplePlanes.ModTools.Interfaces.Parts;
using Jundroo.SimplePlanes.ModTools.Interfaces.Parts.Modifiers.Weapons;
using Jundroo.SimplePlanes.ModTools.Interfaces.Parts.Modifiers.Weapons.Events;
using Jundroo.SimplePlanes.ModTools.Interfaces.Targeting;
using Jundroo.SimplePlanes.ModTools.Interfaces.Targeting.Events;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using static CarnationRED.ActiveDefence.Reflections;

namespace CarnationRED.ActiveDefence
{
    [DefaultExecutionOrder(20)]
    public class MakeMissileTarget : MapPlugin
    {
        public static MakeMissileTarget _Instance;
        public static MakeMissileTarget Instance
        {
            get
            {
                if (!_Instance)
                {
                    var g = new GameObject("MakeMissileTargetPlugin");
                    _Instance = g.AddComponent<MakeMissileTarget>();
                }
                return _Instance;
            }
        }

        internal static ConstructorInfo aircraftContructor;
        private List<IAircraftScript> allAircraftScripts = new List<IAircraftScript>();
        public Dictionary<ITarget, MissileInfo> aircraftLaunchedMissiles = new Dictionary<ITarget, MissileInfo>();
        public Dictionary<ITarget, SAMMissileInfo> samLaunchedMissiles = new Dictionary<ITarget, SAMMissileInfo>();
        internal static XDocument tempAircraftXml = XDocument.Parse("<Aircraft name=\"Aircraft\" url=\"\" theme=\"Default\" size=\"0.5,1.000002,1\" boundsMin=\"-0.25,1.733354,2.5\" hidden=\"true\" legacyJointIdentification=\"false\">"
                                        + "\n<Assembly>"
                                        + "\n<Parts>"
                                        + "\n<Part id=\"1\" partType=\"Cockpit-2\" position=\"0,2.483356,3\" rotation=\"0,0,0\" centerOfMass=\"0,0,0\" drag=\"0,0,0,0,0,0.\" materials=\"7,0\"/>"
                                        + "\n</Parts>"
                                        + "\n</Assembly>"
                                        + "\n</Aircraft>");
        internal static IAircraft NewAircraft => (IAircraft)aircraftContructor.Invoke(new object[] { tempAircraftXml.Root });

        internal static ReflectedType MissileScript = new ReflectedType(gameMainAssembly, "Assets.Scripts.Parts.Modifiers.Weapons.MissileScript");
        internal static ReflectedType ExplosiveWeaponScriptBase = new ReflectedType(gameMainAssembly, "Assets.Scripts.Parts.Modifiers.Weapons.ExplosiveWeaponScriptBase`2[Assets.Game.AircraftIo.Parts.Modifiers.Weapons.Missile,Assets.Scripts.Explosions.ExplosionBaseScript]");
        internal static ReflectedType AntiAircraftMissileScript = new ReflectedType(gameMainAssembly, "Assets.Scripts.Levels.Enemies.AntiAircraftMissileScript");
        internal static ReflectedType Aircraft = new ReflectedType(gameMainAssembly, "Assets.Game.AircraftIo.Aircraft");
        internal static ReflectedType AircraftGeneratedEventArgs = new ReflectedType(gameMainAssembly, "Assets.Game.AircraftIo.Events.AircraftGeneratedEventArgs");

        private Delegate delAircraftGeneratred;

        static MakeMissileTarget()
        {
            MissileScript.RegisterMethod("CheckProximityDetonation");
            ExplosiveWeaponScriptBase.RegisterMethod("Detonate");
            AntiAircraftMissileScript.RegisterMethod("DestroyMissileKeepParticleEffects");
            Aircraft.RegisterEvent("AircraftGenerated");
            AircraftGeneratedEventArgs.RegisterMethod("get_AircraftScript");
        }

        public struct MissileInfo
        {
            public IAircraftScript missileAircraftScript;
            public IMissileScript missileScript;
            public Rigidbody rigidbody;
            public MissileInfo(IAircraftScript missileAircraftScript, IMissileScript missileScript)
            {
                this.missileAircraftScript = missileAircraftScript;
                this.missileScript = missileScript;
                rigidbody = ((IPartScript)((MonoBehaviour)missileScript).GetComponent(PartScript.type)).Body.RigidBody;
            }
        }
        public struct SAMMissileInfo
        {
            public IAircraftScript missileAircraftScript;
            public MonoBehaviour missileScript;
            public Rigidbody rigidbody;
            public SAMMissileInfo(IAircraftScript missileAircraftScript, MonoBehaviour missileScript)
            {
                this.missileAircraftScript = missileAircraftScript;
                this.missileScript = missileScript;
                rigidbody = missileScript.GetComponent<Rigidbody>();
            }
        }
        void Awake()
        {
            if (!AAMissileFiredProbe.Init(this))
            {
                enabled = false;
                Destroy(this);
            }
            if (Aircraft.events.ContainsKey("AircraftGenerated"))
            {
                var e = Aircraft.events["AircraftGenerated"];
                MethodInfo method = GetType().GetMethod(nameof(AircraftGenerated));
                delAircraftGeneratred = Delegate.CreateDelegate(e.EventHandlerType, this, method, true);
                e.AddEventHandler(null, delAircraftGeneratred);
            }
            for (int i = 0; i < allAircraftScripts.Count; i++)
            {
                IAircraftScript a = allAircraftScripts[i];
                if (a == null)
                    allAircraftScripts.RemoveAt(i--);
            }
            for (int i = 0; i < allAircraftScripts.Count; i++)
            {
                IAircraftScript a = allAircraftScripts[i];
                if (a == null)
                    allAircraftScripts.RemoveAt(i--);
            }
            for (int i = 0; i < aircraftLaunchedMissiles.Count; i++)
            {
                var a = aircraftLaunchedMissiles.ElementAt(i);
                if (a.Value.missileScript == null)
                    aircraftLaunchedMissiles.Remove(a.Key);
            }
            for (int i = 0; i < samLaunchedMissiles.Count; i++)
            {
                var a = samLaunchedMissiles.ElementAt(i);
                if (a.Value.missileScript == null)
                    samLaunchedMissiles.Remove(a.Key);
            }
        }
        private void OnDestroy()
        {
            Util.GameWorld = null;
            Aircraft.events["AircraftGenerated"].RemoveEventHandler(null, delAircraftGeneratred);
            _Instance = null;
        }

        static object[] value = new object[1];
        private void FixedUpdate()
        {
            for (int i = 0; i < aircraftLaunchedMissiles.Count; i++)
            {
                var m = aircraftLaunchedMissiles.ElementAt(i).Value;
                Rigidbody rigidbody = m.rigidbody;
                if (!rigidbody)
                {
                    aircraftLaunchedMissiles.Remove(aircraftLaunchedMissiles.ElementAt(i--).Key);
                    continue;
                }
                value[0] = rigidbody.velocity;
                AircraftScript.InvokeMethod("set_Velocity", m.missileAircraftScript, value);
            }
            for (int i = 0; i < samLaunchedMissiles.Count; i++)
            {
                var m = samLaunchedMissiles.ElementAt(i).Value;
                Rigidbody rigidbody = m.rigidbody;
                if (!rigidbody)
                {
                    samLaunchedMissiles.Remove(samLaunchedMissiles.ElementAt(i--).Key);
                    continue;
                }
                value[0] = rigidbody.velocity;
                AircraftScript.InvokeMethod("set_Velocity", m.missileAircraftScript, value);
            }
        }

        public void AircraftGenerated(object sender, EventArgs args)
        {
            if (args != null)
            {
                var aSc = (IAircraftScript)AircraftGeneratedEventArgs.InvokeMethod("get_AircraftScript", args, null);
                if (aSc != null)
                    if (!allAircraftScripts.Contains(aSc))
                        aSc.TargetingSystem.MissileFired += OnMissleFired;
            }
        }
        public void Register(IAircraftScript aircraft)
        {
            if (allAircraftScripts.Contains(aircraft)) return;
            if (aircraftContructor == null) aircraftContructor = aircraft.Aircraft.GetType().GetConstructor(new Type[] { typeof(XElement) });
            allAircraftScripts.Add(aircraft);
            aircraft.TargetingSystem.MissileFired += OnMissleFired;
            for (int i = 0; i < allAircraftScripts.Count; i++)
            {
                IAircraftScript a = allAircraftScripts[i];
                if (a == null) allAircraftScripts.RemoveAt(i--);
            }
        }

        private void OnMissleFired(object sender, IMissileFiredEventArgs e)
        {
            StartCoroutine(AddMissileTargetToAllAircrafts(CreateTarget4LaunchedMissile(e.Missile)));
        }
        /// <summary>
        /// Notify all aircrafts' targeting system that a missile is launched
        /// </summary>
        IEnumerator AddMissileTargetToAllAircrafts(ITarget missileTarget)
        {
            yield return null;
            foreach (var aircraft in allAircraftScripts)
            {
                if (null == aircraft || !((MonoBehaviour)aircraft).isActiveAndEnabled) continue;
                var targetSys = aircraft.TargetingSystem;
                var targets = targetSys.Targets;
                if (targets == null || targetSys.TargetCount == 0) continue;
                targetSys.AddTarget(missileTarget);
            }
        }
        /// <summary>
        /// Add to Dictionary and return AirTarget ref of the adde missile
        /// </summary>
        /// <param name="missileScript"></param>
        /// <returns>Generated Target</returns>
        public ITarget CreateTarget4LaunchedMissile(IMissileScript missileScript)
        {
            //Register callback to handle missile explsion event
            missileScript.Exploded += OnMissleExploded;
            //Add AircraftScript to the missile
            var missileAircraftScript = (MonoBehaviour)((MonoBehaviour)missileScript).gameObject.AddComponent(AircraftScript.type);
            missileAircraftScript.enabled = false;
            IAircraft tempAircraft = NewAircraft;
            IPartScript partScript = (IPartScript)missileAircraftScript.GetComponent(PartScript.type);
            var firedByName = ((MonoBehaviour)partScript.Aircraft).name;
            //Rename Aircraft
            tempAircraft.Name = $"{missileAircraftScript.name} ({firedByName})";
            //Set AircraftScript values
            AircraftScript.InvokeMethod("set_Aircraft", missileAircraftScript, new object[] { tempAircraft });
            ((IAircraftScript)missileAircraftScript).MainCockpit = partScript;
            //Create the target presenting missile
            var target = (ITarget)AirTarget.type.GetConstructor(new Type[] { AircraftScript.type }).Invoke(new object[] { missileAircraftScript });
            aircraftLaunchedMissiles.Add(target, new MissileInfo((IAircraftScript)missileAircraftScript, (IMissileScript)missileScript));
            return target;
        }
        /// <summary>
        /// Add to Dictionary and return AirTarget ref of the adde missile
        /// </summary>
        /// <returns>Generated Target</returns>
        public ITarget CreateTarget4SAMMissile(MonoBehaviour missileScript)
        {
            //have to add scripts to missile's children to prevent bugs
            var mesh = missileScript.transform.Find("Mesh").gameObject;

            //Add AircraftScript to the missile
            var missileAircraftScript = (IAircraftScript)mesh.AddComponent(AircraftScript.type);
            ((MonoBehaviour)missileAircraftScript).enabled = false;

            //Create an Aircraft ref for missile
            var tempAircraft = NewAircraft;
            //Rename Aircraft
            tempAircraft.Name = "AA Missile";

            //Add PartScript to the missile, and use it as MainCockpit of the AircraftScript
            Component ckpt = mesh.AddComponent(PartScript.type);
            ((MonoBehaviour)ckpt).enabled = false;

            //Set AircraftScript values
            // missileAircraftScript.Aircraft = tempAircraft;
            // AircraftScript.InvokeMethod("set_MainCockpit", missileAircraftScript, new object[] { ckpt });
            AircraftScript.InvokeMethod("set_Aircraft", missileAircraftScript, new object[] { tempAircraft });
            missileAircraftScript.MainCockpit = (IPartScript)ckpt;

            //Create the target presenting missile
            var target = (ITarget)AirTarget.type.GetConstructor(new Type[] { AircraftScript.type }).Invoke(new object[] { missileAircraftScript });
            samLaunchedMissiles.Add(target, new SAMMissileInfo(missileAircraftScript, missileScript));
            return target;
        }

        private void OnMissleExploded(object sender, IMissileExplodedEventArgs e)
        {//Find MissileInfo of exloded chasing missile
            var items = aircraftLaunchedMissiles.ToList();
            for (int id = 0; id < items.Count; id++)
            {
                var item = items[id].Value;

                //chasing missile's target
                var target = item.missileScript.CurrentTarget;
                //both aircraft's missile and SAM missile are all marked as AirTarget by this mod
                if (target.TargetType == TargetType.Air)
                {
                    //Aircraft launched missile
                    if (aircraftLaunchedMissiles.ContainsKey(target))
                    {
                        //get intercepted missile
                        MissileInfo hitMissile = aircraftLaunchedMissiles[target];
                        //Post a message on screen
                        var msg = hitMissile.missileAircraftScript.Aircraft.Name;
                        msg = $"{msg} is intercepted by {item.missileAircraftScript.Aircraft.Name}";
                        Util.GameWorld.ShowStatusMessage(msg, 4f);

                        Destroy((MonoBehaviour)(hitMissile.missileAircraftScript));
                        //Manual detonation because the game won't do
                        ExplosiveWeaponScriptBase.InvokeMethod("Detonate", hitMissile.missileScript, new object[] { Vector3.zero });
                        AirTarget.SetField("_aircraft", target, null);
                        aircraftLaunchedMissiles.Remove(target);
                    }
                    //SAM
                    else
                    {
                        bool proximityDetonation = (bool)MissileScript.InvokeMethod("CheckProximityDetonation", item.missileScript, null);
                        if (proximityDetonation)
                        {
                            //Find the SAM missile and detonate
                            KeyValuePair<ITarget, SAMMissileInfo> i = samLaunchedMissiles.FirstOrDefault(p => p.Key == target);
                            var aaMissile = i.Value;
                            if (aaMissile.missileScript != null)
                            {
                                AntiAircraftMissileScript.InvokeMethod("DestroyMissileKeepParticleEffects", aaMissile.missileScript, new object[] { ((MonoBehaviour)aaMissile.missileScript).transform.position });
                                //Post a message on screen
                                var msg = $"AA missile is intercepted by { item.missileAircraftScript.Aircraft.Name}";
                                Util.GameWorld.ShowStatusMessage(msg, 4f);

                                AirTarget.SetField("_aircraft", i.Key, null);
                                samLaunchedMissiles.Remove(i.Key);
                            }
                        }
                    }
                }
            }
        }

        public class AAMissileFiredProbe : MonoBehaviour
        {
            static ReflectedType AntiAircraftMissileScript = new ReflectedType(gameMainAssembly, "Assets.Scripts.Levels.Enemies.AntiAircraftMissileScript");
            static Transform worldRigidbodiesContainer;
            static Transform WorldRigidbodiesContainer
            {
                get
                {
                    if (!worldRigidbodiesContainer)
                        worldRigidbodiesContainer = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(p => p.name == "LevelLoader").transform.Find("WorldRigidbodiesContainer");
                    return worldRigidbodiesContainer;
                }
            }

            private MakeMissileTarget mmt;
            static AAMissileFiredProbe instance;
            public static bool Init(MakeMissileTarget mmt)
            {
                if (instance)
                    return true;
                if (!WorldRigidbodiesContainer) return false;
                instance = worldRigidbodiesContainer.gameObject.AddComponent<AAMissileFiredProbe>();
                instance.mmt = mmt;
                return true;
            }
            int childCount;
            HashSet<Transform> children = new HashSet<Transform>();
            void Start()
            {
                childCount = transform.childCount;
                foreach (Transform c in transform)
                    children.Add(c);
            }
            private void OnDisable() => instance = null;

            private void Update()
            {
                if (childCount != transform.childCount)
                {
                    children.Remove(null);
                    childCount = transform.childCount;
                    foreach (Transform c in transform)
                        if (children.Add(c))
                        {
                            Component component = c.GetComponent(AntiAircraftMissileScript.type);
                            if (!component) continue;
                            MonoBehaviour missileScript = (MonoBehaviour)component;
                            StartCoroutine(mmt.AddMissileTargetToAllAircrafts(mmt.CreateTarget4SAMMissile(missileScript)));
                        }
                }
            }

            void OnDestroy() => worldRigidbodiesContainer = null;
        }
    }
}