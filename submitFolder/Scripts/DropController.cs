using UnityEngine;
using UnityEngine.UI; // UI 컴포넌트(Image, Text) 사용을 위해 필수
using System.Collections;

public class DropController : MonoBehaviour
{
    private BoardManager boardManager;
    public int x, y;

    private bool isDragging = false;
    private float dragTimer = 0f;
    private float MAX_DRAG_TIME = 5.0f;

    private GameObject uiRootObject; // 프로그레스 바와 텍스트를 포함하는 부모 오브젝트 (평소에 끄기 위해)
    private Slider timeSlider; // Image 대신 Slider 컴포넌트 사용
    private Text timeText;          // 초 단위를 표시할 텍스트

    private Coroutine moveCoroutine;

    public void Initialize(BoardManager manager, int gridX, int gridY)
    {
        isDragging = false;

        boardManager = manager;
        x = gridX;
        y = gridY;

        Transform canvas = transform.Find("TimerCanvas");
        if (canvas != null)
        {
            uiRootObject = canvas.gameObject; // 캔버스 전체를 껐다 켜기 위해 할당

            // 1. 슬라이더 (Slider) 찾기
            // 하이어라키에 만드신 슬라이더 이름에 맞춰 "TimeSlider" 부분을 수정하세요.
            Transform sliderObj = canvas.Find("Progress");
            if (sliderObj != null)
            {
                timeSlider = sliderObj.GetComponent<Slider>();

                // 슬라이더의 최대값을 코드로 강제 고정해두면 계산하기 편합니다.
                timeSlider.maxValue = MAX_DRAG_TIME;
                timeSlider.value = MAX_DRAG_TIME;
            }

            // 2. 시간 텍스트 (Text) 찾기
            Transform textObj = canvas.Find("TimeText");
            if (textObj != null)
            {
                timeText = textObj.GetComponent<Text>();
            }
        }
        // ---------------------------------------------

        // 시작할 때는 타이머 UI를 숨김
        if (uiRootObject != null) uiRootObject.SetActive(false);
    }

    private void OnMouseDown()
    {
        if (GameManager.Instance.CurrentState != GameState.Playing) return;
        
        // 콤보가 터지고 있는 중에는 터치 무시!
        if (boardManager.isMatching) return;

        if (!isDragging)
        {
            isDragging = true;
            dragTimer = 0f;
            GetComponent<SpriteRenderer>().sortingOrder = 10;

            // 드래그 시작 시 UI 켜기 및 초기화
            if (uiRootObject != null)
            {
                uiRootObject.SetActive(true);
                // World Space UI가 다른 드롭에 가리지 않게 Z축을 살짝 앞으로 당김
                uiRootObject.transform.localPosition = new Vector3(0, 0, -0.1f);
                UpdateTimerUI(0f); // 5.0s로 초기화
            }
        }
    }

    private void Update()
    {
        if (isDragging)
        {
            dragTimer += Time.deltaTime;

            // 매 프레임 UI 업데이트
            UpdateTimerUI(dragTimer);

            if (dragTimer >= MAX_DRAG_TIME)
            {
                StopDragging();
                return;
            }

            Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mousePosition.z = 0;
            transform.position = mousePosition;

            Vector3 startPos = boardManager.GetStartPos();
            float cellSize = boardManager.CellSize;

            int targetX = Mathf.RoundToInt((transform.position.x - startPos.x) / cellSize);
            int targetY = Mathf.RoundToInt((transform.position.y - startPos.y) / cellSize);

            if (targetX >= 0 && targetX < boardManager.Width && targetY >= 0 && targetY < boardManager.Height)
            {
                if (targetX != x || targetY != y)
                {
                    boardManager.SwapDrops(x, y, targetX, targetY);
                }
            }
        }
    }

    /// <summary>
    /// 현재 경과 시간에 따라 슬라이더와 텍스트를 업데이트합니다.
    /// </summary>
    private void UpdateTimerUI(float elapsed)
    {
        if (timeSlider == null || timeText == null) return;

        // 남은 시간 계산
        float remaining = MAX_DRAG_TIME - elapsed;
        if (remaining < 0) remaining = 0;

        // 1. 슬라이더 업데이트: 남은 시간을 그대로 value에 넣음 (maxValue를 5로 맞춰뒀으므로)
        timeSlider.value = remaining;

        // 2. 텍스트 업데이트: "0.1s" 포맷
        timeText.text = remaining.ToString("F1") + "s";
    }

    private void OnMouseUp()
    {
        if (isDragging)
        {
            StopDragging();
        }
    }

    private void StopDragging()
    {
        isDragging = false;
        GetComponent<SpriteRenderer>().sortingOrder = 0;

        if (uiRootObject != null) uiRootObject.SetActive(false);

        Vector3 finalPos = boardManager.GetStartPos() + new Vector3(x * boardManager.CellSize, y * boardManager.CellSize, 0);
        MoveToPosition(finalPos);

        // 드롭 확정 후 매치 검사 시작!
        boardManager.StartMatchProcess();
    }

    public void MoveToPosition(Vector3 targetPos)
    {
        if (moveCoroutine != null) StopCoroutine(moveCoroutine);
        moveCoroutine = StartCoroutine(SmoothMoveRoutine(targetPos));
    }

    private IEnumerator SmoothMoveRoutine(Vector3 targetPos)
    {
        float duration = 0.15f;
        float elapsed = 0f;
        Vector3 startPos = transform.position;

        while (elapsed < duration)
        {
            if (isDragging) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = 1f - Mathf.Pow(1f - t, 3f);
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }
        transform.position = targetPos;
    }
}