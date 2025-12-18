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
                ";
            komut.CommandText = tabloSql;
            komut.ExecuteNonQuery();
        }
    }
}