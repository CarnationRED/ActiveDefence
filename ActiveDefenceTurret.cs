using System;
using Jundroo.SimplePlanes.ModTools.Parts;
using Jundroo.SimplePlanes.ModTools.Parts.Attributes;
using UnityEngine;

[Serializable]
public class ActiveDefenceTurret : PartModifier
{
    [DesignerPropertySlider(Header = "Azimuth",Label = "Min Range", MinValue = -180, MaxValue = 0, NumberOfSteps = 181)]
    public int AziMinRange = -90;

    [DesignerPropertySlider( Label = "Max Range", MinValue = 0, MaxValue = 180, NumberOfSteps = 181)]
    public int AziMaxRange = 90;

    [DesignerPropertySlider(Header = "Elevation", Label = "Min Range", MinValue = -90, MaxValue = 0, NumberOfSteps = 91)]
    public int EleMinRange = -10;

    [DesignerPropertySlider( Label = "Max Range", MinValue = 0, MaxValue = 90, NumberOfSteps = 91)]
    public int EleMaxRange = 90;

    [DesignerPropertySlider(Header = "Movement", Label = "Speed", MinValue = 10, MaxValue = 100, NumberOfSteps = 10)]
    public int Speed = 100;

    [DesignerPropertySlider(Header = "Targeting", Label = "Rardar Range", MinValue = 500, MaxValue = 10000, NumberOfSteps = 20)]
    public int RadarRange = 2000;

    [DesignerPropertyToggleButton("Yes", "No",  Label = "Auto Fire")]
    public string AutoFire = "No";

    [DesignerPropertySlider(Label = "AutoFire Range(%)",  MinValue = 5, MaxValue = 100, NumberOfSteps = 20)]
    public int AutoFireRangePct = 50;

    [DesignerPropertyToggleButton("Yes", "No", Label = "AutoNextTarget")]
    public string AutoSwitchNextTarget = "Yes";

    [DesignerPropertyToggleButton("Yes", "No", Label = "SkipOccludedTarget")]
    public string SkipOccludedTarget = "Yes";

    [DesignerPropertyToggleButton("Auto", "Yes", "No",Label = "Use Gravity")]
    public string UseGravity = "Auto";

    [DesignerPropertyToggleButton("Yes", "No", Label = "Ignore Obstacles")]
    public string IgnoreObstacles = "Yes";


    public override PartModifierBehaviour Initialize(UnityEngine.GameObject partRootObject)
    {
        var behaviour = partRootObject.AddComponent<ActiveDefenceTurretBehaviour>();
        return behaviour;
    }

    public override void OnDesignerPropertyChanged(string propertyName, string value)
    {
        base.OnDesignerPropertyChanged(propertyName, value);
    }
}