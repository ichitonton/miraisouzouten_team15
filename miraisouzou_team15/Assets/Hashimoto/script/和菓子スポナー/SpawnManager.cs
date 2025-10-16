using UnityEngine;
using System.Collections.Generic;

public class SpawnManager : MonoBehaviour
{
    public int maxSweets = 18;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [SerializeField] List<GameObject> objList;
    [SerializeField] Sponer_Wagashi[] spawners;
    void Start()
    {
        GameObject[] objs = GameObject.FindGameObjectsWithTag("Sweets");
        objList = new List<GameObject>(objs);
    }

    // Update is called once per frame
    void Update()
    {
        if(objList.Count<maxSweets)
        {
            //どのスポーナからだすか
            int val=Random.Range(0, spawners.Length);

           
            GameObject work=spawners[val].Spawn();
            objList.Add(work);
        }
    }

    public void DestroySweets(GameObject sweets)
    {//リストから参照消し

        if (objList.Contains(sweets))
        {
            objList.Remove(sweets);
        }
    }
}
