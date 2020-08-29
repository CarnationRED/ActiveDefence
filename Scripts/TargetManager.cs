using Assets.Game.Weapons;
using Jundroo.SimplePlanes.ModTools.Interfaces;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace CarnationRED.ActiveDefence
{
    public class TargetManager
    {

        public static HashSet<ITarget> allTargets = new HashSet<ITarget>();
        //public static int targetCount = 0;
        //public static List<ITarget> allTargetsSorted = new List<ITarget>();
        public List<ITarget> targetsInRangeSorted = new List<ITarget>();
        public List<string> targetNamessInRangeSorted = new List<string>();
        private readonly IRadar radar;
        static Reflections.ReflectedType AircraftGeneratedEventArgs = new Reflections.ReflectedType(Reflections.gameMainAssembly, "Assets.Game.AircraftIo.Events.AircraftGeneratedEventArgs");

        static TargetManager()
        {
            AircraftGeneratedEventArgs.RegisterMethod("get_AircraftScript");
        }

        public TargetManager(IRadar radar)
        {
            this.radar = radar;
        }

        public IEnumerator UpdateCoroutine()
        {
            yield return new WaitForSeconds(UnityEngine.Random.value * 250 / 500f);
            while (true)
            {
                while (!ActiveDefenceTurretScript.gameRunning) yield return new WaitForEndOfFrame();
                yield return new WaitForSeconds(.5f);
                RemoveDeadTarget();
                SortTargets();
            }
        }

        public void AddTarget(ITarget t)
        {
            if (!t.IsDead)
            {
                allTargets.Add(t);
                SortTargets();
            }
        }
        public void AddTargets(IEnumerable<ITarget> ts)
        {
            foreach (var t in ts)
                if (!t.IsDead)
                    allTargets.Add(t);
            SortTargets();
        }

        private void SortTargets()
        {
            var pos = radar.Position;
            var sqr = radar.RadarRange * radar.RadarRange;
            if (allTargets.Count > 0)
            {
                targetsInRangeSorted.Clear();
                IEnumerable<ITarget> inrange = allTargets.Where(p => !p.IsDead && (pos - p.Position).sqrMagnitude < sqr);
                if (inrange.Count() > 0)
                {
                    targetsInRangeSorted = inrange.OrderBy(p => (pos - p.Position).sqrMagnitude).ToList();
                    targetNamessInRangeSorted.Clear();
                    targetNamessInRangeSorted.Capacity = targetsInRangeSorted.Capacity;

                    for (int i = 0; i < targetsInRangeSorted.Count; i++)
                    {
                        ITarget t = targetsInRangeSorted[i];
                        IAircraftScript airTargetScript;
                        Transform groundTargetTransform;
                        string name = "Unkown";
                        if (t.TargetType == TargetType.Ground)
                        {
                            object v = Reflections.GroundTarget.GetField("_transform", t);
                            if (v != null)
                            {
                                groundTargetTransform = (Transform)v;
                                name = groundTargetTransform.parent.name;
                            }
                        }
                        else if (t.TargetType == TargetType.Air)
                        {
                            object v = Reflections.AirTarget.GetField("_aircraft", t);
                            if (v != null)
                            {
                                airTargetScript = (IAircraftScript)v;
                                name = airTargetScript.Aircraft.Name;
                            }
                        }
                        if (name.EndsWith("(Clone)")) name = name.Substring(0, name.Length - 7);
                        targetNamessInRangeSorted.Add(name);
                    }
                }
            }
            else
            {
                targetsInRangeSorted.Clear();
                targetNamessInRangeSorted.Clear();
            }
        }

        public void RemoveTarget(ITarget t)
        {
            allTargets.Remove(t);
            SortTargets();
        }

        internal void RemoveDeadTarget()
        {
            allTargets.RemoveWhere(t => t == null || t.IsDead);
            if (targetsInRangeSorted.Any(t => t.IsDead))
                SortTargets();
        }
        public void OnAircraftGenerated(object sender, EventArgs args)
        {
            if (args != null)
            {
                var aSc = (IAircraftScript)AircraftGeneratedEventArgs.InvokeMethod("get_AircraftScript", args, null);
                if (aSc != null) ;
            }
        }

    }
    public class TargetManager2 : IDisposable
    {

        //  public static HashSet<ITarget> allTargets = new HashSet<ITarget>();

        public IEnumerable<ITarget> targetsInRangeSorted = new List<ITarget>();
        public IEnumerable<bool> targetsIsMissileInRangeSorted = new List<bool>();
        public IEnumerable<string> targetNamesInRangeSorted = new List<string>();
        private readonly IRadar radar;
        private readonly IAircraftScript aircraftScript;
        static Reflections.ReflectedType AircraftGeneratedEventArgs = new Reflections.ReflectedType(Reflections.gameMainAssembly, "Assets.Game.AircraftIo.Events.AircraftGeneratedEventArgs");

        public float targetAutoSwitchActionTime = 0.5f;

        static TargetManager2()
        {
            AircraftGeneratedEventArgs.RegisterMethod("get_AircraftScript");
        }

        public TargetManager2(IRadar radar)
        {
            this.radar = radar;
            this.aircraftScript = radar.AircraftScript;
            aircraftScript.TargetingSystem.TargetAdded += OnTargetAdded;
            aircraftScript.TargetingSystem.TargetRemoved += OnTargetRemoved;
        }

        private void OnTargetAdded(ITarget t)
        {
            FilterAndSortTargets();
        }

        private void OnTargetRemoved(ITarget t)
        {
            FilterAndSortTargets();
        }

        public void FilterAndSortTargets()
        {
            Vector3 radarPos = radar.Position;
            int sqrRange = radar.RadarRange * radar.RadarRange;

            var currTarget = radar.Target;

            targetsInRangeSorted = aircraftScript.TargetingSystem.Targets.Where(p => p != null && !p.IsDead && (p.Position - radarPos).sqrMagnitude <= sqrRange);
            if (radar.TargetingStyle == "Air")
                targetsInRangeSorted = targetsInRangeSorted.Where(p => p.TargetType == TargetType.Air).Where(p => !MakeMissileTarget.Instance.aircraftLaunchedMissiles.ContainsKey(p) && !MakeMissileTarget.Instance.samLaunchedMissiles.ContainsKey(p));
            else if (radar.TargetingStyle == "Ground")
                targetsInRangeSorted = targetsInRangeSorted.Where(p => p.TargetType == TargetType.Ground);
            else if (radar.TargetingStyle == "Missile")
                targetsInRangeSorted = targetsInRangeSorted.Where(p => p.TargetType == TargetType.Air).Where(p => MakeMissileTarget.Instance.samLaunchedMissiles.ContainsKey(p) || MakeMissileTarget.Instance.aircraftLaunchedMissiles.ContainsKey(p));

            targetsInRangeSorted = targetsInRangeSorted.OrderBy(p => (p.Position - radarPos).sqrMagnitude);

            radar.CurrentTargetIndex = targetsInRangeSorted.TakeWhile(p => p != currTarget).Count();

            targetsIsMissileInRangeSorted = targetsInRangeSorted.Select((t)
                => MakeMissileTarget.Instance.aircraftLaunchedMissiles.ContainsKey(t) || MakeMissileTarget.Instance.samLaunchedMissiles.ContainsKey(t));


            if (radar.CurrentTargetIndex >= 0 && radar.CurrentTargetIndex < targetsInRangeSorted.Count())
                //current target is not a missile
                if (!targetsIsMissileInRangeSorted.ElementAt(radar.CurrentTargetIndex))
                {
                    //there's a missile target available
                    if (radar.TrackMissilesFirst && targetsIsMissileInRangeSorted.Any(p => p))
                    {
                        radar.CurrentTargetIndex = targetsIsMissileInRangeSorted.TakeWhile(p => !p).Count();
                        ITarget target = targetsInRangeSorted.ElementAt(radar.CurrentTargetIndex);
                        //SAM 
                        if (MakeMissileTarget.Instance.samLaunchedMissiles.ContainsKey(target))
                        {
                            radar.SelectTarget(radar.CurrentTargetIndex);
                        }
                        //or Air-2-Air
                        else if (MakeMissileTarget.Instance.aircraftLaunchedMissiles.ContainsKey(target))
                        {
                            //missile is not fired by radar's aircraft
                            if (MakeMissileTarget.Instance.aircraftLaunchedMissiles[target].missileScript.PartScript.Aircraft != radar.AircraftScript)
                                radar.SelectTarget(radar.CurrentTargetIndex);
                        }
                    }
                    targetAutoSwitchActionTime = .5f;
                }
                //there's another closer missile
                else
                {
                    if (radar.TrackMissilesFirst && targetsIsMissileInRangeSorted.Any(p => p))
                    {
                        using (IEnumerator<bool> enumerator = targetsIsMissileInRangeSorted.GetEnumerator())
                        {
                            int i = 0;
                            float currTargetDistance = (radar.Target.Position - radar.Position).sqrMagnitude;
                            while (enumerator.MoveNext())
                            {
                                if (enumerator.Current)
                                {
                                    var missile = targetsInRangeSorted.ElementAt(i);
                                    //there's a closer missile
                                    if ((missile.Position - radar.Position).sqrMagnitude < currTargetDistance)
                                    {
                                        //SAM 
                                        if (MakeMissileTarget.Instance.samLaunchedMissiles.ContainsKey(missile))
                                        {
                                            radar.CurrentTargetIndex = i;
                                            radar.SelectTarget(radar.CurrentTargetIndex);
                                            break;
                                        }
                                        //or Air-2-Air
                                        else if (MakeMissileTarget.Instance.aircraftLaunchedMissiles.ContainsKey(missile))
                                            //missile is not fired by radar's aircraft
                                            if (MakeMissileTarget.Instance.aircraftLaunchedMissiles[missile].missileScript.PartScript.Aircraft != radar.AircraftScript)
                                            {
                                                radar.CurrentTargetIndex = i;
                                                radar.SelectTarget(radar.CurrentTargetIndex);
                                                break;
                                            }
                                    }
                                }
                                i++;
                            }
                        }
                    }

                    targetAutoSwitchActionTime = 0.02f;
                }

            targetNamesInRangeSorted = targetsInRangeSorted.Select((t) =>
             {
                 IAircraftScript airTargetScript;
                 Transform groundTargetTransform;
                 string name = "Unkown";
                 if (t.TargetType == TargetType.Ground)
                 {
                     object v = Reflections.GroundTarget.GetField("_transform", t);
                     if (v != null)
                     {
                         groundTargetTransform = (Transform)v;
                         name = groundTargetTransform.parent.name;
                     }
                 }
                 else if (t.TargetType == TargetType.Air)
                 {
                     object v = Reflections.AirTarget.GetField("_aircraft", t);
                     if (v != null)
                     {
                         airTargetScript = (IAircraftScript)v;
                         name = airTargetScript.Aircraft.Name;
                     }
                 }
                 if (name.EndsWith("(Clone)")) name = name.Substring(0, name.Length - 7);
                 return name;
             });
        }

        public IEnumerator UpdateCoroutine()
        {
            yield return new WaitForSeconds(UnityEngine.Random.value * 250 / 500f);
            while (true)
            {
                while (!ActiveDefenceTurretScript.gameRunning) yield return new WaitForEndOfFrame();
                yield return new WaitForSeconds(targetAutoSwitchActionTime);
                FilterAndSortTargets();
            }
        }

        public void Dispose()
        {
            if (aircraftScript != null && aircraftScript.TargetingSystem != null)
            {
                aircraftScript.TargetingSystem.TargetAdded -= OnTargetAdded;
                aircraftScript.TargetingSystem.TargetRemoved -= OnTargetRemoved;
            }
        }
    }
}