using UnityEngine;
using System.Collections;
using System.Collections.Generic; // Needed for Dictionary
using UnityEngine.UI;
public class SimpleFSM : FSM
{
    public Image healthBar;
    public int StartingHealth = 100;
    public enum FSMState
    {
        None,
        Patrol,
        Chase,
        Attack,
        Dead,
    }

    [System.Serializable]
    public struct AIStateColor
    {
        public FSMState state;
        public Color color;
    }

    public AIStateColor[] stateColors;

    // We'll use a dictionary for fast lookups at runtime
    private Dictionary<FSMState, Color> _colorMap;
    private Renderer _renderer;
    private MaterialPropertyBlock _propBlock;

    //Current state that the NPC is reaching
    // MODIFIED: We change this to a property to automatically update the color on change
    private FSMState _curState;
    public FSMState CurState
    {
        get { return _curState; }
        set
        {
            if (_curState == value) return; // Don't do anything if the state hasn't changed
            _curState = value;
            UpdateStateColor(); // This function will update the material
        }
    }

    //Speed of the tank
    private float curSpeed;

    //Tank Rotation Speed
    private float curRotSpeed;

    //Bullet
    public GameObject Bullet;

    //Whether the NPC is destroyed or not
    private bool bDead;
     private int health
    {
        get
        {
            return _health;
        }
        set
        {
            _health = value;
            healthBar.fillAmount = (float)_health / (float)StartingHealth;
        }
    }
    [SerializeField] private int _health = 100;


    //Initialize the Finite state machine for the NPC tank
    protected override void Initialize()
    {
        _propBlock = new MaterialPropertyBlock();
        _renderer = GetComponent<Renderer>();
        _colorMap = new Dictionary<FSMState, Color>();
        foreach (var stateColor in stateColors)
        {
            _colorMap[stateColor.state] = stateColor.color;
        }

        // Set the initial state using the new property
        CurState = FSMState.Patrol;

        curSpeed = 150.0f;
        curRotSpeed = 2.0f;
        bDead = false;
        elapsedTime = 0.0f;
        shootRate = 3.0f;
        health = StartingHealth;
       
        //Get the list of points
        pointList = GameObject.FindGameObjectsWithTag("WandarPoint");

        //Set Random destination point first
        FindNextPoint();

        //Get the target enemy(Player)
        //GameObject objPlayer = GameObject.FindGameObjectWithTag("Player");
        //playerTransform = objPlayer.transform;

        if (!playerTransform)
            print("Player doesn't exist.. Please add one with Tag named 'Player'");

        //Get the turret of the tank
        turret = gameObject.transform.GetChild(0).transform;
        bulletSpawnPoint = turret.GetChild(0).transform;
    }

    private void UpdateStateColor()
    {
        if (_renderer == null || !_colorMap.ContainsKey(CurState)) return;

        // Get the current properties from the renderer
        _renderer.GetPropertyBlock(_propBlock);
        // Set the color value in the block, using the shader property name "_Color"
        _propBlock.SetColor("_Color", _colorMap[CurState]);
        // Apply the updated block back to the renderer
        _renderer.SetPropertyBlock(_propBlock);
    }

    //Update each frame
    protected override void FSMUpdate()
    {
        // MODIFIED: Use the new property instead of the old field
        switch (CurState)
        {
            case FSMState.Patrol: UpdatePatrolState(); break;
            case FSMState.Chase: UpdateChaseState(); break;
            case FSMState.Attack: UpdateAttackState(); break;
            case FSMState.Dead: UpdateDeadState(); break;
        }

        //Update the time
        elapsedTime += Time.deltaTime;

        //Go to dead state is no health left
        if (health <= 0)
            CurState = FSMState.Dead; // MODIFIED
    }

    protected void UpdatePatrolState()
    {
        if (Vector3.Distance(transform.position, destPos) <= 100.0f)
        {
            print("Reached to the destination point\ncalculating the next point");
            FindNextPoint();
        }
        else if (Vector3.Distance(transform.position, playerTransform.position) <= 300.0f)
        {
            print("Switch to Chase Position");
            CurState = FSMState.Chase; // MODIFIED
        }

        Quaternion targetRotation = Quaternion.LookRotation(destPos - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * curRotSpeed);
        transform.Translate(Vector3.forward * Time.deltaTime * curSpeed);
    }

    protected void UpdateChaseState()
    {
        destPos = playerTransform.position;
        float dist = Vector3.Distance(transform.position, playerTransform.position);
        if (dist <= 200.0f)
        {
            CurState = FSMState.Attack; // MODIFIED
        }
        else if (dist >= 300.0f)
        {
            CurState = FSMState.Patrol; // MODIFIED
        }
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
            CurState = FSMState.Attack; // MODIFIED
        }
        else if (dist >= 300.0f)
        {
            CurState = FSMState.Patrol; // MODIFIED
        }
        Quaternion turretRotation = Quaternion.LookRotation(destPos - turret.position);
        turret.rotation = Quaternion.Slerp(turret.rotation, turretRotation, Time.deltaTime * curRotSpeed);
        ShootBullet();
    }

    // Unchanged methods from here...

    protected void UpdateDeadState() { if (!bDead) { bDead = true; Explode(); } }
    private void ShootBullet() { if (elapsedTime >= shootRate) { Instantiate(Bullet, bulletSpawnPoint.position, bulletSpawnPoint.rotation); elapsedTime = 0.0f; } }
    void OnCollisionEnter(Collision collision) { if (collision.gameObject.tag == "Bullet") health -= collision.gameObject.GetComponent<Bullet>().damage; }
    protected void FindNextPoint() { print("Finding next point"); int rndIndex = Random.Range(0, pointList.Length); float rndRadius = 10.0f; Vector3 rndPosition = Vector3.zero; destPos = pointList[rndIndex].transform.position + rndPosition; if (IsInCurrentRange(destPos)) { rndPosition = new Vector3(Random.Range(-rndRadius, rndRadius), 0.0f, Random.Range(-rndRadius, rndRadius)); destPos = pointList[rndIndex].transform.position + rndPosition; } }
    protected bool IsInCurrentRange(Vector3 pos) { float xPos = Mathf.Abs(pos.x - transform.position.x); float zPos = Mathf.Abs(pos.z - transform.position.z); if (xPos <= 50 && zPos <= 50) return true; return false; }
    protected void Explode() { float rndX = Random.Range(10.0f, 30.0f); float rndZ = Random.Range(10.0f, 30.0f); for (int i = 0; i < 3; i++) { GetComponent<Rigidbody>().AddExplosionForce(10000.0f, transform.position - new Vector3(rndX, 10.0f, rndZ), 40.0f, 10.0f); GetComponent<Rigidbody>().linearVelocity = transform.TransformDirection(new Vector3(rndX, 20.0f, rndZ)); } Destroy(gameObject, 1.5f); }
}