namespace CinemaReservation.Tests;

// Database-backed integration suites share seeded application state and must
// not start competing test hosts against the same PostgreSQL test database.
[CollectionDefinition("Integration", DisableParallelization = true)]
public class IntegrationTestCollection
{
}