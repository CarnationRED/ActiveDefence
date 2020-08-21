using System;
using UnityEngine;

namespace CarnationRED.ActiveDefence
{
    public static class Util
    {
        public static float AngleAroundAxisTo(this Vector3 vector, Vector3 axis, Vector3 to)
        {
            var v = vector;
            if (Vector3.Dot(v, axis) != 0)
                v -= Vector3.Project(v, axis);
            if (Mathf.Abs(v.x) < 1e-4f && Mathf.Abs(v.y) < 1e-4f && Mathf.Abs(v.z) < 1e-4f) return 0;
            if (Vector3.Dot(to, axis) != 0)
                to -= Vector3.Project(to, axis);
            if (Mathf.Abs(to.x) < 1e-4f && Mathf.Abs(to.y) < 1e-4f && Mathf.Abs(to.z) < 1e-4f) return 0;
            var result = Vector3.Angle(v, to);
            if (Vector3.Dot(axis, Vector3.Cross(vector, to)) < 0)
                return result;
            return 360 - result;
        }
        public static float Angle360To180(float angle) => ((angle + 180) % 360) - 180;
        public static Vector3 Angle360To180(Vector3 eular) => new Vector3(Angle360To180(eular.x), Angle360To180(eular.y), Angle360To180(eular.z));
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="axis"></param>
        /// <param name="first"></param>
        /// <param name="second"></param>
        /// <param name="third"></param>
        /// <returns>1 = CW; -1 = CCW; 0 = not ordered</returns>
        public static int VectorsAreOrderedAround(Vector3 axis, Vector3 first, Vector3 second, Vector3 third)
        {
            var f = Vector3.ProjectOnPlane(first.normalized, axis).normalized;
            var a1 = first.AngleAroundAxisTo(axis, f);
            var a2 = second.AngleAroundAxisTo(axis, f);
            var a3 = third.AngleAroundAxisTo(axis, f);
            return (a2 >= a1 && a3 >= a2) ? 1 : ((a2 <= a1 && a3 <= a2) ? -1 : 0);
        }
    }
}