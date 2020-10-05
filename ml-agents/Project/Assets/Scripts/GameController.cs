using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameController : MonoBehaviour
{
    public float _reachDistance = 2.5f;
    public float _spawnDistance = 20f;
    public float _enemySpeed = 3f;
    public float _friendlySpeed = 2f;
    public float _enemySpawnTimer = 1.5f;
    public float _friendlySpawnTimer = 2.8f;
    public int _friendlySaveToWin = 10;
    public int _friendlyKilledToLose = 2;
    public int _enemyEnterToLose = 1;
    public float _powerUpSpawnChance = 0.001f;

    public int _gameScenario = 1;

    public GameObject _tankPrefab;
    public GameObject _turretPrefab;
    public GameObject _powerupPrefab;
    public GameObject _textComponent;

    protected TurretBrain _turret;

    protected const int maxTankArraySize = 16;

    protected GameObject[] mTanksObj = new GameObject[maxTankArraySize];
    protected TankBrain[] mTanks = new TankBrain[maxTankArraySize];
    protected int deadEnemy = 0;
    protected int deadFriendly = 0;
    protected int friendlyEntered = 0;
    protected int enemyEntered = 0;

    protected int currentTankIndex = 0;
    protected bool gameEnds = false;
    protected bool hasReachedLimit = false;

    protected Vector3 origin;

    protected Color[] mPlayerColors =
    {
        Color.blue,
        Color.red,
    };

    protected float mFriendlySpawnTimeRemaining = 0f;
    protected float mEnemySpawnTimeRemaining = 0f;
    protected float mGameTimer = 0f;

    protected float mGameModeTimer = 0f;
    protected int mGameModeState = 1;
    protected int mGameModeReset = 0;
    protected int mGameModeRounds = 0;

    protected int mWinCount = 0;
    protected int mLoseCount = 0;

    protected int mSpawnCycle = 0;

    private TextMesh testMesh;

    void Start()
    {
        origin = this.transform.position + new Vector3(0f, 1f, 0f);

        Quaternion startDir = Quaternion.Euler(0f, 0f, 0f);
        GameObject newTurrentObj = Instantiate(_turretPrefab, origin, startDir);
        _turret = newTurrentObj.GetComponent<TurretBrain>();
        newTurrentObj.name = "turret_"+this.name;

        mGameModeRounds = 1; // (int)(Random.value * 10);
        mGameModeState = (int)(Random.value*2)+1;

        updateDisplayText();
    }

    void Update()
    {
        mGameTimer += Time.deltaTime;

        if (!gameEnds)
        {
            mFriendlySpawnTimeRemaining -= Time.deltaTime;
            mEnemySpawnTimeRemaining -= Time.deltaTime;

            // timer, to check if its time to spawn tanks
            if(mFriendlySpawnTimeRemaining <= 0f)
            {
                mFriendlySpawnTimeRemaining = _friendlySpawnTimer;
                SpawnTank(_spawnDistance, _friendlySpeed, 1);
            }

            if(mEnemySpawnTimeRemaining <= 0f)
            {
                mEnemySpawnTimeRemaining = _enemySpawnTimer;
                SpawnTank(_spawnDistance, _enemySpeed, 2);
            }

            foreach(TankBrain tank in mTanks)
            {
                if(tank != null)
                {
                    // check tank is dead
                    if (tank.isHit())
                    {
                        if (!tank.isEnemy())
                        {
                            ++deadFriendly;
                        }
                        else if (tank.isEnemy())
                        {
                            ++deadEnemy;
                        }
                        tank.RemoveTank();
                    }

                    // check if tank is saved
                    else if(tank.isSaved())
                    {
                        if (!tank.isEnemy())
                        {
                            ++friendlyEntered;
                            _turret.FriendlySaved();
                        }
                        else if (tank.isEnemy())
                        {
                            ++enemyEntered;
                        }
                        tank.RemoveTank();
                    }

                    // if tank alive, move it
                    else if(tank.isAlive())
                    {
                        tank.PerformMove();
                    }
                }
            }

            if(_gameScenario == 3 || _gameScenario == 5)
            {
                float randomChance = Random.value;
                if(randomChance <= _powerUpSpawnChance)
                {
                    Vector3 randomPosition = new Vector3(Random.value * _spawnDistance - (_spawnDistance/2), 0f, Random.value * _spawnDistance - (_spawnDistance/2));
                    Vector3 normVec = randomPosition;
                    randomPosition = origin + normVec.normalized * _spawnDistance;

                    Quaternion randomDirection = Quaternion.Euler(0f, Random.value * 360f, 0f);
                    GameObject newPowerupObj = Instantiate(_powerupPrefab, randomPosition, randomDirection);
                }
            }

            // check if game end
            if (deadFriendly >= _friendlyKilledToLose || enemyEntered >= _enemyEnterToLose)
            {
                mLoseCount++;
                _turret.GameEnded(false);
                gameEnds = true;
                resetGame();
            }

            if(friendlyEntered >= _friendlySaveToWin)
            {
                mWinCount++;
                _turret.GameEnded(true);
                gameEnds = true;
                resetGame();
            }

            // if(mGameTimer >= 3000f && _gameScenario == 1)
            // {
            //     _gameScenario = 2;
            //     Debug.Log("Game Scenario 2");
            // }

            // if(mGameTimer >= 1000f && _friendlySaveToWin == 1)
            // {
            //     _friendlySaveToWin = 10;
            //     _enemySpeed = 3f;
            //     _friendlySpeed = 2f;
            //     _enemySpawnTimer = 1.5f;
            //     _friendlySpawnTimer = 2f;
            //     Debug.Log("Game Scenario upgraded");
            // }

        }

    }

    public void resetGame()
    {
        // if(friendlyEntered != 0){
        //     mGameModeRounds -= 1;
        // }
        mGameModeRounds -= 1;
        
        if(mGameModeRounds <= 0)
        {
            mGameModeReset = 0;
            mGameModeRounds = 1;
        }

        deadEnemy = 0;
        deadFriendly = 0;
        friendlyEntered = 0;
        enemyEntered = 0;
        gameEnds = false;

        foreach(TankBrain tank in mTanks)
        {
            if(tank != null)
            {
                tank.RemoveTank();
            }
        }

        updateDisplayText();
    }

    void SpawnTank(float _spawnDistance, float movespeed, int team)
    {
        Quaternion randomDirection = Quaternion.Euler(0f, 0f, 0f);
        Vector3 randomPosition = origin + new Vector3(0f, 0f, -20f);

        if(_gameScenario == 1)
        {
            // mGameModeTimer -= Time.deltaTime;

            if(mGameModeReset == 0)
            {
                if(mGameModeState==1)
                {
                    mGameModeState = 2;
                }
                else
                {
                    mGameModeState = 1;
                }
                mGameModeReset = 1;
            }

            float randomchoice = Random.value;
            if(team==mGameModeState)
            {
                if(randomchoice<=0.5)
                {
                    randomDirection = Quaternion.Euler(0f, 180f, 0f);
                    randomPosition = origin + new Vector3(0f, 0f, 20f);
                }
                else
                {
                    randomDirection = Quaternion.Euler(0f, 0f, 0f);
                    randomPosition = origin + new Vector3(0f, 0f, -20f);
                }
            }
            else
            {
                if(randomchoice<=0.5)
                {
                    randomDirection = Quaternion.Euler(0f, 270f, 0f);
                    randomPosition = origin + new Vector3(20f, 0f, 0f);
                }
                else
                {
                    randomDirection = Quaternion.Euler(0f, 90f, 0f);
                    randomPosition = origin + new Vector3(-20f, 0f, 0f);
                }
            }
        }
        else
        {
            // fully random
            // randomPosition = new Vector3(Random.value * _spawnDistance - (_spawnDistance/2), 0f, Random.value * _spawnDistance - (_spawnDistance/2));

            // controlled random
            float random_x = Random.value * _spawnDistance;
            float random_z = Random.value * _spawnDistance;
            
            if(mSpawnCycle == 1)
            {
                random_x = random_x * -1;
            }
            else if(mSpawnCycle == 2)
            {
                random_x = random_x * -1;
                random_z = random_z * -1;
            }
            else if(mSpawnCycle == 3)
            {
                random_z = random_z * -1;
            }

            mSpawnCycle = mSpawnCycle + 1;
            
            if(mSpawnCycle == 4)
            {
                mSpawnCycle = 0;
            }

            randomPosition = new Vector3(random_x, 0f, random_z);
            Vector3 normVec = randomPosition;
            randomPosition = origin + normVec.normalized * _spawnDistance;

            randomDirection = Quaternion.Euler(0f, Random.value * 360f, 0f);
        }
        

        if(!hasReachedLimit)
        {
            GameObject newTankObj = Instantiate(_tankPrefab, randomPosition, randomDirection);
            mTanksObj[currentTankIndex] = newTankObj;

            TankBrain newTank = mTanksObj[currentTankIndex].GetComponent<TankBrain>();
            mTanks[currentTankIndex] = newTank;
        }
        else
        {
            mTanksObj[currentTankIndex].SetActive(true);
        }

        // assign tag
        string thistag = "blueAgent";
        if(team == 2)
        {
            thistag = "target";
        }

        mTanksObj[currentTankIndex].tag = thistag;

        foreach(Transform t in mTanksObj[currentTankIndex].transform)
        {
            t.gameObject.tag = thistag;
        }

        // init properties
        bool mRandomMovement = false;
        if(_gameScenario == 4 && team == 2)
        {
            mRandomMovement = true;
        }
        mTanks[currentTankIndex].setProperties(movespeed, team, randomDirection, randomPosition, _reachDistance, origin, this.name, mRandomMovement);

        MeshRenderer[] renderers = mTanks[currentTankIndex].GetComponentsInChildren<MeshRenderer>();
        foreach (MeshRenderer rend in renderers)
            rend.material.color = mPlayerColors[(team-1)];
        
        currentTankIndex++;
        if(currentTankIndex == maxTankArraySize){
            currentTankIndex = 0;
            hasReachedLimit = true;
        }
        
    }

    void updateDisplayText()
     {
         testMesh = _textComponent.GetComponent<TextMesh>();
         string displayText = "Win:"+mWinCount.ToString()+" | Lost:"+mLoseCount.ToString();
         testMesh.text = displayText;
     }
}


public class Timer : MonoBehaviour
{
    public float timeRemaining = 0f;

    public void setTimer(float time)
    {
        timeRemaining = time;
    }

    void Update()
    {
        Debug.Log(timeRemaining);
        if (timeRemaining > 0)
        {
            timeRemaining -= Time.deltaTime;
            
        }
    }
}