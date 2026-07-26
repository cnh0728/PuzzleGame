using System.Collections.Generic;
using UnityEngine;

public class PoolManager : MonoBehaviour
{
    // 싱글톤 인스턴스
    public static PoolManager Instance { get; private set; }

    [Header("Pool Settings")]
    [SerializeField] private GameObject dropPrefab;
    [SerializeField] private int initialPoolSize = 40; // 6x5 보드(30개)보다 조금 여유 있게 생성

    // 비활성화된 드롭들을 담아둘 큐(Queue)
    private Queue<GameObject> dropPool = new Queue<GameObject>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        //InitializePool();
    }

    // 시작할 때 미리 오브젝트들을 생성해서 풀에 넣어둡니다.
    private void InitializePool()
    {
        for (int i = 0; i < initialPoolSize; i++)
        {
            GameObject drop = Instantiate(dropPrefab, transform);
            drop.SetActive(false); // 비활성화 상태로 대기
            dropPool.Enqueue(drop);
        }
    }

    // 풀에서 드롭을 꺼내오는 함수
    public GameObject GetDrop(Vector3 position)
    {
        GameObject drop;

        if (dropPool.Count > 0)
        {
            drop = dropPool.Dequeue();
        }
        else
        {
            // 만약 풀이 비어있다면 추가로 생성합니다.
            drop = Instantiate(dropPrefab, transform);
        }

        drop.transform.position = position;
        drop.SetActive(true); // 다시 활성화
        return drop;
    }

    // 사용이 끝난 드롭을 풀에 반납하는 함수
    public void ReturnDrop(GameObject drop)
    {
        drop.SetActive(false); // 화면에서 숨김
        dropPool.Enqueue(drop);
    }
}