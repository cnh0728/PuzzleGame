using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class Quest
{
    public string questDescription;
    public MatchShape targetShape;
    public int requiredCount;
    public int currentCount;

    public float maxHp;
    public float currentHp;
    public int bonusScore;

    public Quest(MatchShape shape, int reqCount, float durationHp, int bonus)
    {
        targetShape = shape;
        requiredCount = reqCount;
        currentCount = 0;
        maxHp = durationHp;
        currentHp = durationHp;
        bonusScore = bonus;

        string shapeName = GetShapeKoreanName(shape);
        questDescription = $"[{shapeName}] {requiredCount}회 지우기";
    }

    private string GetShapeKoreanName(MatchShape shape)
    {
        switch (shape)
        {
            case MatchShape.Line4: return "4개 이상 일자";
            case MatchShape.TShape: return "T자 모양";
            case MatchShape.LShape: return "L자 모양";
            case MatchShape.Cross: return "십자 모양";
            case MatchShape.Square3x3: return "3x3 정사각형";
            default: return "특수 모양";
        }
    }
}

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    [Header("Quest Spawn Settings")]
    [SerializeField] private float questSpawnInterval = 15f;
    private float spawnTimer = 0f;

    [Header("Active Quests")]
    public List<Quest> activeQuests = new List<Quest>();

    [Header("UI Settings")]
    [SerializeField] private Transform questListParent;
    [SerializeField] private GameObject questItemPrefab;

    // ⭐️ 매 프레임 탐색 비용을 없애기 위해 UI 컴포넌트를 미리 저장해두는 캐시 클래스
    private class QuestUICache
    {
        public GameObject gameObject;
        public Text descText;
        public Text progressText;
        public Slider hpSlider;
    }

    private List<QuestUICache> activeUiCaches = new List<QuestUICache>();

    private List<Quest> possibleTemplates = new List<Quest>()
    {
        new Quest(MatchShape.Line4, 1, 8f, 5),
        new Quest(MatchShape.LShape, 1, 10f, 7),
        new Quest(MatchShape.TShape, 1, 10f, 7),
        new Quest(MatchShape.Cross, 1, 10f, 7),
        new Quest(MatchShape.Square3x3, 1, 15f, 10)
    };

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void ResetQuests()
    {
        activeQuests.Clear();
        spawnTimer = 0f;
        ClearAllQuestUI();
    }

    private void Update()
    {
        if (GameManager.Instance.CurrentState != GameState.Playing) return;

        spawnTimer += Time.deltaTime;
        if (spawnTimer >= questSpawnInterval)
        {
            spawnTimer = 0f;
            GenerateRandomQuest();
        }

        // ⭐️ 최적화: 매 프레임 리스트 정렬(OrderBy)을 제거하고 순수하게 숫자만 뺌
        for (int i = activeQuests.Count - 1; i >= 0; i--)
        {
            Quest q = activeQuests[i];
            q.currentHp -= Time.deltaTime;

            if (q.currentHp <= 0)
            {
                q.currentHp = 0;
                Debug.Log($"💀 퀘스트 [{q.questDescription}] 시간 초과! 게임 오버!");
                GameManager.Instance.EndGame();
                return;
            }

            // ⭐️ 최적화: 글자 할당, 컴포넌트 탐색 없이 캐싱된 슬라이더 value만 딱 조절
            if (activeUiCaches.Count == activeQuests.Count && activeUiCaches[i].hpSlider != null)
            {
                float targetValue = q.currentHp / q.maxHp;
                // 기존 슬라이더 값과 1% 이상 차이날 때만 UI를 갱신 (Canvas Rebuild 방지!)
                if (Mathf.Abs(activeUiCaches[i].hpSlider.value - targetValue) > 0.01f)
                {
                    activeUiCaches[i].hpSlider.value = targetValue;
                }
            }
        }
    }

    public Quest GenerateRandomQuest()
    {
        int randomIndex = Random.Range(0, possibleTemplates.Count);
        Quest template = possibleTemplates[randomIndex];
        int randomCount = Random.Range(1, 6);

        Quest newQuest = new Quest(
            template.targetShape, randomCount,
            template.maxHp * randomCount, template.bonusScore * randomCount
        );

        activeQuests.Add(newQuest);
        RebuildQuestUIObjects(); // UI 갱신 (여기서 정렬도 1회 수행됨)
        return newQuest;
    }

    public void CheckQuestProgress(MatchInfo matchInfo)
    {
        if (GameManager.Instance.CurrentState != GameState.Playing) return;

        bool questRemoved = false;

        for (int i = activeQuests.Count - 1; i >= 0; i--)
        {
            Quest q = activeQuests[i];
            if (q.targetShape == matchInfo.shape)
            {
                q.currentCount++;

                if (q.currentCount >= q.requiredCount)
                {
                    GameManager.Instance.AddBonusScore(q.bonusScore);
                    activeQuests.RemoveAt(i);
                    questRemoved = true;
                }
                else
                {
                    // ⭐️ 최적화: 목표 개수가 올랐을 때만 텍스트를 한 번 갱신함
                    if (activeUiCaches.Count == activeQuests.Count && activeUiCaches[i].progressText != null)
                    {
                        activeUiCaches[i].progressText.text = $"{q.currentCount}/{q.requiredCount}";
                    }
                }
            }
        }

        if (questRemoved) RebuildQuestUIObjects();
    }

    private void RebuildQuestUIObjects()
    {
        if (questListParent == null || questItemPrefab == null) return;

        ClearAllQuestUI();

        // ⭐️ 최적화: UI를 새로 고칠 때(퀘스트가 추가/삭제 될 때)만 정렬 1회 수행
        activeQuests = activeQuests.OrderBy(q => q.currentHp).ToList();

        for (int i = 0; i < activeQuests.Count; i++)
        {
            Quest q = activeQuests[i];
            GameObject newItem = Instantiate(questItemPrefab, questListParent);

            // 캐시에 담아서 매 프레임 Find 하는 것을 방지
            QuestUICache cache = new QuestUICache();
            cache.gameObject = newItem;
            cache.descText = newItem.transform.Find("Text_Description")?.GetComponent<Text>();
            cache.progressText = newItem.transform.Find("Text_Progress")?.GetComponent<Text>();
            cache.hpSlider = newItem.GetComponentInChildren<Slider>();

            // 글자 설정도 생성할 때 딱 1번만 수행
            if (cache.descText != null) cache.descText.text = q.questDescription;
            if (cache.progressText != null) cache.progressText.text = $"{q.currentCount}/{q.requiredCount}";
            if (cache.hpSlider != null)
            {
                cache.hpSlider.minValue = 0f;
                cache.hpSlider.maxValue = 1f;
                cache.hpSlider.value = q.currentHp / q.maxHp;
            }

            activeUiCaches.Add(cache);
        }
    }

    private void ClearAllQuestUI()
    {
        foreach (var cache in activeUiCaches)
        {
            if (cache.gameObject != null) Destroy(cache.gameObject);
        }
        activeUiCaches.Clear();
    }
}