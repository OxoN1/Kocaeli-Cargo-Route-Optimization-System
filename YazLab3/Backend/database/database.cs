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

            //// VERI TABANINI KUR
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
                    Id INT PRIMARY KEY AUTO_INCREMENT,
                    StationName VARCHAR(100) NOT NULL,
                    Latitude DOUBLE NOT NULL,
                    Longitude DOUBLE NOT NULL
                );
                INSERT INTO Stations (StationName, Latitude, Longitude)
                VALUES 
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
                ";
            komut.CommandText = tabloSql;
            komut.ExecuteNonQuery();
        }
    }
}