using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class ResultWagashiSpawner : MonoBehaviour
{
    [Header("生成したいPrefabのリスト")]
    [SerializeField] private List<GameObject> spawnPrefabs = new List<GameObject>();

    [Header("生成場所のリスト")]
    [SerializeField] private List<Transform> spawnPoints = new List<Transform>();

    [Header("生成設定")]
    [Tooltip("何秒ごとに生成するか")]
    [SerializeField] private float spawnInterval = 1.0f;

    [Header("サイズ設定")]
    [SerializeField] private float minScale = 1.0f;
    [SerializeField] private float maxScale = 1.0f;

    [Header("タイマー設定")]
    [Tooltip("StartSpawningが呼ばれてから、さらに待つ時間（秒）")]
    [SerializeField] private float startDelay = 0.0f;

    [Tooltip("生成を続ける時間（秒）。0にするとずっと止まりません")]
    [SerializeField] private float activeDuration = 10.0f;

    [Header("全て出し切った時に起こすアクション")]
    public UnityEvent OnComplete; // ここにUIを動かす命令を登録します

    // 重複して実行されないようにするためのフラグ
    private bool isRunning = false;

    // ▼▼ ここが変更点：自動でスタートしない ▼▼
    private void Start()
    {
        StartSpawning();
    }

    // ▼▼ 外部から呼ぶための関数（public） ▼▼
    public void StartSpawning()
    {
        // すでに動いていたら何もしない（二重起動防止）
        if (isRunning) return;

        StartCoroutine(SpawnLoop());
    }

    // 手動で止めたい時に呼ぶ関数
    public void StopSpawning()
    {
        StopAllCoroutines();
        isRunning = false;
        OnComplete.Invoke();
    }

    private IEnumerator SpawnLoop()
    {
        isRunning = true;

        // 1. 遅延処理
        if (startDelay > 0)
        {
            yield return new WaitForSeconds(startDelay);
        }

        float timer = 0f;

        while (true)
        {
            // 時間制限チェック
            if (activeDuration > 0 && timer >= activeDuration)
            {
                Debug.Log("指定時間が経過したため終了します。");
                isRunning = false;
                break;
            }

            // 生成処理
            if (spawnPrefabs.Count > 0 && spawnPoints.Count > 0)
            {
                SpawnObject();
            }

            yield return new WaitForSeconds(spawnInterval);
            timer += spawnInterval;
        }

        OnComplete.Invoke();
        yield break;
    }

    private void SpawnObject()
    {
        int prefabIndex = Random.Range(0, spawnPrefabs.Count);
        int pointIndex = Random.Range(0, spawnPoints.Count);

        GameObject selectedPrefab = spawnPrefabs[prefabIndex];
        Transform selectedPoint = spawnPoints[pointIndex];

        GameObject obj = Instantiate(selectedPrefab, selectedPoint.position, selectedPoint.rotation);

        float randomScale = Random.Range(minScale, maxScale);
        obj.transform.localScale = Vector3.one * randomScale;
    }
}