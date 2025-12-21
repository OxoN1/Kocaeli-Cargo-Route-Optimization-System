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
                                        Longitude = reader.GetDouble("Longitude")
                                    });
                                } while (reader.Read());
                                return Ok(stations);

                            }
                            else
                            {
                                return NotFound(new { mesaj = "İstasyon bulunamadı." });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { mesaj = "Sunucu hatası: " + ex.Message });
            }
        }
    }
}