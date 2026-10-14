// Decompiled with JetBrains decompiler
// Type: ROP.APIExtensions.ResultDto`1
// Assembly: ROP.ApiExtensions, Version=1.4.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 76A91E6D-B5D3-4850-915A-B722C0E55074
// Assembly location: /Users/gabrielolavarria/.nuget/packages/netmentor.rop.apiextensions/1.4.0/lib/net8.0/ROP.ApiExtensions.dll

using System.Collections.Immutable;

#nullable disable
namespace PriceComparator.Domain.Common;

public class ResultDto<T>
{
    public T Value { get; set; }

    public ImmutableArray<ErrorDto> Errors { get; set; }

    public bool Success => this.Errors.Length == 0;
}