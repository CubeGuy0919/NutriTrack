using MySqlConnector;
using NutriTrack.Core.Models;

namespace NutriTrack.Data;

public class ProductRepository
{
    private readonly DatabaseConnection _db;

    public ProductRepository(DatabaseConnection db)
    {
        _db = db;
    }
}
