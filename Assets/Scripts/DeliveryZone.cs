using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DeliveryZone : MonoBehaviour
{
    [Header("Optional visual feedback")]
    public Renderer zoneRenderer;           // drag the Cube’s MeshRenderer here
    public Color   idleColor   = Color.yellow;
    public Color   activeColor = Color.green;

    void Reset()
    {
        // Ensure the collider is set as trigger when you add the script
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;

        // Try auto‑grab a Renderer on the same object
        if (!zoneRenderer) zoneRenderer = GetComponent<Renderer>();
    }

    private void Start()
    {
        if (zoneRenderer) zoneRenderer.material.color = idleColor;
    }

    /*───────────────────────────────────────────────*/
    private void OnTriggerEnter(Collider other)
    {
        RobotController robot = other.GetComponent<RobotController>();
        if (robot != null)
        {
            robot.IsInDropZone = true;      // public setter on RobotController
            if (zoneRenderer) zoneRenderer.material.color = activeColor;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        RobotController robot = other.GetComponent<RobotController>();
        if (robot != null)
        {
            robot.IsInDropZone = false;
            if (zoneRenderer) zoneRenderer.material.color = idleColor;
        }
    }
}
