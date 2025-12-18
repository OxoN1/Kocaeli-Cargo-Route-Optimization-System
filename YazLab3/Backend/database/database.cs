using MySql.Data.MySqlClient; 
public static class Database
{
    public static void Baslat(string sunucuBaglantisi)
    {
        
        
        using (var baglanti = new MySqlConnection(sunucuBaglantisi))
        {
           
        }
    }
}