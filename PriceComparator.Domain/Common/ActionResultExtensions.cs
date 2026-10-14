
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

#nullable enable
namespace PriceComparator.Domain.Common;

public static class ActionResultExtensions
{
  public static 
  #nullable disable
  IActionResult ToActionResult<T>(this Result<T> result)
  {
    return result.ToDto<T>().ToHttpStatusCode<ResultDto<T>>(result.HttpStatusCode);
  }

  public static async Task<IActionResult> ToActionResult<T>(this Task<Result<T>> result)
  {
    return (await result).ToActionResult<T>();
  }

  public static IActionResult ToValueOrProblemDetails<T>(this Result<T> result)
  {
    if (result.Success)
      return result.Value.ToHttpStatusCode<T>(result.HttpStatusCode);
    return new ProblemDetails()
    {
      Title = "Error(s) found",
      Status = new int?((int) result.HttpStatusCode),
      Detail = "One or more errors occurred",
      Extensions = {
        {
          "ValidationErrors",
          (object) result.Errors.Select<Error, ErrorDto>((Func<Error, ErrorDto>) (x => x.ToErrorDto())).ToList<ErrorDto>()
        }
      }
    }.ToHttpStatusCode<ProblemDetails>(result.HttpStatusCode);
  }

  public static async Task<IActionResult> ToValueOrProblemDetails<T>(this Task<Result<T>> result)
  {
    return (await result).ToValueOrProblemDetails<T>();
  }

  private static IActionResult ToHttpStatusCode<T>(this T resultDto, HttpStatusCode statusCode)
  {
    return (IActionResult) new ActionResultExtensions.ResultWithStatusCode<T>(resultDto, statusCode);
  }

  private static ResultDto<T> ToDto<T>(this Result<T> result)
  {
    if (result.Success)
      return new ResultDto<T>()
      {
        Value = result.Value,
        Errors = ImmutableArray<ErrorDto>.Empty
      };
    return new ResultDto<T>()
    {
      Value = default (T),
      Errors = result.Errors.Select<Error, ErrorDto>((Func<Error, ErrorDto>) (x => x.ToErrorDto())).ToImmutableArray<ErrorDto>()
    };
  }

  private class ResultWithStatusCode<T> : ObjectResult
  {
    public ResultWithStatusCode(T content, HttpStatusCode httpStatusCode)
      : base((object) content)
    {
      this.StatusCode = new int?((int) httpStatusCode);
    }
  }
}
