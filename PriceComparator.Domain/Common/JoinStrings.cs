// Decompiled with JetBrains decompiler
// Type: ROP.IEnumerableUtils
// Assembly: ROP, Version=1.4.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C68082B8-B1F1-4EA2-9011-EA4A3BB1918C
// Assembly location: /Users/gabrielolavarria/.nuget/packages/netmentor.rop/1.4.0/lib/net8.0/ROP.dll

using System.Collections.Generic;

#nullable disable
namespace PriceComparator.Domain.Common;

public static class IEnumerableUtils
{
    public static string JoinStrings(this IEnumerable<string> strings, string separator)
    {
        return string.Join(separator, strings);
    }
}