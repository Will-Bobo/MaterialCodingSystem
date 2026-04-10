namespace MaterialCodingSystem.Validation.infrastructure;

public interface IDbContextProvider
{
    AppDbContext Create();
}
