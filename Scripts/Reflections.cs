using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace CarnationRED.ActiveDefence
{
    public class Reflections
    {
        public static ReflectedType AircraftScript;
        public static ReflectedType LevelBase;
        public static ReflectedType Designer;
        public static ReflectedType GameWorld;
        public static ReflectedType InputControllerScript;
        public static ReflectedType JointRotatorScript;
        public static ReflectedType JointRotator;
        public static ReflectedType PartScript;
        public static ReflectedType Part;
        public static ReflectedType PartType;
        public static ReflectedType PartConnection;
        public static ReflectedType AttachPoint;
        public static ReflectedType BodyScript;
        public static ReflectedType PartGroupScript;
        public static ReflectedType GunScript;
        public static ReflectedType Gun;
        public static ReflectedType CannonScript;
        public static ReflectedType Cannon;
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
            AircraftScript = new ReflectedType(gameMainAssembly, "Assets.Scripts.Parts.AircraftScript");
            AircraftScript.RegisterMethod("get_MainCockpit");

            LevelBase = new ReflectedType(gameMainAssembly, "Assets.Scripts.Levels.LevelBase");
            LevelBase.RegisterMethod("get_CurrentLevel");
            LevelBase.RegisterMethod("get_PlayerControlledAircraft");

            Designer = new ReflectedType(gameMainAssembly, "Assets.Game.Design.Designer");
            Designer.RegisterMethod("get_Instance");
            Designer.RegisterMethod("get_CameraController");
            Designer.RegisterField("_cameras", BindingFlags.NonPublic | BindingFlags.Instance);
            Designer.RegisterField("_planeCamera", BindingFlags.NonPublic | BindingFlags.Instance);


            GameWorld = new ReflectedType(gameMainAssembly, "Assets.Game.GameWorld");
            GameWorld.RegisterMethod("get_Instance");
            GameWorld.RegisterMethod("get_FloatingOriginOffset");

            InputControllerScript = new ReflectedType(gameMainAssembly, "Assets.Scripts.Parts.Modifiers.InputControllerScript");
            InputControllerScript.RegisterMethod("set_Value");

            JointRotatorScript = new ReflectedType(gameMainAssembly, "Assets.Scripts.Parts.Modifiers.JointRotatorScript");
            JointRotatorScript.RegisterField("_controller");
            JointRotatorScript.RegisterMethod("get_JointRotator");

            JointRotator = new ReflectedType(gameMainAssembly, "Assets.Game.AircraftIo.Parts.Modifiers.JointRotator");
            JointRotator.RegisterMethod("get_Range");

            PartType = new ReflectedType(gameMainAssembly, "Assets.Game.AircraftIo.Parts.PartType");
            PartType.RegisterMethod("get_PartTypeId");

            Part = new ReflectedType(gameMainAssembly, "Assets.Game.AircraftIo.Parts.Part");
            Part.RegisterMethod("get_PartConnections");
            Part.RegisterMethod("get_PartType");
            Part.RegisterMethod("get_PartScript");

            PartConnection = new ReflectedType(gameMainAssembly, "Assets.Game.AircraftIo.Parts.PartConnection");
            PartConnection.RegisterMethod("GetOtherPart");
            PartConnection.RegisterMethod("get_PartA");
            PartConnection.RegisterMethod("get_PartB");
            PartConnection.RegisterMethod("get_AttachPointsA");
            PartConnection.RegisterMethod("get_AttachPointsB");

            AttachPoint = new ReflectedType(gameMainAssembly, "Assets.Game.AircraftIo.Parts.AttachPoint");
            AttachPoint.RegisterMethod("get_Id");

            PartScript = new ReflectedType(gameMainAssembly, "Assets.Scripts.Parts.PartScript");
            PartScript.RegisterMethod("get_Part");
            PartScript.RegisterMethod("get_Body");
            PartScript.RegisterMethod("get_Modifiers");

            BodyScript = new ReflectedType(gameMainAssembly, "Assets.Scripts.Parts.BodyScript");
            BodyScript.RegisterMethod("get_PartGroups");

            PartGroupScript = new ReflectedType(gameMainAssembly, "Assets.Scripts.Parts.PartGroupScript");
            PartGroupScript.RegisterMethod("get_Parts");

            GunScript = new ReflectedType(gameMainAssembly, "Assets.Scripts.Parts.Modifiers.Weapons.GunScript");
            //Transform
            GunScript.RegisterField("_bulletStartPoint");
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

            CannonScript = new ReflectedType(gameMainAssembly, "Assets.Scripts.Parts.Modifiers.Weapons.CannonScript");
            //Transform
            CannonScript.RegisterField("_muzzleTip");
            CannonScript.RegisterField("_particleSystem");
            CannonScript.RegisterField("_launchSound");
            CannonScript.RegisterMethod("get_FiringDelay");
            CannonScript.RegisterMethod("set_FiringDelay");
            CannonScript.RegisterMethod("get_IsArmed");
            CannonScript.RegisterMethod("get_ProjectileVelocity");
            CannonScript.RegisterMethod("Fire");
            CannonScript.RegisterMethod("get_CanFire");
            CannonScript.RegisterMethod("CheckLastProjectileClearOfBarrel");

           //Cannon = new ReflectedType(gameMainAssembly, "Assets.Game.AircraftIo.Parts.Modifiers.Weapons.Cannon");
           //Cannon.RegisterField("_firingDelay");

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