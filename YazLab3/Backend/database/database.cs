using MySql.Data.MySqlClient;

public static class Database
{
    public static void Baslat(string sunucuBaglantisi)
    {
        using (var baglanti = new MySqlConnection(sunucuBaglantisi))
        {
            baglanti.Open();

            var komut = new MySqlCommand("CREATE DATABASE IF NOT EXISTS KargoSistemi;", baglanti);
            komut.ExecuteNonQuery();

            komut.CommandText = "USE KargoSistemi;";
            komut.ExecuteNonQuery();

            string tabloSql = @"
                CREATE TABLE IF NOT EXISTS Users (
                    Id INT PRIMARY KEY AUTO_INCREMENT,
                    Isim VARCHAR(50) NOT NULL,
                    Soyisim VARCHAR(50) NOT NULL,
                    Sifre VARCHAR(100) NOT NULL,
                    Email VARCHAR(100) NOT NULL UNIQUE,
                    DogrulamaKodu VARCHAR(6),
                    UserID INT DEFAULT 2
                );

                CREATE TABLE IF NOT EXISTS UserIDs(
                    Id INT PRIMARY KEY,
                    UserType VARCHAR(20) NOT NULL
                );

                INSERT INTO UserIDs (Id, UserType)
                VALUES 
                    (1, 'Admin'),
                    (2, 'Kullanici')
                ON DUPLICATE KEY UPDATE UserType = VALUES(UserType);
                
                INSERT INTO Users (Isim, Soyisim, Email, Sifre, UserID)
                VALUES ('Admin', 'User', 'admin', '03ac674216f3e15c761ee1a5e255f067953623c8b388b4459e13f978d7c846f4', 1)
                ON DUPLICATE KEY UPDATE Email = VALUES(Email);

                CREATE TABLE IF NOT EXISTS Stations (
                    Id INT AUTO_INCREMENT PRIMARY KEY,
                    StationName VARCHAR(100) NOT NULL,
                    Latitude DOUBLE NOT NULL,
                    Longitude DOUBLE NOT NULL,
                    UNIQUE KEY uq_station_name (StationName)
                );
                INSERT INTO Stations (StationName, Latitude, Longitude)
                VALUES 
                    ('KOU MERKEZ', 40.8217, 29.9230),
                    ('Başiskele', 40.7165, 29.9272),
                    ('Çayırova', 40.8175, 29.3734),
                    ('Darıca', 40.7738, 29.4003),
                    ('Derince', 40.7562, 29.8308),
                    ('Dilovası', 40.7876, 29.5447),
                    ('Gebze', 40.8028, 29.4307),
                    ('Gölcük', 40.7170, 29.8202),
                    ('İzmit', 40.7654, 29.9408),
                    ('Kandıra', 41.0709, 30.1539),
                    ('Karamürsel', 40.6917, 29.6163),
                    ('Kartepe', 40.7533, 30.0253),
                    ('Körfez', 40.7761, 29.7364)
                ON DUPLICATE KEY UPDATE StationName = VALUES(StationName);

                /* =========================
                   YENİ: Araçlar / Kargolar / Seferler
                   ========================= */

                CREATE TABLE IF NOT EXISTS Vehicles (
                    Id INT PRIMARY KEY AUTO_INCREMENT,
                    Name VARCHAR(50) NOT NULL,
                    CapacityKg INT NOT NULL,
                    IsOwned TINYINT(1) NOT NULL,
                    RentalCost INT NOT NULL,
                    FuelCostPerKm DOUBLE NOT NULL, 
                    IsActive TINYINT(1) NOT NULL DEFAULT 1
                );

                /* İlk 3 araç: kiralama maliyeti yok (madde 10)
                   Kapasiteler: 500, 750, 1000
                   Yakıt maliyeti (km başına) şimdilik 0; yol maliyeti zaten km*1 (madde 9).
                   İsterseniz FuelCostPerKm ile ayrıca ek yakıt maliyeti de hesaplarız.
                */
                INSERT INTO Vehicles (Id, Name, CapacityKg, IsOwned, RentalCost, FuelCostPerKm, IsActive)
                VALUES
                    (1, 'Arac-1', 500, 1, 0, 0, 1),
                    (2, 'Arac-2', 750, 1, 0, 0, 1),
                    (3, 'Arac-3', 1000, 1, 0, 0, 1)
                ON DUPLICATE KEY UPDATE
                    Name = VALUES(Name),
                    CapacityKg = VALUES(CapacityKg),
                    IsOwned = VALUES(IsOwned),
                    RentalCost = VALUES(RentalCost),
                    FuelCostPerKm = VALUES(FuelCostPerKm),
                    IsActive = VALUES(IsActive);

                CREATE TABLE IF NOT EXISTS Shipments (
                    Id INT PRIMARY KEY AUTO_INCREMENT,
                    UserId INT NOT NULL,
                    StationId INT NOT NULL,
                    WeightKg INT NOT NULL,
                    Content VARCHAR(200) DEFAULT '',
                    ShipDate DATE NOT NULL,
                    Status VARCHAR(20) NOT NULL DEFAULT 'Pending',
                    Quantity INT NOT NULL DEFAULT 1,
                    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (UserId) REFERENCES Users(Id),
                    FOREIGN KEY (StationId) REFERENCES Stations(Id)
                );

                CREATE TABLE IF NOT EXISTS Trips (
                    Id INT PRIMARY KEY AUTO_INCREMENT,
                    TripDate DATE NOT NULL,
                    VehicleId INT NOT NULL,
                    TotalDistanceKm DOUBLE NOT NULL DEFAULT 0,
                    RoadCost DOUBLE NOT NULL DEFAULT 0,
                    RentalCost DOUBLE NOT NULL DEFAULT 0,
                    TotalCost DOUBLE NOT NULL DEFAULT 0,
                    Polyline LONGTEXT,
                    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (VehicleId) REFERENCES Vehicles(Id)
                );

                CREATE TABLE IF NOT EXISTS TripStops (
                    Id INT PRIMARY KEY AUTO_INCREMENT,
                    TripId INT NOT NULL,
                    StopOrder INT NOT NULL,
                    StationId INT NOT NULL,
                    PlannedLoadKg INT NOT NULL DEFAULT 0,
                    FOREIGN KEY (TripId) REFERENCES Trips(Id) ON DELETE CASCADE,
                    FOREIGN KEY (StationId) REFERENCES Stations(Id)
                );

                CREATE TABLE IF NOT EXISTS TripShipments (
                    TripId INT NOT NULL,
                    ShipmentId INT NOT NULL,
                    PRIMARY KEY (TripId, ShipmentId),
                    FOREIGN KEY (TripId) REFERENCES Trips(Id) ON DELETE CASCADE,
                    FOREIGN KEY (ShipmentId) REFERENCES Shipments(Id) ON DELETE CASCADE
                );
            ";

            komut.CommandText = tabloSql;
            komut.ExecuteNonQuery();

            // Mevcut tabloya Polyline kolonu ekle (eğer yoksa)
            try
            {
                var alterCmd = new MySqlCommand("ALTER TABLE Trips ADD COLUMN Polyline LONGTEXT;", baglanti);
                alterCmd.ExecuteNonQuery();
            }
            catch (MySqlException mex) when (mex.Number == 1060)
            {
                // 1060 = Duplicate column name -> kolon zaten var, devam et
            }

            // MySQL sürümleri CREATE INDEX IF NOT EXISTS'i desteklemeyebilir.
            // Bu yüzden index yaratma işlemlerini ayrı komutlarla deniyoruz,
            // ve zaten varsa hata kodu 1061'i yok sayıyoruz.
            var indexSqls = new[]
            {
                "CREATE INDEX IX_Shipments_ShipDate ON Shipments(ShipDate);",
                "CREATE INDEX IX_Shipments_UserId ON Shipments(UserId);",
                "CREATE INDEX IX_Shipments_StationId ON Shipments(StationId);",
                "CREATE INDEX IX_Trips_TripDate ON Trips(TripDate);",
                "CREATE INDEX IX_TripStops_TripId ON TripStops(TripId);",
                "CREATE INDEX IX_TripStops_StationId ON TripStops(StationId);"
            };

            foreach (var idxSql in indexSqls)
            {
                try
                {
                    using var idxCmd = new MySqlCommand(idxSql, baglanti);
                    idxCmd.ExecuteNonQuery();
                }
                catch (MySqlException mex) when (mex.Number == 1061)
                {
                    // 1061 = Duplicate key name -> index zaten var, yoksay
                }
            }
        }
    }
}