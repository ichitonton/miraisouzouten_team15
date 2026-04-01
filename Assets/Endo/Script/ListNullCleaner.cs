using System.Collections.Generic;
using System;

public static class ListNullCleaner
{
    /// <summary>
    /// List内のnull要素を除去する（UnityEngine.Object の Destroy済みも除去対象）
    /// 戻り値: 除去した個数
    /// </summary>
    public static int RemoveNulls<T>(List<T> list)
    {
        if (list == null) throw new ArgumentNullException(nameof(list));

        int before = list.Count;

        // UnityEngine.Object は Destroy されると "== null" が true になる
        if (typeof(UnityEngine.Object).IsAssignableFrom(typeof(T)))
        {
            list.RemoveAll(item => (item as UnityEngine.Object) == null);
        }
        else
        {
            list.RemoveAll(item => item == null);
        }

        return before - list.Count;
    }

    /// <summary>
    /// nullを除外した新しいListを返す（元のListは変更しない）
    /// </summary>
    public static List<T> WithoutNulls<T>(IEnumerable<T> source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        var result = new List<T>();

        if (typeof(UnityEngine.Object).IsAssignableFrom(typeof(T)))
        {
            foreach (var item in source)
            {
                if ((item as UnityEngine.Object) != null) result.Add(item);
            }
        }
        else
        {
            foreach (var item in source)
            {
                if (item != null) result.Add(item);
            }
        }

        return result;
    }
}