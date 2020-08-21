using CarnationRED.ActiveDefence;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class ActiveDefenceTurretBehaviour : Jundroo.SimplePlanes.ModTools.Parts.PartModifierBehaviour
{
    private void Start() => StartCoroutine(Initialize());
    IEnumerator Initialize()
    {
        yield return new WaitForEndOfFrame();
        if (!ServiceProvider.Instance.GameState.IsInDesigner)
        {
            ActiveDefenceTurret setting = (ActiveDefenceTurret)PartModifier;
            ActiveDefenceTurretScript script = gameObject.GetComponent<ActiveDefenceTurretScript>();
            script.P_AziMaxRange = setting.AziMaxRange;
            script.P_AziMinRange = setting.AziMinRange;
            script.P_EleMaxRange = setting.EleMaxRange;
            script.P_EleMinRange = setting.EleMinRange;
            script.p_Speed = setting.Speed;
            script.RadarRange = setting.RadarRange;
            script.p_AutoFire = setting.AutoFire == "Yes";
            script.p_AutoFireRange = (int)(setting.AutoFireRangePct / 100f * setting.RadarRange);
            script.p_AutoSwitchNextTarget = setting.AutoSwitchNextTarget == "Yes";
            script.p_SkipOccludedTarget = setting.SkipOccludedTarget == "Yes";
            script.p_AutoGravity = setting.UseGravity == "Auto";
            script.p_Gravity = setting.UseGravity == "Yes";
            script.p_IgnoreObtacles = setting.IgnoreObstacles == "Yes";
        }
        Destroy(this);
    }
}