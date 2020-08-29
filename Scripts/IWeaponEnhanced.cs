using Jundroo.SimplePlanes.ModTools.Interfaces.Parts.Modifiers;
using UnityEngine;

namespace CarnationRED.ActiveDefence
{
    public interface IWeaponEnhanced
    {
        IModifierScript Script { get; }
        float FireDelayCached { get; }
        float FireDelay { get; }
        float MuzzleVelocity { get; }
        Transform Muzzle { get; }
        bool IsArmed { get; }
        bool Gravity { get; }
        float MaxRange { get; }
        string Name { get; }

        void Fire();
        bool Equals(IWeaponEnhanced other);
    }
}