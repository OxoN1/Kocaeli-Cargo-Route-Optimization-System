import { useState } from 'react'
import './App.css' 
import { Link, useNavigate } from 'react-router-dom' // Kayıt sayfasına yönlendirme için

function Login() {
  const [email, setEmail] = useState("")
  const [sifre, setSifre] = useState("")
  const navigate = useNavigate()

  const girisYap = async () => {
    const veri = {
      email: email,
      password: sifre
    };

    try {
      const adres = 'http://localhost:5000/api/auth/login';
      
      const cevap = await fetch(adres, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(veri)
      });

      if (cevap.ok) {
        const data = await cevap.json();

        // Sunucudan dönen kullanıcı objesini al
        const kullanici = data.kullanici || data.user || {};

        // Email'i güvenli şekilde sakla (sadece email saklanacak)
        const userEmail = kullanici.email || kullanici.Email || email;
        localStorage.setItem('userEmail', userEmail);

        // Username saklamıyoruz burada — Home sayfası / Profile bileşeni email üzerinden veriyi çekecek
        alert("Giriş Başarılı 🎉");
        navigate('/home')
      } else {
        const err = await cevap.json().catch(()=>null);
        alert("HATA: " + (err?.mesaj || "Kullanıcı adı veya şifre yanlış!"));
      }

    } catch (error) {
      console.error(error);
      alert("SUNUCUYA BAĞLANILAMADI!");
    }
  }

  return (
    <div className="auth-wrapper">
      <div className="ana-kutu">
        <h1>Giriş Yap</h1>
        
        <div className="form-elemani">
          <label>Email:</label>
          <input 
            type="text" 
            placeholder="admin@example.com"
            onChange={(e) => setEmail(e.target.value)} 
          />
        </div>

        <div className="form-elemani">
          <label>Şifre:</label>
          <input 
            type="password" 
            placeholder="12345"
            onChange={(e) => setSifre(e.target.value)} 
          />
        </div>

        <button className="giris-butonu" onClick={girisYap}>Giriş Yap</button>

        <p style={{ marginTop: '15px' }}>
          Hesabın yok mu? <Link to="/register" style={{ fontWeight: 'bold' }}>Kayit Ol</Link>
        </p>
        <p style={{marginTop:'15px'}}>
          Şifremi unuttum! <Link to="/forgot_password" style={{ fontWeight: 'bold' }}>Şifre Sifirlama</Link>
        </p>
      </div>
    </div>
  )
}

export default Login