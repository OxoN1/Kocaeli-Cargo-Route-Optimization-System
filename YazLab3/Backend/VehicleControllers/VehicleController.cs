using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

namespace YazLab2.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VehicleController : ControllerBase
    {
        private readonly IConfiguration _config;

        public VehicleController(IConfiguration config)
        {
            _config = config;
        }

        // Tüm araçları listele (kiralık olmayanlar dahil)
        [HttpGet("vehicles")]
        public IActionResult GetVehicles()
        {
            try
            {
                string connString = _config.GetConnectionString("MyDatabaseConnection");

                using (var connection = new MySqlConnection(connString))
                {
                    connection.Open();
                    
                    const string sql = @"
                        SELECT Id, Name, CapacityKg, IsOwned, RentalCost 
                        FROM Vehicles 
                        WHERE IsOwned = 1
                        ORDER BY Id;";

                    var vehicles = new List<object>();

                    using (var cmd = new MySqlCommand(sql, connection))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                vehicles.Add(new
                                {
                                    id = reader.GetInt32("Id"),
                                    name = reader.GetString("Name"),
                                    capacityKg = reader.GetInt32("CapacityKg"),
                                    isOwned = reader.GetBoolean("IsOwned"),
                                    rentalCost = reader.GetDouble("RentalCost")
                                });
                            }
                        }
                    }

                    return Ok(new
                    {
                        mesaj = $"{vehicles.Count} araç bulundu.",
                        vehicles
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { mesaj = "Sunucu hatası: " + ex.Message });
            }
        }

        // Araç güncelleme
        [HttpPut("{id}")]
        public IActionResult UpdateVehicle(int id, [FromBody] UpdateVehicleRequest request)
        {
            if (request is null)
            {
                return BadRequest(new { mesaj = "Geçersiz istek." });
            }

            if (request.CapacityKg <= 0)
            {
                return BadRequest(new { mesaj = "Kapasite 0'dan büyük olmalıdır." });
            }

            try
            {
                string connString = _config.GetConnectionString("MyDatabaseConnection");

                using (var connection = new MySqlConnection(connString))
                {
                    connection.Open();

                    // Aracın var olup olmadığını ve sahip olunan araç olup olmadığını kontrol et
                    using (var checkCmd = new MySqlCommand("SELECT IsOwned FROM Vehicles WHERE Id=@id LIMIT 1;", connection))
                    {
                        checkCmd.Parameters.AddWithValue("@id", id);
                        var result = checkCmd.ExecuteScalar();
                        
                        if (result == null)
                        {
                            return NotFound(new { mesaj = "Araç bulunamadı." });
                        }

                        bool isOwned = Convert.ToBoolean(result);
                        if (!isOwned)
                        {
                            return BadRequest(new { mesaj = "Kiralık araçlar düzenlenemez." });
                        }
                    }

                    const string updateSql = @"
                        UPDATE Vehicles 
                        SET Name = @name,
                            CapacityKg = @capacity
                        WHERE Id = @id;";

                    using (var updateCmd = new MySqlCommand(updateSql, connection))
                    {
                        updateCmd.Parameters.AddWithValue("@id", id);
                        updateCmd.Parameters.AddWithValue("@name", request.Name ?? "");
                        updateCmd.Parameters.AddWithValue("@capacity", request.CapacityKg);

                        int rowsAffected = updateCmd.ExecuteNonQuery();

                        if (rowsAffected == 0)
                        {
                            return NotFound(new { mesaj = "Araç güncellenemedi." });
                        }
                    }

                    return Ok(new { mesaj = "Araç başarıyla güncellendi." });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { mesaj = "Sunucu hatası: " + ex.Message });
            }
        }

        public sealed class UpdateVehicleRequest
        {
            public string Name { get; set; } = string.Empty;
            public int CapacityKg { get; set; }
            public double FuelCostPerKm { get; set; }
            public bool IsActive { get; set; }
        }
    }
}