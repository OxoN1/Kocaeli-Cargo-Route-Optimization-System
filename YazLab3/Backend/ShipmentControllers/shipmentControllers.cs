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
            public string Content { get; set; } = string.Empty;
            public int Quantity { get; set; } = 1;
        }

        [HttpPost]
        public IActionResult CreateShipment([FromBody] CreateShipmentRequest request)
        {
            if (request is null)
            {
                return BadRequest(new { mesaj = "Ge�ersiz istek." });
            }

            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest(new { mesaj = "Email zorunludur." });
            }

            if (request.StationId <= 0)
            {
                return BadRequest(new { mesaj = "�stasyon se�imi zorunludur." });
            }

            if (request.WeightKg <= 0)
            {
                return BadRequest(new { mesaj = "A��rl�k 0'dan b�y�k olmal�d�r." });
            }
            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return BadRequest(new { mesaj = "Kargo içeriği zorunludur." });
            }

            if (request.Quantity <= 0)
            {
                return BadRequest(new { mesaj = "Adet 0'dan büyük olmalıdır." });
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
                            return BadRequest(new { mesaj = "Kullanıcı bulunamadı." });
                        }

                        userId = Convert.ToInt32(userObj);
                    }

                    using (var stCmd = new MySqlCommand("SELECT COUNT(1) FROM Stations WHERE Id=@id;", connection))
                    {
                        stCmd.Parameters.AddWithValue("@id", request.StationId);
                        var exists = Convert.ToInt32(stCmd.ExecuteScalar() ?? 0);
                        if (exists == 0)
                        {
                            return BadRequest(new { mesaj = "İstasyon bulunamadı." });
                        }
                    }

                    const string insertSql = @"
INSERT INTO Shipments (UserId, StationId, WeightKg, Content, ShipDate, Status)
VALUES (@userId, @stationId, @weightKg, @content, @shipDate, 'Pending');";

                    var shipmentIds = new List<long>();
                    
                    // Quantity kadar ayrı satır oluştur
                    for (int i = 0; i < request.Quantity; i++)
                    {
                        using (var insertCmd = new MySqlCommand(insertSql + " SELECT LAST_INSERT_ID();", connection))
                        {
                            insertCmd.Parameters.AddWithValue("@userId", userId);
                            insertCmd.Parameters.AddWithValue("@stationId", request.StationId);
                            insertCmd.Parameters.AddWithValue("@weightKg", request.WeightKg);
                            insertCmd.Parameters.AddWithValue("@content", request.Content);
                            insertCmd.Parameters.AddWithValue("@shipDate", tomorrow.ToString("yyyy-MM-dd"));

                            var idObj = insertCmd.ExecuteScalar();
                            shipmentIds.Add(Convert.ToInt64(idObj));
                        }
                    }

                    return Ok(new
                    {
                        mesaj = $"Kargo talebi alındı. {request.Quantity} adet kargo oluşturuldu.",
                        shipmentIds,
                        totalShipments = shipmentIds.Count,
                        shipDate = tomorrow.ToString("yyyy-MM-dd"),
                        status = "Pending",
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