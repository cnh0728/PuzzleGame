using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum DropType
{
    Fire,    // 불
    Water,   // 물
    Grass,   // 나무
    Light,   // 빛
    Dark,    // 어둠
    Heal    // 회복
}

// 드롭이 터진 형태(모양) 구분
public enum MatchShape
{
    Line4,      // 4개 일자 (2체)
    Cross,      // 십자 모양
    TShape,     // T자 모양
    LShape,     // L자 모양
    Square3x3,  // 3x3 정사각형 모양
    Other       // 기타 묶음 (네모 등)
}

// 매치 결과를 전달할 구조체
public struct MatchInfo
{
    public DropType dropType;  // 터진 드롭 속성 (Fire, Heal 등)
    public int dropCount;      // 터진 드롭 개수
    public MatchShape shape;   // 터진 드롭 모양

    public MatchInfo(DropType dropType, int dropCount, MatchShape shape)
    {
        this.dropType = dropType;
        this.dropCount = dropCount;
        this.shape = shape;
    }
}

public struct GridObject
{
    public GameObject dropObject;
    public DropType dropType;
}

public class BoardManager : MonoBehaviour
{
    [Header("Grid Object Settings")]
    [SerializeField] public int width = 6;         // 가로 드롭 개수
    [SerializeField] public int height = 5;        // 세로 드롭 개수
    [SerializeField] public float cellSize = 0.7f; // 드롭 간격

    [Header("Prefabs & Sprites")]
    [SerializeField] private GameObject dropPrefab; // 드롭으로 사용할 프리팹
    [SerializeField] private Sprite[] colorSprites; // DropType 순서대로 할당할 스프라이트 배열

    // ========================================================================
    // 1. 미리 선언해두는 회전 패턴 데이터 (GC 발생 X, 코드 깔끔)
    // ========================================================================

    // T자(TShape) 패턴: 교차하는 중심점(center)을 기준으로 나머지 4개 드롭의 상대 위치
    private static readonly Vector2Int[][] TShapePatterns = new Vector2Int[][]
    {
        // ㅗ 모양 (가로 3개 + 위로 2개)
        new Vector2Int[] { new Vector2Int(-1, 0), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(0, 2) },
        
        // ㅜ 모양 (가로 3개 + 아래로 2개)
        new Vector2Int[] { new Vector2Int(-1, 0), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(0, -2) },
        
        // ㅏ 모양 (세로 3개 + 오른쪽으로 2개)
        new Vector2Int[] { new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(2, 0) },
        
        // ㅓ 모양 (세로 3개 + 왼쪽으로 2개)
        new Vector2Int[] { new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(-1, 0), new Vector2Int(-2, 0) }
    };

    // L자(LShape) 패턴: 모서리 교차점 기준 (한쪽 3개 + 다른쪽 3개 = 총 5개 드롭)
    private static readonly Vector2Int[][] LShapePatterns = new Vector2Int[][]
    {
        // └ 모양
        new Vector2Int[] { new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(1, 0), new Vector2Int(2, 0) },
        // ┘ 모양
        new Vector2Int[] { new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(-1, 0), new Vector2Int(-2, 0) },
        // ┌ 모양
        new Vector2Int[] { new Vector2Int(0, -1), new Vector2Int(0, -2), new Vector2Int(1, 0), new Vector2Int(2, 0) },
        // ┐ 모양
        new Vector2Int[] { new Vector2Int(0, -1), new Vector2Int(0, -2), new Vector2Int(-1, 0), new Vector2Int(-2, 0) }
    };

    // 십자(Cross) 패턴 (중심점 기준 상하좌우 1칸씩 = 총 5개 드롭)
    private static readonly Vector2Int[] CrossPattern = new Vector2Int[]
    {
        new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(-1, 0), new Vector2Int(1, 0)
    };

    private GridObject[,] grid;

    // DropController 등 외부에서 접근하기 위한 프로퍼티(PascalCase 사용)
    public int Width => width;
    public int Height => height;
    public float CellSize => cellSize;

    // 매치(콤보)가 진행 중인지 확인하는 플래그 (이 동안에는 드롭을 조작할 수 없게 함)
    public bool isMatching { get; private set; } = false;

    void Start()
    {
        //InitializeBoard();
    }

