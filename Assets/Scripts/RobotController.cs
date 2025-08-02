using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Animator))]
public class RobotController : MonoBehaviour
{
    [Header("Manual movement")]
    public float moveSpeed = 3.5f;
    public float turnSpeed = 180f;

    [Header("AI navigation")]
    public Transform target;
    public float arriveThreshold = 0.2f;

    [Header("Pickup Settings")]
    public Transform holdPoint;
    public float pickupRange = 2f;
    public LayerMask pickupLayer;

    [Header("Battery Settings")]
    public float maxBattery = 100f;
    public float currentBattery = 100f;
    public float drainRate = 5f;
    public float chargeRate = 10f;
    public bool isCharging = false;
    public Transform chargingStation;

    [Header("UI")]
    public Slider batterySlider;
    public Image batteryFill;

    public bool IsInDropZone { get; set; } = false;

    private NavMeshAgent agent;
    private Rigidbody rb;
    private Controls controls; // Use local instance
    private Animator animator;
    private Vector2 moveInput;
    private bool manualMode;
    private GameObject heldObject;
    private Gradient batteryGradient;

    private bool batteryLow = false;
    private bool finishingTaskBeforeCharge = false;

    private bool canPickup = true;
    private float pickupCooldownTime = 1.5f;

    private HashSet<GameObject> deliveredBoxes = new HashSet<GameObject>();

    private enum AIState { Idle, SeekingBox, MovingToBox, LiftingBox, MovingToDropZone, DroppingBox, Charging }
    private AIState currentState = AIState.Idle;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        rb.isKinematic = true;

        controls = new Controls(); // Restore local initialization
        controls.Gameplay.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Gameplay.Move.canceled += _ => moveInput = Vector2.zero;
        controls.Gameplay.ToggleManual.performed += _ => ToggleControlMode();
        controls.Gameplay.LiftUp.performed += _ => { animator.SetTrigger("LiftUp"); StartCoroutine(DelayedPickup()); };
        controls.Gameplay.LiftDown.performed += _ => { animator.SetTrigger("LiftDown"); DropObject(); };

