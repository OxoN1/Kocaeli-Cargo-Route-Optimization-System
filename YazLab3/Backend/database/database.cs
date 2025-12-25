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
                    ('KOU MERKEZ',40.82345000,29.92550000),
                    ('Başiskele', 40.64574000, 29.90015000),
                    ('Çayırova', 40.82784000, 29.39014000),
                    ('Darıca', 40.76780000, 29.37126000),
                    ('Derince', 40.75694000, 29.81472000),
                    ('Dilovası', 40.77972000, 29.53500000),
                    ('Gebze', 40.80276000, 29.43068000),
                    ('Gölcük', 40.70323000, 29.87216000),
                    ('İzmit', 40.77521000, 29.94624000),
                    ('Kandıra', 41.07000000, 30.15262000),
                    ('Karamürsel', 40.69129000, 29.61649000),
                    ('Kartepe', 40.75246000, 30.02787000),
                    ('Körfez', 40.76704000, 29.78275000)
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