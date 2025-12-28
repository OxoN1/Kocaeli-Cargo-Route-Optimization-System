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

        // Yeni: mode (unlimited | limited), maxVehicles (s�n�rl� senaryoda), objective (maxWeight|maxCount)
        [HttpPost("plan-next-day")]
        public async Task<IActionResult> PlanNextDay([FromQuery] string date = "", [FromQuery] string mode = "unlimited", [FromQuery] int maxVehicles = 0, [FromQuery] string objective = "maxWeight")
        {
            DateTime shipDate;
            if (string.IsNullOrWhiteSpace(date))
            {
                // Local time kullan (UTC değil)
                shipDate = DateTime.Now.Date.AddDays(1);
            }
            else if (!DateTime.TryParse(date, out shipDate))
            {
                return BadRequest(new { mesaj = "Ge�ersiz tarih format�." });
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
                    return StatusCode(500, new { mesaj = "Veritaban� ba�lant� dizesi yok." });
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
                            return StatusCode(500, new { mesaj = "Depo (KOU MERKEZ) bulunamad� ve ba�ka istasyon yok." });
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

                // Bekleyen kargolar (shipments) topla - koordinatlarla birlikte + Quantity
                var shipments = new List<(long Id, int StationId, int WeightKg, int UserId, double Lat, double Lng, int Quantity)>();
                using (var conn = new MySqlConnection(connString))
                {
                    await conn.OpenAsync();
                    const string sqlShip = @"SELECT s.Id, s.UserId, s.StationId, s.WeightKg, s.Quantity, st.Latitude, st.Longitude 
                        FROM Shipments s 
                        INNER JOIN Stations st ON s.StationId = st.Id 
                        WHERE s.ShipDate=@d AND s.Status='Pending';";
                    using var cmd = new MySqlCommand(sqlShip, conn);
                    cmd.Parameters.AddWithValue("@d", shipDate.ToString("yyyy-MM-dd"));
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var id = reader.GetInt64(reader.GetOrdinal("Id"));
                        var userId = reader.GetInt32(reader.GetOrdinal("UserId"));
                        var stationId = reader.GetInt32(reader.GetOrdinal("StationId"));
                        var weight = reader.GetInt32(reader.GetOrdinal("WeightKg"));
                        var quantity = reader.GetInt32(reader.GetOrdinal("Quantity"));
                        var lat = reader.GetDouble(reader.GetOrdinal("Latitude"));
                        var lng = reader.GetDouble(reader.GetOrdinal("Longitude"));
                        shipments.Add((id, stationId, weight, userId, lat, lng, quantity));
                    }
                }

                if (shipments.Count == 0)
                {
                    return Ok(new { mesaj = "Belirtilen tarihte bekleyen kargo yok.", plans = new List<PlanResult>() });
                }

                // Ara�lar� oku (active)
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
                // - unlimited: owned ara�lar� kullan, yetmezse kirala; ara� atamas� i�in First-Fit Decreasing (�nce a��r kargolar).
                // - limited: maxVehicles param ile greedy se�im: objective = maxWeight -> b�y�k a��rl�klar� �nce; maxCount -> k���k a��rl�klar� �nce.
                List<VehicleSlot> slots = new();
                // ba�lang��: veritaban�ndaki ara�lar
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

                // Helper: kiral�k ara� ekle - vehicles tablosuna INSERT yapar
                async Task<VehicleSlot> AddRentedSlotAsync(MySqlConnection connection)
                {
                    // Kiralık araç için vehicles tablosuna kayıt ekle
                    const string insertVehicle = @"INSERT INTO Vehicles (Name, CapacityKg, IsOwned, IsActive, RentalCost, FuelCostPerKm) 
                        VALUES (@name, @capacity, 0, 1, @rental, @fuel); SELECT LAST_INSERT_ID();";
                    
                    int newVehicleId;
                    using (var cmd = new MySqlCommand(insertVehicle, connection))
                    {
                        cmd.Parameters.AddWithValue("@name", $"Kiral�k Ara�-{DateTime.Now.Ticks}");
                        cmd.Parameters.AddWithValue("@capacity", 500);  // 500 kg sabit
                        cmd.Parameters.AddWithValue("@rental", 200.0);   // 200 birim kiralama ücreti
                        cmd.Parameters.AddWithValue("@fuel", 0.0);
                        var idObj = await cmd.ExecuteScalarAsync();
                        newVehicleId = Convert.ToInt32(idObj);
                    }

                    var newSlot = new VehicleSlot
                    {
                        VehicleId = newVehicleId,
                        CapacityKg = 500,
                        RemainingKg = 500,
                        IsRented = true,
                        RentalCost = 200.0,
                        FuelCostPerKm = 0
                    };
                    slots.Add(newSlot);
                    return newSlot;
                }

                // Planlama için kullanılacak shipment set - Quantity'ye göre genişlet
                // HER KARGO KAYDI QUANTITY KADAR KARGO İÇERİR, AMA AĞIRLIK WeightKg'DIR (Quantity ile çarpılmaz)
                var candidateShipments = new List<(long Id, int StationId, int WeightKg, int UserId, double Lat, double Lng, int Quantity)>();
                foreach (var sh in shipments)
                {
                    // Her kargo kaydını quantity bilgisiyle ekle
                    // NOT: Rotalamada WeightKg kullanılacak, Quantity sadece kaç adet olduğunu gösterir
                    candidateShipments.Add(sh);
                }

                if (mode.Equals("limited", StringComparison.OrdinalIgnoreCase))
                {
                    if (maxVehicles <= 0) return BadRequest(new { mesaj = "limited mod i�in maxVehicles parametresi girilmelidir." });
                    // maxVehicles kadar slot olu�tur (�nce DB ara�lar�n� kullan, sonra gerekiyorsa rented ile tamamla)
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
                    // Kiralık araç ekleme için veritabanı bağlantısı aç
                    if (initial.Count < maxVehicles)
                    {
                        using (var tempConn = new MySqlConnection(connString))
                        {
                            await tempConn.OpenAsync();
                            while (initial.Count < maxVehicles)
                            {
                                var rentedSlot = await AddRentedSlotAsync(tempConn);
                                initial.Add(rentedSlot);
                            }
                        }
                    }
                    slots = initial;

                    // se�im stratejisi - CO�RAF� + A�IRLIK
                    if (objective == "maxCount")
                    {
                        // Coğrafi sıralama (Longitude), sonra ağırlık (küçük önce)
                        candidateShipments = candidateShipments
                            .OrderBy(s => s.Lng)  // Batıdan doğuya
                            .ThenBy(s => s.WeightKg)
                            .ToList();
                    }
                    else // maxWeight
                    {
                        // Coğrafi sıralama (Longitude), sonra ağırlık (büyük önce)
                        candidateShipments = candidateShipments
                            .OrderBy(s => s.Lng)  // Batıdan doğuya
                            .ThenByDescending(s => s.WeightKg)
                            .ToList();
                    }
                }
                else // unlimited
                {
                    // unlimited modda sıralama önemli değil, Bölge Bazlı Algoritma kullanacağız
                    candidateShipments = candidateShipments.ToList();
                }

                // ========== KOCAELİ YOL BAZLI OPTİMİZASYON ALGORİTMASI ==========
                // Gerçek yol ağına göre 3 hat:
                // HAT 1 (D100): Çayırova, Gebze, Darıca, Dilovası, Körfez, Derince (İstanbul-Kocaeli ana yolu)
                // HAT 2 (KUZEY): Kandıra, Kartepe (Kuzey bölge)
                // HAT 3 (GÜNEY KIYI): Karamürsel, Gölcük, Başiskele, İzmit (Güney sahil yolu)

                var assignment = new Dictionary<VehicleSlot, List<(long ShipmentId, int StationId, int WeightKg, int UserId, int Quantity)>>();
                var cargoToVehicle = new Dictionary<long, VehicleSlot>();

                // İstasyon bazlı gruplama
                var stationGroups = candidateShipments
                    .GroupBy(s => s.StationId)
                    .Select(g => new {
                        StationId = g.Key,
                        Lat = g.First().Lat,
                        Lng = g.First().Lng,
                        TotalWeight = g.Sum(x => x.WeightKg),
                        Shipments = g.ToList()
                    })
                    .ToList();

                // İstasyon isimlerini al (database'den)
                var stationNames = new Dictionary<int, string>();
                using (var nameConn = new MySqlConnection(connString))
                {
                    await nameConn.OpenAsync();
                    using var nameCmd = new MySqlCommand("SELECT Id, StationName FROM Stations", nameConn);
                    using var nameReader = await nameCmd.ExecuteReaderAsync();
                    while (await nameReader.ReadAsync())
                    {
                        stationNames[nameReader.GetInt32(0)] = nameReader.GetString(1);
                    }
                }

                // Yol bazlı gruplama - istasyon ismine göre
                var d100Hatti = new List<string> { "Çayırova", "Gebze", "Darıca", "Dilovası", "Körfez", "Derince" };
                var kuzeyHatti = new List<string> { "Kandıra", "Kartepe" };
                var guneyKiyiHatti = new List<string> { "Karamürsel", "Gölcük", "Başiskele", "İzmit" };

                var d100Bolge = stationGroups.Where(s => {
                    var name = stationNames.ContainsKey(s.StationId) ? stationNames[s.StationId] : "";
                    return d100Hatti.Any(h => name.Contains(h));
                }).ToList();

                var kuzeyBolge = stationGroups.Where(s => {
                    var name = stationNames.ContainsKey(s.StationId) ? stationNames[s.StationId] : "";
                    return kuzeyHatti.Any(h => name.Contains(h));
                }).ToList();

                var guneyKiyiBolge = stationGroups.Where(s => {
                    var name = stationNames.ContainsKey(s.StationId) ? stationNames[s.StationId] : "";
                    return guneyKiyiHatti.Any(h => name.Contains(h));
                }).ToList();

                // Atanmamış istasyonları kontrol et ve en yakın bölgeye ekle
                var assignedIds = d100Bolge.Select(x => x.StationId)
                    .Concat(kuzeyBolge.Select(x => x.StationId))
                    .Concat(guneyKiyiBolge.Select(x => x.StationId))
                    .ToHashSet();

                foreach (var station in stationGroups.Where(s => !assignedIds.Contains(s.StationId)))
                {
                    // En yakın bölgeyi bul
                    double d100Dist = d100Bolge.Count > 0 ? d100Bolge.Min(s => CalculateDistance(station.Lat, station.Lng, s.Lat, s.Lng)) : double.MaxValue;
                    double kuzeyDist = kuzeyBolge.Count > 0 ? kuzeyBolge.Min(s => CalculateDistance(station.Lat, station.Lng, s.Lat, s.Lng)) : double.MaxValue;
                    double guneyDist = guneyKiyiBolge.Count > 0 ? guneyKiyiBolge.Min(s => CalculateDistance(station.Lat, station.Lng, s.Lat, s.Lng)) : double.MaxValue;

                    if (d100Dist <= kuzeyDist && d100Dist <= guneyDist)
                        d100Bolge = d100Bolge.Concat(new[] { station }).ToList();
                    else if (kuzeyDist <= guneyDist)
                        kuzeyBolge = kuzeyBolge.Concat(new[] { station }).ToList();
                    else
                        guneyKiyiBolge = guneyKiyiBolge.Concat(new[] { station }).ToList();
                }

                // Her bölgenin toplam ağırlığı
                int d100BolgeWeight = d100Bolge.Sum(s => s.TotalWeight);
                int kuzeyBolgeWeight = kuzeyBolge.Sum(s => s.TotalWeight);
                int guneyKiyiBolgeWeight = guneyKiyiBolge.Sum(s => s.TotalWeight);

                // Bölgeleri ağırlığa göre sırala (en ağır bölge en büyük aracı alır)
                var regions = new List<(string name, dynamic stations, int weight)>
                {
                    ("D100", d100Bolge, d100BolgeWeight),
                    ("KUZEY", kuzeyBolge, kuzeyBolgeWeight),
                    ("GUNEY_KIYI", guneyKiyiBolge, guneyKiyiBolgeWeight)
                };

                regions = regions.Where(r => r.weight > 0).OrderByDescending(r => r.weight).ToList();

                // Araçları kapasiteye göre büyükten küçüğe sırala
                slots = slots.OrderByDescending(s => s.CapacityKg).ToList();

                // Her bölgeyi uygun araca ata
                int vehicleIndex = 0;
                foreach (var region in regions)
                {
                    if (region.stations.Count == 0) continue;

                    // Kapasitesi yeterli araç bul
                    VehicleSlot? selectedVehicle = null;
                    
                    // Önce mevcut araçlardan uygun olanı bul
                    for (int i = vehicleIndex; i < slots.Count; i++)
                    {
                        if (slots[i].RemainingKg >= region.weight)
                        {
                            selectedVehicle = slots[i];
                            vehicleIndex = i + 1;
                            break;
                        }
                    }

                    // Araç bulunamadıysa ve unlimited modda, kiralık ekle
                    if (selectedVehicle == null && mode.Equals("unlimited", StringComparison.OrdinalIgnoreCase))
                    {
                        using (var tempConn = new MySqlConnection(connString))
                        {
                            await tempConn.OpenAsync();
                            selectedVehicle = await AddRentedSlotAsync(tempConn);
                        }
                        slots.Add(selectedVehicle);
                    }

                    if (selectedVehicle != null)
                    {
                        if (!assignment.ContainsKey(selectedVehicle))
                            assignment[selectedVehicle] = new List<(long, int, int, int, int)>();

                        foreach (var station in region.stations)
                        {
                            var stationId = (int)station.StationId;
                            var stationShipments = ((IEnumerable<(long Id, int StationId, int WeightKg, int UserId, double Lat, double Lng, int Quantity)>)station.Shipments).ToList();
                            
                            foreach (var sh in stationShipments)
                            {
                                // Kapasite kontrolü - eğer mevcut araç doluysa yeni araç bul/kirala
                                if (selectedVehicle.RemainingKg < sh.WeightKg)
                                {
                                    // Başka boş araç ara
                                    VehicleSlot? newVehicle = null;
                                    foreach (var slot in slots)
                                    {
                                        if (slot.RemainingKg >= sh.WeightKg)
                                        {
                                            newVehicle = slot;
                                            break;
                                        }
                                    }

                                    // Uygun araç yoksa ve unlimited modda kiralık ekle
                                    if (newVehicle == null && mode.Equals("unlimited", StringComparison.OrdinalIgnoreCase))
                                    {
                                        using (var tempConn = new MySqlConnection(connString))
                                        {
                                            await tempConn.OpenAsync();
                                            newVehicle = await AddRentedSlotAsync(tempConn);
                                        }
                                        slots.Add(newVehicle);
                                    }

                                    // Yeni araç bulunamadıysa (limited modda) hata ver
                                    if (newVehicle == null)
                                    {
                                        return BadRequest(new { mesaj = $"Kargo {sh.Id} için yeterli kapasite yok. Limited modda daha fazla araç kiralanamaz." });
                                    }

                                    selectedVehicle = newVehicle;
                                    
                                    if (!assignment.ContainsKey(selectedVehicle))
                                        assignment[selectedVehicle] = new List<(long, int, int, int, int)>();
                                }

                                assignment[selectedVehicle].Add((sh.Id, sh.StationId, sh.WeightKg, sh.UserId, sh.Quantity));
                                selectedVehicle.RemainingKg -= sh.WeightKg;
                                cargoToVehicle[sh.Id] = selectedVehicle;
                            }
                        }
                    }
                }

                // �imdi her slot i�in rota s�rala (NearestNeighbor using open connection) ve maliyeti hesapla
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
                            
                            // Araç ilk istasyondan (en uzak) başlıyor - Depodan çıkmıyor
                            // Sadece istasyonlar arası mesafeler hesaplanıyor

                            // Ara istasyonlar: Nearest Neighbor sırası
                            for (int i = 0; i < ordered.Count - 1; i++)
                            {
                                var (fromLat, fromLng) = await GetStationLatLngFromOpenConnection(conn, ordered[i]);
                                var (toLat, toLng) = await GetStationLatLngFromOpenConnection(conn, ordered[i + 1]);
                                var routeInfo = await GetRouteFromOsrmAsync(fromLat, fromLng, toLat, toLng);
                                totalKm += routeInfo.distanceKm;
                                if (routeInfo.polyline.Count > 0)
                                {
                                    if (fullPoints.Count > 0 && !PointsEqual(fullPoints.Last(), routeInfo.polyline.First()))
                                        fullPoints.AddRange(routeInfo.polyline);
                                    else if (fullPoints.Count > 0)
                                        fullPoints.AddRange(routeInfo.polyline.Skip(1));
                                    else
                                        fullPoints.AddRange(routeInfo.polyline);
                                }
                            }

                            // Son mesafe: Son istasyondan KOU MERKEZ'e dönüş (varış noktası)
                            if (ordered.Count > 0)
                            {
                                var (lastLat, lastLng) = await GetStationLatLngFromOpenConnection(conn, ordered[ordered.Count - 1]);
                                var returnRoute = await GetRouteFromOsrmAsync(lastLat, lastLng, depotLat, depotLng);
                                totalKm += returnRoute.distanceKm;
                                if (returnRoute.polyline.Count > 0)
                                {
                                    if (fullPoints.Count > 0 && !PointsEqual(fullPoints.Last(), returnRoute.polyline.First()))
                                        fullPoints.AddRange(returnRoute.polyline);
                                    else if (fullPoints.Count > 0)
                                        fullPoints.AddRange(returnRoute.polyline.Skip(1));
                                    else
                                        fullPoints.AddRange(returnRoute.polyline);
                                }
                            }

                            double roadCost = totalKm * 1.0;
                            double extraFuel = slot.FuelCostPerKm * totalKm;
                            double rentC = slot.IsRented ? slot.RentalCost : 0.0;
                            double totalCost = roadCost + extraFuel + rentC;

                            // Polyline'ı JSON string'e çevir
                            string polylineJson = System.Text.Json.JsonSerializer.Serialize(fullPoints);

                            // Insert Trip
                            const string insertTrip = @"INSERT INTO Trips (TripDate, VehicleId, TotalDistanceKm, RoadCost, RentalCost, TotalCost, Polyline) 
VALUES (@d, @vehicleId, @dist, @road, @rental, @total, @polyline); SELECT LAST_INSERT_ID();";
                            long tripId;
                            using (var tripCmd = new MySqlCommand(insertTrip, conn, (MySqlTransaction)tran))
                            {
                                tripCmd.Parameters.AddWithValue("@d", shipDate.ToString("yyyy-MM-dd"));
                                tripCmd.Parameters.AddWithValue("@vehicleId", slot.VehicleId);
                                tripCmd.Parameters.AddWithValue("@dist", totalKm);
                                tripCmd.Parameters.AddWithValue("@road", roadCost);
                                tripCmd.Parameters.AddWithValue("@rental", rentC);
                                tripCmd.Parameters.AddWithValue("@total", totalCost);
                                tripCmd.Parameters.AddWithValue("@polyline", polylineJson);
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

                return Ok(new { mesaj = "Plan ba�ar�yla olu�turuldu.", plans = planResults });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { mesaj = "Sunucu hatas�: " + ex.Message });
            }
        }

        // Yard�mc� tip
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

        // Furthest First with Fixed End: En uzak istasyondan başla, KOU MERKEZ'de bitir
        private async Task<List<int>> NearestNeighborOrder(List<int> stationIds, double depotLat, double depotLng, MySqlConnection conn)
        {
            if (stationIds.Count == 0) return new List<int>();
            if (stationIds.Count == 1) return new List<int> { stationIds[0] };

            var remaining = new HashSet<int>(stationIds);
            var order = new List<int>();

            // 1. Depodan EN UZAK istasyonu bul (GERÇEK YOL MESAFESİNE GÖRE - OSRM)
            int furthestId = -1;
            double furthestDist = 0;
            foreach (var sid in remaining)
            {
                var (lat, lng) = await GetStationLatLngFromOpenConnection(conn, sid);
                // OSRM ile gerçek yol mesafesini hesapla
                var routeInfo = await GetRouteFromOsrmAsync(depotLat, depotLng, lat, lng);
                double d = routeInfo.distanceKm;
                Console.WriteLine($"[DEBUG] İstasyon {sid} -> Depodan uzaklık: {d:F2} km");
                if (d > furthestDist) { furthestDist = d; furthestId = sid; }
            }
            Console.WriteLine($"[DEBUG] En uzak istasyon seçildi: {furthestId} ({furthestDist:F2} km)");

            // 2. En uzak istasyonu başlangıç noktası yap
            order.Add(furthestId);
            remaining.Remove(furthestId);
            var (curLat, curLng) = await GetStationLatLngFromOpenConnection(conn, furthestId);

            // 3. Nearest Neighbor ile devam et (GERÇEK YOL MESAFESİ KULLAN)
            while (remaining.Count > 0)
            {
                int bestId = -1;
                double bestDist = double.MaxValue;
                foreach (var sid in remaining)
                {
                    var (lat, lng) = await GetStationLatLngFromOpenConnection(conn, sid);
                    // OSRM ile gerçek yol mesafesini hesapla
                    var routeInfo = await GetRouteFromOsrmAsync(curLat, curLng, lat, lng);
                    double d = routeInfo.distanceKm;
                    if (d < bestDist) { bestDist = d; bestId = sid; }
                }
                if (bestId == -1) break;
                Console.WriteLine($"[DEBUG] Bir sonraki en yakın istasyon: {bestId} ({bestDist:F2} km)");
                order.Add(bestId);
                remaining.Remove(bestId);
                var (nlat, nlng) = await GetStationLatLngFromOpenConnection(conn, bestId);
                curLat = nlat; curLng = nlng;
            }

            Console.WriteLine($"[DEBUG] Final rota sırası: {string.Join(" → ", order)}");

            // 4. Rota: En uzak istasyon → ... → Son istasyon (KOU MERKEZ'e en yakın)
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

        // Haversine formula ile iki nokta arasındaki mesafe (km)
        private static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371.0; // Dünya yarıçapı (km)
            var dLat = ToRad(lat2 - lat1);
            var dLon = ToRad(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private static async Task<(double lat, double lng)> GetStationLatLngFromOpenConnection(MySqlConnection connection, int stationId)
        {
            const string sql = "SELECT Latitude, Longitude FROM Stations WHERE Id=@id LIMIT 1;";
            using var cmd = new MySqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@id", stationId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                throw new Exception($"�stasyon bulunamad�. Id={stationId}");
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
                throw new Exception($"�stasyon bulunamad�. Id={stationId}");
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

        // Yeni endpoint: tüm oluşturulan rotaları listele
        [HttpGet("all-routes")]
        public async Task<IActionResult> GetAllRoutes()
        {
            try
            {
                string connString = _config.GetConnectionString("MyDatabaseConnection");
                if (string.IsNullOrWhiteSpace(connString))
                {
                    connString = _config.GetConnectionString("MyConnection") ?? connString;
                }

                if (string.IsNullOrWhiteSpace(connString))
                {
                    return StatusCode(500, new { mesaj = "Veritabanı bağlantı dizesi yok." });
                }

                var routes = new List<object>();

                using (var conn = new MySqlConnection(connString))
                {
                    await conn.OpenAsync();

                    // Trips tablosundan tüm seferleri çek
                    const string sqlTrips = @"
                        SELECT Id, TripDate, VehicleId, TotalDistanceKm, RoadCost, RentalCost, TotalCost, CreatedAt
                        FROM Trips
                        ORDER BY CreatedAt DESC;";

                    using var cmdTrips = new MySqlCommand(sqlTrips, conn);
                    using var rdrTrips = await cmdTrips.ExecuteReaderAsync();

                    var tripsList = new List<(int TripId, DateTime TripDate, int VehicleId, double DistanceKm, double RoadCost, double RentalCost, double TotalCost, DateTime CreatedAt)>();

                    while (await rdrTrips.ReadAsync())
                    {
                        var tripId = rdrTrips.GetInt32(rdrTrips.GetOrdinal("Id"));
                        var tripDate = rdrTrips.GetDateTime(rdrTrips.GetOrdinal("TripDate"));
                        var vehicleId = rdrTrips.GetInt32(rdrTrips.GetOrdinal("VehicleId"));
                        var distanceKm = rdrTrips.GetDouble(rdrTrips.GetOrdinal("TotalDistanceKm"));
                        var roadCost = rdrTrips.GetDouble(rdrTrips.GetOrdinal("RoadCost"));
                        var rentalCost = rdrTrips.GetDouble(rdrTrips.GetOrdinal("RentalCost"));
                        var totalCost = rdrTrips.GetDouble(rdrTrips.GetOrdinal("TotalCost"));
                        var createdAt = rdrTrips.GetDateTime(rdrTrips.GetOrdinal("CreatedAt"));

                        tripsList.Add((tripId, tripDate, vehicleId, distanceKm, roadCost, rentalCost, totalCost, createdAt));
                    }

                    rdrTrips.Close();

                    // Her sefer için kargo bilgilerini çek
                    foreach (var trip in tripsList)
                    {
                        const string sqlShipments = @"
                            SELECT s.Id, s.StationId, s.WeightKg, st.StationName
                            FROM TripShipments ts
                            INNER JOIN Shipments s ON ts.ShipmentId = s.Id
                            INNER JOIN Stations st ON s.StationId = st.Id
                            WHERE ts.TripId = @tripId;";

                        var shipments = new List<object>();

                        using var cmdShip = new MySqlCommand(sqlShipments, conn);
                        cmdShip.Parameters.AddWithValue("@tripId", trip.TripId);
                        using var rdrShip = await cmdShip.ExecuteReaderAsync();

                        while (await rdrShip.ReadAsync())
                        {
                            shipments.Add(new
                            {
                                shipmentId = rdrShip.GetInt64(rdrShip.GetOrdinal("Id")),
                                stationId = rdrShip.GetInt32(rdrShip.GetOrdinal("StationId")),
                                stationName = rdrShip.GetString(rdrShip.GetOrdinal("StationName")),
                                weightKg = rdrShip.GetInt32(rdrShip.GetOrdinal("WeightKg"))
                            });
                        }

                        rdrShip.Close();

                        // TripStops tablosundan doğru istasyon sırasını çek (istasyon isimleriyle)
                        const string sqlStops = @"
                            SELECT ts.StationId, st.StationName
                            FROM TripStops ts
                            INNER JOIN Stations st ON ts.StationId = st.Id
                            WHERE ts.TripId = @tripId
                            ORDER BY ts.StopOrder;";

                        var stationOrderList = new List<string>();
                        using var cmdStops = new MySqlCommand(sqlStops, conn);
                        cmdStops.Parameters.AddWithValue("@tripId", trip.TripId);
                        using var rdrStops = await cmdStops.ExecuteReaderAsync();

                        while (await rdrStops.ReadAsync())
                        {
                            stationOrderList.Add(rdrStops.GetString(rdrStops.GetOrdinal("StationName")));
                        }

                        rdrStops.Close();

                        // Polyline bilgisi tabloda yok, boş liste gönder
                        var polylineList = new List<double[]>();

                        routes.Add(new
                        {
                            tripId = trip.TripId,
                            tripDate = trip.TripDate.ToString("yyyy-MM-dd"),
                            vehicleId = trip.VehicleId,
                            distanceKm = trip.DistanceKm,
                            roadCost = trip.RoadCost,
                            rentalCost = trip.RentalCost,
                            totalCost = trip.TotalCost,
                            stationOrder = stationOrderList,
                            polyline = polylineList,
                            createdAt = trip.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                            shipments
                        });
                    }
                }

                return Ok(new
                {
                    mesaj = $"{routes.Count} rota bulundu.",
                    routes
                });
            }
            catch (MySqlException mysqlEx)
            {
                // MySQL özel hatası (örn: tablo yok)
                return StatusCode(500, new { mesaj = "Veritabanı hatası.", detay = mysqlEx.Message, kod = mysqlEx.Number });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { mesaj = "Rotalar listelenirken hata oluştu.", detay = ex.Message, tip = ex.GetType().Name });
            }
        }
    }
}