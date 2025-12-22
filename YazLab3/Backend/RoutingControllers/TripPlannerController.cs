using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using System.Globalization;
using System.Text.Json;

namespace YazLab2.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TripPlannerController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _httpClient = new();

        public TripPlannerController(IConfiguration config)
        {
            _config = config;
        }

        public class PlanResult
        {
            public long TripId { get; set; }
            public int VehicleId { get; set; }
            public bool IsRented { get; set; }
            public double DistanceKm { get; set; }
            public double RoadCost { get; set; }
            public double RentalCost { get; set; }
            public double TotalCost { get; set; }
            public List<int> StationOrder { get; set; } = new();
            public List<long> ShipmentIds { get; set; } = new();
            public List<double[]> Polyline { get; set; } = new();
        }

        // Yeni: mode (unlimited | limited), maxVehicles (sýnýrlý senaryoda), objective (maxWeight|maxCount)
        [HttpPost("plan-next-day")]
        public async Task<IActionResult> PlanNextDay([FromQuery] string date = "", [FromQuery] string mode = "unlimited", [FromQuery] int maxVehicles = 0, [FromQuery] string objective = "maxWeight")
        {
            DateTime shipDate;
            if (string.IsNullOrWhiteSpace(date))
            {
                shipDate = DateTime.UtcNow.Date.AddDays(1);
            }
            else if (!DateTime.TryParse(date, out shipDate))
            {
                return BadRequest(new { mesaj = "Geçersiz tarih formatý." });
            }

            try
            {
                // Connection string al
                string connString = _config.GetConnectionString("MyDatabaseConnection");
                if (string.IsNullOrWhiteSpace(connString))
                {
                    // fallback eski anahtar
                    connString = _config.GetConnectionString("MyConnection") ?? connString;
                }

                if (string.IsNullOrWhiteSpace(connString))
                {
                    return StatusCode(500, new { mesaj = "Veritabaný baðlantý dizesi yok." });
                }

                // DEPOT: "KOU MERKEZ" istasyonunu bul
                int depotStationId;
                double depotLat, depotLng;
                using (var conn = new MySqlConnection(connString))
                {
                    await conn.OpenAsync();
                    using var cmd = new MySqlCommand("SELECT Id, Latitude, Longitude FROM Stations WHERE StationName='KOU MERKEZ' LIMIT 1;", conn);
                    using var rdr = await cmd.ExecuteReaderAsync();
                    if (!await rdr.ReadAsync())
                    {
                        // fallback: ilk istasyon
                        rdr.Close();
                        using var cmd2 = new MySqlCommand("SELECT Id, Latitude, Longitude FROM Stations LIMIT 1;", conn);
                        using var rdr2 = await cmd2.ExecuteReaderAsync();
                        if (!await rdr2.ReadAsync())
                        {
                            return StatusCode(500, new { mesaj = "Depo (KOU MERKEZ) bulunamadý ve baþka istasyon yok." });
                        }
                        depotStationId = rdr2.GetInt32(rdr2.GetOrdinal("Id"));
                        depotLat = rdr2.GetDouble(rdr2.GetOrdinal("Latitude"));
                        depotLng = rdr2.GetDouble(rdr2.GetOrdinal("Longitude"));
                    }
                    else
                    {
                        depotStationId = rdr.GetInt32(rdr.GetOrdinal("Id"));
                        depotLat = rdr.GetDouble(rdr.GetOrdinal("Latitude"));
                        depotLng = rdr.GetDouble(rdr.GetOrdinal("Longitude"));
                    }
                }

                // Bekleyen kargolar (shipments) topla
                var shipments = new List<(long Id, int StationId, int WeightKg, int UserId)>();
                using (var conn = new MySqlConnection(connString))
                {
                    await conn.OpenAsync();
                    const string sqlShip = "SELECT Id, UserId, StationId, WeightKg FROM Shipments WHERE ShipDate=@d AND Status='Pending';";
                    using var cmd = new MySqlCommand(sqlShip, conn);
                    cmd.Parameters.AddWithValue("@d", shipDate.ToString("yyyy-MM-dd"));
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var id = reader.GetInt64(reader.GetOrdinal("Id"));
                        var userId = reader.GetInt32(reader.GetOrdinal("UserId"));
                        var stationId = reader.GetInt32(reader.GetOrdinal("StationId"));
                        var weight = reader.GetInt32(reader.GetOrdinal("WeightKg"));
                        shipments.Add((id, stationId, weight, userId));
                    }
                }

                if (shipments.Count == 0)
                {
                    return Ok(new { mesaj = "Belirtilen tarihte bekleyen kargo yok.", plans = new List<PlanResult>() });
                }

                // Araçlarý oku (active)
                var vehicles = new List<(int Id, int CapacityKg, bool IsOwned, double RentalCost, double FuelCostPerKm)>();
                using (var conn = new MySqlConnection(connString))
                {
                    await conn.OpenAsync();
                    const string sqlVeh = "SELECT Id, CapacityKg, IsOwned, RentalCost, FuelCostPerKm FROM Vehicles WHERE IsActive=1 ORDER BY Id;";
                    using var cmd = new MySqlCommand(sqlVeh, conn);
                    using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        vehicles.Add((
                            rdr.GetInt32(rdr.GetOrdinal("Id")),
                            rdr.GetInt32(rdr.GetOrdinal("CapacityKg")),
                            rdr.GetBoolean(rdr.GetOrdinal("IsOwned")),
                            rdr.GetDouble(rdr.GetOrdinal("RentalCost")),
                            rdr.GetDouble(rdr.GetOrdinal("FuelCostPerKm"))
                        ));
                    }
                }

                // Kiralama parametreleri
                int rentalCapacity = _config.GetValue<int?>("Routing:DefaultRentalCapacity") ?? 500;
                double rentalCost = _config.GetValue<double?>("Routing:DefaultRentalCost") ?? 200.0;

                // Heuristics:
                // - unlimited: owned araçlarý kullan, yetmezse kirala; araç atamasý için First-Fit Decreasing (önce aðýr kargolar).
                // - limited: maxVehicles param ile greedy seçim: objective = maxWeight -> büyük aðýrlýklarý önce; maxCount -> küçük aðýrlýklarý önce.
                List<VehicleSlot> slots = new();
                // baþlangýç: veritabanýndaki araçlar
                foreach (var v in vehicles)
                {
                    slots.Add(new VehicleSlot
                    {
                        VehicleId = v.Id,
                        CapacityKg = v.CapacityKg,
                        RemainingKg = v.CapacityKg,
                        IsRented = !v.IsOwned,
                        RentalCost = v.RentalCost,
                        FuelCostPerKm = v.FuelCostPerKm
                    });
                }

                // Helper: kiralýk araç ekle
                void AddRentedSlot()
                {
                    slots.Add(new VehicleSlot
                    {
                        VehicleId = 0,
                        CapacityKg = rentalCapacity,
                        RemainingKg = rentalCapacity,
                        IsRented = true,
                        RentalCost = rentalCost,
                        FuelCostPerKm = 0
                    });
                }

                // Planlama için kullanýlacak shipment set
                var candidateShipments = new List<(long Id, int StationId, int WeightKg, int UserId)>(shipments);

                if (mode.Equals("limited", StringComparison.OrdinalIgnoreCase))
                {
                    if (maxVehicles <= 0) return BadRequest(new { mesaj = "limited mod için maxVehicles parametresi girilmelidir." });
                    // maxVehicles kadar slot oluþtur (önce DB araçlarýný kullan, sonra gerekiyorsa rented ile tamamla)
                    var initial = new List<VehicleSlot>();
                    // use DB slots up to maxVehicles
                    foreach (var s in slots)
                    {
                        initial.Add(new VehicleSlot
                        {
                            VehicleId = s.VehicleId,
                            CapacityKg = s.CapacityKg,
                            RemainingKg = s.RemainingKg,
                            IsRented = s.IsRented,
                            RentalCost = s.RentalCost,
                            FuelCostPerKm = s.FuelCostPerKm
                        });
                        if (initial.Count == maxVehicles) break;
                    }
                    while (initial.Count < maxVehicles)
                    {
                        initial.Add(new VehicleSlot
                        {
                            VehicleId = 0,
                            CapacityKg = rentalCapacity,
                            RemainingKg = rentalCapacity,
                            IsRented = true,
                            RentalCost = rentalCost,
                            FuelCostPerKm = 0
                        });
                    }
                    slots = initial;

                    // seçim stratejisi
                    if (objective == "maxCount")
                    {
                        candidateShipments = candidateShipments.OrderBy(s => s.WeightKg).ToList(); // küçük aðýrlýk önce
                    }
                    else // maxWeight
                    {
                        candidateShipments = candidateShipments.OrderByDescending(s => s.WeightKg).ToList();
                    }
                }
                else // unlimited
                {
                    // unlimited: sort heavy first for packing
                    candidateShipments = candidateShipments.OrderByDescending(s => s.WeightKg).ToList();
                }

                // assignment: First-Fit per shipment
                var assignment = new Dictionary<VehicleSlot, List<(long ShipmentId, int StationId, int WeightKg, int UserId)>>();

                foreach (var sh in candidateShipments)
                {
                    // in limited mode with objective and maxVehicles, we used restricted slots list length
                    var placed = false;
                    // try vehicles that already have same station (minimize extra stops)
                    var pref = slots.Where(s => s.RemainingKg >= sh.WeightKg)
                                    .OrderBy(s => assignment.ContainsKey(s) && assignment[s].Any(a => a.StationId == sh.StationId) ? 0 : 1)
                                    .ThenByDescending(s => s.RemainingKg)
                                    .FirstOrDefault();
                    if (pref != null)
                    {
                        if (!assignment.ContainsKey(pref)) assignment[pref] = new List<(long, int, int, int)>();
                        assignment[pref].Add((sh.Id, sh.StationId, sh.WeightKg, sh.UserId));
                        pref.RemainingKg -= sh.WeightKg;
                        placed = true;
                    }

                    if (!placed)
                    {
                        // if unlimited: create rented slot and place
                        if (mode.Equals("unlimited", StringComparison.OrdinalIgnoreCase))
                        {
                            AddRentedSlot();
                            var last = slots.Last();
                            assignment[last] = new List<(long, int, int, int)> { (sh.Id, sh.StationId, sh.WeightKg, sh.UserId) };
                            last.RemainingKg -= sh.WeightKg;
                            placed = true;
                        }
                        else
                        {
                            // limited: if cannot place -> skip this shipment (objective-driven)
                            // continue to next shipment
                            continue;
                        }
                    }
                }

                // þimdi her slot için rota sýrala (NearestNeighbor using open connection) ve maliyeti hesapla
                var planResults = new List<PlanResult>();
                using (var conn = new MySqlConnection(connString))
                {
                    await conn.OpenAsync();
                    using var tran = await conn.BeginTransactionAsync();
                    try
                    {
                        foreach (var kv in assignment)
                        {
                            var slot = kv.Key;
                            var list = kv.Value;
                            var stationsToVisit = list.Select(x => x.StationId).Distinct().ToList();

                            var ordered = await NearestNeighborOrder(stationsToVisit, depotLat, depotLng, conn);

                            // build polyline and compute distance
                            var fullPoints = new List<double[]>();
                            double totalKm = 0.0;
                            double lastLat = depotLat, lastLng = depotLng;

                            foreach (var sid in ordered)
                            {
                                // use open connection helper to get lat/lng
                                var (lat, lng) = await GetStationLatLngFromOpenConnection(conn, sid);
                                var routeInfo = await GetRouteFromOsrmAsync(lastLat, lastLng, lat, lng);
                                totalKm += routeInfo.distanceKm;
                                if (fullPoints.Count > 0 && routeInfo.polyline.Count > 0)
                                {
                                    if (!PointsEqual(fullPoints.Last(), routeInfo.polyline.First()))
                                        fullPoints.AddRange(routeInfo.polyline);
                                    else
                                        fullPoints.AddRange(routeInfo.polyline.Skip(1));
                                }
                                else
                                {
                                    fullPoints.AddRange(routeInfo.polyline);
                                }
                                lastLat = lat; lastLng = lng;
                            }

                            // return to depot
                            var returnRoute = await GetRouteFromOsrmAsync(lastLat, lastLng, depotLat, depotLng);
                            totalKm += returnRoute.distanceKm;
                            if (returnRoute.polyline.Count > 0)
                            {
                                if (!PointsEqual(fullPoints.Last(), returnRoute.polyline.First()))
                                    fullPoints.AddRange(returnRoute.polyline);
                                else
                                    fullPoints.AddRange(returnRoute.polyline.Skip(1));
                            }

                            double roadCost = totalKm * 1.0;
                            double extraFuel = slot.FuelCostPerKm * totalKm;
                            double rentC = slot.IsRented ? slot.RentalCost : 0.0;
                            double totalCost = roadCost + extraFuel + rentC;

                            // Insert Trip
                            const string insertTrip = @"INSERT INTO Trips (TripDate, VehicleId, TotalDistanceKm, RoadCost, RentalCost, TotalCost) 
VALUES (@d, @vehicleId, @dist, @road, @rental, @total); SELECT LAST_INSERT_ID();";
                            long tripId;
                            using (var tripCmd = new MySqlCommand(insertTrip, conn, (MySqlTransaction)tran))
                            {
                                tripCmd.Parameters.AddWithValue("@d", shipDate.ToString("yyyy-MM-dd"));
                                tripCmd.Parameters.AddWithValue("@vehicleId", slot.VehicleId);
                                tripCmd.Parameters.AddWithValue("@dist", totalKm);
                                tripCmd.Parameters.AddWithValue("@road", roadCost);
                                tripCmd.Parameters.AddWithValue("@rental", rentC);
                                tripCmd.Parameters.AddWithValue("@total", totalCost);
                                var idObj = await tripCmd.ExecuteScalarAsync();
                                tripId = Convert.ToInt64(idObj);
                            }

                            // TripStops
                            int order = 1;
                            foreach (var sid in ordered)
                            {
                                const string insertStop = "INSERT INTO TripStops (TripId, StopOrder, StationId, PlannedLoadKg) VALUES (@tid, @ord, @sid, @load);";
                                int plannedLoad = list.Where(a => a.StationId == sid).Sum(a => a.WeightKg);
                                using var stopCmd = new MySqlCommand(insertStop, conn, (MySqlTransaction)tran);
                                stopCmd.Parameters.AddWithValue("@tid", tripId);
                                stopCmd.Parameters.AddWithValue("@ord", order++);
                                stopCmd.Parameters.AddWithValue("@sid", sid);
                                stopCmd.Parameters.AddWithValue("@load", plannedLoad);
                                await stopCmd.ExecuteNonQueryAsync();
                            }

                            // TripShipments and update Shipments
                            foreach (var sh in list)
                            {
                                const string insertTS = "INSERT INTO TripShipments (TripId, ShipmentId) VALUES (@tid, @sid);";
                                using var tsCmd = new MySqlCommand(insertTS, conn, (MySqlTransaction)tran);
                                tsCmd.Parameters.AddWithValue("@tid", tripId);
                                tsCmd.Parameters.AddWithValue("@sid", sh.ShipmentId);
                                await tsCmd.ExecuteNonQueryAsync();

                                const string updateShip = "UPDATE Shipments SET Status='Assigned' WHERE Id=@id;";
                                using var upCmd = new MySqlCommand(updateShip, conn, (MySqlTransaction)tran);
                                upCmd.Parameters.AddWithValue("@id", sh.ShipmentId);
                                await upCmd.ExecuteNonQueryAsync();
                            }

                            planResults.Add(new PlanResult
                            {
                                TripId = tripId,
                                VehicleId = slot.VehicleId,
                                IsRented = slot.IsRented,
                                DistanceKm = totalKm,
                                RoadCost = roadCost,
                                RentalCost = rentC,
                                TotalCost = totalCost,
                                StationOrder = ordered,
                                ShipmentIds = list.Select(x => x.ShipmentId).ToList(),
                                Polyline = fullPoints
                            });
                        }

                        await tran.CommitAsync();
                    }
                    catch
                    {
                        await tran.RollbackAsync();
                        throw;
                    }
                }

                return Ok(new { mesaj = "Plan baþarýyla oluþturuldu.", plans = planResults });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { mesaj = "Sunucu hatasý: " + ex.Message });
            }
        }

        // Yardýmcý tip
        private class VehicleSlot
        {
            public int VehicleId { get; set; }
            public int CapacityKg { get; set; }
            public int RemainingKg { get; set; }
            public bool IsRented { get; set; }
            public double RentalCost { get; set; }
            public double FuelCostPerKm { get; set; }
        }

        private static bool PointsEqual(double[] a, double[] b)
        {
            if (a == null || b == null) return false;
            return Math.Abs(a[0] - b[0]) < 1e-6 && Math.Abs(a[1] - b[1]) < 1e-6;
        }

        // Nearest neighbor uses open connection to avoid connString redaction issues
        private async Task<List<int>> NearestNeighborOrder(List<int> stationIds, double startLat, double startLng, MySqlConnection conn)
        {
            var remaining = new HashSet<int>(stationIds);
            var order = new List<int>();
            double curLat = startLat, curLng = startLng;

            while (remaining.Count > 0)
            {
                int bestId = -1;
                double bestDist = double.MaxValue;
                foreach (var sid in remaining)
                {
                    var (lat, lng) = await GetStationLatLngFromOpenConnection(conn, sid);
                    double d = HaversineKm(curLat, curLng, lat, lng);
                    if (d < bestDist) { bestDist = d; bestId = sid; }
                }
                if (bestId == -1) break;
                order.Add(bestId);
                remaining.Remove(bestId);
                var (nlat, nlng) = await GetStationLatLngFromOpenConnection(conn, bestId);
                curLat = nlat; curLng = nlng;
            }
            return order;
        }

        private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
        {
            double R = 6371;
            double dLat = ToRad(lat2 - lat1);
            double dLon = ToRad(lon2 - lon1);
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private static double ToRad(double deg) => deg * Math.PI / 180.0;

        private static async Task<(double lat, double lng)> GetStationLatLngFromOpenConnection(MySqlConnection connection, int stationId)
        {
            const string sql = "SELECT Latitude, Longitude FROM Stations WHERE Id=@id LIMIT 1;";
            using var cmd = new MySqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@id", stationId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                throw new Exception($"Ýstasyon bulunamadý. Id={stationId}");
            }

            var lat = reader.GetDouble(reader.GetOrdinal("Latitude"));
            var lng = reader.GetDouble(reader.GetOrdinal("Longitude"));
            return (lat, lng);
        }

        private static async Task<(double lat, double lng)> GetStationLatLngAsync(string connString, int stationId)
        {
            if (string.IsNullOrWhiteSpace(connString)) throw new ArgumentNullException(nameof(connString));
            if (stationId <= 0) throw new ArgumentOutOfRangeException(nameof(stationId));

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

            var lat = reader.GetDouble(reader.GetOrdinal("Latitude"));
            var lng = reader.GetDouble(reader.GetOrdinal("Longitude"));
            return (lat, lng);
        }

        private async Task<(double distanceKm, List<double[]> polyline)> GetRouteFromOsrmAsync(double fromLat, double fromLng, double toLat, double toLng)
        {
            var osrmBase = _config["Routing:OsrmBaseUrl"] ?? "https://router.project-osrm.org";
            var url =
                $"{osrmBase.TrimEnd('/')}/route/v1/driving/" +
                $"{fromLng.ToString(CultureInfo.InvariantCulture)},{fromLat.ToString(CultureInfo.InvariantCulture)};" +
                $"{toLng.ToString(CultureInfo.InvariantCulture)},{toLat.ToString(CultureInfo.InvariantCulture)}" +
                "?overview=full&geometries=geojson&steps=false";
            using var resp = await _httpClient.GetAsync(url);
            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var routes = doc.RootElement.GetProperty("routes");
            if (routes.GetArrayLength() == 0) return (0.0, new List<double[]>());
            var first = routes[0];
            var distM = first.GetProperty("distance").GetDouble();
            var geom = first.GetProperty("geometry").GetProperty("coordinates");
            var pts = new List<double[]>();
            foreach (var c in geom.EnumerateArray())
            {
                var lng = c[0].GetDouble();
                var lat = c[1].GetDouble();
                pts.Add(new[] { lat, lng });
            }
            return (distM / 1000.0, pts);
        }
    }
}