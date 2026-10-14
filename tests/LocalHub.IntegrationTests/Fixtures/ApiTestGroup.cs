namespace LocalHub.IntegrationTests.Fixtures;

// Every integration test class shares one API and one database, and the classes run one at a time.
[CollectionDefinition(Name)]
public class ApiTestGroup : ICollectionFixture<LocalHubApiFactory>
{
    public const string Name = "Api";
}