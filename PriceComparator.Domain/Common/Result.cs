
using System;
using System.Collections.Immutable;
using System.Net;

#nullable disable
namespace PriceComparator.Domain.Common;

public struct Result<T>
{
    public readonly T Value;
    public readonly ImmutableArray<Error> Errors;
    public readonly HttpStatusCode HttpStatusCode;

    public static implicit operator Result<T>(T value) => new Result<T>(value, HttpStatusCode.OK);

    public static implicit operator Result<T>(ImmutableArray<Error> errors)
    {
        return new Result<T>(errors, HttpStatusCode.BadRequest);
    }

    public bool Success => this.Errors.Length == 0;

    public Result(T value, HttpStatusCode statusCode)
    {
        this.Value = value;
        this.Errors = ImmutableArray<Error>.Empty;
        this.HttpStatusCode = statusCode;
    }

    public Result(ImmutableArray<Error> errors, HttpStatusCode statusCode)
    {
        if (errors.Length == 0)
            throw new InvalidOperationException("You should specify at least one error");
        this.HttpStatusCode = statusCode;
        this.Value = default (T);
        this.Errors = errors;
    }
}