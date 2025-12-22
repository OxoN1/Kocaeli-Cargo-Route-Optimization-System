using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using System.Text.Json; 

namespace YazLab2.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StationController : ControllerBase
    {
        private readonly IConfiguration _config;

        public StationController(IConfiguration config)
        {
            _config = config;
        }

        public sealed class CreateStationRequest
        {
            public string AdminEmail { get; set; } = string.Empty;
            public string StationName { get; set; } = string.Empty;
            public double Latitude { get; set; }
            public double Longitude { get; set; }
        }

        [HttpGet("stations")]
        public IActionResult GetStations()
        {
            try
            {
                string connString = _config.GetConnectionString("MyDatabaseConnection");

                using (var connection = new MySqlConnection(connString))
                {
                    connection.Open();
                    string sql = "SELECT Id, StationName, Latitude, Longitude FROM Stations";
                    using (var cmd = new MySqlCommand(sql, connection))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var stations = new List<object>();
                                do
                                {
                                    stations.Add(new
                                    {
                                        Id = reader.GetInt32("Id"),
                                        Name = reader.GetString("StationName"),
                                        Latitude = reader.GetDouble("Latitude"),
                                        Longitude = reader.GetDouble("Longitude"),
                                    });
                                } while (reader.Read());
                                return Ok(stations);
                            }

                            return NotFound(new { mesaj = "İstasyon bulunamadı." });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { mesaj = "Sunucu hatası: " + ex.Message });
            }
        }

        [HttpPost]
        public IActionResult CreateStation([FromBody] CreateStationRequest request)
        {
            if (request is null)
            {
                return BadRequest(new { mesaj = "Geçersiz istek." });
            }

            if (string.IsNullOrWhiteSpace(request.AdminEmail))
            {
                return BadRequest(new { mesaj = "AdminEmail zorunludur." });
            }

            if (string.IsNullOrWhiteSpace(request.StationName))
            {
                return BadRequest(new { mesaj = "StationName zorunludur." });
            }

            if (request.Latitude is < -90 or > 90)
            {
                return BadRequest(new { mesaj = "Latitude -90 ile 90 arasında olmalıdır." });
            }

            if (request.Longitude is < -180 or > 180)
            {
                return BadRequest(new { mesaj = "Longitude -180 ile 180 arasında olmalıdır." });
            }

            try
            {
                string connString = _config.GetConnectionString("MyDatabaseConnection");

                using (var connection = new MySqlConnection(connString))
                {
                    connection.Open();

                    // Admin kontrolü (Users.UserID == 1)
                    using (var adminCmd = new MySqlCommand("SELECT UserID FROM Users WHERE Email=@email LIMIT 1;", connection))
                    {
                        adminCmd.Parameters.AddWithValue("@email", request.AdminEmail);
                        var userTypeObj = adminCmd.ExecuteScalar();
                        if (userTypeObj == null)
                        {
                            return Forbid();
                        }

                        var userIdType = Convert.ToInt32(userTypeObj);
                        if (userIdType != 1)
                        {
                            return Forbid();
                        }
                    }

                    // Aynı isimli istasyon var mı?
                    using (var existsCmd = new MySqlCommand("SELECT COUNT(1) FROM Stations WHERE StationName=@name;", connection))
                    {
                        existsCmd.Parameters.AddWithValue("@name", request.StationName);
                        var exists = Convert.ToInt32(existsCmd.ExecuteScalar() ?? 0);
                        if (exists > 0)
                        {
                            return Conflict(new { mesaj = "Bu isimde bir istasyon zaten mevcut." });
                        }
                    }

                    const string insertSql = @"
INSERT INTO Stations (StationName, Latitude, Longitude)
VALUES (@name, @lat, @lng);
SELECT LAST_INSERT_ID();";

                    long stationId;
                    using (var insertCmd = new MySqlCommand(insertSql, connection))
                    {
                        insertCmd.Parameters.AddWithValue("@name", request.StationName);
                        insertCmd.Parameters.AddWithValue("@lat", request.Latitude);
                        insertCmd.Parameters.AddWithValue("@lng", request.Longitude);

                        var idObj = insertCmd.ExecuteScalar();
                        stationId = Convert.ToInt64(idObj);
                    }

                    return Ok(new
                    {
                        mesaj = "İstasyon eklendi.",
                        stationId,
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { mesaj = "Sunucu hatası: " + ex.Message });
            }
        }
    }
}