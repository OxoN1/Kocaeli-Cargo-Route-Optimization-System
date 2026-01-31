# Kocaeli Kargo Rota Optimizasyon Sistemi / Kocaeli Cargo Route Optimization System

## Türkçe

### Özet
Bu proje, Kocaeli bölgesinde kargo dağıtımı yapan bir simülasyon/iş uygulamasıdır. Backend ASP.NET Core (C#) ile yazılmıştır, frontend React + Vite kullanır ve MySQL veritabanı ile çalışır. Amaç: günlük kargo taleplerini alıp araçlara en uygun şekilde atamak, rotaları optimize etmek ve toplam maliyeti minimize etmektir.

### Başlıca Özellikler
- **Kargo talep oluşturma (kullanıcı)**: Kullanıcılar, kargo taleplerini sisteme iletebilirler. Her bir kargo talebi, istasyon, ağırlık, adet ve içerik gibi bilgileri içerir.
- **Admin paneli**: Sistem yöneticileri, hazırlayıcılar ve yöneticiler için bir kontrol merkezi sağlar. Test senaryoları yüklenebilir, araç ve istasyonlar yönetilebilir.
- **Rota optimizasyonu**: Kargo talepleri, belirli bölgeler veya hatlar bazında analize tabi tutulur ve araç kapasitesi dikkate alınarak en uygun şekilde atanır.
- **Otomatik kiralık araç ekleme (unlimited mod)**: Sistem, ihtiyaç duyulması halinde otomatik olarak yeni kiralık araçlar ekleyebilir.
- **Harita görselleştirme (Leaflet)**: Kargo rotaları ve istasyonlar, Leaflet kütüphanesi kullanılarak harita üzerinde görselleştirilir.
- **Test senaryoları (4 hazır senaryo)**: Sistemin farklı senaryolar altında nasıl çalıştığını test etmek için dört adet hazır senaryo bulunmaktadır.

### Mimari / Dosya Yapısı (özet)
- `YazLab3/` — çözüm kökü
  - `Backend/` — ASP.NET Core API
    - `RoutingControllers/TripPlannerController.cs` — rota planlama ve optimizasyon işlemlerinin yapıldığı kontrolör.
    - `ShipmentControllers/shipmentControllers.cs` — kargo ekleme ve istatistiklerin alındığı kontrolör.
    - `database/database.cs` — Veritabanı şemasının tanımlandığı ve başlangıç verilerinin yüklendiği dosya.
  - `FrontEnd/` — React + Vite uygulaması
    - `src/Menu/menu.jsx` — Uygulamamızın ana kullanıcı arayüzü, admin paneli ve test butonlarının bulunduğu dosya.

### Gereksinimler
- Projemiz .NET 8 veya daha yeni bir versiyon gerektirmektedir.
- Node.js 18+ sürümü ve npm
- MySQL Server (local veya uzak)

### Hızlı Kurulum
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

### Kullanım
- Normal kullanıcı olarak: uygulamaya giriş yaptıktan sonra kargo ekleyin (istasyon, ağırlık, adet, içerik).
- Admin olarak: Admin panelinden test senaryolarını yükleyebilir, araç/istasyon yönetimi yapabilirsiniz.
- Rota planlamak için: Admin panelde "Seferi Planla" butonunu kullanın.

### Test Senaryoları
Admin panelinde dört hazır senaryo vardır (Senaryo 1-4). Her senaryo tek tıklamayla db'ye yüklenir ve ardından rota planlaması çalıştırılarak sonuçlar karşılaştırılabilir.

Kısa açıklamalar:
- **Senaryo 1**: Bölgeye yayılmış ortalama yükler (genel test)
- **Senaryo 2**: Bazı istasyonlarda yüksek yoğunluk
- **Senaryo 3**: Çok ağır tekil kargolar (bölünmüş ekleme davranışını test eder)
- **Senaryo 4**: Kıyı/kuzey ağırlıklı yük dağılımı

### Önemli Notlar ve Davranışlar
- Kargo eklerken `Quantity` ve `WeightKg` girilir. Örneğin `Quantity=3, WeightKg=700` girilirse backend her biri için yaklaşık `700/3` kg olacak şekilde 3 ayrı kayıt oluşturur.
- Planlayıcı unlimited modda (kiralama izinliyse) ihtiyaç oldukça yeni araç kiralar; limited modda ise sadece mevcut araçlarla çalışır ve kapasite yetersizse hata döner.
- Rotalama; öncelikle istasyonları tanımlı hatlara/bölgelere ayırır, bölgelere göre ağırlık hesapları ve araç kapasitesi kullanılarak atama yapar.

### Yaygın Sorunlar ve Çözümleri
- "Unknown column 'station_id'" hatası: `TripPlannerController` veya diğer sorgularda `Stations` tablosu sütun isimleri doğru olmalıdır (`Id`, `StationName`, `Latitude`, `Longitude`).
- `GetConnectionString` nullable uyarıları: `appsettings.json` içindeki connection string anahtarının adı `MyDatabaseConnection` olmalıdır.
- Araç kapasitesi aşılıyorsa: unlimited mod açıksa sistem yeni araç kiralayacak; değilse gönderilen verileri (adet/ağırlık) kontrol edin.

---

## English

### Summary
This project is a simulation/operational application for cargo distribution in the Kocaeli region. The backend is written in ASP.NET Core (C#), the frontend uses React + Vite, and the application works with a MySQL database. Goal: accept daily cargo requests, assign them to vehicles optimally, optimize routes, and minimize total cost.

### Key Features
- **Create shipment requests (user)**: Users can submit cargo requests to the system, including details like station, weight, quantity, and content.
- **Admin panel**: A control center for system administrators, preparers, and managers. Test scenarios can be uploaded, and vehicle and station management can be performed.
- **Route optimization**: Cargo requests are analyzed based on specific regions or routes and assigned optimally considering vehicle capacity.
- **Automatic rental vehicle addition (unlimited mode)**: The system can automatically add new rental vehicles as needed.
- **Map visualization (Leaflet)**: Cargo routes and stations are visualized on a map using the Leaflet library.
- **Test scenarios (4 built-in scenarios)**: Four ready-to-use scenarios are available to test the system's performance under different conditions.

### Architecture / File Structure (summary)
- `YazLab3/` — solution root
  - `Backend/` — ASP.NET Core API
    - `RoutingControllers/TripPlannerController.cs` — controller for trip planning and optimization.
    - `ShipmentControllers/shipmentControllers.cs` — controller for adding shipments and retrieving statistics.
    - `database/database.cs` — file defining the database schema and seeding initial data.
  - `FrontEnd/` — React + Vite app
    - `src/Menu/menu.jsx` — file containing the main UI, admin panel, and test buttons of our application.

### Requirements
- The project requires .NET 8 or a newer version.
- Node.js 18+ version and npm
- MySQL Server (local or remote)

### Quick Setup
1. Set the DB connection in `Backend/appsettings.json` (example):

```json
{
  "ConnectionStrings": {
    "MyDatabaseConnection": "server=127.0.0.1;user=root;password=pass;database=KargoSistemi;"
  }
}
```

2. Run backend

```bash
cd YazLab3
dotnet build
dotnet run --project YazLab3/Backend
```

By default the API runs at `http://localhost:5000`.

3. Run frontend

```bash
cd YazLab3/FrontEnd
npm install
npm run dev
```

Frontend typically runs at `http://localhost:5173`.

### Usage
- As a normal user: add shipments after logging in (station, weight, quantity, content).
- As admin: load test scenarios, manage vehicles/stations via the admin panel.
- To plan routes: use the "Plan Trip" button in the admin panel.

### Test Scenarios
There are four built-in scenarios in the admin panel (Scenario 1-4). Each can be loaded into the DB with one click and route planning can be run to compare results.

Short descriptions:
- **Scenario 1**: Average loads distributed across the region (general test)
- **Scenario 2**: High concentration at some stations
- **Scenario 3**: Very heavy single shipments (tests split-adding behavior)
- **Scenario 4**: Coastal/northern load distribution

### Important Notes & Behavior
- When adding shipments, provide `Quantity` and `WeightKg`. For example, `Quantity=3, WeightKg=700` results in 3 separate records of approximately `700/3` kg each.
- Planner in unlimited mode (if renting is allowed) will rent new vehicles as needed; in limited mode it only uses existing vehicles and returns an error if capacity is insufficient.
- Routing first groups stations into defined lines/regions and assigns shipments using region weight sums and vehicle capacities.

### Common Issues & Fixes
- "Unknown column 'station_id'" error: ensure `Stations` table column names match (`Id`, `StationName`, `Latitude`, `Longitude`).
- `GetConnectionString` nullable warnings: the connection string key in `appsettings.json` must be `MyDatabaseConnection`.
- If vehicle capacity is exceeded: if unlimited mode is enabled the system will rent additional vehicles; otherwise check input quantities/weights.

---

Prepared by: Project Contributor

If you want, I can split this into separate `README.tr.md` and `README.en.md` files or add an ER diagram and API endpoint list.