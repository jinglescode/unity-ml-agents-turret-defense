using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;

public class TurretBrain : Agent
{
    public float _rotationSpeed = 180f;
    public float _fireCooldown = 0.75f;

    public float _currentRotationSpeed = 180f;
    public float _currentFireCooldown = 0.75f;

    protected Rigidbody mRigidbody;
    protected float mHorizontalInputValue = 0f;
    protected float mfireCooldownTimer = 0f;

    protected int mReadyToFire = 1;
    protected float mPowerUpTimeRemaining = 0f;

    public Transform _raySpawnPoint;

    void Start()
    {
        mRigidbody = GetComponent<Rigidbody>();
        _currentRotationSpeed = _rotationSpeed;
        _currentFireCooldown = _fireCooldown;
    }

    void Update()
    {
        if(mfireCooldownTimer > 0)
        {
            mfireCooldownTimer -= Time.deltaTime;
        }

        if(mfireCooldownTimer < 0)
        {
            mfireCooldownTimer = 0;
        }

        if(mPowerUpTimeRemaining >= 0)
        {
            mPowerUpTimeRemaining -= Time.deltaTime;

            if(mPowerUpTimeRemaining <= 0)
            {
                _currentRotationSpeed = _rotationSpeed;
                _currentFireCooldown = _fireCooldown;
            }
        }
    }

    public override void OnEpisodeBegin()
    {
        if(this.mRigidbody != null)
        {
            this.mRigidbody.angularVelocity = Vector3.zero;
        }
        this.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if(mfireCooldownTimer <= 0f)
        {
            mReadyToFire = 1;
        }
        sensor.AddObservation(mReadyToFire);
        sensor.AddObservation(mfireCooldownTimer);
    }

    public override void OnActionReceived(float[] vectorAction)
    {
        float turn = vectorAction[0];
        float fire = vectorAction[1];
        if(turn == 2)
        {
            turn = -1;
        }
        Rotate(turn);

        if(fire == 1 && mReadyToFire == 1)
        {
            mReadyToFire = 0;
            mfireCooldownTimer = _currentFireCooldown;
            Fire();
        }
    }

    public override void Heuristic(float[] actionsOut)
    {
        actionsOut[0] = Input.GetAxis("Horizontal");
        actionsOut[1] = Input.GetAxis("Fire1");
    }

    public void Fire()
    {
        RaycastHit hit;

        float thickness = 1f;
        Vector3 origin = _raySpawnPoint.position;
        Vector3 direction = _raySpawnPoint.TransformDirection(Vector3.forward);
        if (Physics.SphereCast(origin, thickness, direction, out hit, 50f))
        // if (Physics.Raycast(_raySpawnPoint.position, _raySpawnPoint.TransformDirection(Vector3.forward), out hit, 50f))
        {
            Debug.DrawRay(_raySpawnPoint.position, _raySpawnPoint.TransformDirection(Vector3.forward) * hit.distance, Color.yellow);    
            // TankBrain tank = hit.collider.transform.parent.gameObject.GetComponent<TankBrain>(); // has errors

            if(hit.collider.gameObject.tag == "target" || hit.collider.gameObject.tag == "blueAgent")
            {
                TankBrain tank = hit.collider.gameObject.GetComponent<TankBrain>();
                if(tank == null)
                {
                    tank = hit.collider.transform.parent.gameObject.GetComponent<TankBrain>();
                }
                
                if(tank != null)
                {
                    tank.GotHit();
                    // SetReward(0.1f);
                    // Debug.Log(tank.GetTeam());
                    if(tank.GetTeam() == 2)
                    {
                        // SetReward(1f);
                        float thisSCore = tank.GetScore();
                        if(thisSCore <= 0)
                        {
                            thisSCore = 1f;
                        }
                        SetReward(thisSCore);
                    }
                }
                else{
                    SetReward(-1f);
                }

            }

            if(hit.collider.gameObject.tag == "food")
            {
                SetReward(1f);

                _currentRotationSpeed = 540f;
                _currentFireCooldown = 0f;
                mPowerUpTimeRemaining = 10f;

                hit.collider.gameObject.SetActive(false);
                Destroy(hit.collider.gameObject);
            }
        }
        else
        {
            Debug.DrawRay(_raySpawnPoint.position, _raySpawnPoint.TransformDirection(Vector3.forward) * 20, Color.white);
            // SetReward(-0.1f);
        }
    }

    public void Rotate(float mHorizontalInputValue)
    {
        if(mRigidbody != null)
        {
            float rotationDegree = _currentRotationSpeed * Time.deltaTime * mHorizontalInputValue;
            Quaternion rotQuat = Quaternion.Euler(0f, rotationDegree, 0f);
            mRigidbody.MoveRotation(mRigidbody.rotation * rotQuat);
        }
    }

    public void GameEnded(bool win)
    {
        if(win)
        {
            Debug.Log("AGENT WIN");
            SetReward(10f);
        }
        else{
            Debug.Log("AGENT LOSE");
            SetReward(-100f);
        }
        EndEpisode();
    }

    public void FriendlySaved()
    {
        SetReward(1f);
    }

}
