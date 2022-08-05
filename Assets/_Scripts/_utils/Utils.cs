using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class Utils
{
    public static StringBuilder ArrayToString(uint[] array, uint maxElements = 4096)
    {
        StringBuilder builder = new StringBuilder("");
        for (var i = 0; i < array.Length; i++)
        {
            if (i >= maxElements) break;
            builder.Append(array[i] + " ");
        }

        return builder;
    }
    
    
    public static StringBuilder ArrayToString<T>(T[] array, uint maxElements = 4096)
    {
        StringBuilder builder = new StringBuilder("");
        for (var i = 0; i < array.Length; i++)
        {
            if (i >= maxElements) break;
            builder.Append(array[i] + " ");
        }

        return builder;
    }
}
