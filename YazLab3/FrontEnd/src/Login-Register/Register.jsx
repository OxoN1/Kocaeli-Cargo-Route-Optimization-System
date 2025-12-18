import { useState } from "react";
import { useNavigate } from "react-router-dom";
import './Register.css'; // veya Register.css

function Register() {
  const [kullaniciAdi, setKullaniciAdi] = useState("");
  const [sifre, setSifre] = useState("");
  const [email, setEmail] = useState("");
  const [isim, setIsim] = useState("");
  const [soyisim, setSoyisim] = useState("");

  const navigate = useNavigate();

  const kayitOl = async () => {
    const veri = { username: kullaniciAdi, password: sifre, email: email,  isim: isim, soyisim: soyisim };
    try {
      const cevap = await fetch("http://localhost:5000/api/auth/register", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(veri),
      });

      if (cevap.ok) {
        alert("Kayit basarili!");
        navigate("/");
      } else {
        alert("Kayit basarisiz!");
      }
    } catch (error) {
      console.error("Hata:", error);
      alert("Kayit sirasinda bir hata olustu.");
    }
  };

    return (
    <div className="auth-wrapper">
        <div className="register-container">
          <button className="back-button" onClick={() => navigate(-1)}>← Geri</button>
          <h2>Kayit Ol</h2>
          <div className="yan-yana">
          <input
            type="text"
            placeholder="İsim"
            value={isim}
            onChange={(e) => setIsim(e.target.value)}
          />
          <input
            type="text"
            placeholder="Soyisim"
            value={soyisim}
            onChange={(e) => setSoyisim(e.target.value)}
          />
          </div>
          <input
            type="text"
            placeholder="Kullanici Adi"
            value={kullaniciAdi}
            onChange={(e) => setKullaniciAdi(e.target.value)}
          />
          <input
            type="email"
            placeholder="E-posta"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
          />
          <input
            type="password"
            placeholder="Şifre"
            value={sifre}
            onChange={(e) => setSifre(e.target.value)}
          />
          <button onClick={kayitOl}>Kayit Ol</button>
          </div>
     </div>
  );
}

export default Register;