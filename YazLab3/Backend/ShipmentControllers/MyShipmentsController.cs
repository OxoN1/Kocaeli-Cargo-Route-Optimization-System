using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

namespace YazLab2.Controllers
{
    [Route("api/shipments")]
    [ApiController]
    public class MyShipmentsController : ControllerBase
    {
        private readonly IConfiguration _config;

        public MyShipmentsController(IConfiguration config)
        {
            _config = config;
        }

        [HttpGet("my-shipments")]
        public async Task<IActionResult> GetMyShipments([FromQuery] string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return BadRequest(new { mesaj = "Email zorunludur." });
            }

            try
            {
                string connString = _config.GetConnectionString("MyDatabaseConnection");
                if (string.IsNullOrWhiteSpace(connString))
                {
                    connString = _config.GetConnectionString("MyConnection") ?? connString;
                }

                var shipments = new List<object>();

                using (var conn = new MySqlConnection(connString))
                {
                    await conn.OpenAsync();

                    // Önce kullanıcı ID'sini bul
                    int userId;
                    const string userSql = "SELECT Id FROM Users WHERE Email=@email LIMIT 1;";
                    using (var userCmd = new MySqlCommand(userSql, conn))
                    {
                        userCmd.Parameters.AddWithValue("@email", email);
                        var userObj = await userCmd.ExecuteScalarAsync();
                        if (userObj == null)
                        {
                            return BadRequest(new { mesaj = "Kullanıcı bulunamadı." });
                        }
                        userId = Convert.ToInt32(userObj);
                    }

                    // Kullanıcının tüm kargolarını getir
                    const string shipmentsSql = @"
                        SELECT 
                            s.Id,
                            s.StationId,
                            st.StationName,
                            s.WeightKg,
                            s.Content,
                            s.ShipDate,
                            s.Status,
                            s.CreatedAt,
                            t.Id AS TripId,
                            t.VehicleId,
                            v.Name AS VehicleName,
                            t.TripDate,
                            t.TotalDistanceKm,
                            t.TotalCost,
                            t.Polyline
                        FROM Shipments s
                        INNER JOIN Stations st ON s.StationId = st.Id
                        LEFT JOIN TripShipments ts ON s.Id = ts.ShipmentId
                        LEFT JOIN Trips t ON ts.TripId = t.Id
                        LEFT JOIN Vehicles v ON t.VehicleId = v.Id
                        WHERE s.UserId = @userId
                        ORDER BY s.CreatedAt DESC;";

                    using (var cmd = new MySqlCommand(shipmentsSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var shipmentId = reader.GetInt64(reader.GetOrdinal("Id"));
                                var stationId = reader.GetInt32(reader.GetOrdinal("StationId"));
                                var stationName = reader.GetString(reader.GetOrdinal("StationName"));
                                var weightKg = reader.GetInt32(reader.GetOrdinal("WeightKg"));
                                
                                var contentOrd = reader.GetOrdinal("Content");
                                var content = reader.IsDBNull(contentOrd) ? "" : reader.GetString(contentOrd);
                                
                                var shipDate = reader.GetDateTime(reader.GetOrdinal("ShipDate"));
                                var status = reader.GetString(reader.GetOrdinal("Status"));
                                var createdAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"));

                                // Trip bilgileri (eğer atanmışsa)
                                long? tripId = null;
                                int? vehicleId = null;
                                string? vehicleName = null;
                                DateTime? tripDate = null;
                                double? totalDistanceKm = null;
                                double? totalCost = null;
                                string? polyline = null;

                                var tripIdOrd = reader.GetOrdinal("TripId");
                                if (!reader.IsDBNull(tripIdOrd))
                                {
                                    tripId = reader.GetInt32(tripIdOrd);
                                    
                                    var vehicleIdOrd = reader.GetOrdinal("VehicleId");
                                    if (!reader.IsDBNull(vehicleIdOrd))
                                        vehicleId = reader.GetInt32(vehicleIdOrd);
                                    
                                    var vehicleNameOrd = reader.GetOrdinal("VehicleName");
                                    if (!reader.IsDBNull(vehicleNameOrd))
                                        vehicleName = reader.GetString(vehicleNameOrd);
                                    
                                    var tripDateOrd = reader.GetOrdinal("TripDate");
                                    if (!reader.IsDBNull(tripDateOrd))
                                        tripDate = reader.GetDateTime(tripDateOrd);
                                    
                                    var distanceOrd = reader.GetOrdinal("TotalDistanceKm");
                                    if (!reader.IsDBNull(distanceOrd))
                                        totalDistanceKm = reader.GetDouble(distanceOrd);
                                    
                                    var costOrd = reader.GetOrdinal("TotalCost");
                                    if (!reader.IsDBNull(costOrd))
                                        totalCost = reader.GetDouble(costOrd);
                                    
                                    var polylineOrd = reader.GetOrdinal("Polyline");
                                    if (!reader.IsDBNull(polylineOrd))
                                        polyline = reader.GetString(polylineOrd);
                                }

                                shipments.Add(new
                                {
                                    shipmentId,
                                    stationId,
                                    stationName,
                                    weightKg,
                                    content,
                                    shipDate = shipDate.ToString("yyyy-MM-dd"),
                                    status,
                                    createdAt = createdAt.ToString("yyyy-MM-dd HH:mm:ss"),
                                    tripId,
                                    vehicleId,
                                    vehicleName,
                                    tripDate = tripDate?.ToString("yyyy-MM-dd"),
                                    totalDistanceKm,
                                    totalCost,
                                    polyline
                                });
                            }
                        }
                    }
                }

                return Ok(new
                {
                    mesaj = $"{shipments.Count} kargo bulundu.",
                    shipments
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { mesaj = "Kargolar listelenirken hata oluştu.", detay = ex.Message });
            }
        }
    }
}
