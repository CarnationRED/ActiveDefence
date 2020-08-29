using Assets.Game;
using Assets.Game.Weapons;
using Jundroo.SimplePlanes.ModTools;
using Jundroo.SimplePlanes.ModTools.Input;
using Jundroo.SimplePlanes.ModTools.Interfaces;
using Jundroo.SimplePlanes.ModTools.Interfaces.Parts;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
namespace CarnationRED.ActiveDefence
{
    public class ActiveDefenceTurretScript : MonoBehaviour, IRadar
    {
        public IPartScript partScript;
        public Rotator elevator;
        public Rotator azimutor;
        public int p_MaxDamperMultiplier = 150000;
        public int p_MinDamperMultiplier = 5000;
        public int p_Speed = 100;
        private int p_AziMinRange = -170;
        private int p_AziMaxRange = +170;
        private int p_EleMinRange = -89;
        private int p_EleMaxRange = 89;
        public int P_AziMaxRange
        {
            get => p_AziMaxRange; set
            {
                p_AziMaxRange = Mathf.Max(p_AziMinRange, value);
                p_Azi360 = p_AziMaxRange == 180 && p_AziMinRange == -180;
            }
        }
        public int P_AziMinRange
        {
            get => p_AziMinRange; set
            {
                p_AziMinRange = Mathf.Min(p_AziMaxRange, value);
                p_Azi360 = p_AziMaxRange == 180 && p_AziMinRange == -180;
            }
        }
        public int P_EleMinRange
        {
            get => p_EleMinRange; set
            {
                p_EleMinRange = Mathf.Min(p_EleMaxRange, value);
            }
        }
        public int P_EleMaxRange
        {
            get => p_EleMaxRange; set
            {
                p_EleMaxRange = Mathf.Max(p_EleMinRange, value);
            }
        }

        public bool p_MortarMode;
        public bool p_AutoSwitchNextTarget = false;
        public bool p_SkipOccludedTarget = true;
        public bool p_AutoGravity = true;
        public bool p_Gravity = false;
        public bool p_Azi360 = false;
        public bool p_AutoFire = true;
        public bool p_IgnoreObtacles = false;
        public int p_AutoFireRange = 15000;
        public string p_TargetingStyle = "Missile";
        public string TargetingStyle { get => p_TargetingStyle; }
        public static bool p_SilentGod = false;
        public int RadarRange { get; set; } = 15000;
        public int CurrentTargetIndex { get => targetDeadOrLost ? -1 : targetIndex; set => targetIndex = value; }
        public bool TrackMissilesFirst { get => p_TrackMissilesFirst; set => p_TrackMissilesFirst = value; }

        public int p_ActionGroup = 1;
        public bool p_TrackMissilesFirst = true;

        private bool AGEnabled =>
            p_ActionGroup == 0 ? true : (partScript.Aircraft.Controls.GetActivationState(p_ActionGroup));

        private float _speedMult;
        private float _directionErrorPct;
        private float _directionErrorIntergral;
        private float _directionErrorIntergralLimit = 3f;
        private float _directionErrorAngle = 15;
        private float _inertiaCompensator = 0;
        private float targetSwitchedTime;
        private Vector3 targetPosition;
        private Vector3 v_aimAtPosition;
        private Vector3 v_aimDirection;


        private Vector3 v_neutralAzi;
        private Vector3 v_neutralEle;


        public Vector3 Position => transform.position;

        public IAircraftScript AircraftScript => partScript.Aircraft;

        public ITarget Target
        {
            get => target; set
            {
                target = value;
                airTargetScript = null;
                groundTargetTransform = null;
                if (target != null)
                {
                    if (target.TargetType == TargetType.Air)

                        airTargetScript = (IAircraftScript)Reflections.AirTarget.GetField("_aircraft", target);
                    else if (target.TargetType == TargetType.Ground)
                        groundTargetTransform = (Transform)Reflections.GroundTarget.GetField("_transform", target);
                }
            }
        }


        public string Error;
        public string Message = "";

        public bool TargetOutofRange = false;
        public bool OutofZoneOfFire = false;
        public bool TargetOutOfAutoFireRange = false;
        public bool TargetIsOccluded;

        public static bool gameRunning;
        private HashSet<GunInfo> gunInfo;
        private HashSet<CannonInfo> cannonInfo;
        private IWeaponEnhanced mainWeapon;
        private ITarget target;
        private Transform groundTargetTransform;
        private IAircraftScript airTargetScript;
        private bool targetDeadOrLost;
        public TargetManager2 targetManager;
        private int targetIndex = -1;
        private static IGameWorld gameWorld;
        public Transform axle;
        private Transform hub;
        private Transform @base;
        public Material indicatorMat;

        #region Modifying camera fov
        static Type cameraType = Reflections.gameMainAssembly.GetType("Assets.Scripts.Parts.Modifiers.CameraVantageScript", true);
        private bool hasCameraPart, usingCameraView;
        private float cameraDefaultFOV;
        private float cameraCustomFOV;
        private static Camera mcam, pcam, ocam;
        private static Transform cameraPart;

        private float aircraftInitialSizeSqr;

