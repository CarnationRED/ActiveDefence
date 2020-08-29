using Assets.Game;
using Jundroo.SimplePlanes.ModTools.Interfaces.Parts;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CarnationRED.ActiveDefence
{
    public class Rotator : MonoBehaviour
    {
        private ConfigurableJoint _joint;
        private Rigidbody _rigidBody;
        private Rigidbody _connectedRigidBody;
        public Transform _movingPart;
        public Transform _invertMovingPart;
        public Vector3 meshAxis;
        private IPartScript PartScript;

        private int AttachPointIndex = 0;

        private float _damperMultiplier = 500;
        private float _angle;
        private float _targetAngle;
        private bool invert = true;
        private float initialDamper;

        public float TargetAngle
        {
            get => invert ? -_targetAngle : _targetAngle;
            set
            {
                _targetAngle = invert ? -value : value;
            }
        }

        public float DamperMultiplier
        {
            get => _damperMultiplier; set
            {
                if (Mathf.Abs(value - _damperMultiplier) > 250)
                {
                    if (_joint)
                    {
                        _damperMultiplier = value;
                        var jd = _joint.angularXDrive;
                        jd.positionDamper = initialDamper * _damperMultiplier;
                        _joint.angularXDrive = jd;
                    }
                }
            }
        }

        private void Start() => enabled = false;

        public bool Init(IPartScript ps, int AttachPointIndex, Transform movingPart, Transform invertMovingPart = null)
        {
            PartScript = ps;
            this.AttachPointIndex = AttachPointIndex;
            _movingPart = movingPart;
            _invertMovingPart = invertMovingPart;
            return SetupJoint();
        }

        public void SetAxis(Vector3 axis) => _joint.axis = axis;
        public void SetMeshAxis(Vector3 axis) => meshAxis = axis;

        private bool SetupJoint()
        {
            _joint = null;
            _rigidBody = null;
            _connectedRigidBody = null;
            int attachPointIndex = AttachPointIndex;
            if (PartScript.Part.AttachPoints.Count() > attachPointIndex)
            {
                IAttachPoint attachPoint = PartScript.Part.AttachPoints.ElementAt(attachPointIndex);
                if (attachPoint.PartConnections.Count() == 1)
                {
                    IBodyScript anotherBody = attachPoint.PartConnections.ElementAt(0).GetOtherPart(PartScript.Part).PartScript.Body;
                    var anotherRigidBody = anotherBody.RigidBody;
                    //find self
                    foreach (var item in PartScript.Body.Joints)
                    {
                        foreach (var info in (IList)Reflections.BodyJoint.GetField("_joints", item))
                        {
                            var obj = Reflections.JointInfo.InvokeMethod("get_Joint", info, null);
                            if (obj != null)
                            {
                                ConfigurableJoint obj1 = (ConfigurableJoint)obj;
                                if (obj1.connectedBody == anotherRigidBody)
                                {
                                    _joint = obj1;
                                    _rigidBody = _joint.GetComponent<Rigidbody>();
                                    _connectedRigidBody = _joint.connectedBody;
                                }
                            }
                        }
                    }
                    if (_joint == null)
                    {
                        //find another
                        var thisRigidBody = PartScript.Body.RigidBody;
                        foreach (var item in anotherBody.Joints)
                        {
                            foreach (var info in (IList)Reflections.BodyJoint.GetField("_joints", item))
                            {
                                var obj = Reflections.JointInfo.InvokeMethod("get_Joint", info, null);
                                if (obj != null)
                                {
                                    ConfigurableJoint obj1 = (ConfigurableJoint)obj;
                                    if (obj1.connectedBody == thisRigidBody)
                                    {
                                        invert = false;
                                        _joint = obj1;
                                        _rigidBody = _joint.GetComponent<Rigidbody>();
                                        _connectedRigidBody = _joint.connectedBody;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            if (_joint != null)
            {
                JointDrive angularXDrive = _joint.angularXDrive;
                initialDamper = angularXDrive.positionDamper;
                angularXDrive.positionDamper *= DamperMultiplier;
                _joint.angularXDrive = angularXDrive;
                if (_movingPart != null)
                {
                    _movingPart.parent = _joint.connectedBody.transform;
                }
            }
            else return false;
            return true;
        }

        private void FixedUpdate()
        {
            if (_joint != null && ActiveDefenceTurretScript.gameRunning)
            {
                var delta = Mathf.DeltaAngle(_angle, TargetAngle);
                if (delta != 0f)
                {
                    float action = delta / Mathf.Abs(delta) * 100 * Time.deltaTime;
                    if (Mathf.Abs(action) > Mathf.Abs(delta))
                        action = delta;
                    if (!float.IsNaN(action))
                        _angle += action;
                }


                _joint.targetRotation = Quaternion.Euler(invert ? -_angle : _angle, 0f, 0f);
                if (_rigidBody.IsSleeping())
                    _rigidBody.WakeUp();
                if (_connectedRigidBody.IsSleeping())
                    _connectedRigidBody.WakeUp();
            }
        }
        private void Update()
        {
#if ABC
            if (!GameState.Instance.IsPaused)
#else
            if (!ServiceProvider.Instance.GameState.IsPaused)
#endif
                if (_joint != null)
                {
                    if (_movingPart != null)
                        _movingPart.localRotation = Quaternion.AngleAxis(_angle, meshAxis);
                    if (_invertMovingPart != null)
                        _invertMovingPart.localRotation = Quaternion.AngleAxis(-_angle, meshAxis);
                }
        }

    }
}