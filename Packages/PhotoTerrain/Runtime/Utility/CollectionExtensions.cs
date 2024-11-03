using System;
using System.Collections.Generic;

namespace Hollow.Extensions
{
internal static class CollectionExtensions
{
    public static bool IsNullOrEmpty<T>(this T[]            array) => array is null || array.Length == 0;
    public static bool IsNullOrEmpty<T>(this List<T>        list)  => list is null  || list.Count == 0;
    public static bool IsNullOrEmpty<T>(this IList<T>       list)  => list is null  || list.Count == 0;
    public static bool IsNullOrEmpty<T>(this ICollection<T> list)  => list is null  || list.Count == 0;

    public static bool IsNullOrEmpty(this string s)
    {
        return string.IsNullOrEmpty(s);
    }

    public static bool IsNullOrWhiteSpace(this string s)
    {
        return string.IsNullOrWhiteSpace(s);
    }

    public static bool NotNullOrEmpty<T>(this T[]            array) => !IsNullOrEmpty(array);
    public static bool NotNullOrEmpty<T>(this List<T>        list)  => !IsNullOrEmpty(list);
    public static bool NotNullOrEmpty<T>(this IList<T>       list)  => !IsNullOrEmpty(list);
    public static bool NotNullOrEmpty<T>(this ICollection<T> list)  => !IsNullOrEmpty(list);

    public static bool ContainsValue<T>(this ReadOnlySpan<T> span, in T value) where T : unmanaged, IEquatable<T>
    {
        for (int i = 0; i < span.Length; i++)
        {
            if (span[i].Equals(value))
                return true;
        }

        return false;
    }
}
}