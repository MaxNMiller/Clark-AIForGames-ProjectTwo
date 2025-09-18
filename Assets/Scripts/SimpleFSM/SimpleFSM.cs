using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

public class SimpleFSM : FSM
{
    public Image healthBar;
    public int StartingHealth = 100;
    public int criticalHealth;
    public PredictPlayer playerpredictor;
    private bool hasPredictor = false;
    private Vector3 ambushPoint;
    public enum FSMState
   
    {
        None,
        Patrol,
        Chase,
        Attack,
        Dead,
        Dance,
        Ninja,
        Heal
    }

    [System.Serializable]
    public struct AIStateColor
    {
        public FSMState state;
        public Color color;
    }

    public AIStateColor[] stateColors;
    private Dictionary<FSMState, Color> _colorMap;
    private Renderer _renderer;
    private MaterialPropertyBlock _propBlock;

    private FSMState _curState;
    public FSMState CurState
    {
        get { return _curState; }
        set
        {
            if (_curState == value) return;
            _curState = value;
            UpdateStateColor();
        }
    }

    private float curSpeed;
    private float curRotSpeed;
    public GameObject Bullet;
    float startDanceTime, stopDanceTime;
    private bool bDead;
    private int health
    {
        get { return _health; }
        set { _health = value; healthBar.fillAmount = (float)_health / (float)StartingHealth; }
    }
    [SerializeField] private int _health = 100;

    protected override void Initialize()
    {
        _propBlock = new MaterialPropertyBlock();
        _renderer = GetComponent<Renderer>();
        _colorMap = new Dictionary<FSMState, Color>();
        foreach (var stateColor in stateColors) _colorMap[stateColor.state] = stateColor.color;

        CurState = FSMState.Patrol;
        curSpeed = 150.0f;
        curRotSpeed = 2.0f;
        bDead = false;
        elapsedTime = 0.0f;
        shootRate = 3.0f;
        health = StartingHealth;

        pointList = GameObject.FindGameObjectsWithTag("WandarPoint");
        FindNextPoint();

        if (!playerTransform)
            print("Player doesn't exist.. Please add one with Tag named 'Player'");

        turret = gameObject.transform.GetChild(0).transform;
        bulletSpawnPoint = turret.GetChild(0).transform;
    }

    private void UpdateStateColor()
    {
        if (_renderer == null || !_colorMap.ContainsKey(CurState)) return;
        _renderer.GetPropertyBlock(_propBlock);
        _propBlock.SetColor("_Color", _colorMap[CurState]);
        _renderer.SetPropertyBlock(_propBlock);
    }

    protected override void FSMUpdate()
    {
        switch (CurState)
        {
            case FSMState.Patrol: UpdatePatrolState(); break;
            case FSMState.Chase: UpdateChaseState(); break;
            case FSMState.Attack: UpdateAttackState(); break;
            case FSMState.Dead: UpdateDeadState(); break;
            case FSMState.Dance: UpdateDanceState(); break;
            case FSMState.Ninja: UpdateNinjaState(); break;
        }

        elapsedTime += Time.deltaTime;

        if (health <= 0) CurState = FSMState.Dead;
        else if (health <= criticalHealth) CurState = FSMState.Heal;
    }

    protected void UpdateDanceState()
    {
        transform.Rotate(new Vector3(0, 1, 0));
        if (elapsedTime >= stopDanceTime)
        {
            CurState = FSMState.Patrol;
            startDanceTime = elapsedTime + Random.Range(2, 5);
        }
        if (Vector3.Distance(transform.position, playerTransform.position) <= 300.0f)
            CurState = FSMState.Chase;
    }

    protected void UpdatePatrolState()
    {
        if (Vector3.Distance(transform.position, destPos) <= 100.0f)
            FindNextPoint();
        else if (Vector3.Distance(transform.position, playerTransform.position) <= 300.0f)
            CurState = FSMState.Chase;

        Quaternion targetRotation = Quaternion.LookRotation(destPos - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * curRotSpeed);
        transform.Translate(Vector3.forward * Time.deltaTime * curSpeed);

        if (elapsedTime > startDanceTime)
        {
            stopDanceTime = elapsedTime + Random.Range(1, 2);
            CurState = FSMState.Dance;
        }

        if (Random.value < 0.0002f)
        {
            CurState = FSMState.Ninja;
        }
    }

