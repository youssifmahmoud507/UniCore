namespace UniCore.IntegrationTests.Infrastructure
{
    // Every class in this collection shares one fixture and runs one test at a time.
    [CollectionDefinition(Name)]
    public sealed class ApiCollection : ICollectionFixture<ApiFixture>
    {
        public const string Name = "Api";
    }

}
