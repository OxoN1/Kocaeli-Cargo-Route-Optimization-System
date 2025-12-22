using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

namespace YazLab2.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ShipmentsController : ControllerBase
    {
        private readonly IConfiguration _config;

        public ShipmentsController(IConfiguration config)
        {
            _config = config;
        }

        public sealed class CreateShipmentRequest
        {
            public string Email { get; set; } = string.Empty;
            public int StationId { get; set; }
            public int WeightKg { get; set; }
        }

        [HttpPost]
        public IActionResult CreateShipment([FromBody] CreateShipmentRequest request)
        {
            if (request is null)
            {
                return BadRequest(new { mesaj = "Geçersiz istek." });
            }

            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest(new { mesaj = "Email zorunludur." });
            }

            if (request.StationId <= 0)
            {
                return BadRequest(new { mesaj = "Ýstasyon seçimi zorunludur." });
            }

            if (request.WeightKg <= 0)
            {
                return BadRequest(new { mesaj = "Aðýrlýk 0'dan büyük olmalýdýr." });
            }

            var tomorrow = DateOnly.FromDateTime(DateTime.Today.AddDays(1));

            try
            {
                string connString = _config.GetConnectionString("MyDatabaseConnection");

                using (var connection = new MySqlConnection(connString))
                {
                    connection.Open();

                    int userId;
                    using (var userCmd = new MySqlCommand("SELECT Id FROM Users WHERE Email=@email LIMIT 1;", connection))
                    {
                        userCmd.Parameters.AddWithValue("@email", request.Email);
                        var userObj = userCmd.ExecuteScalar();
                        if (userObj == null)
                        {
                            return BadRequest(new { mesaj = "Kullanýcý bulunamadý." });
                        }

                        userId = Convert.ToInt32(userObj);
                    }

                    using (var stCmd = new MySqlCommand("SELECT COUNT(1) FROM Stations WHERE Id=@id;", connection))
                    {
                        stCmd.Parameters.AddWithValue("@id", request.StationId);
                        var exists = Convert.ToInt32(stCmd.ExecuteScalar() ?? 0);
                        if (exists == 0)
                        {
                            return BadRequest(new { mesaj = "Ýstasyon bulunamadý." });
                        }
                    }

                    const string insertSql = @"
INSERT INTO Shipments (UserId, StationId, WeightKg, ShipDate, Status)
VALUES (@userId, @stationId, @weightKg, @shipDate, 'Pending');
SELECT LAST_INSERT_ID();";

                    long shipmentId;
                    using (var insertCmd = new MySqlCommand(insertSql, connection))
                    {
                        insertCmd.Parameters.AddWithValue("@userId", userId);
                        insertCmd.Parameters.AddWithValue("@stationId", request.StationId);
                        insertCmd.Parameters.AddWithValue("@weightKg", request.WeightKg);
                        insertCmd.Parameters.AddWithValue("@shipDate", tomorrow.ToString("yyyy-MM-dd"));

                        var idObj = insertCmd.ExecuteScalar();
                        shipmentId = Convert.ToInt64(idObj);
                    }

                    return Ok(new
                    {
                        mesaj = "Kargo talebi alýndý.",
                        shipmentId,
                        shipDate = tomorrow.ToString("yyyy-MM-dd"),
                        status = "Pending",
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { mesaj = "Sunucu hatasý: " + ex.Message });
            }
        }
    }
}