// Decompiled with JetBrains decompiler
// Type: ROP.APIExtensions.ErrorDto
// Assembly: ROP.ApiExtensions, Version=1.4.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 76A91E6D-B5D3-4850-915A-B722C0E55074
// Assembly location: /Users/gabrielolavarria/.nuget/packages/netmentor.rop.apiextensions/1.4.0/lib/net8.0/ROP.ApiExtensions.dll

using System;

#nullable disable
namespace PriceComparator.Domain.Common;

public class ErrorDto
{
    public string Message { get; set; }

    public Guid? ErrorCode { get; set; }

    public string[] TranslationVariables { get; set; }
}