        batteryGradient = new Gradient();
        batteryGradient.colorKeys = new GradientColorKey[]
        {
            new GradientColorKey(Color.red, 0f),
            new GradientColorKey(Color.yellow, 0.5f),
            new GradientColorKey(Color.green, 1f)
        };
    }

    void Start()
    {
        if (!agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
            Debug.Log("✅ Snapped to NavMesh at: " + hit.position);
        }

        if (batterySlider != null)
        {
            batterySlider.maxValue = maxBattery;
            batterySlider.value = currentBattery;
            UpdateBatteryColor();
        }
    }

    void OnEnable() => controls?.Enable();
    void OnDisable() => controls?.Disable();

    void Update()
    {
        if (currentState == AIState.Charging)
        {
            // Only handle charging logic
            if (isCharging && currentBattery < maxBattery)
            {
                currentBattery += chargeRate * Time.deltaTime;
                if (currentBattery >= maxBattery)
                {
                    currentBattery = maxBattery;
                    isCharging = false;
                    batteryLow = false;
                    Debug.Log("✅ Fully charged!");
                    currentState = AIState.Idle; // Ensure state returns to Idle after charging
                    GameManager.Instance?.ActivateNextRobot(this);
                }
            }

            if (batterySlider != null)
            {
                batterySlider.value = currentBattery;
                UpdateBatteryColor();
            }
            return; // Skip the rest of Update while charging
        }

        if (manualMode)
        {
            ManualMove();
        }
        else
        {
            HandleAIStateMachine();
        }

        if (heldObject != null)
        {
            heldObject.transform.position = holdPoint.position;
            heldObject.transform.rotation = holdPoint.rotation;
        }

        bool isMoving = manualMode || (agent.enabled && agent.velocity.magnitude > 0.1f);

        if (!isCharging && isMoving)
        {
            currentBattery -= drainRate * Time.deltaTime;

            if (!batteryLow && currentBattery <= 20f)
            {
                batteryLow = true;
                Debug.Log("⚠️ Battery under 20%! Going to charge NOW.");
                GoToChargingStation();
            }

            if (currentBattery <= 0f) currentBattery = 0f;
        }
        else if (isCharging && currentBattery < maxBattery)
        {
            currentBattery += chargeRate * Time.deltaTime;
            if (currentBattery >= maxBattery)
            {
                currentBattery = maxBattery;
                isCharging = false;
                batteryLow = false;
                Debug.Log("✅ Fully charged!");
                currentState = AIState.Idle; // Ensure state returns to Idle after charging
                GameManager.Instance?.ActivateNextRobot(this);
            }
        }

        if (batterySlider != null)
        {
            batterySlider.value = currentBattery;
            UpdateBatteryColor();
        }
    }

    private void HandleAIStateMachine()
    {
        switch (currentState)
        {
            case AIState.Idle:
                currentState = batteryLow ? AIState.Charging : AIState.SeekingBox;
                break;

            case AIState.SeekingBox:
                GameObject box = FindClosestPickup();
                if (box != null)
                {
                    target = box.transform;
                    SetNewDestination();
                    currentState = AIState.MovingToBox;
                }
                else
                {
                    currentState = AIState.Idle;
                }
                break;

            case AIState.MovingToBox:
                if (!agent.pathPending && agent.remainingDistance <= arriveThreshold)
                {
                    currentState = AIState.LiftingBox;
                    StartCoroutine(DelayedLiftUp());
                }
                break;

            case AIState.MovingToDropZone:
                if (!agent.pathPending && agent.remainingDistance <= arriveThreshold)
                {
                    StartCoroutine(DelayedDrop());
                    currentState = AIState.DroppingBox;
                }
                break;

            case AIState.Charging:
                // Stay in charging state until fully charged
                agent.isStopped = true;
                // Do not trigger any animation or state change here
                break;
        }
    }


    private IEnumerator DelayedLiftUp()
    {
        animator.SetTrigger("LiftUp");
        yield return new WaitForSeconds(0.75f);
        TryPickup();

        GameObject dropZone = GameObject.FindGameObjectWithTag("DropZone");
        if (dropZone != null)
        {
            target = dropZone.transform;
            SetNewDestination();
            currentState = AIState.MovingToDropZone;
        }
        else currentState = AIState.Idle;
    }

    private IEnumerator DelayedDrop()
{
    animator.SetTrigger("LiftDown");
    yield return new WaitForSeconds(0.75f);

    if (heldObject != null)
    {
        deliveredBoxes.Add(heldObject);
    }

    DropObject();
    target = null;

    if (batteryLow && finishingTaskBeforeCharge)
    {
        finishingTaskBeforeCharge = false;
        GoToChargingStation();
        currentState = AIState.Charging;
    }
    else
    {
        currentState = batteryLow ? AIState.Charging : AIState.Idle;
    }
}


    private GameObject FindClosestPickup()
    {
        GameObject[] pickups = GameObject.FindGameObjectsWithTag("Pickup");
        GameObject closest = null;
        float minDist = Mathf.Infinity;
        Vector3 pos = transform.position;

        foreach (GameObject pickup in pickups)
        {
            if (deliveredBoxes.Contains(pickup)) continue;

            float dist = Vector3.Distance(pickup.transform.position, pos);
            if (dist < minDist)
            {
                minDist = dist;
                closest = pickup;
            }
        }
        return closest;
    }

    private void ManualMove()
    {
        Vector3 forward = transform.forward * moveInput.y * moveSpeed * Time.deltaTime;
        transform.position += forward;
        float yaw = moveInput.x * turnSpeed * Time.deltaTime;
        transform.Rotate(0, yaw, 0);
    }

    private void ToggleControlMode()
    {
        manualMode = !manualMode;
        agent.enabled = !manualMode;
        rb.isKinematic = !manualMode;
        if (agent.enabled)
        {
            if (agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            else
            {
                Debug.LogWarning("⚠️ Tried to reset path, but agent is not on NavMesh.");
            }
        }
    }

    private void SetNewDestination()
    {
        if (target == null) return;
        agent.isStopped = false;
        agent.SetDestination(target.position);
    }

    private IEnumerator DelayedPickup()
    {
        yield return new WaitForSeconds(0.75f);
        TryPickup();
    }

    private void TryPickup()
    {
        if (!canPickup || heldObject != null) return;

        Vector3 rayStart = holdPoint.position - Vector3.up * 0.3f;
        if (Physics.Raycast(rayStart, Vector3.up, out RaycastHit hit, pickupRange, pickupLayer))
        {
            GameObject obj = hit.collider.gameObject;
            if (deliveredBoxes.Contains(obj)) return;

            heldObject = obj;
            Rigidbody rb = obj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
            obj.transform.SetParent(holdPoint, false);
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.identity;
            Debug.Log("✅ Picked up: " + obj.name);
        }
        else Debug.Log("❌ No object detected.");
    }

    private void DropObject()
    {
        if (heldObject == null) return;
        Rigidbody rb = heldObject.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = false;

        heldObject.transform.SetParent(null);
        heldObject.transform.position += transform.forward * 0.5f; // push it forward a bit
        heldObject = null;

        Debug.Log("🟡 Dropped object.");
        if (IsInDropZone) GameManager.Instance?.AddScore();

        StartCoroutine(PickupCooldown());
    }

    private IEnumerator PickupCooldown()
    {
        canPickup = false;
        yield return new WaitForSeconds(pickupCooldownTime);
        canPickup = true;
    }

    private void GoToChargingStation()
{
    manualMode = false;
    isCharging = false; // charging will begin only when entering the trigger zone
    rb.isKinematic = true;
    if (agent.enabled && agent.isOnNavMesh && chargingStation != null)
    {
        agent.isStopped = false;
        agent.SetDestination(chargingStation.position);
    }
}

    private void UpdateBatteryColor()
    {
        if (batteryFill != null)
        {
            float t = currentBattery / maxBattery;
            batteryFill.color = batteryGradient.Evaluate(t);
        }
    }

    private bool IsHoldingSomething() => heldObject != null;

    public void SetBatterySlider(Slider slider)
    {
        batterySlider = slider;
        batterySlider.maxValue = maxBattery;
        batterySlider.value = currentBattery;
        UpdateBatteryColor();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.transform == chargingStation)
        {
            isCharging = true;
            agent.isStopped = true;
            currentState = AIState.Charging;
            Debug.Log("🔋 Arrived at charging station, charging...");
        }
    }
}
    

