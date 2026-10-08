using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Text;
using UniCore.Api.Extensions;
using UniCore.Domain.Common.Results;

namespace UniCore.IntegrationTests.Api
{
    public class ResultExtensionsTests
    {
        [Theory]
        [InlineData(ErrorType.Validation, 400)]
        [InlineData(ErrorType.NotFound, 404)]
        [InlineData(ErrorType.Conflict, 409)]
        [InlineData(ErrorType.Concurrency, 409)]
        [InlineData(ErrorType.Unauthorized, 401)]
        [InlineData(ErrorType.InvalidCredentials, 401)]
        [InlineData(ErrorType.Forbidden, 403)]
        [InlineData(ErrorType.BusinessRule, 422)]
        [InlineData(ErrorType.External, 503)]
        [InlineData(ErrorType.Failure, 500)]
        public void Failure_maps_to_the_expected_status_and_problem_details(ErrorType type, int expectedStatus)
        {
            var result = Result.Fail(new Error("SOME_CODE", "something happened", type));

            var action = result.ToActionResult(new DefaultHttpContext());

            var obj = Assert.IsType<ObjectResult>(action);
            Assert.Equal(expectedStatus, obj.StatusCode);
            var problem = Assert.IsType<ProblemDetails>(obj.Value);
            Assert.Equal(expectedStatus, problem.Status);
            Assert.Equal("SOME_CODE", (string?)problem.Extensions["errorCode"]);
            Assert.True(problem.Extensions.ContainsKey("traceId"));
        }

        [Fact]
        public void Validation_failure_includes_errors_dictionary()
        {
            var result = Result.Fail(new[]
            {
            Error.Validation("EMAIL_INVALID", "Email is invalid."),
            Error.Validation("PASSWORD_WEAK", "Password is too weak.")
        });

            var obj = Assert.IsType<ObjectResult>(result.ToActionResult(new DefaultHttpContext()));
            var problem = Assert.IsType<ProblemDetails>(obj.Value);

            var errors = Assert.IsType<Dictionary<string, string[]>>(problem.Extensions["errors"]);
            Assert.Equal(2, errors.Count);
        }

        [Fact]
        public void Success_without_value_returns_NoContent()
        {
            var action = Result.Ok().ToActionResult(new DefaultHttpContext());

            Assert.IsType<NoContentResult>(action);
        }

        [Fact]
        public void Success_with_value_returns_Ok_with_the_value()
        {
            var action = Result<int>.Ok(5).ToActionResult(new DefaultHttpContext());

            var ok = Assert.IsType<OkObjectResult>(action);
            Assert.Equal(5, ok.Value);
        }
    }
}