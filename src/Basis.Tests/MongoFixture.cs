using MongoDB.Driver;

namespace Basis.Tests
{
    public class MongoFixture : FixtureBase
    {
        protected const string MongoDbConnectionString = "mongodb://localhost/basis_test";

        protected static MongoDatabase GetDatabase(string databaseName = null)
        {
            return new MongoClient(MongoDbConnectionString)
                .GetServer()
                .GetDatabase(databaseName ?? new MongoUrl(MongoDbConnectionString).DatabaseName);
        }
    }
}