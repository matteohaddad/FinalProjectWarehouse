using TMPro;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("UI References")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI highScoreText;
    public GameObject deliveredPopup;

    [Header("Battery Gradient")]
    public Gradient batteryGradient = new Gradient
    {
        colorKeys = new GradientColorKey[]
        {
            new GradientColorKey(Color.red, 0f),
            new GradientColorKey(Color.yellow, 0.5f),
            new GradientColorKey(Color.green, 1f)
        }
    };

    [Header("Game Settings")]
    private int score = 0;
    private int highScore = 0;
    private float timer = 60f;
    private bool gameActive = true;

    // ─────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void Start()
    {
        highScore = PlayerPrefs.GetInt("HighScore", 0);

        UpdateScoreUI();
        UpdateTimerUI();
        UpdateHighScoreUI();

        if (deliveredPopup != null)
            deliveredPopup.SetActive(false);
    }

    void Update()
    {
        if (!gameActive) return;

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            timer = 0f;
            gameActive = false;
            Debug.Log("🛑 Timer ended!");
        }

        UpdateTimerUI();
    }

    public void AddScore()
    {
        if (!gameActive) return;

        score++;

        if (score > highScore)
        {
            highScore = score;
            PlayerPrefs.SetInt("HighScore", highScore);
        }

        UpdateScoreUI();
        UpdateHighScoreUI();
        ShowDeliveredPopup();
    }

    // ─────────────────────────────────────────────────────────────

    private void UpdateScoreUI()
    {
        if (scoreText != null)
            scoreText.text = $"Score: {score}";
    }

    private void UpdateTimerUI()
    {
        if (timerText != null)
            timerText.text = $"Time: {timer:F1}s";
    }

    private void UpdateHighScoreUI()
    {
        if (highScoreText != null)
            highScoreText.text = $"High Score: {highScore}";
    }

    private void ShowDeliveredPopup()
    {
        if (deliveredPopup == null) return;

        deliveredPopup.SetActive(true);
        CancelInvoke(nameof(HideDeliveredPopup));
        Invoke(nameof(HideDeliveredPopup), 1f);
    }

    private void HideDeliveredPopup()
    {
        if (deliveredPopup != null)
            deliveredPopup.SetActive(false);
    }

    // Optional: For testing or UI button
    public void ResetHighScore()
    {
        PlayerPrefs.DeleteKey("HighScore");
        highScore = 0;
        UpdateHighScoreUI();
    }

    // Optional: Called by Robot when battery finishes charging
    public void ActivateNextRobot(RobotController previous)
    {
        Debug.Log("🔄 Switching to next robot (not implemented yet).");
        // TODO: Enable next robot or switch control logic here
    }
}
