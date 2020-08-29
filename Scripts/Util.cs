using Jundroo.SimplePlanes.ModTools;
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
        static object cameraManagerScript;
        public static object CameraManagerScript
        {
            get
            {
                if (cameraManagerScript == null)
                {
                    cameraManagerScript = Reflections.CameraManagerScript.InvokeMethod("get_Instance", null, null);
                }
                return cameraManagerScript;
            }
        }
        static IGameWorld gameWorld;
        public static IGameWorld GameWorld
        {
            get
            {
                if (gameWorld == null)
                    gameWorld = (IGameWorld)Reflections.GameWorld.InvokeMethod("get_Instance", null, null);
                return gameWorld;
            }set => gameWorld = value;
        }
        public static Rect BoundsToScreenRect(this Bounds bounds)
        {
            // Get mesh origin and farthest extent (this works best with simple convex meshes)
            Vector3 origin = Camera.main.WorldToScreenPoint(new Vector3(bounds.min.x, bounds.max.y, 0f));
            Vector3 extent = Camera.main.WorldToScreenPoint(new Vector3(bounds.max.x, bounds.min.y, 0f));

            // Create rect in screen space and return - does not account for camera perspective
            return new Rect(origin.x, Screen.height - origin.y, extent.x - origin.x, origin.y - extent.y);
        }
    }
}