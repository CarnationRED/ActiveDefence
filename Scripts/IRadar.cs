using Assets.Game.Weapons;
using Jundroo.SimplePlanes.ModTools.Interfaces;
using UnityEngine;

namespace CarnationRED.ActiveDefence
{
    public interface IRadar
    {
        int RadarRange { get; set; }
        int CurrentTargetIndex { get; set; }

        bool TrackMissilesFirst { get; set; }

        Vector3 Position { get; }
        IAircraftScript AircraftScript { get; }
        ITarget Target { get; set; }
        string TargetingStyle { get; }

        void SelectTarget(int id);
    }
}