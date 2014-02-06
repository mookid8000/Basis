using MongoDB.Driver;

namespace Basis.Tests
{
    public class MongoFixture : FixtureBase
    {
        protected const string MongoDbConnectionString = "mongodb://localhost/basis_test";

        protected static MongoDatabase GetDatabase(string databaseName = null)
        {
            var databaseNameToUse = databaseName ?? new MongoUrl(MongoDbConnectionString).DatabaseName;

            var mongoDatabase = new MongoClient(MongoDbConnectionString)
                .GetServer()
                .GetDatabase(databaseNameToUse);

            return mongoDatabase;
        }
    }
}