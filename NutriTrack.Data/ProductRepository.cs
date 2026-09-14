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

    public List<Product> GetAll()
    {
        var products = new List<Product>();

        using var connection = _db.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT ProductId, Name, Category, Brand, Unit,
                   CaloriesPer100, ProteinPer100, CarbsPer100,
                   FatPer100, FiberPer100, SugarPer100, SaltPer100
            FROM Products
            ORDER BY Name;";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            products.Add(new Product
            {
                ProductId = reader.GetInt32("ProductId"),
                Name = reader.GetString("Name"),
                Category = reader.IsDBNull(reader.GetOrdinal("Category")) ? null : reader.GetString("Category"),
                Brand = reader.IsDBNull(reader.GetOrdinal("Brand")) ? null : reader.GetString("Brand"),
                Unit = reader.GetString("Unit"),
                CaloriesPer100 = reader.GetDouble("CaloriesPer100"),
                ProteinPer100 = reader.GetDouble("ProteinPer100"),
                CarbsPer100 = reader.GetDouble("CarbsPer100"),
                FatPer100 = reader.GetDouble("FatPer100"),
                FiberPer100 = reader.GetDouble("FiberPer100"),
                SugarPer100 = reader.GetDouble("SugarPer100"),
                SaltPer100 = reader.GetDouble("SaltPer100"),
            });
        }

        return products;
    }

    public int Insert(Product product)
    {
        using var connection = _db.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Products
                (Name, Category, Brand, Unit, CaloriesPer100, ProteinPer100,
                 CarbsPer100, FatPer100, FiberPer100, SugarPer100, SaltPer100)
            VALUES
                (@Name, @Category, @Brand, @Unit, @Calories, @Protein,
                 @Carbs, @Fat, @Fiber, @Sugar, @Salt);
            SELECT LAST_INSERT_ID();";

        command.Parameters.AddWithValue("@Name", product.Name);
        command.Parameters.AddWithValue("@Category", (object?)product.Category ?? DBNull.Value);
        command.Parameters.AddWithValue("@Brand", (object?)product.Brand ?? DBNull.Value);
        command.Parameters.AddWithValue("@Unit", product.Unit);
        command.Parameters.AddWithValue("@Calories", product.CaloriesPer100);
        command.Parameters.AddWithValue("@Protein", product.ProteinPer100);
        command.Parameters.AddWithValue("@Carbs", product.CarbsPer100);
        command.Parameters.AddWithValue("@Fat", product.FatPer100);
        command.Parameters.AddWithValue("@Fiber", product.FiberPer100);
        command.Parameters.AddWithValue("@Sugar", product.SugarPer100);
        command.Parameters.AddWithValue("@Salt", product.SaltPer100);

        var result = command.ExecuteScalar();
        return Convert.ToInt32(result);
    }
}
