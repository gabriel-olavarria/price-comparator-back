// Decompiled with JetBrains decompiler
// Type: ROP.ErrorResultException
// Assembly: ROP, Version=1.4.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C68082B8-B1F1-4EA2-9011-EA4A3BB1918C
// Assembly location: /Users/gabrielolavarria/.nuget/packages/netmentor.rop/1.4.0/lib/net8.0/ROP.dll

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

#nullable disable
namespace PriceComparator.Domain.Common;

public class ErrorResultException : Exception
{
    public ImmutableArray<Error> Errors { get; }

    public ErrorResultException(ImmutableArray<Error> errors)
        : base(ErrorResultException.ValidateAndGetErrorMessage(errors))
    {
        this.Errors = errors;
    }

    public ErrorResultException(Error error)
        : this(((IEnumerable<Error>) new Error[1]{ error }).ToImmutableArray<Error>())
    {
    }

    private static string ValidateAndGetErrorMessage(ImmutableArray<Error> errors)
    {
        if (errors.Length == 0)
            throw new Exception("You should include at least one Error");
        if (errors.Length == 1)
            return errors[0].Message;
        return errors.Select<Error, string>((Func<Error, string>) (e => e.Message)).Prepend<string>($"{errors.Length} Errors occurred:").JoinStrings(Environment.NewLine);
    }
}