
#define ABC

using Assets.Game.Weapons;
using Jundroo.SimplePlanes.ModTools;
using Jundroo.SimplePlanes.ModTools.Interfaces;
using Jundroo.SimplePlanes.ModTools.Interfaces.Parts;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        public bool p_AutoGravity = false;
        public bool p_Gravity = false;
        public bool p_Azi360 = false;
        public static bool p_SilentGod = false;

        private float _speedMult;
        private float _directionErrorPct;
        private float _directionErrorIntergral;
        private float _directionErrorIntergralLimit = 3f;
        private float _directionErrorAngle = 15;
        private float _inertiaCompensator = 0;


        private Vector3 v_neutralAzi;
        private Vector3 v_neutralEle;


        public int RadarRange { get; set; } = 15000;
        public Vector3 Position => transform.position;

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
        public bool p_AutoFire = true;
        public bool p_IgnoreObtacles = false;
        public int p_AutoFireRange = 15000;

        public static bool gameRunning;
        private HashSet<GunInfo> gunInfo;
        private HashSet<CannonInfo> cannonInfo;
        private IWeaponEnhanced mainWeapon;
        private ITarget target;
        private Transform groundTargetTransform;
        private IAircraftScript airTargetScript;
        private bool targetDeadOrLost;
        public TargetManager targetManager;
        private IGameWorld gameWorld;
        private int targetIndex;

        public Transform axle;

        private void Start()
        {
#if ABC
            if (ServiceProvider.Instance.GameState.IsInLevel && !ServiceProvider.Instance.GameState.IsInDesigner)
#else
            if (GameState.Instance.IsInLevel && !GameState.Instance.IsInDesigner)
#endif
            {
                partScript = (IPartScript)GetComponent(Reflections.PartScript.type);
                elevator = gameObject.AddComponent<Rotator>();
                azimutor = gameObject.AddComponent<Rotator>();
                StartCoroutine(
                    ActivateRotators(
                        elevator.Init(partScript, 0, transform.Find("Body").Find("Hub")),
                        azimutor.Init(partScript, 1, transform.Find("Body"), transform)));
                FindWeapons();
                axle = transform.Find("Body").Find("Hub").Find("Axle");


                targetManager = new TargetManager(this);

                StartCoroutine(AddTargetsCoroutine());

                gameWorld = (IGameWorld)Reflections.GameWorld.InvokeMethod("get_Instance", null, null);
                _inertiaCompensator = Mathf.Clamp(50 / partScript.Body.InertiaTensorMagnitude, 0.2f, 1);



                //P_AziMaxRange = 180;
                //P_AziMinRange = -180;
            }
            else Destroy(this);
        }

        IEnumerator AddTargetsCoroutine()
        {
            yield return new WaitForEndOfFrame();
            Vector3 offset = gameWorld.FloatingOriginOffset;
            targetManager.AddTargets(partScript.Aircraft.TargetingSystem.Targets);
            partScript.Aircraft.TargetingSystem.TargetAdded += targetManager.AddTarget;
            partScript.Aircraft.TargetingSystem.TargetRemoved += targetManager.RemoveTarget;
            testTarget = TestTarget.Create;
        }

        private void FindWeapons()
        {
            //const string weaponModifierNames = "GunScript, CannonScript";

            gunInfo = new HashSet<GunInfo>();
            cannonInfo = new HashSet<CannonInfo>();
            #region Get connected part groups
            IPart connectedPart = partScript.Part.AttachPoints.FirstOrDefault(p => p.Id == 0).PartConnections.First().GetOtherPart(partScript.Part);
            IEnumerable<IPartGroupScript> partGroups = connectedPart.PartScript.Body.PartGroups;
            foreach (var att in connectedPart.AttachPoints)
                foreach (var pc in att.PartConnections)
                {
                    IPart part1 = pc.GetOtherPart(connectedPart);
                    if (part1 != null && part1 != partScript.Part)
                        partGroups = partGroups.Concat(part1.PartScript.Body.PartGroups);
                }
            #endregion

            foreach (var item in partGroups)
                foreach (var part in item.Parts)
                    foreach (var weapon in part.Modifiers.Where(p => p is IWeapon))//(p => weaponModifierNames.Contains(p.GetType().Name)))
                        if (weapon.GetType().Name == "GunScript")
                            gunInfo.Add(new GunInfo(weapon));
                        else if (weapon.GetType().Name == "CannonScript")
                            cannonInfo.Add(new CannonInfo(weapon));
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
            azimutor.enabled = b2;
            elevator.enabled = b1;

            azimutor.SetAxis(Vector3.up);
            elevator.SetAxis(Vector3.right);
        }

        TestTarget testTarget;
        private Vector3 v_localActionEuler;
        bool test_TrackMouse = false;
        private float targetSwitchedTime;

        private void Update()
        {
            gameRunning = ServiceProvider.Instance.GameState.IsInLevel && !ServiceProvider.Instance.GameState.IsInDesigner && !ServiceProvider.Instance.GameState.IsPaused;

            if (gameRunning)
            {
                targetDeadOrLost = Target == null || (Target.IsDead || (Target.IsDead && Target.IsLost));
                _inertiaCompensator = Mathf.Clamp(50 / partScript.Body.InertiaTensorMagnitude, 0.2f, 1);
                #region Test
                if (Input.GetKey(KeyCode.I))
                {
                    azimutor.TargetAngle += Input.GetAxis("Mouse X");
                    elevator.TargetAngle += Input.GetAxis("Mouse Y");
                }
                if (Input.GetKeyDown(KeyCode.O))
                {
                    test_TrackMouse = !test_TrackMouse;
                    testTarget.transform.position = new Vector3(25, 25, 25);
                    TestTarget.body.velocity = Vector3.zero;
                    TestTarget.body.WakeUp(); ;
                    testTarget.gameObject.SetActive(true);
                }
                else if (test_TrackMouse)
                {
                    Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                    var pos = transform.position;
                    Vector3 point;
                    while (true)
                    {
                        if (Physics.Raycast(ray, out var hit, 600000, 0b1111111111111111111111111111111))
                        {
                            point = hit.point;
                            if ((point - pos).sqrMagnitude < 25)
                            {
                                ray.origin = point;
                                continue;
                            }
                            break;
                        }
                        else
                        {
                            point = ray.origin + ray.direction * 1000f;
                            break;
                        }
                    }

                    testTarget.Dist = point;
                    if (point != Vector3.zero)
                        Target = testTarget;
                }
                if (Input.GetKeyDown(KeyCode.T))
                {
                    targetIndex = Mathf.Min(targetIndex, targetManager.targetsInRangeSorted.Count - 1);
                    if (targetIndex >= 0)
                        Target = targetManager.targetsInRangeSorted[targetIndex];
                }
                else if (Input.GetKeyDown(Settings.NextTarget))
                {
                    NextTarget();
                }
                else if (Input.GetKeyDown(Settings.PrevTarget))
                {
                    PrevTarget();
                }
                #endregion
                if (!targetDeadOrLost)
                {
                    #region Remove damaged or disapeared weapon
                    foreach (var weapon in gunInfo)
                        if (weapon.Script == null || !((MonoBehaviour)weapon.Script).isActiveAndEnabled)
                            gunInfo.Remove(weapon);
                    foreach (var weapon in cannonInfo)
                        if (weapon.Script == null || !((MonoBehaviour)weapon.Script).isActiveAndEnabled)
                            cannonInfo.Remove(weapon);
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
                    if (!TargetOutofRange)
                        if (p_AutoFire)
                        {
                            if (p_IgnoreObtacles || (!OutofZoneOfFire && _directionErrorAngle < 0.2f))
                                if (!IsTargetOccluded())
                                    if (Vector3.SqrMagnitude(Target.Position - mainWeapon.Muzzle.position) <= p_AutoFireRange * p_AutoFireRange)
                                    {
                                        if (mainWeapon != null && mainWeapon.IsArmed && Time.time - targetSwitchedTime > 0.5f)
                                        {
                                            mainWeapon.Fire();
                                        }
                                    }
                                    else
                                        NextTarget();
                        }
                }
                else if (p_AutoSwitchNextTarget)
                {
                    targetIndex = 0;
                    if (targetManager.targetsInRangeSorted.Count > 0)
                    {
                        Target = targetManager.targetsInRangeSorted[0];
                        targetManager.RemoveDeadTarget();
                    }
                }
            }
        }

        private void PrevTarget()
        {
            targetIndex--;
            if (targetIndex == -1) targetIndex = targetManager.targetsInRangeSorted.Count - 1;
            targetIndex = Mathf.Min(targetIndex, targetManager.targetsInRangeSorted.Count - 1);
            if (targetIndex >= 0)
                Target = targetManager.targetsInRangeSorted[targetIndex];
            targetManager.RemoveDeadTarget();
            targetSwitchedTime = Time.time;
        }

        private void NextTarget()
        {
            targetIndex++;
            if (targetIndex == targetManager.targetsInRangeSorted.Count) targetIndex = 0;
            targetIndex = Mathf.Min(targetIndex, targetManager.targetsInRangeSorted.Count - 1);
            if (targetIndex >= 0)
                Target = targetManager.targetsInRangeSorted[targetIndex];
            targetManager.RemoveDeadTarget();
            targetSwitchedTime = Time.time;
        }

        public bool IsTargetOccluded()
        {
            if (targetDeadOrLost || mainWeapon == null || !mainWeapon.IsArmed) return true;
            Ray ray = new Ray(mainWeapon.Muzzle.position, _directionErrorAngle > 0.2f ? mainWeapon.Muzzle.forward : (Target.Position - mainWeapon.Muzzle.position));
            if (Physics.Raycast(ray, out var hit, 600000, 0b1111111111111111111111111111111))
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
                return true;
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
                //v_maxAzi = Quaternion.AngleAxis(p_AziMaxRange, Vector3.up) * fwd;
                //v_maxEle = Quaternion.AngleAxis(p_EleMaxRange, Vector3.right) * fwd;
                //v_minAzi = Quaternion.AngleAxis(p_AziMinRange, Vector3.up) * fwd;
                //v_minEle = Quaternion.AngleAxis(p_EleMinRange, Vector3.right) * fwd;
                v_neutralAzi = Quaternion.AngleAxis((p_AziMaxRange + p_AziMinRange) * .5f, Vector3.up) * fwd;
                v_neutralEle = Quaternion.AngleAxis((p_EleMinRange + p_EleMaxRange) * .5f, Vector3.right) * fwd;
            }
        }

        void FixedUpdate()
        {
            if (!gameRunning || Target == null || mainWeapon == null) return;
            targetDeadOrLost = Target.IsDead || (Target.IsDead && Target.IsLost);
            if (targetDeadOrLost) return;

            #region Aim at target
            var v_gunToTarget = Target.Position - mainWeapon.Muzzle.position;
            var v_gunDirection = mainWeapon.Muzzle.forward;
            Vector3 v_aimAtPosition, v_aimDirection;

            v_aimAtPosition = Target.Position;


            #region Fts solution, this is awesome!
            {
                Message = "";

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
                #endregion

                v_aimDirection = v_aimAtPosition - mainWeapon.Muzzle.position;
                int solutions = Fts.solve_ballistic_arc(proj_pos: mainWeapon.Muzzle.position,
                                                            proj_speed: mainWeapon.MuzzleVelocity,
                                                            target_pos: Target.Position + Target.Velocity * (1 + _directionErrorIntergral * 2) * Time.fixedDeltaTime,
                                                            target_velocity: relativeVel,
                                                            gravity: (p_Gravity || (p_AutoGravity && mainWeapon.Gravity)) ? -Physics.gravity.y : 0,
                                                            s0: out var d1,
                                                            s1: out var d2);
                if (0 < solutions)
                {
                    v_aimDirection = (p_MortarMode && solutions == 2) ? d2 : d1;
                    TargetOutofRange = false;
                }
                else TargetOutofRange = true;
            }
            #endregion

            var v_localGunDir = transform.InverseTransformDirection(v_gunDirection);
            var v_localGunAimDir = transform.InverseTransformDirection(v_aimDirection);

            var s_actionLimit = p_Speed * Mathf.Max(0.125f, _speedMult * _speedMult) * Time.fixedUnscaledDeltaTime;


            float s_AziAction, s_EleAction, s_AziActionUnclamped, s_EleActionUnclamped;

            if (!p_Azi360)
                s_AziActionUnclamped = Util.Angle360To180(v_localGunDir.AngleAroundAxisTo(Vector3.up, v_neutralAzi))
                    + Util.Angle360To180(v_neutralAzi.AngleAroundAxisTo(Vector3.up, v_localGunAimDir));
            else
                s_AziActionUnclamped = Util.Angle360To180(v_localGunDir.AngleAroundAxisTo(Vector3.up, v_localGunAimDir));
            s_AziAction = Mathf.Clamp(-s_AziActionUnclamped, -s_actionLimit, s_actionLimit) * Mathf.Clamp(Mathf.Abs(s_AziActionUnclamped * 0.85f), _inertiaCompensator, 1);

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


            _speedMult = Mathf.Max(Mathf.Abs(s_AziAction), Mathf.Abs(s_EleAction)) / s_actionLimit;

            azimutor.DamperMultiplier = Mathf.Max(p_MinDamperMultiplier, p_MaxDamperMultiplier - Mathf.Abs(s_AziActionUnclamped / 180) * p_MaxDamperMultiplier);
            elevator.DamperMultiplier = Mathf.Max(p_MinDamperMultiplier, p_MaxDamperMultiplier - Mathf.Abs(s_EleActionUnclamped / 180) * p_MaxDamperMultiplier);

            if (Mathf.Abs(s_AziAction) < 1.5f) s_AziAction *= s_AziAction > 0 ? s_AziAction : -s_AziAction; else s_AziAction += .625f;
            if (Mathf.Abs(s_EleAction) < 1.5f) s_EleAction *= s_EleAction > 0 ? s_EleAction : -s_EleAction; else s_EleAction += .625f;

            v_localActionEuler = new Vector3(s_EleAction, s_AziAction, 0);
            _directionErrorAngle = Mathf.Sqrt(s_EleActionUnclamped * s_EleActionUnclamped + s_AziActionUnclamped * s_AziActionUnclamped);
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

        private void OnDisable()
        {
            gameRunning = false;
        }
        private void OnGUI()
        {
            if (gameRunning)
            {
                int y = 230;
                GUI.Box(new Rect(100, y, 400, 20), $"自动炮塔MOD测试版 by CarnationRED， 按9或0切换目标");
                targetDeadOrLost = Target == null || Target.IsDead || (Target.IsDead && Target.IsLost);
                // GUI.Box(new Rect(100, 200, 280, 48), "azi: " + azimutor.TargetAngle + "\nele: " + elevator.TargetAngle);

                if (!targetDeadOrLost && mainWeapon != null)
                {
                    y += 20;
                    GUI.Box(new Rect(100, y, 280, 20), $"Target No:  {targetIndex}");
                    y += 20;
                    GUI.Box(new Rect(100, y, 280, 20), $"Target position:  {Target.Position}");
                    y += 20;
                    GUI.Box(new Rect(100, y, 280, 20), $"Directon error:  {_directionErrorAngle:F2}");
                    y += 20;

                    if (OutofZoneOfFire)
                    {
                        GUI.Box(new Rect(100, y, 280, 20), "Out of ZoneOfFire");
                        y += 20;
                    }
                    if (TargetOutofRange)
                    {
                        GUI.Box(new Rect(100, y, 280, 20), "Target is out of Range");
                        y += 20;
                    }
                    for (int i = 0; i < targetManager.targetsInRangeSorted.Count; i++)
                    {
                        ITarget t = targetManager.targetsInRangeSorted[i];
                        IAircraftScript airTargetScript;
                        Transform groundTargetTransform;
                        if (t.TargetType == TargetType.Ground)
                        {
                            groundTargetTransform = (Transform)Reflections.GroundTarget.GetField("_transform", t);
                            GUI.Box(new Rect(100, y, 280, 20), $"Target #{i}: {groundTargetTransform.parent.name}");
                        }
                        else if (t.TargetType == TargetType.Air)
                        {
                            airTargetScript = (IAircraftScript)Reflections.AirTarget.GetField("_aircraft", t);
                            GUI.Box(new Rect(100, y, 280, 20), $"Target #{i}: {airTargetScript.Aircraft.Name}");
                        }
                        else
                            GUI.Box(new Rect(100, y, 280, 20), $"Target #{i}: Unidentified");
                        y += 20;
                    }
                }
            }
        }
    }
    class TestTarget : MonoBehaviour, ITarget
    {
        public Vector3 AngularVelocity => throw new NotImplementedException();

        public bool IsDead => false;

        public bool IsLocked => throw new NotImplementedException();

        public bool IsLost => throw new NotImplementedException();

        public float MaxVisibleRange => throw new NotImplementedException();

        internal Vector3 Dist;
        public static Rigidbody body;

        public TargetType TargetType => (Assets.Game.Weapons.TargetType)(-1);

        public Vector3 Velocity => body.velocity;

        public Vector3 Position => body.position;

        public static TestTarget Create
        {
            get
            {
                var r = new GameObject("TestTarget").AddComponent<TestTarget>();
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
                body = g.AddComponent<Rigidbody>();
                body.mass = 10;
                body.useGravity = true;
                body.angularDrag = 0;
                body.drag = 0;
                g.AddComponent<SphereCollider>().radius = 1;
                g.transform.position = new Vector3(0, 25, 25);
                g.SetActive(false);
                return r;
            }
        }
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
                    body.velocity = (Dist - body.position).normalized * Mathf.Clamp((Dist - body.position).sqrMagnitude * .05f, 0, 70f);
        }
        bool applyForce = false;
        public void FixedUpdate()
        {
            //if (applyForce)
            //    body.AddForce((Dist - body.position).normalized * Mathf.Clamp((Dist - body.position).sqrMagnitude*.001f, 0, 15f), ForceMode.VelocityChange);


        }
        public void Alert(bool locked)
        {
            throw new NotImplementedException();
        }
    }
}