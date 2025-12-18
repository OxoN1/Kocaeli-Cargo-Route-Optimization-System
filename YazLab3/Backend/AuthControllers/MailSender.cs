using System.Net;
using System.Net.Mail;

public class MailHelper
{
    public static void KodGonder(string aliciEmail, int kod)
    {
        
        string gonderenEmail = "sosyalguvenlik489@gmail.com"; 
        string gonderenSifre = "tjji vikw briu nabz"; 
        
        try
        {
            
            MailMessage mail = new MailMessage();
            mail.From = new MailAddress(gonderenEmail, "Yazlab Destek");
            mail.To.Add(aliciEmail);
            mail.Subject = "Şifre Sıfırlama Kodunuz";
            
            // HTML Tasarımı (Kutucuk içinde şık görünsün)
            mail.Body = $@"
                <div style='font-family: Helvetica, Arial, sans-serif; max-width: 500px; margin: auto; padding: 20px; border: 1px solid #ddd; border-radius: 10px;'>
                    <h2 style='color: #333; text-align: center;'>Şifre Sıfırlama İsteği</h2>
                    <p style='font-size: 16px; color: #555;'>Merhaba,</p>
                    <p style='font-size: 16px; color: #555;'>Hesabınızın şifresini sıfırlamak için aşağıdaki kodu kullanın:</p>
                    
                    <div style='background-color: #f4f4f4; padding: 15px; text-align: center; border-radius: 5px; margin: 20px 0;'>
                        <span style='font-size: 32px; font-weight: bold; letter-spacing: 5px; color: #0a84ff;'>{kod}</span>
                    </div>

                    <p style='font-size: 14px; color: #999; text-align: center;'>Bu kodu siz talep etmediyseniz, bu e-postayı görmezden gelin.</p>
                </div>
            ";
            
            mail.IsBodyHtml = true; 

            
            SmtpClient smtp = new SmtpClient("smtp.gmail.com", 587);
            smtp.Credentials = new NetworkCredential(gonderenEmail, gonderenSifre);
            smtp.EnableSsl = true; // Güvenli bağlantı ŞART

            
            smtp.Send(mail);
        }
        catch (Exception ex)
        {
            
            throw new Exception("Mail Gönderme Hatası: " + ex.Message);
        }
    }
}