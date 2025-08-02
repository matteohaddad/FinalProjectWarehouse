using UnityEngine;

public class ChargingZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        RobotController robot = other.GetComponent<RobotController>();
        if (robot != null)
        {
            robot.isCharging = true;
            Debug.Log("⚡ Robot entered charging zone.");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        RobotController robot = other.GetComponent<RobotController>();
        if (robot != null)
        {
            robot.isCharging = false;
            Debug.Log("🔋 Robot left charging zone.");
        }
    }
}
