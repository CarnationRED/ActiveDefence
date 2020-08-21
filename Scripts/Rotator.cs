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
        private float _speed;
        private bool _floppyJoint;
        public Transform _movingPart;
        public Transform _invertMovingPart;
        private bool _freeSpin;
        private IPartScript PartScript;

        private int AttachPointIndex = 0;

        private Vector3 HingeOffset;
        private float MaxSpeed = 50;
        private float Speed = 50;
        private float _damperMultiplier = 500;
        private bool AllowFreeSpin = false;
        private float Range = 180f;
        private bool DisableBaseMesh = false;
        private float _angle;
        private float _targetAngle;
        private float _timeAngle;
        private float _velAngle;
        private float _lastVelAngle;
        private float _lastAngle;
        private float _lastLastAngle;
        public float _accAngle;
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

        //public AudioSource _audio;

        private void Start()
        {
            enabled = false;
        }

        public bool Init(IPartScript ps, int AttachPointIndex, Transform movingPart, Transform invertMovingPart = null)
        {
            PartScript = ps;
            this.AttachPointIndex = AttachPointIndex;
            _movingPart = movingPart;
            _invertMovingPart = invertMovingPart;
            return SetupJoint();
        }

        public void SetAxis(Vector3 axis) => _joint.axis = axis;

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
                                    _joint.anchor += HingeOffset;
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
                                        _joint.anchor += HingeOffset;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            _speed = Speed * Speed * MaxSpeed;
            if (_joint != null)
            {
                JointDrive angularXDrive = _joint.angularXDrive;
                initialDamper = angularXDrive.positionDamper;
                angularXDrive.positionDamper *= DamperMultiplier;
                _joint.angularXDrive = angularXDrive;
                if (Range < 0.0001f && AllowFreeSpin)
                {
                    _freeSpin = true;
                    angularXDrive.positionDamper = 0f;
                    angularXDrive.positionSpring = 0f;
                    _joint.angularXDrive = angularXDrive;
                }
                else if (Speed < 0.0001f)
                {
                    _floppyJoint = true;
                    angularXDrive.positionDamper = 0f;
                    angularXDrive.positionSpring = 0f;
                    _joint.angularXDrive = angularXDrive;
                    _joint.angularXMotion = ConfigurableJointMotion.Limited;
                    SoftJointLimit lowAngularXLimit = _joint.lowAngularXLimit;
                    lowAngularXLimit.limit = -Range;
                    _joint.lowAngularXLimit = lowAngularXLimit;
                    lowAngularXLimit.limit = Range;
                    _joint.highAngularXLimit = lowAngularXLimit;
                }
                if (_floppyJoint && _movingPart != null)
                {
                    _movingPart.parent = _joint.connectedBody.transform;
                }
            }
            else return false;
            if (Speed < 0.0001f || DisableBaseMesh)
            {
                EnableBaseMesh(false);
            }
            return true;
        }

        private void FixedUpdate()
        {
            if (_joint != null && ActiveDefenceTurretScript.gameRunning && !_freeSpin && !_floppyJoint)
            {
                var delta = Mathf.DeltaAngle(_angle, TargetAngle);
                if (delta != 0f)
                {
                    float action = delta / Mathf.Abs(delta) * _speed * Time.deltaTime;
                    if (Mathf.Abs(action) > Mathf.Abs(delta))
                        action = delta;
                    if (!float.IsNaN(action))
                        _angle += action;
                }
                //{
                //    var t = Time.fixedTime;
                //    var tt = (t - _timeAngle);
                //    _timeAngle = t;
                //    var dt = 1f / tt;
                //    _velAngle = (_angle - _lastAngle) * dt;
                //    _accAngle = (_velAngle - _lastVelAngle) * dt;
                //    if (Math.Abs(_accAngle) > 6400 && Math.Abs(_velAngle) > 50f)
                //    {
                //        _accAngle = Mathf.Clamp(_accAngle, -6400, 6400);
                //        var newV = _lastVelAngle + _accAngle * tt;
                //        _angle = (float)(_lastAngle + newV * tt);
                //        _angle = (float)(_lastAngle + (_lastVelAngle * .8f + _velAngle * .1f) * tt * 0.5f);
                //        _velAngle = (_angle - _lastAngle) * dt;

                //        double Clamp(double v, double min, double max) => v > max ? max : (v < min ? min : v);
                //    }
                //    _lastVelAngle = _velAngle;
                //    _lastAngle = _angle;
                //    _lastLastAngle = _lastAngle;
                //}


                _joint.targetRotation = Quaternion.Euler(invert ? -_angle : _angle, 0f, 0f);
                if (_rigidBody.IsSleeping())
                    _rigidBody.WakeUp();
                if (_connectedRigidBody.IsSleeping())
                    _connectedRigidBody.WakeUp();
            }
        }
        private void Update()
        {
            if (!ServiceProvider.Instance. GameState.IsPaused)
            {
                if (_joint != null)
                {
                    /*float num = Mathf.Abs(_targetAngle - _angle);
                    num = Mathf.Clamp(num, 0f, 0.5f);
                    float num2 = Mathf.Max(_audio.volume, num);
                    num2 -= Time.deltaTime * 3f;
                    num2 = Mathf.Clamp(num2, 0f, 1f);
                    _audio.volume = num2;
                    if (_audio.volume > 0.1f && !_audio.isPlaying)
                        _audio.Play();*/
                    if (!_floppyJoint)
                    {
                        if (_movingPart != null)
                            _movingPart.localRotation = Quaternion.AngleAxis(_angle, _joint.axis);
                        if (_invertMovingPart != null)
                            _invertMovingPart.localRotation = Quaternion.AngleAxis(180f - _angle, _joint.axis);
                    }
                }
                /*else if (_audio.isPlaying)
                    _audio.Stop();*/
            }
            if (ServiceProvider.Instance.GameState.IsInDesigner && !ServiceProvider.Instance.GameState.IsInLevel)
            {
                if (Speed < 0.0001f || DisableBaseMesh)
                {
                    EnableBaseMesh(false);
                    return;
                }
                EnableBaseMesh(true);
            }
        }


        private void EnableBaseMesh(bool v)
        {
        }
    }
}