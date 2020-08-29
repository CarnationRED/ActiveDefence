using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace CarnationRED.ActiveDefence
{
    public class Reflections
    {
        public static ReflectedType GameWorld;
        public static ReflectedType AircraftScript;
        public static ReflectedType CameraManagerScript;
        public static ReflectedType InputControllerScript;
        public static ReflectedType PartScript;
        public static ReflectedType GunScript;
        public static ReflectedType Gun;
        public static ReflectedType CannonScript;
        public static ReflectedType BodyJoint;
        public static ReflectedType JointInfo;
        public static ReflectedType AirTarget;
        public static ReflectedType GroundTarget;


        public static Assembly gameMainAssembly = AppDomain.CurrentDomain
                                        .GetAssemblies()
                                        .Where(x => x.GetName().Name.ToLower() == "assembly-csharp")
                                        .FirstOrDefault();

        static Reflections()
        {
             GameWorld = new ReflectedType(gameMainAssembly, "Assets.Game.GameWorld");
            GameWorld.RegisterMethod("get_Instance");
            GameWorld.RegisterMethod("get_FloatingOriginOffset");

            AircraftScript  = new ReflectedType(gameMainAssembly, "Assets.Scripts.Parts.AircraftScript");
            AircraftScript.RegisterMethod("set_Aircraft");
            AircraftScript.RegisterMethod("set_Velocity");

            CameraManagerScript = new ReflectedType(gameMainAssembly, "Assets.Scripts.Levels.Camera.CameraManagerScript");
            CameraManagerScript.RegisterMethod("get_Instance");
            CameraManagerScript.RegisterMethod("get_MainCamera");
            CameraManagerScript.RegisterField("_currentCameraController", BindingFlags.NonPublic | BindingFlags.Instance);
            CameraManagerScript.RegisterField("_cameras", BindingFlags.NonPublic | BindingFlags.Instance);
            CameraManagerScript.RegisterField("_planeCamera", BindingFlags.NonPublic | BindingFlags.Instance);

            InputControllerScript = new ReflectedType(gameMainAssembly, "Assets.Scripts.Parts.Modifiers.InputControllerScript");
            InputControllerScript.RegisterMethod("set_Value");

            PartScript = new ReflectedType(gameMainAssembly, "Assets.Scripts.Parts.PartScript");

            GunScript = new ReflectedType(gameMainAssembly, "Assets.Scripts.Parts.Modifiers.Weapons.GunScript");
            //Transform
            GunScript.RegisterField("_bulletStartPoint");
            GunScript.RegisterField("_activateFunc");
            GunScript.RegisterMethod("get_FireDelay");
            GunScript.RegisterMethod("set_FireDelay");
            GunScript.RegisterMethod("get_IsArmed");
            GunScript.RegisterMethod("get_Gun");
            GunScript.RegisterMethod("FireGun");

            Gun = new ReflectedType(gameMainAssembly, "Assets.Game.AircraftIo.Parts.Modifiers.Weapons.Gun");
            Gun.RegisterMethod("get_MuzzleVelocity");
            Gun.RegisterMethod("get_Spread");
            Gun.RegisterMethod("set_Spread");
            Gun.RegisterMethod("get_MinTimeBetweenRounds");
            Gun.RegisterMethod("get_TimeBetweenBursts");
            Gun.RegisterMethod("get_BurstCount");
            Gun.RegisterMethod("get_Lifetime");

            CannonScript = new ReflectedType(gameMainAssembly, "Assets.Scripts.Parts.Modifiers.Weapons.CannonScript");
            //Transform
            CannonScript.RegisterField("_muzzleTip");
            CannonScript.RegisterField("_particleSystem");
            CannonScript.RegisterField("_launchSound");
            CannonScript.RegisterField("_activateFunc");
            CannonScript.RegisterMethod("get_FiringDelay");
            CannonScript.RegisterMethod("set_FiringDelay");
            CannonScript.RegisterMethod("get_IsArmed");
            CannonScript.RegisterMethod("get_ProjectileVelocity");
            CannonScript.RegisterMethod("Fire");
            CannonScript.RegisterMethod("get_CanFire");
            CannonScript.RegisterMethod("CheckLastProjectileClearOfBarrel");


            BodyJoint = new ReflectedType(gameMainAssembly, "Assets.Scripts.Parts.BodyJoint");
            BodyJoint.RegisterField("_joints");
            JointInfo = new ReflectedType(gameMainAssembly, "Assets.Scripts.Parts.BodyJoint+JointInfo");
            JointInfo.RegisterMethod("get_Joint");

            AirTarget = new ReflectedType(gameMainAssembly, "Assets.Scripts.Parts.Targeting.AirTarget");
            GroundTarget = new ReflectedType(gameMainAssembly, "Assets.Scripts.Parts.Targeting.GroundTarget");
            AirTarget.RegisterField("_aircraft");
            GroundTarget.RegisterField("_transform");
        }
        public struct ReflectedType
        {
            public Type type;
            public Dictionary<string, MethodInfo> methods;
            public Dictionary<string, FieldInfo> fields;
            public Dictionary<string, EventInfo> events;
            public object InvokeMethod(string name, object obj, object[] args) => methods[name]?.Invoke(obj, args);
            public object GetField(string name, object obj) => fields[name]?.GetValue(obj);

            public void SetField(string name, object obj, object value)
            {
                FieldInfo f = fields[name];
                if (f != null) f.SetValue(obj, value);
            }

            public void RegisterMethod(string name, BindingFlags binding = (BindingFlags)0b1111111111111111111111111)
            {
                MethodInfo[] methodInfos = type.GetMethods((BindingFlags)0b1111111111111111111111111);
                var m = methodInfos.FirstOrDefault(p => p.Name == name);
                if (m != null)
                {
                    methods.Add(name, m);
                }
            }
            public void RegisterField(string name, BindingFlags binding = (BindingFlags)0b1111111111111111111111111)
            {
                var f = type.GetField(name, binding);
                if (f != null)
                    fields.Add(name, f);
            }
            public void RegisterEvent(string name, BindingFlags binding = (BindingFlags)0b1111111111111111111111111)
            {
                var e = type.GetEvent(name, binding);
                if (e != null)
                    events.Add(name, e);
            }
            public void AddEventHandler(string eventName, object eventSender, object handlerObject, MethodInfo handleMethod)
            {
                if (events.ContainsKey(eventName))
                {
                    var e = events[eventName];
                    var del = Delegate.CreateDelegate(e.EventHandlerType, this, handleMethod);
                    e.AddEventHandler(eventSender, del);
                }
            }
            public ReflectedType(Assembly assem, string name)
            {
                type = assem.GetType(name, true);
                methods = new Dictionary<string, MethodInfo>();
                fields = new Dictionary<string, FieldInfo>();
                events = new Dictionary<string, EventInfo>();
            }
        }
    }
}