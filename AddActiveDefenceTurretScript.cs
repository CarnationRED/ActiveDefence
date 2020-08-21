#define ABC
using CarnationRED.ActiveDefence;
using System;
using System.Linq;
using UnityEngine;

public class AddActiveDefenceTurretScript : MonoBehaviour
{
    static Type scriptType;
    void Start()
    {
#if !ABC
        if (scriptType == null)
        {
            Debug.Log("Finding ActiveDefence assembly");
            var ass = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(p => p.FullName.Contains("ActiveDefence"));
            if (ass != null)
            {
                Debug.Log("Finding ActiveDefenceTurretScript in assembly");
                scriptType = ass.GetTypes().FirstOrDefault(p => p.Name.Contains("ActiveDefenceTurretScript"));
            }
        }
        if (scriptType != null)
        {
            try
            {
                if (GetComponent(scriptType) == null)
                    gameObject.AddComponent(scriptType);
                Debug.Log("ActiveDefenceTurretScript added to turret part");
            }
            catch { }
        }
#else
        try
        {
            if (GetComponent< ActiveDefenceTurretScript>() == null)
                gameObject.AddComponent<ActiveDefenceTurretScript>();
            Debug.Log("ActiveDefenceTurretScript added to turret part");
        }
        catch { }
#endif
        Destroy(this);
    }
}
