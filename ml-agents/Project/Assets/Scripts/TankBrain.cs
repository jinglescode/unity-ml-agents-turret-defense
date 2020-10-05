using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;

public class TankBrain : Agent
{
    public float _rotationSpeed = 90;
    public float _moveSpeed = 3f;
    public int _team = 1;

    protected float mReachDistance = 2.5f;

    protected Rigidbody mRigidbody;

    protected float mVerticalInputValue = 0f;
    protected float mHorizontalInputValue = 0f; 

    protected Quaternion mFacingDirection;
    protected Vector3 mStartPosition;
    protected Vector3 mGoalPosition; 

    protected string mGameName; 
    protected float mScore = 0f;

    protected bool mRandomMovement = false; 

    public enum State
    {
        isFree = 0,
        isAlive,
        isHit,
        isSaved,
    };
    protected State mState;

    void Start()
    {
        mRigidbody = GetComponent<Rigidbody>();
        mRigidbody.centerOfMass = Vector3.zero;
    }

    public void PerformMove()
    {
        if(mRigidbody != null && mState == State.isAlive)
        {
            GameObject obj = GameObject.Find("turret_"+mGameName);
            transform.LookAt(obj.transform);

            MoveTank(1);

            if(mRandomMovement)
            {
                float randomchoice = Random.value;
                if(randomchoice<=0.5){
                    Vector3 controlSignal = Vector3.zero;

                    float randomX = (int)(Random.value*3);
                    if(randomX==2)
                    {
                        randomX = -1;
                    }
                    float randomY = (int)(Random.value*3);
                    if(randomY==2)
                    {
                        randomY = -1;
                    }

                    controlSignal.x = randomX;
                    controlSignal.z = randomY;
                    mRigidbody.AddForce(controlSignal * 100f);
                }
            }

            float distanceToTarget = Vector3.Distance(mRigidbody.position, mGoalPosition);
            if (distanceToTarget < (1f+mReachDistance))
            {
                if(_team == 1)
                {
                    Debug.Log("Tank Saved");
                }
                mState = State.isSaved;
                gameObject.SetActive(false);
            }

            mScore -= Time.deltaTime;
        }
    }

    public void Rotate(float mHorizontalInputValue) // -1 0 1
    {
        float rotationDegree = _rotationSpeed * Time.deltaTime * mHorizontalInputValue;
        Quaternion rotQuat = Quaternion.Euler(0f, rotationDegree, 0f);
        
        mFacingDirection = mRigidbody.rotation * rotQuat;
        mRigidbody.MoveRotation(mFacingDirection);
    }

    public void setProperties(float speed, int team, Quaternion faceDirection, Vector3 randomPosition, float reachDistance, Vector3 goalPosition, string gameName, bool randomMovement)
    {
        _moveSpeed = speed;
        _team = team;
        mRandomMovement = randomMovement;

        mReachDistance = reachDistance;
        mGoalPosition = goalPosition;
        mGameName = gameName;

        mFacingDirection = faceDirection;
        transform.rotation = faceDirection;
        transform.position = randomPosition;

        mState = State.isAlive;
        gameObject.SetActive(true);

        mScore = 5f;
    }

    public void MoveTank(float mVerticalInputValue) // 0 or 1
    {
        Vector3 moveVect = transform.forward * _moveSpeed * Time.deltaTime * mVerticalInputValue;
        mRigidbody.MovePosition(mRigidbody.position + moveVect);
    }

    public void GotHit()
    {
        mState = State.isHit;
        gameObject.SetActive(false);
    }
    
    public bool isEnemy()
    {
        return _team == 2;
    }

    public bool isFree()
    {
        return mState == State.isFree;
    }

    public bool isAlive()
    {
        return mState == State.isAlive;
    }

    public bool isHit()
    {
        return mState == State.isHit;
    }

    public bool isSaved()
    {
        return mState == State.isSaved;
    }

    public void RemoveTank()
    {
        mState = State.isFree;
        gameObject.SetActive(false);
        mScore = 0f;
    }

    public int GetTeam()
    {
        return _team;
    }

    public float GetScore()
    {
        return mScore;
    }

    public State state
    {
        get { return mState; }
        set
        {
            if (mState != value)
            {
                mState = value;
            }
        }
    }
}