    /// <summary>
    /// 그리드 보드를 초기화하고 드롭을 생성합니다.
    /// </summary>
    public void InitializeBoard()
    {
        grid = new GridObject[width, height];

        Vector3 startPos = transform.position - new Vector3((width - 1) * cellSize / 2f, (height - 1) * cellSize / 2f, 0);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                DropType randomColor = GetValidRandomColor(x, y);

                grid[x, y].dropType = randomColor;

                Vector3 spawnPos = startPos + new Vector3(x * cellSize, y * cellSize, 0);
                GameObject dropObj = PoolManager.Instance.GetDrop(spawnPos);

                dropObj.name = $"Drop_{x}_{y}";
                SetDropColor(dropObj, randomColor);

                if (!dropObj.TryGetComponent(out DropController controller))
                {
                    controller = dropObj.AddComponent<DropController>();
                }
                controller.Initialize(this, x, y);

                grid[x, y].dropObject = dropObj;
            }
        }
    }

    /// <summary>
    /// (x, y) 위치에 3-Match가 발생하지 않는 랜덤한 색상을 반환합니다.
    /// </summary>
    private DropType GetValidRandomColor(int x, int y)
    {
        List<DropType> possibleColors = new List<DropType>((DropType[])Enum.GetValues(typeof(DropType)));

        // 가로방향으로 왼쪽 두 개 체크해서 같으면 제외
        if (x >= 2)
        {
            if (grid[x - 1, y].dropType == grid[x - 2, y].dropType)
            {
                possibleColors.Remove(grid[x - 1, y].dropType);
            }
        }

        // 마찬가지로 세로방향 제외
        if (y >= 2)
        {
            if (grid[x, y - 1].dropType == grid[x, y - 2].dropType)
            {
                possibleColors.Remove(grid[x, y - 1].dropType);
            }
        }

        int randomIndex = UnityEngine.Random.Range(0, possibleColors.Count);
        return possibleColors[randomIndex];
    }

    /// <summary>
    /// 드롭 오브젝트의 SpriteRenderer 색상/스프라이트를 적용합니다.
    /// </summary>
    private void SetDropColor(GameObject dropObj, DropType color)
    {
        SpriteRenderer sr = dropObj.GetComponent<SpriteRenderer>();
        if (sr != null && colorSprites != null && (int)color < colorSprites.Length)
        {
            sr.sprite = colorSprites[(int)color];
        }
    }

    // 보드의 좌측 하단 시작점(0,0) 좌표를 반환하는 함수
    public Vector3 GetStartPos()
    {
        return transform.position - new Vector3((width - 1) * cellSize / 2f, (height - 1) * cellSize / 2f, 0);
    }

    /// <summary>
    /// 드래그 중인 드롭(x1, y1)과 타겟 위치의 드롭(x2, y2) 데이터를 교환합니다.
    /// </summary>
    public void SwapDrops(int x1, int y1, int x2, int y2)
    {
        // 1. 배열 범위를 벗어나거나 제자리인 경우 무시
        if (x1 < 0 || x1 >= width || y1 < 0 || y1 >= height || x2 < 0 || x2 >= width || y2 < 0 || y2 >= height) return;
        if (x1 == x2 && y1 == y2) return;

        // 2. 오브젝트 및 색상 데이터 구조체 통째로 교환
        GridObject dragDrop = grid[x1, y1];
        GridObject targetDrop = grid[x2, y2];

        grid[x1, y1] = targetDrop;
        grid[x2, y2] = dragDrop;

        // 3. 밀려난 기존 드롭(targetDrop)의 이동 처리 (여기 수정됨!)
        if (targetDrop.dropObject != null)
        {
            DropController targetController = targetDrop.dropObject.GetComponent<DropController>();
            if (targetController != null)
            {
                targetController.x = x1;
                targetController.y = y1;

                // 순간이동 대신 부드러운 이동 함수 호출
                Vector3 newPos = GetStartPos() + new Vector3(x1 * cellSize, y1 * cellSize, 0);
                targetController.MoveToPosition(newPos);
            }
        }

        // 4. 잡고 있는 드롭(dragDrop)의 내부 좌표값 업데이트
        if (dragDrop.dropObject != null)
        {
            DropController dragController = dragDrop.dropObject.GetComponent<DropController>();
            if (dragController != null)
            {
                dragController.x = x2;
                dragController.y = y2;
            }
        }

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySwapSFX();
    }

    /// <summary>
    /// DropController에서 손을 뗄 때 호출하여 매치 프로세스를 시작합니다.
    /// </summary>
    public void StartMatchProcess()
    {
        StartCoroutine(ProcessMatchesRoutine());
    }

    private IEnumerator ProcessMatchesRoutine()
    {
        isMatching = true;
        bool hasMatch = true;
        int totalComboCount = 0;

        while (hasMatch)
        {
            List<List<Vector2Int>> matchGroups = FindMatchGroups();

            if (matchGroups.Count > 0)
            {
                foreach (List<Vector2Int> group in matchGroups)
                {
                    totalComboCount++;

                    Vector2Int firstPos = group[0];
                    DropType groupType = grid[firstPos.x, firstPos.y].dropType;

                    // 1. 모양 분석 수행
                    MatchShape shape = AnalyzeShape(group);
                    MatchInfo matchInfo = new MatchInfo(groupType, group.Count, shape);

                    // 해당 덩어리 파괴 연출
                    foreach (Vector2Int pos in group)
                    {
                        if (grid[pos.x, pos.y].dropObject != null)
                        {
                            Vector3 dropWorldPos = grid[pos.x, pos.y].dropObject.transform.position;

                            // ⭐️ Shape를 그대로 넘겨주어 모양별 이펙트/색상으로 터지게 함
                            if (EffectManager.Instance != null)
                            {
                                EffectManager.Instance.PlayShapeEffect(dropWorldPos, shape);
                            }

                            PoolManager.Instance.ReturnDrop(grid[pos.x, pos.y].dropObject);
                            grid[pos.x, pos.y].dropObject = null;

                            if (AudioManager.Instance != null)
                                AudioManager.Instance.PlayClearSFX();
                        }
                    }

                    // GameManager 및 QuestManager 연동
                    if (groupType == DropType.Heal)
                    {
                        GameManager.Instance.HealByDrop(matchInfo);
                    }
                    else
                    {
                        GameManager.Instance.AddScore(matchInfo);
                    }

                    yield return new WaitForSeconds(0.25f);
                }

                MakeDropsFall();
                yield return new WaitForSeconds(0.3f);

                RefillEmptySpaces();
                yield return new WaitForSeconds(0.3f);
            }
            else
            {
                hasMatch = false;
            }
        }

        isMatching = false;
    }

    /// <summary>
    /// 매치된 모든 드롭을 찾고, 인접한 같은 색상끼리 덩어리(그룹)로 묶어서 반환합니다.
    /// </summary>
    private List<List<Vector2Int>> FindMatchGroups()
    {
        HashSet<Vector2Int> allMatched = new HashSet<Vector2Int>();

        // 1단계: 기존처럼 가로/세로 매치된 '모든 좌표'를 찾아 allMatched에 넣습니다.
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width - 2; x++)
            {
                if (grid[x, y].dropObject == null) continue;
                DropType currentType = grid[x, y].dropType;
                if (grid[x + 1, y].dropObject != null && grid[x + 1, y].dropType == currentType &&
                    grid[x + 2, y].dropObject != null && grid[x + 2, y].dropType == currentType)
                {
                    allMatched.Add(new Vector2Int(x, y));
                    allMatched.Add(new Vector2Int(x + 1, y));
                    allMatched.Add(new Vector2Int(x + 2, y));
                }
            }
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height - 2; y++)
            {
                if (grid[x, y].dropObject == null) continue;
                DropType currentType = grid[x, y].dropType;
                if (grid[x, y + 1].dropObject != null && grid[x, y + 1].dropType == currentType &&
                    grid[x, y + 2].dropObject != null && grid[x, y + 2].dropType == currentType)
                {
                    allMatched.Add(new Vector2Int(x, y));
                    allMatched.Add(new Vector2Int(x, y + 1));
                    allMatched.Add(new Vector2Int(x, y + 2));
                }
            }
        }

        // 2단계: BFS(너비 우선 탐색)로 인접한 같은 색상 좌표들을 하나의 덩어리로 묶어줍니다.
        List<List<Vector2Int>> matchGroups = new List<List<Vector2Int>>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        foreach (Vector2Int pos in allMatched)
        {
            if (!visited.Contains(pos))
            {
                List<Vector2Int> currentGroup = new List<Vector2Int>();
                Queue<Vector2Int> queue = new Queue<Vector2Int>();

                queue.Enqueue(pos);
                visited.Add(pos);

                DropType groupType = grid[pos.x, pos.y].dropType;

                while (queue.Count > 0)
                {
                    Vector2Int curr = queue.Dequeue();
                    currentGroup.Add(curr);

                    foreach (Vector2Int dir in directions)
                    {
                        Vector2Int neighbor = curr + dir;

                        // 인접한 좌표가 전체 매치 목록에 있고, 아직 방문하지 않았고, 색상도 같다면 한 덩어리로 묶음
                        if (allMatched.Contains(neighbor) && !visited.Contains(neighbor) &&
                            grid[neighbor.x, neighbor.y].dropType == groupType)
                        {
                            visited.Add(neighbor);
                            queue.Enqueue(neighbor);
                        }
                    }
                }
                matchGroups.Add(currentGroup); // 완성된 콤보 덩어리를 리스트에 추가
            }
        }

        return matchGroups;
    }

    private void MakeDropsFall()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // 빈칸을 발견하면
                if (grid[x, y].dropObject == null)
                {
                    // 그 위로 탐색하여 가장 처음 발견된 드롭을 아래로 내림
                    for (int k = y + 1; k < height; k++)
                    {
                        if (grid[x, k].dropObject != null)
                        {
                            // 데이터 교환 (위의 드롭 -> 아래 빈칸)
                            grid[x, y] = grid[x, k];

                            // 원래 있던 위쪽 자리는 비움
                            grid[x, k].dropObject = null;

                            // 시각적 이동 및 내부 좌표 업데이트
                            DropController controller = grid[x, y].dropObject.GetComponent<DropController>();
                            if (controller != null)
                            {
                                controller.x = x;
                                controller.y = y;
                                Vector3 targetPos = GetStartPos() + new Vector3(x * cellSize, y * cellSize, 0);
                                controller.MoveToPosition(targetPos);
                            }
                            break; // 하나 내렸으면 다음 y축 검사로 넘어감
                        }
                    }
                }
            }
        }
    }

    private void RefillEmptySpaces()
    {
        Vector3 startPos = GetStartPos();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y].dropObject == null)
                {
                    int randomIndex = UnityEngine.Random.Range(0, Enum.GetValues(typeof(DropType)).Length);
                    DropType randomColor = (DropType)randomIndex;
                    grid[x, y].dropType = randomColor;

                    Vector3 spawnPos = startPos + new Vector3(x * cellSize, (height + 2) * cellSize, 0);
                    Vector3 targetPos = startPos + new Vector3(x * cellSize, y * cellSize, 0);

                    GameObject newDrop = PoolManager.Instance.GetDrop(spawnPos);

                    newDrop.name = $"Drop_{x}_{y}";
                    SetDropColor(newDrop, randomColor);

                    // 변경됨: 기존처럼 안전하게 컨트롤러 가져오기/초기화
                    if (!newDrop.TryGetComponent(out DropController controller))
                    {
                        controller = newDrop.AddComponent<DropController>();
                    }
                    controller.Initialize(this, x, y);

                    controller.MoveToPosition(targetPos);

                    grid[x, y].dropObject = newDrop;
                }
            }
        }
    }

    /// <summary>
    /// 게임 재시작 시 기존에 남은 드롭들을 모두 풀에 반납하고 새 판을 짭니다.
    /// </summary>
    public void ClearAndResetBoard()
    {
        if (grid != null)
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (grid[x, y].dropObject != null)
                    {
                        PoolManager.Instance.ReturnDrop(grid[x, y].dropObject);
                        grid[x, y].dropObject = null;
                    }
                }
            }
        }
        InitializeBoard(); // 새 판 생성
    }

    // ========================================================================
    // 2. 데이터 기반 패턴 매칭 함수
    // ========================================================================
    private MatchShape AnalyzeShape(List<Vector2Int> group)
    {
        int count = group.Count;
        HashSet<Vector2Int> set = new HashSet<Vector2Int>(group);

        // 1. 3x3 정사각형 (최소 9개 이상)
        if (count == 9)
        {
            foreach (var pos in group)
            {
                if (Check3x3Square(set, pos)) return MatchShape.Square3x3;
            }
        }

        // 2. 총 드롭 개수가 5개 이상일 때만 T자, L자, 십자(Cross) 검사
        if (count == 5)
        {
            foreach (var center in group)
            {
                // 십자(Cross) 검사 (중심 1 + 상하좌우 4 = 5개)
                if (MatchPattern(set, center, CrossPattern))
                    return MatchShape.Cross;

                // T자 검사 (중심 1 + 줄기 4 = 5개)
                foreach (var pattern in TShapePatterns)
                {
                    if (MatchPattern(set, center, pattern))
                        return MatchShape.TShape;
                }

                // L자 검사 (모서리 1 + 양축 4 = 5개)
                foreach (var pattern in LShapePatterns)
                {
                    if (MatchPattern(set, center, pattern))
                        return MatchShape.LShape;
                }
            }
        }

        // 3. 직선(Line) 형태 판정
        if (count == 4) return MatchShape.Line4;

        return MatchShape.Other;
    }

    private bool MatchPattern(HashSet<Vector2Int> set, Vector2Int center, Vector2Int[] offsets)
    {
        foreach (var offset in offsets)
        {
            if (!set.Contains(center + offset)) return false;
        }
        return true;
    }

    private bool Check3x3Square(HashSet<Vector2Int> set, Vector2Int startPos)
    {
        for (int x = 0; x < 3; x++)
        {
            for (int y = 0; y < 3; y++)
            {
                if (!set.Contains(new Vector2Int(startPos.x + x, startPos.y + y)))
                    return false;
            }
        }
        return true;
    }
}