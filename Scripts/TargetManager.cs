using Assets.Game.Weapons;
using System.Collections.Generic;
using System.Linq;

namespace CarnationRED.ActiveDefence
{
    public class TargetManager
    {
        public HashSet<ITarget> targets = new HashSet<ITarget>();
        public List<ITarget> targetsInRangeSorted = new List<ITarget>();
        private readonly IRadar radar;

        public TargetManager(IRadar radar)
        {
            this.radar = radar;
        }

        public void AddTarget(ITarget t)
        {
            if (!t.IsDead)
            {
                targets.Add(t);
                SortTargets();
            }
        }
        public void AddTargets(IEnumerable<ITarget> ts)
        {
            foreach (var t in ts)
                if (!t.IsDead)
                    targets.Add(t);
            SortTargets();
        }

        private void SortTargets()
        {
            var pos = radar.Position;
            var sqr = radar.RadarRange * radar.RadarRange;
            if (targets.Count > 0)
            {
                targetsInRangeSorted.Clear();
                IEnumerable<ITarget> inrange = targets.Where(p => !p.IsDead && (pos - p.Position).sqrMagnitude < sqr);
                if (inrange.Count() > 0)
                    targetsInRangeSorted = inrange.OrderBy(p => (pos - p.Position).sqrMagnitude).ToList();
            }
            else
                targetsInRangeSorted.Clear();
        }

        public void RemoveTarget(ITarget t)
        {
            targets.Remove(t);
            SortTargets();
        }

        internal void RemoveDeadTarget()
        {
            targets.RemoveWhere(t => t.IsDead);
            if (targetsInRangeSorted.Any(t => t.IsDead))
                SortTargets();
        }
    }
}