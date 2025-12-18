using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using System.Text.Json; 

namespace YazLab2.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _config;

        public AuthController(IConfiguration config)
        {
            _config = config;
        }

        public class ResetPasswordModel
        {
            public string Email { get; set; }
            public string Kod { get; set; }
            public string YeniSifre { get; set; }
            public string YeniSifreTekrar { get; set; }
        }
        public class LoginModel
        {
            public string Email { get; set; }
            public string Password { get; set; }
        }
        public class RegisterModel
        {
            public string Isim { get; set; }
            public string Soyisim { get; set; }
            public string Email { get; set; }
            public string Password { get; set; }

        }
        public class ForgotModel
        {
            public string Email { get; set; }
            public string CaptchaToken { get; set; } // Yeni ekledik
        }

        [HttpPost("login")]
        public IActionResult GirisYap([FromBody] LoginModel veri)
        {
            if (veri == null || string.IsNullOrEmpty(veri.Email) || string.IsNullOrEmpty(veri.Password))
            {
                return BadRequest(new { mesaj = "Lütfen kullanıcı adı ve şifreyi giriniz." });
            }

            try
            {
                string connString = _config.GetConnectionString("MyDatabaseConnection");

                using (var connection = new MySqlConnection(connString))
                {
                    connection.Open();
                    string sql = "SELECT Id, Isim, Soyisim, Email FROM Users WHERE Email=@email AND Sifre=@sifre";

                    using (var cmd = new MySqlCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@email", veri.Email);
                        cmd.Parameters.AddWithValue("@sifre", sha256_hash(veri.Password));

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var userInfo = new
                                {
                                    Id = reader["Id"],
                                    Isim = reader["Isim"],
                                    Soyisim = reader["Soyisim"],
                                    Email = reader["Email"]
                                };

                                return Ok(new { mesaj = "Giriş Başarılı!", kullanici = userInfo });
                            }
                            else
                            {
                                return Unauthorized(new { mesaj = "Kullanıcı adı veya şifre hatalı!" });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { mesaj = "Sunucu Hatası: " + ex.Message });
            }
        }

        [HttpPost("register")]
        public IActionResult KayitOl([FromBody] RegisterModel veri)
        {
            if (veri == null
                || string.IsNullOrEmpty(veri.Isim)
                || string.IsNullOrEmpty(veri.Soyisim)
                || string.IsNullOrEmpty(veri.Email)
                || string.IsNullOrEmpty(veri.Password))
            {
                return BadRequest(new { mesaj = "Gecersiz veri!" });
            }
            try
            {
                string connString = _config.GetConnectionString("MyDatabaseConnection");

                using (var connection = new MySql.Data.MySqlClient.MySqlConnection(connString))
                {
                    connection.Open();
                    string query = "INSERT INTO Users (Isim, Soyisim, Email, Sifre, UserID) VALUES (@Isim, @Soyisim, @Email, @Password, 2)";
                    using (var command = new MySql.Data.MySqlClient.MySqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Isim", veri.Isim);
                        command.Parameters.AddWithValue("@Soyisim", veri.Soyisim);
                        command.Parameters.AddWithValue("@Email", veri.Email);
                        command.Parameters.AddWithValue("@Password", sha256_hash(veri.Password));
                        command.ExecuteNonQuery();
                    }
                }

                return Ok(new { mesaj = "Kayit Basarili! RAGH" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { mesaj = "Sunucu hatası: " + ex.Message });
            }
        }
        public static string sha256_hash(string value)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value));
                return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
            }
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> SifremiUnuttum([FromBody] ForgotModel veri)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    string secretKey = "6LdbSRcsAAAAALTSaXv6IxgO-3cAKNO3D-1X64wk";
                    string googleUrl = $"https://www.google.com/recaptcha/api/siteverify?secret={secretKey}&response={veri.CaptchaToken}";

                    var response = await client.GetStringAsync(googleUrl);

                    if (!response.Contains("\"success\": true"))
                    {
                        return BadRequest(new { mesaj = "Robot doğrulaması başarısız!" });
                    }
                }
            }
            catch
            {
                return StatusCode(500, new { mesaj = "Captcha sunucusuna ulaşılamadı." });
            }


            Random rnd = new Random();
            int uretilenKod = rnd.Next(100000, 999999);

            try
            {
                string connString = _config.GetConnectionString("MyDatabaseConnection");

                using (var connection = new MySqlConnection(connString))
                {
                    connection.Open();

                    string sql = "UPDATE Users SET DogrulamaKodu = @kod WHERE Email = @email";

                    using (var cmd = new MySqlCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@kod", uretilenKod.ToString());
                        cmd.Parameters.AddWithValue("@email", veri.Email);

                        int sonuc = cmd.ExecuteNonQuery();

                        if (sonuc == 0)
                        {
                            return BadRequest(new { mesaj = "Bu e-posta adresi sistemde kayıtlı değil." });
                        }
                    }
                }

                MailHelper.KodGonder(veri.Email, uretilenKod);
                return Ok(new { mesaj = "Doğrulama kodu e-posta adresinize gönderildi." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { mesaj = "Hata: " + ex.Message });
            }
        }
        
        [HttpPost("reset-password")]
        public IActionResult SifreYenile([FromBody] ResetPasswordModel veri)
        {
            try
            {
                if (veri.YeniSifre != veri.YeniSifreTekrar)
                {
                    return BadRequest(new { mesaj = "Yeni şifreler uyuşmuyor!" });
                }

                string connString = _config.GetConnectionString("MyDatabaseConnection");
                using (var connection = new MySqlConnection(connString))
                {
                    connection.Open();

                    string checkSql = "SELECT Id FROM Users WHERE Email=@email AND DogrulamaKodu=@kod";
                    using (var cmd = new MySqlCommand(checkSql, connection))
                    {
                        cmd.Parameters.AddWithValue("@email", veri.Email);
                        cmd.Parameters.AddWithValue("@kod", veri.Kod);

                        var sonuc = cmd.ExecuteScalar();

                        if (sonuc == null)
                        {
                            return BadRequest(new { mesaj = "Girdiğiniz kod hatalı!" });
                        }
                    }

                    string updateSql = "UPDATE Users SET Sifre=@yeniSifre, DogrulamaKodu=NULL WHERE Email=@email";
                    using (var cmd = new MySqlCommand(updateSql, connection))
                    {
                        cmd.Parameters.AddWithValue("@yeniSifre", sha256_hash(veri.YeniSifre));
                        cmd.Parameters.AddWithValue("@email", veri.Email);
                        cmd.ExecuteNonQuery();
                    }
                }

                return Ok(new { mesaj = "Şifreniz başarıyla değiştirildi. Giriş yapabilirsiniz." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { mesaj = "Hata: " + ex.Message });
            }
        }
    }
}