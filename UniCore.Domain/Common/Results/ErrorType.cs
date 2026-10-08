
namespace UniCore.Domain.Common.Results
{
    public enum ErrorType
    {
        Failure = 0,
        Validation = 1,
        NotFound = 2,
        Conflict = 3,
        Unauthorized = 4,
        Forbidden = 5,
        InvalidCredentials = 6,
        BusinessRule = 7,
        Concurrency = 8,
        External = 9
    }
}
