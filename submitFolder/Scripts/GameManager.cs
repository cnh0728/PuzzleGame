using UnityEngine;
using UnityEngine.UI;

public enum GameState
{
    MainMenu,
    Playing,
    GameOver
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public GameState CurrentState { get; private set; }

    [Header("Managers")]
    public BoardManager boardManager;

    [Header("Game Settings")]
    [SerializeField] private float maxHP = 100f;             // 최초 HP
    [SerializeField] public float baseHpDrainRate = 5f;     // 초기 초당 HP 감소량 (5/sec)
    [SerializeField] public float drainAcceleration = 0.2f; // 1초마다 증가하는 추가 감소량
    [SerializeField] private float healPerDrop = 10f;        // 회복 드롭 1개당 회복 HP

    [Header("UI Panels")]
    [SerializeField] private GameObject panelMainMenu;
    [SerializeField] private GameObject panelInGame;
    [SerializeField] private GameObject panelResult;
    [SerializeField] private GameObject panelHowToPlay;

    [Header("UI Elements")]
    [SerializeField] private Slider remainHpSlider;       // 인게임 HP 슬라이더
    [SerializeField] private Text inGameScoreText;        // [신규] 체력 슬라이더 아래 표시될 실시간 점수 텍스트
    [SerializeField] private Text resultScoreText;        // 결과창 이번 판 점수 텍스트
    [SerializeField] private Text resultHighScoreText;    // 결과창 최고 기록 텍스트

    private float currentHP;
    public float playTime;
    private int totalClearedDrops;

    // 최고 기록 저장 키
    private int highScore;
    private const string HIGH_SCORE_KEY = "HighScore_ClearedDrops";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        Application.targetFrameRate = 60;

        boardManager = FindObjectOfType<BoardManager>();

        // 저장된 최고 기록 불러오기 (없으면 0)
        highScore = PlayerPrefs.GetInt(HIGH_SCORE_KEY, 0);

