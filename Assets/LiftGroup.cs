using UnityEngine;

/// Put this on the LiftGroup / platform root.
/// A trigger collider (green wireframe) should cover the top surface.
[RequireComponent(typeof(Collider))]
public class LiftGroup : MonoBehaviour
{
    [Header("Motion")]
    [Tooltip("Local Y position when platform is down")]
    public float downY = 0f;
    [Tooltip("Local Y position when fully lifted")]
    public float upY   = 0.35f;
    public float speed = 0.4f;               // m/s

    [Header("Input (optional)")]
    public KeyCode keyUp   = KeyCode.U;      // test keys
    public KeyCode keyDown = KeyCode.J;

    // ─────────────────────────────────────────────────────────────
    Transform  _load;        // current thing being carried
    Rigidbody  _loadRb;
    float      _targetY;
    bool       _busy;

    void Awake()
    {
        // make sure the collider is trigger‑only (so robot wheels don’t hit it)
        var col = GetComponent<Collider>();
        col.isTrigger = true;

        // start lowered
        _targetY = downY;
        SetLocalY(downY);
    }

    void Update()
    {
        // Demo keyboard input (replace with your own control calls)
        if (Input.GetKeyDown(keyUp))   Lift();
        if (Input.GetKeyDown(keyDown)) Lower();

        // Smoothly move toward the target height
        if (_busy)
        {
            float newY = Mathf.MoveTowards(LocalY(), _targetY, speed * Time.deltaTime);
            SetLocalY(newY);

            if (Mathf.Approximately(newY, _targetY))
            {
                _busy = false;                    // reached goal
                if (Mathf.Approximately(newY, downY))
                    DetachLoad();                 // finished lowering
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    public void Lift()
    {
        if (_busy || Mathf.Approximately(LocalY(), upY)) return;
        _targetY = upY;
        _busy    = true;
        AttachLoadIfPresent();
    }

    public void Lower()
    {
        if (_busy || Mathf.Approximately(LocalY(), downY)) return;
        _targetY = downY;
        _busy    = true;
    }

    // ─────────────────────────────────────────────────────────────
    void AttachLoadIfPresent()
    {
        if (_load != null) return;               // already have one

        // Search children overlapped by the trigger collider
        Collider[] hits = Physics.OverlapBox(
            transform.position + transform.up * 0.02f,
            GetComponent<Collider>().bounds.extents * 0.9f,
            transform.rotation);

        foreach (var h in hits)
        {
            if (h.CompareTag("Load"))
            {
                _load   = h.transform;
                _loadRb = _load.GetComponent<Rigidbody>();
                if (_loadRb) _loadRb.isKinematic = true;

                _load.SetParent(transform, true); // keep current world pose
                return;
            }
        }
    }

    void DetachLoad()
    {
        if (_load == null) return;

        _load.SetParent(null, true);
        if (_loadRb) _loadRb.isKinematic = false;

        _load   = null;
        _loadRb = null;
    }

    // ─────────────────────────────────────────────────────────────
    float LocalY()                      => transform.localPosition.y;
    void  SetLocalY(float y)            => transform.localPosition = new Vector3(0, y, 0);
}
