using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class PauseMenu : MonoBehaviour
{
    [Header("UI References")]
    public GameObject PauseMenuCanvas; // renamed from pauseCanvas
    public TMP_InputField speedInput;
    public TextMeshProUGUI speedLabel;
    public TextMeshProUGUI gamePausedText;
    public Button resumeButton;
    public Button restartButton;

    [Header("Robot Reference")]
    public RobotController robot;

    private Controls controls;
    private bool isPaused = false;

    void Awake()
    {
        controls = new Controls();
        controls.Gameplay.Pause.performed += _ =>
        {
            Debug.Log($"🔍 Pause key pressed! TimeScale: {Time.timeScale}, IsPaused: {isPaused}");
            TogglePause();
        };
        controls.Enable();
    }

    void Start()
    {
        PauseMenuCanvas.SetActive(false);

        resumeButton.onClick.AddListener(ResumeGame);
        restartButton.onClick.AddListener(RestartGame);
        speedInput.onEndEdit.AddListener(SetRobotSpeed);

        if (speedInput != null)
        {
            speedInput.contentType = TMP_InputField.ContentType.DecimalNumber;
            speedInput.characterValidation = TMP_InputField.CharacterValidation.Decimal;

            var placeholder = speedInput.placeholder.GetComponent<TextMeshProUGUI>();
            if (placeholder != null)
            {
                placeholder.text = "Robot Speed:";
                placeholder.gameObject.SetActive(true);
            }

            if (robot != null)
                speedInput.text = robot.moveSpeed.ToString("F1");
        }
    }

    void OnDestroy()
    {
        if (controls != null)
        {
            controls.Gameplay.Pause.performed -= _ => TogglePause();
            controls.Disable();
        }
    }

    void TogglePause()
    {
        if (isPaused)
            ResumeGame();
        else
            PauseGame();
    }

    public void PauseGame()
    {
        Time.timeScale = 0f;
        isPaused = true;
        PauseMenuCanvas.SetActive(true);
        Debug.Log("⏸️ Game paused");
    }

    public void ResumeGame()
    {
        Time.timeScale = 1f;
        isPaused = false;
        PauseMenuCanvas.SetActive(false);
        Debug.Log("▶️ Game resumed");
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void SetRobotSpeed(string input)
    {
        if (robot == null || string.IsNullOrWhiteSpace(input)) return;

        if (float.TryParse(input, out float newSpeed))
        {
            newSpeed = Mathf.Clamp(newSpeed, 0.1f, 10f);
            robot.moveSpeed = newSpeed;
            speedInput.text = newSpeed.ToString("F1");
            Debug.Log($"✅ Robot speed updated: {newSpeed}");
        }
        else
        {
            speedInput.text = robot.moveSpeed.ToString("F1");
            Debug.LogWarning("⚠️ Invalid speed input.");
        }
    }
}
