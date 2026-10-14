// Decompiled with JetBrains decompiler
// Type: ROP.Error
// Assembly: ROP, Version=1.4.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C68082B8-B1F1-4EA2-9011-EA4A3BB1918C
// Assembly location: /Users/gabrielolavarria/.nuget/packages/netmentor.rop/1.4.0/lib/net8.0/ROP.dll

using System;
using System.Collections.Generic;

#nullable disable
namespace PriceComparator.Domain.Common;

public class Error
{
    public readonly string Message;
    public readonly Guid? ErrorCode;
    public readonly string[] TranslationVariables;

    private Error(string message, Guid? errorCode, string[] translationVariables)
    {
        this.Message = message;
        this.ErrorCode = errorCode;
        this.TranslationVariables = translationVariables;
    }

    public static Error Create(string message, Guid? errorCode = null, string[] translationVariables = null)
    {
        return new Error(message, errorCode, translationVariables);
    }

    public static Error Create(Guid errorCode, string[] translationVariables = null)
    {
        return Error.Create(string.Empty, new Guid?(errorCode), translationVariables);
    }

    public static IEnumerable<Error> Exception(System.Exception e)
    {
        if (e is ErrorResultException errorResultException)
            return (IEnumerable<Error>) errorResultException.Errors;
        return (IEnumerable<Error>) new Error[1]
        {
            Error.Create(e.ToString())
        };
    }
}