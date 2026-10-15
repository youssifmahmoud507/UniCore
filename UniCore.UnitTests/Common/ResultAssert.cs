using UniCore.Domain.Common.Results;

namespace UniCore.UnitTests.Common
{
    // Small helpers so tests read the same way everywhere and never use "!" on a Result value.
    public static class ResultAssert
    {
        public static T Succeeded<T>(Result<T> result) where T : class
        {
            var ok = result.TryGetValue(out var value);
            Assert.True(ok, "Expected success but got: " + result.Error?.Code);
            Assert.NotNull(value);
            return value;
        }

        public static void Succeeded(Result result)
        {
            Assert.True(result.IsSuccess, "Expected success but got: " + result.Error?.Code);
        }

        public static void Failed(Result result, Error expected)
        {
            Assert.True(result.IsFailure, "Expected failure " + expected.Code + " but the result succeeded.");
            Assert.Equal(expected.Code, result.Error?.Code);
        }
    }
}
