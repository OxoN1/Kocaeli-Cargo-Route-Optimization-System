using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using System.Globalization;
using System.Text.Json;

namespace YazLab2.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RoutingController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _httpClient = new();

        public RoutingController(IConfiguration config)
        {
            _config = config;
        }

        [HttpGet("route")]
        public async Task<IActionResult> GetRoute([FromQuery] string fromStationId, [FromQuery] string toStationId)
        {
            if (string.IsNullOrWhiteSpace(fromStationId) || string.IsNullOrWhiteSpace(toStationId))
            {
                return BadRequest(new { mesaj = "fromStationId ve toStationId zorunludur." });
            }

            if (fromStationId == toStationId)
            {
                return BadRequest(new { mesaj = "fromStationId ve toStationId farklý olmalýdýr." });
            }

            try
            {
                var connString = _config.GetConnectionString("MyDatabaseConnection");

                // String parametreleri int'e çevirerek gönderiyoruz
                if (!int.TryParse(fromStationId, out var fromId) || fromId <= 0)
                {
                    return BadRequest(new { mesaj = "fromStationId geçersiz bir sayý." });
                }

                if (!int.TryParse(toStationId, out var toId) || toId <= 0)
                {
                    return BadRequest(new { mesaj = "toStationId geçersiz bir sayý." });
                }

                var (fromLat, fromLng) = await GetStationLatLng(connString, fromId);
                var (toLat, toLng) = await GetStationLatLng(connString, toId);

                var osrmBaseUrl = _config["Routing:OsrmBaseUrl"];
                if (string.IsNullOrWhiteSpace(osrmBaseUrl))
                {
                    osrmBaseUrl = "https://router.project-osrm.org";
                }

                var url =
                    $"{osrmBaseUrl.TrimEnd('/')}/route/v1/driving/" +
                    $"{fromLng.ToString(CultureInfo.InvariantCulture)},{fromLat.ToString(CultureInfo.InvariantCulture)};" +
                    $"{toLng.ToString(CultureInfo.InvariantCulture)},{toLat.ToString(CultureInfo.InvariantCulture)}" +
                    "?overview=full&geometries=geojson&steps=false";

                using var response = await _httpClient.GetAsync(url);
                var jsonString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode(502, new { mesaj = "OSRM hatasý.", detal = jsonString });
                }

                using var doc = JsonDocument.Parse(jsonString);

                var routes = doc.RootElement.GetProperty("routes");
                if (routes.ValueKind != JsonValueKind.Array || routes.GetArrayLength() == 0)
                {
                    return NotFound(new { mesaj = "Rota bulunamadý." });
                }

                var firstRoute = routes[0];
                var distanceMeters = firstRoute.GetProperty("distance").GetDouble();
                var distanceKm = distanceMeters / 1000.0;

                var geometry = firstRoute.GetProperty("geometry");
                var coordinates = geometry.GetProperty("coordinates");

                var points = new List<double[]>(capacity: coordinates.GetArrayLength());
                foreach (var c in coordinates.EnumerateArray())
                {
                    var lng = c[0].GetDouble();
                    var lat = c[1].GetDouble();
                    points.Add(new[] { lat, lng });
                }

                return Ok(new
                {
                    fromStationId,
                    toStationId,
                    distanceKm,
                    roadCost = distanceKm,
                    polyline = points,
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { mesaj = "Sunucu hatasý: " + ex.Message });
            }
        }

        // Metot parametresi int olarak deðiþtirildi
        private static async Task<(double lat, double lng)> GetStationLatLng(string connString, int stationId)
        {
            if (stationId <= 0)
            {
                throw new Exception($"Ýstasyon Id geçersiz: {stationId}");
            }

            using var connection = new MySqlConnection(connString);
            await connection.OpenAsync();

            const string sql = "SELECT Latitude, Longitude FROM Stations WHERE Id=@id LIMIT 1;";
            using var cmd = new MySqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@id", stationId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                throw new Exception($"Ýstasyon bulunamadý. Id={stationId}");
            }

            // reader.GetDouble(int) overload kullanýlýyor; önce sütun indeksini alýyoruz
            var lat = reader.GetDouble(reader.GetOrdinal("Latitude"));
            var lng = reader.GetDouble(reader.GetOrdinal("Longitude"));
            return (lat, lng);
        }
    }
}