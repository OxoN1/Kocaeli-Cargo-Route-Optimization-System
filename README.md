# Kocaeli Kargo Rota Optimizasyon Sistemi

## Özet
Bu proje, Kocaeli bölgesinde kargo dağıtımı yapan bir simülasyon/iş uygulamasıdır. Backend ASP.NET Core (C#) ile yazılmıştır, frontend React + Vite kullanır ve MySQL veritabanı ile çalışır. Amaç: günlük kargo taleplerini alıp araçlara en uygun şekilde atamak, rotaları optimize etmek ve toplam maliyeti minimize etmektir.

## Başlıca Özellikler
- Kargo talep oluşturma (kullanıcı)
- Admin paneli: test senaryoları, araç yönetimi, istasyon yönetimi
- Rota optimizasyonu: bölge/hatt bazlı ve kapasite-kontrollü atama
- Otomatik kiralık araç ekleme (unlimited mod)
- Harita görselleştirme (Leaflet)
- Test senaryoları (4 hazır senaryo)

## Mimari / Dosya Yapısı (özet)
- `YazLab3/` — çözüm kökü
  - `Backend/` — ASP.NET Core API
    - `RoutingControllers/TripPlannerController.cs` — rota planlama ve optimizasyon
    - `ShipmentControllers/shipmentControllers.cs` — kargo ekleme, istatistikler
    - `database/database.cs` — DB şeması ve başlangıç verileri
  - `FrontEnd/` — React + Vite uygulaması
    - `src/Menu/menu.jsx` — ana UI, admin paneli, test butonları

## Gereksinimler
- .NET 8 veya daha yeni
- Node.js 18+ (ve npm)
- MySQL Server (local veya uzak)

## Hızlı Kurulum
1. Veritabanı bağlantısını `Backend/appsettings.json` içinde ayarlayın (örnek):

```json
{
  "ConnectionStrings": {
    "MyDatabaseConnection": "server=127.0.0.1;user=root;password=pass;database=KargoSistemi;"
  }
}
```

2. Backend çalıştırma

```bash
cd YazLab3
dotnet build
dotnet run --project YazLab3/Backend
```

Varsayılan olarak API `http://localhost:5000` üzerinde çalışır.

3. Frontend çalıştırma

```bash
cd YazLab3/FrontEnd
npm install
npm run dev
```

Frontend tipik olarak `http://localhost:5173` üzerinde çalışır.

## Kullanım
- Normal kullanıcı olarak: uygulamaya giriş yaptıktan sonra kargo ekleyin (istasyon, ağırlık, adet, içerik).
- Admin olarak: Admin panelinden test senaryolarını yükleyebilir, araç/istasyon yönetimi yapabilirsiniz.
- Rota planlamak için: Admin panelde "Seferi Planla" butonunu kullanın.

## Test Senaryoları
Admin panelinde dört hazır senaryo vardır (Senaryo 1-4). Her senaryo tek tıklamayla db'ye yüklenir ve ardından rota planlaması çalıştırılarak sonuçlar karşılaştırılabilir.

Senaryoların kısa açıklaması:
- Senaryo 1: Bölgeye yayılmış ortalama yükler (genel test)
- Senaryo 2: Bazı istasyonlarda yüksek yoğunluk
- Senaryo 3: Çok ağır tekil kargolar (bölünmüş ekleme davranışını test eder)
- Senaryo 4: Kıyı/kuzey ağırlıklı yük dağılımı

## Önemli Notlar ve Davranışlar
- Kargo eklerken `Quantity` ve `WeightKg` girilir. Eğer örneğin `Quantity=3, WeightKg=700` girilirse backend her biri için yaklaşık `700/3` kg olacak şekilde 3 ayrı kayıt oluşturur (kalanlar eşit dağıtılır).
- Planlayıcı unlimited modda (kiralama izinliyse) ihtiyaç oldukça yeni araç kiralar; limited modda ise sadece mevcut araçlarla çalışır ve kapasite yetersizse hata döner.
- Rotalama; öncelikle istasyonları tanımlı hatlara/bölgelere ayırır, bölgelere göre ağırlık hesapları ve araç kapasitesi kullanılarak atama yapar.

## Yaygın Sorunlar ve Çözümleri
- "Unknown column 'station_id'" hatası: `TripPlannerController` veya diğer sorgularda `Stations` tablosu sütun isimleri doğru olmalıdır (`Id`, `StationName`, `Latitude`, `Longitude`).
- `GetConnectionString` nullable uyarıları: `appsettings.json` içindeki connection string anahtarının adı `MyDatabaseConnection` olmalıdır.
- Araç kapasitesi aşılıyorsa: unlimited mod açıksa sistem yeni araç kiralayacak; değilse gönderilen verileri (adet/ağırlık) kontrol edin.

## Geliştirme ve Test
- Kodda rota algoritması üzerinde denemeler yaptım; `TripPlannerController.cs` içinde alternatif yaklaşımlar (kümeleme, Clarke-Wright, bölge bazlı) bulunuyor.
- Frontend'de admin paneline test butonları eklendi: `FrontEnd/src/Menu/menu.jsx`.

## API (seçme örnek endpointler)
- `POST /api/shipments` — Kargo oluşturma (body: email, stationId, weightKg, content, quantity)
- `POST /api/tripplanner/plan-next-day` — Ertesi gün için rota planla (query: mode=unlimited|limited, maxVehicles)
- `GET /api/shipments/station-stats` — İstasyon bazlı kargo istatistikleri

## Katkı
- Fork -> feature branch -> PR. Kod formatlamaya, unit test eklemeye ve açık issue'ları kapatmaya yardımcı olun.

## Lisans
MIT

---

Hazırlayan: Proje Katılımcısı

İstersen README'yi İngilizce'ye çevirebilirim ya da bölümler ekleyip ayrıntılandırabilirim (ör. ER diyagramı, endpoint listesi, konfig örnekleri).