        public void OnSwitchedToCameraView(bool b)
        {
            usingCameraView = b;
        }
        private float GameFOV
        {
            get
            {
                if (!mcam || !pcam)
                {
                    var node = transform.root.Find("CameraNode").Find("ChaseCameras");
                    mcam = node.Find("MainCamera").GetComponent<Camera>();
                    pcam = node.Find("CameraPlane").GetComponent<Camera>();
                    ocam = node.Find("OverlayCamera").GetComponent<Camera>();
                }
                return mcam.fieldOfView;
            }
            set
            {
                if (!mcam || !pcam)
                {
                    var node = transform.root.Find("CameraNode").Find("ChaseCameras");
                    mcam = node.Find("MainCamera").GetComponent<Camera>();
                    pcam = node.Find("CameraPlane").GetComponent<Camera>();
                    ocam = node.Find("OverlayCamera").GetComponent<Camera>();
                }
                ocam.fieldOfView = mcam.fieldOfView = pcam.fieldOfView = value;
            }
        }
        #endregion

#if !ABC
        void OnAircraftViewChanged(object sender, Jundroo.SimplePlanes.ModTools.Events.AircraftViewChangedEventArgs e) => OnSwitchedToCameraView(e.ViewName.StartsWith("Camera"));
        private void Start()
        {
            if (ServiceProvider.Instance.GameState.IsInLevel && !ServiceProvider.Instance.GameState.IsInDesigner)
            {
                ServiceProvider.Instance.GameState.AircraftViewChanged += OnAircraftViewChanged;
                partScript = (IPartScript)GetComponent(Reflections.PartScript.type);
#else
        private void Start()
        {
            if (GameState.Instance.IsInLevel && !GameState.Instance.IsInDesigner)
            {
                partScript = (IPartScript)GetComponent(Reflections.PartScript.type);

#endif
                if (!partScript.ConnectedToMainCockpit)
                {
                    enabled = false;
                    Destroy(this);
                    return;
                }
                MakeMissileTarget.Instance.Register(partScript.Aircraft);
                elevator = gameObject.AddComponent<Rotator>();
                azimutor = gameObject.AddComponent<Rotator>();
                Transform body = transform.Find("Body");
                hub = body.Find("Hub");
                StartCoroutine(
                    ActivateRotators(
                        elevator.Init(partScript, 0, hub),
                        azimutor.Init(partScript, 1, null, @base = transform.Find("Base"))));
                FindWeapons();
                axle = hub.Find("Axle");
                indicatorMat = body.Find("Indicator").GetComponent<MeshRenderer>().material;
                indicatorMat.EnableKeyword("_EMISSION");

                targetManager = new TargetManager2(this);
                StartCoroutine(targetManager.UpdateCoroutine());

                StartCoroutine(AddTargetsCoroutine());

                if (gameWorld == null)
                    gameWorld = (IGameWorld)Reflections.GameWorld.InvokeMethod("get_Instance", null, null);
                _inertiaCompensator = Mathf.Clamp(50 / partScript.Body.InertiaTensorMagnitude, 0.2f, 1);

                aircraftInitialSizeSqr = partScript.Aircraft.Aircraft.Size.sqrMagnitude;


                newestInstanceHash = GetHashCode();
                instances.Add(this);
            }
            else
            {
                Destroy(this);
                instances.Clear();
            }
        }

        IEnumerator AddTargetsCoroutine()
        {
            yield return new WaitForEndOfFrame();
            Vector3 offset = gameWorld.FloatingOriginOffset;
            targetManager.FilterAndSortTargets();

            if (!dummyTarget) dummyTarget = DummyTarget.Create;
        }
        IEnumerator ActivateRotators(bool b1, bool b2)
        {
            if (!b1 || !b2)
            {
                Error = "Can't find Joints to create rotator, contact mod author";
                enabled = false;
                Destroy(this);
            }
            yield return new WaitForEndOfFrame();
            drawUIOnThisOne = GetHashCode() == newestInstanceHash;
            if (drawUIOnThisOne)
                drawUIInstanceID = instances.IndexOf(this);
            azimutor.enabled = b2;
            elevator.enabled = b1;

            //Fix a bug if airplane is spwaned at Bandit Airport
            //transform.root is AircraftContainer
            azimutor.SetAxis(transform.root.InverseTransformDirection(transform.up));
            azimutor.SetMeshAxis(Vector3.up);
            elevator.SetAxis(transform.root.InverseTransformDirection(-transform.right));
            elevator.SetMeshAxis(Vector3.right);

            //this is a must, idk y the game put the hub into another partgroup
            hub.SetParent(transform, true);
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            if (cameraPart)
            {
                float angle = Vector3.Dot(cameraPart.forward, mcam.transform.forward);
                usingCameraView = angle > 0.99f;
            }
        }

        private void FindWeapons()
        {
            gunInfo = new HashSet<GunInfo>(new GunInfo.Comparer());
            cannonInfo = new HashSet<CannonInfo>(new CannonInfo.Comparer());
            #region Get connected part groups
            IPart connectedPart = partScript.Part.AttachPoints.FirstOrDefault(p => p.Id == 0).PartConnections.First().GetOtherPart(partScript.Part);
            IEnumerable<IPartGroupScript> partGroups = connectedPart.PartScript.Body.PartGroups;
            var bodies = new List<Rigidbody>() { connectedPart.PartScript.Body.RigidBody };
            GatherParts(connectedPart, ref partGroups, ref bodies, depth: 0);
            #endregion

            foreach (var item in partGroups)
                foreach (var part in item.Parts)
                    foreach (var weapon in part.Modifiers.Where(p => p is IWeapon))//(p => weaponModifierNames.Contains(p.GetType().Name)))
                        if (weapon.GetType().Name == "GunScript")
                            gunInfo.Add(new GunInfo(weapon));
                        else if (weapon.GetType().Name == "CannonScript")
                            cannonInfo.Add(new CannonInfo(weapon));

            Component a;
            foreach (var b in bodies)
                if (a = b.GetComponentInChildren(cameraType))
                {
                    cameraPart = a.transform;
                    cameraCustomFOV = cameraDefaultFOV = GameFOV;
                    hasCameraPart = true;
                    break;
                }
        }

        private void GatherParts(IPart connectedPart, ref IEnumerable<IPartGroupScript> partGroups, ref List<Rigidbody> bodies, int depth)
        {
            const int maxDepth = 5;
            depth++;
            foreach (var att in connectedPart.AttachPoints)
                foreach (var pc in att.PartConnections)
                {
                    IPart part1 = pc.GetOtherPart(connectedPart);
                    if (depth < maxDepth) GatherParts(part1, ref partGroups, ref bodies, depth);
                    if (part1 != null && part1 != partScript.Part)
                    {
                        partGroups = partGroups.Concat(part1.PartScript.Body.PartGroups);
                        bodies.Add(part1.PartScript.Body.RigidBody);
                    }
                }
        }

        static DummyTarget dummyTarget;
        static bool dummyTargetEnabled = false;
        private bool p_MouseAim;


        #region Interacts with game's camera control
        static object inputWidgetScript;
        static Type typeInputWidgetScript;
        static object InputWidgetScript
        {
            get
            {
                if (inputWidgetScript == null)
                {
                    if (typeInputWidgetScript == null)
                        typeInputWidgetScript = Reflections.gameMainAssembly.GetTypes().FirstOrDefault(p => p.Name.IndexOf("InputWidgetScript") >= 0);
                    var objs = FindObjectsOfType(typeInputWidgetScript);
                    inputWidgetScript = objs.FirstOrDefault();
                }
                return inputWidgetScript;
            }
        }

        private bool mouseOnWindow;

        public static bool GameCameraControlActive
        {
            set
            {
                if (InputWidgetScript != null)
                {
                    GameObject gameObject1 = ((MonoBehaviour)InputWidgetScript).gameObject;
                    if (gameObject1) gameObject1.SetActive(value);
                }
            }
        }

        #endregion

        private void Update()
        {
#if ABC
            gameRunning = GameState.Instance.IsInLevel && !GameState.Instance.IsInDesigner && !GameState.Instance.IsPaused;
#else
            gameRunning = ServiceProvider.Instance.GameState.IsInLevel && !ServiceProvider.Instance.GameState.IsInDesigner && !ServiceProvider.Instance.GameState.IsPaused;
#endif
            if (gameRunning)
            {
                if (partScript.Aircraft.CriticallyDamaged)
                {
                    enabled = false;
                    Destroy(this);
                }

                Vector3 pos = Input.mousePosition;
                if (drawUIOnThisOne)
                {
                    #region Toggle UI
                    if (Input.GetKeyDown(KeyCode.H))
                    {
                        showUI = !showUI;
                        if (!showUI)
                            GameCameraControlActive = true;
                    }
                    #endregion

                    #region Disable Camera Control is mouse is on ui
                    if (showUI)
                    {
                        bool v = windowRect.Contains(new Vector2(pos.x, Screen.height - pos.y));
                        if (mouseOnWindow != v)
                        {
                            mouseOnWindow = v;
                            GameCameraControlActive = !mouseOnWindow;
                        }
                    }
                    #endregion

                    #region Handles Camera FOV
                    if (hasCameraPart)
                    {
                        if (!cameraPart)
                            hasCameraPart = false;
                        else
                        {
                            if (usingCameraView)
                            {
                                float angle = Vector3.Dot(cameraPart.forward, mcam.transform.forward);
                                usingCameraView = angle > 0.99f;
                            }
                            if (!usingCameraView)
                                GameFOV = cameraDefaultFOV;
                            else
                            {
                                var s = Input.GetAxis("Mouse ScrollWheel");
                                cameraCustomFOV -= s * Mathf.Clamp(cameraCustomFOV * .15f, 0, 15) * .5f;
                                cameraCustomFOV = Mathf.Clamp(cameraCustomFOV, 0.25f, 90f);
                                GameFOV = cameraCustomFOV;
                            }
                        }
                    }
                    #endregion
                }
                if (!AGEnabled) return;

                targetDeadOrLost = Target == null || (Target.IsDead || (Target.IsDead && Target.IsLost));
                _inertiaCompensator = Mathf.Clamp(50 / partScript.Body.InertiaTensorMagnitude, 0.2f, 1);
                var color = TargetOutofRange ? outofrange : (OutofZoneOfFire ? outofZoF : normal);
                #region Aim at mouse position
                //Aim at mouse position
                if (Input.GetKeyDown(KeyCode.M))
                {
                    if (drawUIOnThisOne)
                    {
                        p_MouseAim = !p_MouseAim;
                        if (Input.GetKey(KeyCode.RightAlt))
                            foreach (var adts in instances.Where(p => p && p != this))
                            {
                                adts.p_MouseAim = p_MouseAim;
                                if (p_MouseAim)
                                {
                                    adts.Target = dummyTarget;
                                }
                                else
                                {
                                    adts.Target = null;
                                    adts.targetDeadOrLost = true;
                                }
                            }

                        if (p_MouseAim)
                        {
                            Target = dummyTarget;
                            dummyTarget.gameObject.SetActive(false);
                            dummyTargetEnabled = false;
                        }
                        else
                        {
                            Target = null;
                            targetDeadOrLost = true;
                        }
                    }
                }
                #endregion

                #region create a debug target (red cube)
                if (drawUIOnThisOne)
                {
                    if (Input.GetKeyDown(KeyCode.O))
                    {
                        dummyTargetEnabled = !dummyTargetEnabled;
                        if (Input.GetKey(KeyCode.RightAlt))
                            foreach (var adts in instances.Where(p => p && p != this))
                            {
                                if (dummyTargetEnabled)
                                {
                                    adts.Target = dummyTarget;
                                }
                                else
                                {
                                    adts.Target = null;
                                    adts.targetDeadOrLost = true;
                                }
                            }
                        if (dummyTargetEnabled)
                        {
                            p_MouseAim = false;
                            target = dummyTarget;
                            dummyTarget.transform.position = new Vector3(25, 25, 25);
                            dummyTarget.body.velocity = Vector3.zero;
                            dummyTarget.body.WakeUp();
                        }
                        else
                        {
                            Target = null;
                            targetDeadOrLost = true;
                            dummyTarget.body.position = Vector3.zero;
                        }

                        dummyTarget.gameObject.SetActive(dummyTargetEnabled);
                    }
                    #endregion

                    #region Handles mouse aiming or debug target movement
                    else if (dummyTargetEnabled || p_MouseAim)
                    {
                        #region Find mouse pointing point
                        Ray ray = Camera.main.ScreenPointToRay(pos);
                        pos = transform.position;
                        Vector3 point;
                        int i = 4;
                        point = ray.origin + ray.direction * 1000f;
                        while (i-- > 0)
                        {
                            if (Physics.Raycast(ray, out var hit, 65000, 0b1111111111111111111111111111111))
                            {
                                point = hit.point;
                                if ((point - pos).sqrMagnitude < 10)
                                {
                                    ray.origin = point;
                                    continue;
                                }
                                break;
                            }
                            //if no hit point, get a point on a sphere 
                            else
                            {
                                point = ray.origin + ray.direction * 1000f;
                                break;
                            }
                        }
                        #endregion

                        if (p_MouseAim)
                            dummyTarget.body.position = point;
                        else
                            dummyTarget.Dist = point;
                    }
                }
                #endregion

                #region Switch target
                if (Input.GetKeyDown(Settings.NextTarget))
                {
                    NextTarget();
                }
                else if (Input.GetKeyDown(Settings.PrevTarget))
                {
                    PrevTarget();
                }
                #endregion
                TargetOutOfAutoFireRange = false;
                if (!targetDeadOrLost)
                {
                    #region Remove damaged or disapeared weapon
                    gunInfo.RemoveWhere(weapon => weapon.Script == null || !((MonoBehaviour)weapon.Script).isActiveAndEnabled);
                    cannonInfo.RemoveWhere(weapon => weapon.Script == null || !((MonoBehaviour)weapon.Script).isActiveAndEnabled);
                    if (gunInfo.Count + cannonInfo.Count == 0)
                    {
                        enabled = false;
                        Destroy(this);
                        return;
                    }
                    #endregion
                    #region Make sure there's an armed weapon as main weapon
                    if (mainWeapon == null || !mainWeapon.IsArmed)
                    {
                        FindMainWeapon();
                    }
                    #endregion
                    #region Auto Fire
                    if (!TargetOutofRange)
                        if (p_AutoFire)
                        {
                            if (p_IgnoreObtacles || (!OutofZoneOfFire && _directionErrorAngle < 0.5f))
                                if (!(TargetIsOccluded = IsTargetOccluded()))
                                {
                                    if (Vector3.Distance(Target.Position, mainWeapon.Muzzle.position) <= p_AutoFireRange)
                                    {
                                        if (mainWeapon != null && mainWeapon.IsArmed && Time.time - targetSwitchedTime > 0.25f)
                                        {
                                            foreach (var w in gunInfo) w.Fire();
                                            foreach (var w in cannonInfo) w.Fire();
                                            color = firing;
                                        }
                                    }
                                    else
                                    {
                                        TargetOutOfAutoFireRange = true;
                                        if (!p_MouseAim)
                                            NextTarget();
                                    }
                                }
                                else color = occluded;
                        }
                    #endregion
                }
                #region Auto Switch target
                else if (p_AutoSwitchNextTarget)
                {
                    if (targetManager.targetsInRangeSorted.Count() > 0)
                    {
                        Target = targetManager.targetsInRangeSorted.Where(p =>
                        {
                            if (MakeMissileTarget.Instance.aircraftLaunchedMissiles.ContainsKey(p))
                                if (MakeMissileTarget.Instance.aircraftLaunchedMissiles[p].missileScript.PartScript.Aircraft == AircraftScript)
                                    return false;
                            return true;
                        }).FirstOrDefault();
                        if (Target != null)
                            targetIndex = targetManager.targetsInRangeSorted.TakeWhile(p => p != target).Count();
                    }
                }
                #endregion
                if (p_MouseAim || dummyTargetEnabled) color = debug;
                #region Set indicator's color
                indicatorMat.SetColor("_EmissionColor", color);
                #endregion
            }
        }

        #region Switch Target
        private void PrevTarget()
        {
            targetIndex--;
            if (targetIndex == -1) targetIndex = targetManager.targetsInRangeSorted.Count() - 1;
            targetIndex = Mathf.Clamp(targetIndex, 0, targetManager.targetsInRangeSorted.Count() - 1);
            if (targetManager.targetsInRangeSorted.Count() > 0)
                Target = targetManager.targetsInRangeSorted.ElementAt(targetIndex);
            targetSwitchedTime = Time.time;
        }
        private void NextTarget()
        {
            targetIndex++;
            if (targetIndex == targetManager.targetsInRangeSorted.Count()) targetIndex = 0;
            targetIndex = Mathf.Clamp(targetIndex, 0, targetManager.targetsInRangeSorted.Count() - 1);
            if (targetManager.targetsInRangeSorted.Count() > 0)
                Target = targetManager.targetsInRangeSorted.ElementAt(targetIndex);
            targetSwitchedTime = Time.time;
        }

        public void SelectTarget(int id)
        {
            targetIndex = id;
            targetIndex = Mathf.Clamp(targetIndex, 0, targetManager.targetsInRangeSorted.Count() - 1);
            if (targetManager.targetsInRangeSorted.Count() > 0)
                Target = targetManager.targetsInRangeSorted.ElementAt(targetIndex);

            targetSwitchedTime = Time.time;
        }
        #endregion

        public bool IsTargetOccluded()
        {
            if (targetDeadOrLost || mainWeapon == null || !mainWeapon.IsArmed) return true;
            Vector3 muzzlePos = mainWeapon.Muzzle.position;
            Ray ray = new Ray(muzzlePos, _directionErrorAngle > 0.2f ? mainWeapon.Muzzle.forward : (((p_Gravity || (p_AutoGravity && mainWeapon.Gravity)) ? targetPosition : v_aimAtPosition) - muzzlePos));
            if (Physics.Raycast(ray, out var hit, 600000, 0b1111111111111111111111111111111 & (~(1 << 25 | 1 << 4))))
            {
                if (Target.TargetType == TargetType.Air)
                {
                    if (airTargetScript == null || hit.collider.transform.IsChildOf(airTargetScript.Children))
                        return false;
                }
                else if (Target.TargetType == TargetType.Ground)
                {
                    if (!groundTargetTransform || (hit.rigidbody && groundTargetTransform.parent && hit.rigidbody.transform.IsChildOf(groundTargetTransform.parent)) || hit.collider.transform.IsChildOf(groundTargetTransform))
                        return false;
                }
                else if (p_MouseAim) return false;
                var n = hit.transform.root.name;
                return n == "LevelLoader" || n == "AircraftContainer" || Vector3.SqrMagnitude(transform.position - hit.point) < aircraftInitialSizeSqr || hit.distance < (v_aimAtPosition - muzzlePos).magnitude - 2.5f;
            }
            else return false;
        }

        private void FindMainWeapon()
        {
            mainWeapon = null;
            foreach (var wp in cannonInfo)
                if (wp.IsArmed)
                {
                    mainWeapon = wp;
                    break;
                }
            foreach (var wp in gunInfo)
                if (wp.IsArmed)
                {
                    mainWeapon = wp;
                    break;
                }
            if (mainWeapon != null)
            {
                var fwd = axle.InverseTransformDirection(mainWeapon.Muzzle.forward);
                v_neutralAzi = Quaternion.AngleAxis((p_AziMaxRange + p_AziMinRange) * .5f, Vector3.up) * fwd;
                v_neutralEle = Quaternion.AngleAxis((p_EleMinRange + p_EleMaxRange) * .5f, Vector3.right) * fwd;
            }
        }

        void FixedUpdate()
        {
            if (!gameRunning || Target == null || mainWeapon == null) return;
            if (!AGEnabled) return;
            targetDeadOrLost = Target.IsDead || (Target.IsDead && Target.IsLost);
            if (targetDeadOrLost) return;

            #region Aim at target
            var v_gunToTarget = Target.Position - mainWeapon.Muzzle.position;
            var v_gunDirection = mainWeapon.Muzzle.forward;

            targetPosition = Target.Position;
            v_aimAtPosition = targetPosition;


            #region Fts solution, this is awesome! I added some code to handle some rare cases
            {
                Message = "";

                v_aimDirection = v_aimAtPosition - mainWeapon.Muzzle.position;
                #region Compensating error due to target velocity change
                _directionErrorIntergral -= _directionErrorIntergral * (Time.fixedDeltaTime / 1f);
                Vector3 relativeVel = Target.Velocity - partScript.Body.RigidBody.velocity;
                var dir = (_directionErrorPct * _directionErrorIntergral);
                if (dir < 0)
                {
                    _directionErrorIntergral += _directionErrorPct * 75f;
                    if (_directionErrorIntergral * _directionErrorPct > 0)
                        _directionErrorIntergral = 0;
                }
                else
                {
                    _directionErrorIntergral += _directionErrorPct * .1f;
                    _directionErrorIntergral = Mathf.Clamp(_directionErrorIntergral, -_directionErrorIntergralLimit, _directionErrorIntergralLimit);
                }
                float compensation = (1 + _directionErrorIntergral * 3);
                compensation /= Mathf.Clamp(Mathf.Abs(Mathf.Cos(Vector3.Angle(v_aimDirection, Target.Velocity) * Mathf.Deg2Rad)), 0.7f, 1f);
                #endregion

                int solutions = Fts.solve_ballistic_arc(proj_pos: mainWeapon.Muzzle.position,
                                                            proj_speed: mainWeapon.MuzzleVelocity,
                                                            target_pos: targetPosition + Target.Velocity * compensation * Time.fixedDeltaTime,
                                                            target_velocity: p_MouseAim ? relativeVel : relativeVel,
                                                            gravity: (p_Gravity || (p_AutoGravity && mainWeapon.Gravity)) ? -Physics.gravity.y : 0,
                                                            s0: out Vector3 d1,
                                                            s1: out var d2);
                if (0 < solutions)
                {
                    v_aimDirection = (p_MortarMode && solutions == 2) ? d2 : d1;
                    v_aimAtPosition = mainWeapon.Muzzle.position + v_aimDirection.normalized * v_gunToTarget.magnitude;
                    float range = mainWeapon.MaxRange;
                    TargetOutofRange = v_gunToTarget.sqrMagnitude > range * range;
                }
                else TargetOutofRange = true;
            }
            #endregion

            var v_localGunDir = @base.InverseTransformDirection(v_gunDirection);
            var v_localGunAimDir = @base.InverseTransformDirection(v_aimDirection);

            var s_actionLimit = p_Speed * Mathf.Max(0.125f, _speedMult * _speedMult) * Time.fixedDeltaTime;


            float s_AziAction, s_EleAction, s_AziActionUnclamped, s_EleActionUnclamped;

            #region Get Azimuth action
            if (!p_Azi360)
            {
                //prevent stupid movement
                float gundir2Neutral = Util.Angle360To180(v_localGunDir.AngleAroundAxisTo(Vector3.up, v_neutralAzi));
                float neutral2AimDir = Util.Angle360To180(v_neutralAzi.AngleAroundAxisTo(Vector3.up, v_localGunAimDir));
                s_AziActionUnclamped = gundir2Neutral
                    + neutral2AimDir;
            }
            else
                //no need to prevent stupid movement
                s_AziActionUnclamped = Util.Angle360To180(v_localGunDir.AngleAroundAxisTo(Vector3.up, v_localGunAimDir));
            // limit action magnitude,
            s_AziAction = Mathf.Clamp(-s_AziActionUnclamped, -s_actionLimit, s_actionLimit) * Mathf.Clamp(Mathf.Abs(s_AziActionUnclamped * 0.85f), _inertiaCompensator, 1);

            #endregion
            #region Get Elevation action
            {
                var curr = v_gunDirection;
                var dest = v_aimDirection;
                var q = Quaternion.AngleAxis(s_AziActionUnclamped, transform.up);
                v_localGunDir = axle.InverseTransformDirection(q * curr);
                v_localGunAimDir = axle.InverseTransformDirection(q * dest);
                s_EleActionUnclamped = Mathf.Clamp(Util.Angle360To180(v_localGunDir.AngleAroundAxisTo(Vector3.right, v_neutralEle)), -90, 90)
                 + Mathf.Clamp(Util.Angle360To180(v_neutralEle.AngleAroundAxisTo(Vector3.right, v_localGunAimDir)), -90, 90);
                var b = Mathf.Clamp(-s_EleActionUnclamped, -s_actionLimit, s_actionLimit);

                float c = Mathf.Clamp01(1f - Mathf.Abs(s_AziActionUnclamped * .02f));
                s_EleAction = Mathf.Lerp(b * .2f, b, c * c) * Mathf.Clamp(Mathf.Abs(s_EleActionUnclamped * 0.85f), _inertiaCompensator, 1);
            }
            #endregion


            _speedMult = Mathf.Max(Mathf.Abs(s_AziAction), Mathf.Abs(s_EleAction)) / s_actionLimit;

            //auto adapt damper
            azimutor.DamperMultiplier = Mathf.Max(p_MinDamperMultiplier, p_MaxDamperMultiplier - Mathf.Abs(s_AziActionUnclamped / 180) * p_MaxDamperMultiplier);
            elevator.DamperMultiplier = Mathf.Max(p_MinDamperMultiplier, p_MaxDamperMultiplier - Mathf.Abs(s_EleActionUnclamped / 180) * p_MaxDamperMultiplier);

            #region More presice movement
            if (Mathf.Abs(s_AziAction) < 1.5f) s_AziAction *= s_AziAction > 0 ? s_AziAction : -s_AziAction; else s_AziAction += .625f;
            if (Mathf.Abs(s_EleAction) < 1.5f) s_EleAction *= s_EleAction > 0 ? s_EleAction : -s_EleAction; else s_EleAction += .625f;
            #endregion

            _directionErrorAngle = Mathf.Sqrt(s_EleActionUnclamped * s_EleActionUnclamped + s_AziActionUnclamped * s_AziActionUnclamped);
            if (float.IsNaN(_directionErrorAngle)) _directionErrorAngle = 0;
            _directionErrorPct = _directionErrorAngle / s_actionLimit;

            #endregion
            OutofZoneOfFire = false;
            if (!p_Azi360)
                azimutor.TargetAngle = ClampAngle(azimutor.TargetAngle + s_AziAction, p_AziMinRange, p_AziMaxRange);
            else
            {
                var ta = azimutor.TargetAngle + s_AziAction;
                if (ta > 180) ta -= 360;
                else if (ta < -180) ta += 360;
                azimutor.TargetAngle = ta;
            }

            elevator.TargetAngle = ClampAngle(elevator.TargetAngle + s_EleAction, p_EleMinRange, p_EleMaxRange);

            float ClampAngle(float angle, int minAngle, int maxAngle)
            {
                if (!OutofZoneOfFire)
                    OutofZoneOfFire = angle - minAngle < 1e-3f;
                if (!OutofZoneOfFire)
                    OutofZoneOfFire = maxAngle - angle < 1e-3f;
                return Mathf.Clamp(angle, minAngle, maxAngle);
            }
        }

        private void OnDisable() => gameRunning = false;
        private void OnDestroy()
        {
            if (targetManager != null)
                targetManager.Dispose();
            if (instances.Contains(this))
            {
                ServiceProvider.Instance.GameState.AircraftViewChanged -= OnAircraftViewChanged;
                instances.Remove(this);
            }
            drawUIInstanceID = -1;
            drawUIInstanceID = instances.FindIndex(p => p.isActiveAndEnabled);
            if (drawUIInstanceID < 0)
                inputWidgetScript = mcam = ocam = pcam = null;
        }
        #region GUI

        private static GUIStyle targetPosStyle;
        private static GUIStyle aimPosStyle;
        private static GUIStyle centeredStyle;
        private static GUIStyle targetOptionStyle;
        private static GUIStyle missileTargetOptionStyle;
        private static GUIStyle targetOccludedStyle;
        private static Color occluded = Color.magenta;
        private static Color normal = Color.green;
        private static Color outofrange = Color.yellow;
        private static Color outofZoF = Color.cyan;
        private static Color firing = Color.red;
        private static Color debug = new Color(.2f, .2f, 1);
        private bool drawUIOnThisOne;
        private static bool showUI = true;
        private static Rect windowRect = new Rect(100, 100, 280, 360);
        private Vector2 windowScrollPos;
        private static int newestInstanceHash;
        private static int drawUIInstanceID;
        private static List<ActiveDefenceTurretScript> instances = new List<ActiveDefenceTurretScript>();
        private static bool showHelp;
        private static GUIStyle targetOutofRangeStyle;
        private static GUIStyle targetOutofZoFStyle;
        private static GUIStyle mouseAimStyle;
        private static GUIStyle missileTargetNameStyle;


        private void OnGUI()
        {
            if (gameRunning && drawUIOnThisOne && showUI)
            {
                if (centeredStyle == null)
                    CreateGUIStyles();

                targetDeadOrLost = Target == null || Target.IsDead || (Target.IsDead && Target.IsLost);
                windowRect.height = showHelp ? 448 : 360;
                windowRect = GUILayout.Window(newestInstanceHash, windowRect, OnWindow, "ActiveDefenceTurret");
                var sy = Screen.height - 10;
                if (!targetDeadOrLost && AGEnabled)
                {
                    var pos = Camera.main.WorldToScreenPoint(targetPosition);
                    if (pos.z > 0)
                        GUI.Label(new Rect(pos.x - 10, sy - pos.y, 20, 20), "+", targetPosStyle);
                    pos = Camera.main.WorldToScreenPoint(v_aimAtPosition);
                    //pos = Camera.main.WorldToScreenPoint(mainWeapon.Muzzle.position+d1);
                    if (pos.z > 0)
                        GUI.Label(new Rect(pos.x - 10, sy - pos.y, 20, 20), "+", aimPosStyle);
                }
                if (instances.Count > 1)
                {
                    var poss = Camera.main.WorldToScreenPoint(transform.position);
                    GUI.Label(new Rect(poss.x - 50, sy - poss.y, 100, 20), $"Turret #{drawUIInstanceID}", centeredStyle);
                }
            }
        }

        private void CreateGUIStyles()
        {
            aimPosStyle = new GUIStyle("label")
            {
                alignment = TextAnchor.MiddleCenter
            };
            aimPosStyle.normal.textColor = Color.green;
            targetPosStyle = new GUIStyle("label")
            {
                alignment = TextAnchor.MiddleCenter
            };
            targetPosStyle.normal.textColor = Color.red;
            centeredStyle = new GUIStyle("label")
            {
                alignment = TextAnchor.MiddleCenter
            };
            targetOptionStyle = new GUIStyle("button")
            {
                alignment = TextAnchor.MiddleLeft
            };
            missileTargetOptionStyle = new GUIStyle("button")
            {
                alignment = TextAnchor.MiddleLeft
            };
            missileTargetOptionStyle.normal.textColor = new Color(1, .4f, .2f);
            missileTargetOptionStyle.hover.textColor = new Color(1, .4f, .2f);
            missileTargetNameStyle = new GUIStyle("label")
            {
                alignment = TextAnchor.MiddleLeft
            };
            missileTargetNameStyle.normal.textColor = new Color(1, .4f, .2f);
            targetOccludedStyle = new GUIStyle("label")
            {
                fontStyle = FontStyle.Bold
            };
            targetOccludedStyle.margin.top = targetOccludedStyle.margin.bottom = 0;
            targetOccludedStyle.normal.textColor = occluded;
            targetOutofRangeStyle = new GUIStyle("label")
            {
                fontStyle = FontStyle.Bold
            };
            targetOutofRangeStyle.margin.top = targetOutofRangeStyle.margin.bottom = 0;
            targetOutofRangeStyle.normal.textColor = outofrange;
            targetOutofZoFStyle = new GUIStyle("label")
            {
                fontStyle = FontStyle.Bold,

            };
            targetOutofZoFStyle.margin.top = targetOutofZoFStyle.margin.bottom = 0;
            targetOutofZoFStyle.normal.textColor = outofZoF;
            mouseAimStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold
            };
            mouseAimStyle.margin.top = mouseAimStyle.margin.bottom = 0;
            mouseAimStyle.normal.textColor = debug;
        }

        private void OnWindow(int id)
        {
            GUILayout.BeginVertical();
            {
                if (drawUIInstanceID != -1 && instances.Count > 1)
                {
                    if (instances.Count == 0)
                    {
                        drawUIInstanceID = -1;
                    }
                    else
                    {
                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.FlexibleSpace();
                            if (GUILayout.Button(" < "))
                            {
                                drawUIInstanceID = drawUIInstanceID == 0 ? (instances.Count - 1) : drawUIInstanceID - 1;
                                drawUIOnThisOne = false;
                                instances[drawUIInstanceID].drawUIOnThisOne = true;
                            }
                            GUILayout.Label($" Turret #{drawUIInstanceID} ");
                            if (GUILayout.Button(" > "))
                            {
                                drawUIInstanceID = drawUIInstanceID == (instances.Count - 1) ? 0 : drawUIInstanceID + 1;
                                drawUIOnThisOne = false;
                                instances[drawUIInstanceID].drawUIOnThisOne = true;
                            }
                            GUILayout.FlexibleSpace();
                        }
                        GUILayout.EndHorizontal();
                    }
                }

                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label($"Target Style: {p_TargetingStyle}");
                    if (mainWeapon != null)
                        GUILayout.Label($" Main weapon: {mainWeapon.Name}");
                    if (!AGEnabled)
                        GUILayout.Label($"(not Armed)", missileTargetNameStyle);
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label("Target");

                    var drawTarget = false;
                    if (targetIndex != -1 && targetManager.targetNamesInRangeSorted.Count() > 0)
                    {
                        drawTarget = true;
                        targetIndex = Mathf.Clamp(targetIndex, 0, targetManager.targetNamesInRangeSorted.Count() - 1);
                    }

                    int i = targetIndex;
                    if (int.TryParse(GUILayout.TextField($"{(targetDeadOrLost ? -1 : targetIndex)}", 2), out i) && i != targetIndex)
                        if (i >= 0)
                        {
                            SelectTarget(i);
                            p_MouseAim = dummyTargetEnabled = false;
                        }
                    if (targetManager.targetNamesInRangeSorted.Count() > 0)
                        GUILayout.Label($": {(drawTarget ? targetManager.targetNamesInRangeSorted.ElementAt(targetIndex) : "")}", targetManager.targetsIsMissileInRangeSorted.ElementAt(targetIndex) ? missileTargetNameStyle : GUI.skin.label);
                    GUILayout.FlexibleSpace();
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label($"Distance: {(targetDeadOrLost ? 0 : Vector3.Distance(transform.position, targetPosition)) * 0.001f:F1} km");
                    GUILayout.FlexibleSpace();
                    GUILayout.Label($"Directon error: {(targetDeadOrLost ? 0 : _directionErrorAngle):F2}");
                }
                GUILayout.EndHorizontal();


                windowScrollPos = GUILayout.BeginScrollView(windowScrollPos, targetOptionStyle);
                {
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label("Choose Target");
                        GUILayout.FlexibleSpace();
                        var _pAF = p_AutoFire;
                        if (p_AutoFire = GUILayout.Toggle(p_AutoFire, "Auto Fire"))
                        {
                            if (p_AutoFire)
                                p_AutoSwitchNextTarget = true;
                        }
                        if (p_AutoFire != _pAF && Input.GetKey(KeyCode.RightAlt))
                        {
                            foreach (var adts in instances)
                                if (adts && adts != this && adts.isActiveAndEnabled)
                                {
                                    adts.p_AutoFire = p_AutoFire;
                                    if (adts.p_AutoFire) adts.p_AutoSwitchNextTarget = true;
                                }
                        }
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.BeginVertical();
                    {
                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.Space(10);
                            GUILayout.BeginVertical();
                            {
                                if (targetManager.targetNamesInRangeSorted.Count() == 0)
                                {
                                    GUILayout.Label("No target in radar range");
                                }
                                else
                                    for (int a = 0; a < targetManager.targetNamesInRangeSorted.Count(); a++)
                                    {
                                        if (GUILayout.Button($"{(a == targetIndex ? "►#" : "#")}{a}: {targetManager.targetNamesInRangeSorted.ElementAt(a)}", targetManager.targetsIsMissileInRangeSorted.ElementAt(a) ? missileTargetOptionStyle : targetOptionStyle))
                                        {
                                            SelectTarget(a);
                                            p_MouseAim = dummyTargetEnabled = false;
                                            if (Input.GetKey(KeyCode.RightAlt))
                                            {
                                                ITarget t = target;
                                                foreach (var adts in instances)
                                                    if (adts && adts.isActiveAndEnabled)
                                                    {
                                                        var i = adts.targetManager.targetsInRangeSorted.TakeWhile(p => p != t).Count();
                                                        if (i >= 0)
                                                        {
                                                            adts.SelectTarget(i);
                                                            adts.p_MouseAim = false;
                                                        }

                                                    }
                                            }
                                        }
                                    }
                            }
                            GUILayout.EndVertical();
                        }
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.EndVertical();

                }
                GUILayout.EndScrollView();

                GUILayout.FlexibleSpace();

                if (TargetIsOccluded)
                    GUILayout.Label("Target Is Occluded", targetOccludedStyle);
                if (OutofZoneOfFire)
                    GUILayout.Label("Out of ZoneOfFire", targetOutofZoFStyle);
                if (TargetOutOfAutoFireRange)
                    GUILayout.Label($"Target is out of AutoFire Range", targetOutofRangeStyle);
                else if (TargetOutofRange)
                    GUILayout.Label($"Target is out of Range", targetOutofRangeStyle);
                if (p_MouseAim)
                    GUILayout.Label("Mouse Aim", mouseAimStyle);
                else if (dummyTargetEnabled)
                    GUILayout.Label("Using debug target", mouseAimStyle);

                showHelp = GUILayout.Toggle(showHelp, "Show shortcuts");
                if (showHelp)
                    GUILayout.Label("Switch targets: Alpha 9 / Alpha 0\nAim by mouse: M\nDebug target: O  (Click RMB to move the target)\nRight Alt: Apply to all turrets");

                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label("Press H to show/hide UI");
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("by CarnationRED");
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();


            GUI.DragWindow();
        }
        #endregion
    }
    class DummyTarget : MonoBehaviour, ITarget
    {
        public Vector3 AngularVelocity => body.angularVelocity;

        public bool IsDead => false;

        public bool IsLocked => false;

        public bool IsLost => false;

        public float MaxVisibleRange => 25000;

        internal Vector3 Dist;
        public Rigidbody body;

        public TargetType TargetType => TargetType.Information;

        public Vector3 Velocity => body.velocity;

        public Vector3 Position => body.position;

        public static DummyTarget Create
        {
            get
            {
                var r = new GameObject("MouseAimTarget").AddComponent<DummyTarget>();
                GameObject g = r.gameObject;
                g.AddComponent<MeshFilter>().mesh = new Mesh()
                {
                    vertices = new Vector3[]
                   {
                        new Vector3(  1,  1,  1  ),
                        new Vector3( -1,  1 ,  1  ),
                        new Vector3( -1, -1 ,  1  ),
                        new Vector3(  1, -1 ,  1  ),
                        new Vector3(  1, -1 , -1  ),
                        new Vector3(  1,  1 , -1  ),
                        new Vector3( -1,  1 , -1  ),
                        new Vector3( -1, -1 , -1  )
                   },
                    triangles = new int[] {
                        0, 1, 2,   0, 2, 3,    // 前
                        0, 3, 4,   0, 4, 5,    // 右
                        0, 5, 6,   0, 6, 1,    // 上
                        1, 6, 7,   1, 7, 2,    // 左
                        7, 4, 3,   7, 3, 2,    // 下
                        4, 7, 6,   4, 6, 5 },     // 后
                };
                g.AddComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("GUI/Text Shader")) { color = Color.red };
                r.body = g.AddComponent<Rigidbody>();
                r.body.mass = 10;
                r.body.useGravity = true;
                r.body.angularDrag = 0;
                r.body.drag = 0;
                g.AddComponent<SphereCollider>().radius = 1;
                g.transform.position = new Vector3(0, 25, 25);
                g.SetActive(false);
                return r;
            }
        }
        bool applyForce = false;
        private void Update()
        {
            applyForce = Input.GetKeyDown(KeyCode.Mouse1);
            if (applyForce)
                if (new Vector2(body.velocity.x, body.velocity.z).magnitude > 0.5f)
                {
                    body.angularVelocity = Vector3.zero;
                    body.velocity = new Vector3(0, body.velocity.y, 0);
                }
                else
                    body.velocity = (Dist - body.position).normalized * Mathf.Clamp((Dist - body.position).sqrMagnitude * .05f, 0, 100f);
        }
        public void Alert(bool locked)
        {
        }
    }
}