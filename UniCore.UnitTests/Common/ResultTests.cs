using UniCore.Domain.Common.Results;

namespace UniCore.UnitTests.Common
{
    public class ResultTests
    {
        private static readonly Error TestError = Error.Validation("TEST_ERROR", "test");

        [Fact]
        public void Ok_is_success_with_no_errors()
        {
            var result = Result.Ok();

            Assert.True(result.IsSuccess);
            Assert.False(result.IsFailure);
            Assert.Empty(result.Errors);
            Assert.Null(result.Error);
        }

        [Fact]
        public void Fail_with_single_error_is_failure()
        {
            var result = Result.Fail(TestError);

            Assert.True(result.IsFailure);
            Assert.Single(result.Errors);
            Assert.Equal(TestError, result.Error);
        }

        [Fact]
        public void Fail_with_list_keeps_all_errors()
        {
            var second = Error.Conflict("SECOND", "second");
            var result = Result.Fail(new[] { TestError, second });

            Assert.Equal(2, result.Errors.Count);
            Assert.Equal(TestError, result.Error);
        }

        [Fact]
        public void Generic_Ok_exposes_value()
        {
            var result = Result<int>.Ok(42);

            Assert.True(result.IsSuccess);
            Assert.Equal(42, result.Value);
            Assert.True(result.TryGetValue(out var value));
            Assert.Equal(42, value);
        }

        [Fact]
        public void Generic_Fail_does_not_throw_when_value_is_read()
        {
            var result = Result<string>.Fail(TestError);

            Assert.True(result.IsFailure);
            Assert.Null(result.Value);
            Assert.False(result.TryGetValue(out _));
            Assert.Equal(TestError, result.Error);
        }

        [Fact]
        public void Map_transforms_value_on_success()
        {
            var result = Result<int>.Ok(2).Map(x => x * 10);

            Assert.True(result.IsSuccess);
            Assert.Equal(20, result.Value);
        }

        [Fact]
        public void Map_keeps_errors_on_failure_and_skips_function()
        {
            var called = false;
            var result = Result<int>.Fail(TestError).Map(x => { called = true; return x; });

            Assert.True(result.IsFailure);
            Assert.Equal(TestError, result.Error);
            Assert.False(called);
        }

        [Fact]
        public void Bind_chains_on_success()
        {
            var result = Result<int>.Ok(2).Bind(x => Result<string>.Ok(x.ToString()));

            Assert.True(result.IsSuccess);
            Assert.Equal("2", result.Value);
        }

        [Fact]
        public void Bind_returns_inner_failure()
        {
            var inner = Error.NotFound("INNER", "inner");
            var result = Result<int>.Ok(2).Bind(_ => Result<string>.Fail(inner));

            Assert.True(result.IsFailure);
            Assert.Equal(inner, result.Error);
        }

        [Fact]
        public void Bind_short_circuits_on_failure()
        {
            var called = false;
            var result = Result<int>.Fail(TestError).Bind(x => { called = true; return Result<int>.Ok(x); });

            Assert.True(result.IsFailure);
            Assert.False(called);
        }

        [Fact]
        public void Match_picks_the_right_branch()
        {
            var ok = Result<int>.Ok(5).Match(v => $"ok:{v}", e => "fail");
            var fail = Result<int>.Fail(TestError).Match(v => "ok", e => $"fail:{e.Count}");

            Assert.Equal("ok:5", ok);
            Assert.Equal("fail:1", fail);
            Assert.Equal("ok", Result.Ok().Match(() => "ok", _ => "fail"));
            Assert.Equal("fail", Result.Fail(TestError).Match(() => "ok", _ => "fail"));
        }

        [Fact]
        public void Implicit_conversions_work()
        {
            Result<int> fromValue = 7;
            Result<int> fromError = TestError;

            Assert.True(fromValue.IsSuccess);
            Assert.Equal(7, fromValue.Value);
            Assert.True(fromError.IsFailure);
            Assert.Equal(TestError, fromError.Error);
        }

        [Fact]
        public void Error_factories_set_the_matching_type()
        {
            Assert.Equal(ErrorType.Failure, Error.Failure().ErrorType);
            Assert.Equal(ErrorType.Validation, Error.Validation().ErrorType);
            Assert.Equal(ErrorType.NotFound, Error.NotFound().ErrorType);
            Assert.Equal(ErrorType.Conflict, Error.Conflict().ErrorType);
            Assert.Equal(ErrorType.Unauthorized, Error.Unauthorized().ErrorType);
            Assert.Equal(ErrorType.Forbidden, Error.Forbidden().ErrorType);
            Assert.Equal(ErrorType.InvalidCredentials, Error.InvalidCredentials().ErrorType);
            Assert.Equal(ErrorType.BusinessRule, Error.BusinessRule().ErrorType);
            Assert.Equal(ErrorType.Concurrency, Error.Concurrency().ErrorType);
            Assert.Equal(ErrorType.External, Error.External().ErrorType);
        }
    }
}
