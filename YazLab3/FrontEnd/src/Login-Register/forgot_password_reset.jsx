import { useState } from "react";
import { useNavigate, useLocation } from "react-router-dom";
import './ForgotPassword.css'; // Tasarımın bozulmaması için aynı CSS'i kullanıyoruz

function ForgotPasswordReset() {
    const navigate = useNavigate();
    const location = useLocation();

    // Bir önceki sayfadan gönderilen e-posta adresini yakalıyoruz.
    // Eğer kullanıcı linke direkt tıkladıysa hata vermesin diye || "" ekledik.
    const email = location.state?.email || ""; 

    const [kod, setKod] = useState("");
    const [yeniSifre, setYeniSifre] = useState("");
    const [yeniSifreTekrar, setYeniSifreTekrar] = useState("");

    const handleSifreGuncelle = async (e) => {
        e.preventDefault();

        // Basit kontrol
        if (!kod || !yeniSifre) {
            alert("Lütfen kodu ve yeni şifrenizi giriniz.");
            return;
        }

        const veri = { 
            email: email, 
            kod: kod, 
            yeniSifre: yeniSifre,
            yeniSifreTekrar: yeniSifreTekrar
        };

        try {
            const cevap = await fetch("http://localhost:5000/api/auth/reset-password", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(veri)
            });

            const sonuc = await cevap.json();

            if (cevap.ok) {
                alert("Başarılı! " + sonuc.mesaj);
                // İşlem bitince Giriş ekranına gönder
                navigate("/"); 
            } else {
                alert("Hata: " + sonuc.mesaj);
            }
        } catch (error) {
            console.error(error);
            alert("Sunucuya bağlanılamadı!");
        }
    };

    return (
        <div className="register-container">
            <div className="forgot-card">
                <h2>Yeni Şifre Belirle</h2>
                
                {/* Kullanıcıya hangi e-posta için işlem yaptığını hatırlatalım */}
                <p style={{color:"#aaa", fontSize:"14px", textAlign:"center"}}>
                    {email ? `${email} adresine gelen kodu giriniz.` : "Lütfen doğrulama kodunu giriniz."}
                </p>

                <form onSubmit={handleSifreGuncelle}>
                    {/* 1. Kod Girişi */}
                    <input 
                        type="text" 
                        className="input-field" 
                        placeholder="Doğrulama Kodu (Örn: 123456)" 
                        value={kod}
                        onChange={(e) => setKod(e.target.value)}
                        maxLength={6} // Kod zaten 6 haneli
                    />

                    <input 
                        type="password" 
                        className="input-field" 
                        placeholder="Yeni Şifreniz" 
                        value={yeniSifre}
                        onChange={(e) => setYeniSifre(e.target.value)}
                    />
                    <input type="password" 
                        className="input-field" 
                        placeholder="Yeni Şifreniz (Tekrar)" 
                        value={yeniSifreTekrar}
                        onChange={(e) => setYeniSifreTekrar(e.target.value)}
                    />

                    <button type="submit" className="submit-btn">Şifreyi Güncelle</button>
                </form>
            </div>
        </div>
    );
}

export default ForgotPasswordReset;