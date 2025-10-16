using UnityEngine;

public class Sponer_Wagashi : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [Header("スポーンさせるオブジェクトたち")]
    [SerializeField] private GameObject[] wagashi;
    
    [Header("それぞれの確率、合計で100を超えないように整数で入力" +
        "確率が低いものから順に書くこと！例)10,30,60")]
    [SerializeField] private int[] param;

    void Start()
    {
        if (wagashi.Length!=param.Length)
        {
            Debug.LogError("WagashiとParamの数が合ってないよ");
        }

        int work = 0;
        foreach (int f in param)
        {
            work += f;
        }

        if(work!=100)
        {
            Debug.LogError("Paramが合計で1にならないよ");
        }

        int sort = 0;
        bool correct = true;
        foreach (int f in param)
        {
            if(sort>f)
            {
                correct = false;
                break;
            }
            sort = f;
        }

        if(!correct)
        {
            Debug.LogError("Paramの順番が間違ってるよ。一番低い確率を上から順に書いてね");
        }
    }

    // Update is called once per frame
    void Update()
    {
       
    }

    public GameObject Spawn()
    {
        int val=Random.Range(0, 100);

        int num = 0;
        int sum = 0;
        GameObject obj = null;
        foreach (int i in param)
        { 
            sum += i;

            if (val < sum)
            {
                obj=Instantiate(wagashi[num], this.transform.position, Quaternion.identity);
                break;
            }
            num++;
        }

        return obj;
    }
}
