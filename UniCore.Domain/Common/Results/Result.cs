using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace UniCore.Domain.Common.Results
{
    public class Result
    {
        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;
        public IReadOnlyList<Error> Errors { get; }
        public Error? Error => Errors.Count > 0 ? Errors[0] : null;

        protected Result(bool isSuccess, IReadOnlyList<Error> errors)
        {
            IsSuccess = isSuccess;
            Errors = errors;
        }

        public static Result Ok() => new(true, []);
        public static Result Fail(Error error) => new(false, [error]);
        public static Result Fail(IReadOnlyList<Error> errors) => new(false, errors);

        public TOut Match<TOut>(Func<TOut> onSuccess, Func<IReadOnlyList<Error>, TOut> onFailure) => IsSuccess ? onSuccess() : onFailure(Errors);
    }

    public sealed class Result<TValue> : Result
    {
        private readonly TValue _value;

        private Result(TValue value) : base(true, [])
        {
            _value = value;
        }

        private Result(IReadOnlyList<Error> errors) : base(false, errors)
        {
            _value = default!;
        }

        public TValue? Value => IsSuccess ? _value : default;

        public bool TryGetValue([MaybeNullWhen(false)] out TValue value)
        {
            value = _value;
            return IsSuccess;
        }

        public static Result<TValue> Ok(TValue value) => new(value);
        public static new Result<TValue> Fail(Error error) => new(new[] { error });
        public static new Result<TValue> Fail(IReadOnlyList<Error> errors) => new(errors);

        public TOut Match<TOut>(Func<TValue, TOut> onSuccess, Func<IReadOnlyList<Error>, TOut> onFailure) => TryGetValue(out var value) ? onSuccess(value) : onFailure(Errors);

        public Result<TOut> Map<TOut>(Func<TValue, TOut> map) => TryGetValue(out var value) ? Result<TOut>.Ok(map(value)) : Result<TOut>.Fail(Errors);

        public Result<TOut> Bind<TOut>(Func<TValue, Result<TOut>> bind) => TryGetValue(out var value) ? bind(value) : Result<TOut>.Fail(Errors);

        public static implicit operator Result<TValue>(TValue value) => Ok(value);
        public static implicit operator Result<TValue>(Error error) => Fail(error);
    }
}
