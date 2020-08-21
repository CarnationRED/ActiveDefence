using UnityEngine;

namespace CarnationRED.ActiveDefence
{
    public interface IRadar
    {
        int RadarRange { get; set; }
        Vector3 Position { get; }
    }
}