import { useState } from "react";
import { useNavigate } from "react-router-dom";
import ReCAPTCHA from "react-google-recaptcha"; // Kütüphaneyi ekledik

function ForgotPassword() {
    const [email, setEmail] = useState("");
    const [captchaToken, setCaptchaToken] = useState(null); // Token'ı tutacak state
    const navigate = useNavigate();

    const handleGonder = async (e) => {
        e.preventDefault();

        // 1. Kontrol: Kutu işaretlendi mi?
        if (!captchaToken) {
            alert("Lütfen robot olmadığınızı doğrulayın!");
            return;
        }

        // Token'ı da backend'e gönderiyoruz
        const veri = { email: email, captchaToken: captchaToken };

        try {
            const cevap = await fetch("http://localhost:5000/api/auth/forgot-password", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(veri)
            });
            
            const sonuc = await cevap.json();

            if (cevap.ok) {
                alert("Kod gönderildi! " + sonuc.mesaj);
                navigate("/forgot-password/reset", { state: { email: email } });
            } else {
                alert("Hata: " + sonuc.mesaj);
            }
        } catch (err) {
            console.log(err);
            alert("Sunucu hatası.");
        }
    };

    return (
      <div className="auth-wrapper">
        <div className="register-container">
            <div className="forgot-card">
                <button className="back-link" onClick={() => navigate(-1)}>← Geri</button>
                <h2>Şifremi Unuttum</h2>
                
                <form onSubmit={handleGonder}>
                    <input 
                        type="email" 
                        className="input-field"
                        placeholder="E-posta" 
                        onChange={(e)=>setEmail(e.target.value)} 
                    />

                    <div style={{ margin: "20px 0", display:"flex", justifyContent:"center" }}>
                        <ReCAPTCHA
                            sitekey="6LdbSRcsAAAAAL8yTGodl4Pj00NYysRThOPVW19b" 
                            onChange={(token) => setCaptchaToken(token)} 
                        />
                    </div>

                    <button type="submit" className="submit-btn">Gönder</button>
                </form>
            </div>
        </div>
      </div>
    );
}
export default ForgotPassword;