        ShowMainMenu();
    }

    private void Update()
    {
        if (CurrentState == GameState.Playing)
        {
            playTime += Time.deltaTime;

            float currentDrainRate = baseHpDrainRate + (playTime * drainAcceleration);
            currentHP -= currentDrainRate * Time.deltaTime;

            if (remainHpSlider != null)
            {
                if (currentHP < 0) currentHP = 0;

                remainHpSlider.value = currentHP / maxHP;
            }

            if (currentHP <= 0)
            {
                currentHP = 0;
                EndGame();
            }
        }
    }

    public void ShowMainMenu()
    {
        CurrentState = GameState.MainMenu;
        panelMainMenu.SetActive(true);
        panelInGame.SetActive(false);
        panelResult.SetActive(false);

        if (panelHowToPlay != null)
            panelHowToPlay.SetActive(false); // ⭐️ 메인 진입 시 팝업 닫기

        if (boardManager != null)
            boardManager.gameObject.SetActive(false);

        if (AudioManager.Instance != null) AudioManager.Instance.PlayMainMenuBGM();
    }

    // ⭐️ 추가: 게임 방법 팝업 열기
    public void OpenHowToPlay()
    {
        if (panelHowToPlay != null)
            panelHowToPlay.SetActive(true);
    }

    // ⭐️ 추가: 게임 방법 팝업 닫기
    public void CloseHowToPlay()
    {
        if (panelHowToPlay != null)
            panelHowToPlay.SetActive(false);
    }

    public void ExitGame()
    {
        Debug.Log("게임을 종료합니다.");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void StartGame()
    {
        CurrentState = GameState.Playing;

        currentHP = maxHP;
        playTime = 0f;
        totalClearedDrops = 0;

        if (remainHpSlider != null)
            remainHpSlider.value = 1f;

        UpdateInGameScoreUI();

        panelMainMenu.SetActive(false);
        panelInGame.SetActive(true);
        panelResult.SetActive(false);

        if (boardManager != null)
        {
            boardManager.gameObject.SetActive(true);
            boardManager.ClearAndResetBoard();
        }

        // ⭐️ 퀘스트 초기화 및 시작
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.ResetQuests();
            QuestManager.Instance.GenerateRandomQuest(); // 게임 시작 시 첫 퀘스트 1개 지급
        }

        if (AudioManager.Instance != null) AudioManager.Instance.PlayInGameBGM();
    }

    public void EndGame()
    {
        CurrentState = GameState.GameOver;

        bool isNewRecord = false;
        if (totalClearedDrops > highScore)
        {
            highScore = totalClearedDrops;
            PlayerPrefs.SetInt(HIGH_SCORE_KEY, highScore);
            PlayerPrefs.Save();
            isNewRecord = true;
        }

        panelMainMenu.SetActive(false);
        panelInGame.SetActive(false);
        panelResult.SetActive(true);

        if (resultScoreText != null)
            resultScoreText.text = $"지운 드롭 수 : {totalClearedDrops}개";

        if (resultHighScoreText != null)
        {
            if (isNewRecord)
            {
                resultHighScoreText.text = $"🏆 최고 기록 : {highScore}개 (NEW!)";
            }
            else
            {
                resultHighScoreText.text = $"🏆 최고 기록 : {highScore}개";
            }
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopBGM();
            AudioManager.Instance.PlayGameOverSFX();
        }
    }

    /// <summary>
    /// 특수 모양(T, L, Cross, 3x3, Line4, Line5)에 해당하는지 확인하는 함수
    /// </summary>
    private bool IsSpecialShape(MatchShape shape)
    {
        return shape == MatchShape.TShape ||
               shape == MatchShape.LShape ||
               shape == MatchShape.Cross ||
               shape == MatchShape.Square3x3 ||
               shape == MatchShape.Line4;
    }

    /// <summary>
    /// 일반 공격 드롭이 터졌을 때 호출 (특수 모양일 경우 점수 1.5배 적용)
    /// </summary>
    public void AddScore(MatchInfo matchInfo)
    {
        if (CurrentState != GameState.Playing) return;

        // 1. 특수 모양 여부에 따른 배율 적용
        float scoreMultiplier = IsSpecialShape(matchInfo.shape) ? 1.5f : 1.0f;
        int earnedScore = Mathf.RoundToInt(matchInfo.dropCount * scoreMultiplier);
        totalClearedDrops += earnedScore;

        UpdateInGameScoreUI();

        // 2. 일반 드롭 회복 (회복 드롭의 1/3)
        float normalHealAmount = (healPerDrop / 3f) * matchInfo.dropCount;
        currentHP += normalHealAmount;

        if (currentHP > maxHP) currentHP = maxHP;
        if (remainHpSlider != null) remainHpSlider.value = currentHP / maxHP;

        // ⭐️ 3. 퀘스트 진행도 체크 요청!
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.CheckQuestProgress(matchInfo);
        }
    }

    /// <summary>
    /// ⭐️ 퀘스트 성공 보너스 점수 추가 함수
    /// </summary>
    public void AddBonusScore(int bonusScore)
    {
        if (CurrentState != GameState.Playing) return;

        totalClearedDrops += bonusScore;
        UpdateInGameScoreUI();
    }

    /// <summary>
    /// 회복 드롭이 터졌을 때 호출
    /// </summary>
    public void HealByDrop(MatchInfo matchInfo)
    {
        if (CurrentState != GameState.Playing) return;

        currentHP += matchInfo.dropCount * healPerDrop;

        if (currentHP > maxHP)
        {
            currentHP = maxHP;
        }

        if (remainHpSlider != null)
        {
            remainHpSlider.value = currentHP / maxHP;
        }
    }

    /// <summary>
    /// 인게임 점수 UI 텍스트를 최신화하는 함수
    /// </summary>
    private void UpdateInGameScoreUI()
    {
        if (inGameScoreText != null)
        {
            inGameScoreText.text = $"SCORE: {totalClearedDrops}";
        }
    }
}