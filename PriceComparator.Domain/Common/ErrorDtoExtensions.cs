// Decompiled with JetBrains decompiler
// Type: ROP.APIExtensions.ErrorDtoExtensions
// Assembly: ROP.ApiExtensions, Version=1.4.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 76A91E6D-B5D3-4850-915A-B722C0E55074
// Assembly location: /Users/gabrielolavarria/.nuget/packages/netmentor.rop.apiextensions/1.4.0/lib/net8.0/ROP.ApiExtensions.dll

#nullable disable
namespace PriceComparator.Domain.Common;

public static class ErrorDtoExtensions
{
    internal static ErrorDto ToErrorDto(this Error error)
    {
        return new ErrorDto()
        {
            ErrorCode = error.ErrorCode,
            Message = error.Message,
            TranslationVariables = error.TranslationVariables
        };
    }
}