    protected void UpdateChaseState()
    {
        destPos = playerTransform.position;
        float dist = Vector3.Distance(transform.position, playerTransform.position);
        if (dist <= 200.0f) CurState = FSMState.Attack;
        else if (dist >= 300.0f) CurState = FSMState.Patrol;

        transform.Translate(Vector3.forward * Time.deltaTime * curSpeed);
    }

    protected void UpdateAttackState()
    {
        destPos = playerTransform.position;
        float dist = Vector3.Distance(transform.position, playerTransform.position);
        if (dist >= 200.0f && dist < 300.0f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(destPos - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * curRotSpeed);
            transform.Translate(Vector3.forward * Time.deltaTime * curSpeed);
            CurState = FSMState.Attack;
        }
        else if (dist >= 300.0f) CurState = FSMState.Patrol;

        Quaternion turretRotation = Quaternion.LookRotation(destPos - turret.position);
        turret.rotation = Quaternion.Slerp(turret.rotation, turretRotation, Time.deltaTime * curRotSpeed);
        ShootBullet();
    }

    protected void UpdateNinjaState()
    {
        if (!hasPredictor)
        {
            ambushPoint = playerpredictor.GetPredictedPosition();
            hasPredictor = true;
        }

        float distToAmbush = Vector3.Distance(transform.position, ambushPoint);

        if (distToAmbush > 0.1f) // Move toward ambush point
        {
            Quaternion targetRotation = Quaternion.LookRotation(ambushPoint - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * curRotSpeed * 2.0f);

            float step = curSpeed * 1.5f * Time.deltaTime;
            transform.position = Vector3.MoveTowards(transform.position, ambushPoint, step);
        }
        else // Stop moving, rotate to track player
        {
            Quaternion lookAtPlayer = Quaternion.LookRotation(playerTransform.position - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookAtPlayer, Time.deltaTime * curRotSpeed * 2.0f);
        }

        float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        if (distToPlayer <= 200.0f)
        {
            CurState = FSMState.Attack;
            hasPredictor = false;
        }
    }


    protected void UpdateDeadState() { if (!bDead) { bDead = true; Explode(); } }
    private void ShootBullet() { if (elapsedTime >= shootRate) { Instantiate(Bullet, bulletSpawnPoint.position, bulletSpawnPoint.rotation); elapsedTime = 0.0f; } }
    void OnCollisionEnter(Collision collision) { if (collision.gameObject.tag == "Bullet") health -= collision.gameObject.GetComponent<Bullet>().damage; }
    protected void FindNextPoint() { int rndIndex = Random.Range(0, pointList.Length); float rndRadius = 10.0f; Vector3 rndPosition = new Vector3(Random.Range(-rndRadius, rndRadius), 0f, Random.Range(-rndRadius, rndRadius)); destPos = pointList[rndIndex].transform.position + rndPosition; if (IsInCurrentRange(destPos)) { rndPosition = new Vector3(Random.Range(-rndRadius, rndRadius), 0f, Random.Range(-rndRadius, rndRadius)); destPos = pointList[rndIndex].transform.position + rndPosition; } }
    protected bool IsInCurrentRange(Vector3 pos) { float xPos = Mathf.Abs(pos.x - transform.position.x); float zPos = Mathf.Abs(pos.z - transform.position.z); return !(xPos <= 50 && zPos <= 50); }
    protected void Explode() { float rndX = Random.Range(10.0f, 30.0f); float rndZ = Random.Range(10.0f, 30.0f); for (int i = 0; i < 3; i++) { GetComponent<Rigidbody>().AddExplosionForce(10000.0f, transform.position - new Vector3(rndX, 10.0f, rndZ), 40.0f, 10.0f); GetComponent<Rigidbody>().linearVelocity = transform.TransformDirection(new Vector3(rndX, 20.0f, rndZ)); } Destroy(gameObject, 1.5f); }
}
