import {
  MapContainer,
  TileLayer,
  Marker,
  Popup,
  Polyline,
} from "react-leaflet";
import { useState, useEffect } from "react";
import L from "leaflet";
import "leaflet/dist/leaflet.css";
import "./menu.css";

delete L.Icon.Default.prototype._getIconUrl;
L.Icon.Default.mergeOptions({
  iconRetinaUrl:
    "https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png",
  iconUrl:
    "https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png",
  shadowUrl:
    "https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png",
});

const KOCAELI_CENTER = [40.8533, 29.8815];

function MapPage() {
  const [points, setPoints] = useState([]);
  const [path, setPath] = useState([]);
  const [stations, setStations] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  // Yeni: planlanan seferler (admin)
  const [trips, setTrips] = useState([]);
  const [planMsg, setPlanMsg] = useState("");

  // Yeni: Tüm oluşturulan rotalar
  const [allRoutes, setAllRoutes] = useState([]);
  const [routesMsg, setRoutesMsg] = useState("");
  const [openRoutesPanel, setOpenRoutesPanel] = useState(false);

  // Yeni: Kullanıcının kendi kargoları
  const [myShipments, setMyShipments] = useState([]);
  const [myShipmentsMsg, setMyShipmentsMsg] = useState("");
  const [openMyShipmentsPanel, setOpenMyShipmentsPanel] = useState(false);

  // Shipment panel
  const [selectedStationId, setSelectedStationId] = useState("");
  const [weightKg, setWeightKg] = useState("");
  const [cargoContent, setCargoContent] = useState("");
  const [cargoQuantity, setCargoQuantity] = useState("1");
  const [submitMsg, setSubmitMsg] = useState("");

  // Admin station add panel
  const [newStationName, setNewStationName] = useState("");
  const [newStationLat, setNewStationLat] = useState("");
  const [newStationLng, setNewStationLng] = useState("");
  const [adminMsg, setAdminMsg] = useState("");

  // Yeni: İstasyon kargo istatistikleri (Admin için)
  const [stationStats, setStationStats] = useState([]);
  const [statsMsg, setStatsMsg] = useState("");

  // Yeni: Araç yönetimi (Admin için)
  const [vehicles, setVehicles] = useState([]);
  const [vehiclesMsg, setVehiclesMsg] = useState("");
  const [openVehiclesPanel, setOpenVehiclesPanel] = useState(false);
  const [editingVehicle, setEditingVehicle] = useState(null);

  // Yeni: Route draw panel
  const [fromStationId, setFromStationId] = useState("");
  const [toStationId, setToStationId] = useState("");
  const [routeMsg, setRouteMsg] = useState("");
  
  // Araç kiralama checkbox
  const [allowRental, setAllowRental] = useState(true);

  // UI control: panel açık/kapalı
  const [openShipmentPanel, setOpenShipmentPanel] = useState(false);
  const [openAdminPanel, setOpenAdminPanel] = useState(false);
  const [openRoutePanel, setOpenRoutePanel] = useState(false);

  // Kullanıcı admin mi?
  const [isAdmin, setIsAdmin] = useState(false);

  // herhangi bir panel açık mı? -> FAB'ları gizlemek için
  const anyPanelOpen = openShipmentPanel || openAdminPanel || openRoutePanel || openRoutesPanel || openMyShipmentsPanel || openVehiclesPanel;

  const fetchStations = async () => {
    try {
      setLoading(true);
      const response = await fetch("http://localhost:5000/api/station/stations");

      if (!response.ok) {
        throw new Error("İstasyonlar yüklenemedi");
      }

      const data = await response.json();
      setStations(data);
      setError(null);

      if (data.length > 0) {
        if (selectedStationId === "") {
          setSelectedStationId(String(data[0].id));
        }

        if (fromStationId === "") {
          setFromStationId(String(data[0].id));
        }

        if (toStationId === "" && data.length > 1) {
          setToStationId(String(data[1].id));
        } else if (toStationId === "") {
          setToStationId(String(data[0].id));
        }
      }
    } catch (err) {
      console.error("İstasyon yükleme hatası:", err);
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchStations();

    // admin kontrolü
    const email = localStorage.getItem("userEmail");
    if (email) {
      (async () => {
        try {
          const resp = await fetch(
            `http://localhost:5000/api/auth/is-admin?email=${encodeURIComponent(
              email
            )}`
          );
          if (resp.ok) {
            const j = await resp.json();
            setIsAdmin(!!j.isAdmin);
          } else {
            setIsAdmin(false);
          }
        } catch (e) {
          console.warn("Admin kontrol hatası", e);
          setIsAdmin(false);
        }
      })();
    }

    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const submitShipment = async () => {
    try {
      setSubmitMsg("");

      const email = localStorage.getItem("userEmail");
      if (!email) {
        setSubmitMsg("Önce giriş yapmalısınız (userEmail yok).");
        return;
      }

      const stationId = Number(selectedStationId);
      const kg = Number(weightKg);
      const content = cargoContent.trim();
      const quantity = Number(cargoQuantity);

      if (!stationId || stationId <= 0) {
        setSubmitMsg("İstasyon seçiniz.");
        return;
      }

      if (!kg || kg <= 0) {
        setSubmitMsg("Ağırlık 0'dan büyük olmalıdır.");
        return;
      }

      if (!content) {
        setSubmitMsg("Kargo içeriği giriniz.");
        return;
      }

      if (!quantity || quantity <= 0) {
        setSubmitMsg("Adet 0'dan büyük olmalıdır.");
        return;
      }

      const response = await fetch("http://localhost:5000/api/shipments", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          email,
          stationId,
          weightKg: kg,
          content,
          quantity,
        }),
      });

      const data = await response.json().catch(() => null);

      if (!response.ok) {
        setSubmitMsg("Hata: " + (data?.mesaj || "Kargo talebi gönderilemedi."));
        return;
      }

      setSubmitMsg(
        `OK: ${data?.mesaj || "Kargo talebi alındı."} (ID: ${data?.shipmentId})`
      );
      setWeightKg("");
      setCargoContent("");
      setCargoQuantity("1");
    } catch (e) {
      console.error(e);
      setSubmitMsg("Sunucuya bağlanılamadı.");
    }
  };

  const submitNewStation = async () => {
    try {
      setAdminMsg("");

      const adminEmail = localStorage.getItem("userEmail");
      if (!adminEmail) {
        setAdminMsg("Önce giriş yapmalısınız (userEmail yok).");
        return;
      }

      const name = newStationName.trim();
      const lat = Number(newStationLat);
      const lng = Number(newStationLng);

      if (!name) {
        setAdminMsg("İstasyon adı zorunludur.");
        return;
      }

      if (!Number.isFinite(lat) || lat < -90 || lat > 90) {
        setAdminMsg("Latitude -90 ile 90 arasında olmalıdır.");
        return;
      }

      if (!Number.isFinite(lng) || lng < -180 || lng > 180) {
        setAdminMsg("Longitude -180 ile 180 arasında olmalıdır.");
        return;
      }

      const response = await fetch("http://localhost:5000/api/station", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          adminEmail,
          stationName: name,
          latitude: lat,
          longitude: lng,
        }),
      });

      const data = await response.json().catch(() => null);

      if (!response.ok) {
        setAdminMsg("Hata: " + (data?.mesaj || "İstasyon eklenemedi."));
        return;
      }

      setAdminMsg(`OK: ${data?.mesaj || "İstasyon eklendi."} (ID: ${data?.stationId})`);
      setNewStationName("");
      setNewStationLat("");
      setNewStationLng("");

      await fetchStations();
    } catch (e) {
      console.error(e);
      setAdminMsg("Sunucuya bağlanılamadı.");
    }
  };

  // Yeni: İstasyon istatistiklerini getir
  const fetchStationStats = async () => {
    try {
      setStatsMsg("Yükleniyor...");
      setStationStats([]);

      const response = await fetch("http://localhost:5000/api/shipments/station-stats");
      const data = await response.json().catch(() => null);

      if (!response.ok) {
        setStatsMsg("Hata: " + (data?.mesaj || "İstatistikler yüklenemedi."));
        return;
      }

      setStationStats(data.stats || []);
      setStatsMsg(data.mesaj || "İstatistikler yüklendi.");
    } catch (e) {
      console.error(e);
      setStatsMsg("Sunucuya bağlanılamadı.");
    }
  };

  // Yeni: Araçları getir
  const fetchVehicles = async () => {
    try {
      setVehiclesMsg("Yükleniyor...");
      setVehicles([]);

      const response = await fetch("http://localhost:5000/api/vehicle/vehicles");
      const data = await response.json().catch(() => null);

      if (!response.ok) {
        setVehiclesMsg("Hata: " + (data?.mesaj || "Araçlar yüklenemedi."));
        return;
      }

      setVehicles(data.vehicles || []);
      setVehiclesMsg(data.mesaj || "Araçlar yüklendi.");
    } catch (e) {
      console.error(e);
      setVehiclesMsg("Sunucuya bağlanılamadı.");
    }
  };

  // Yeni: Araç güncelle
  const updateVehicle = async (vehicleId) => {
    try {
      if (!editingVehicle) return;

      const response = await fetch(`http://localhost:5000/api/vehicle/${vehicleId}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          name: editingVehicle.name,
          capacityKg: editingVehicle.capacityKg
        })
      });

      const data = await response.json().catch(() => null);

      if (!response.ok) {
        setVehiclesMsg("Hata: " + (data?.mesaj || "Araç güncellenemedi."));
        return;
      }

      setVehiclesMsg(data.mesaj || "Araç güncellendi.");
      setEditingVehicle(null);
      await fetchVehicles();
    } catch (e) {
      console.error(e);
      setVehiclesMsg("Sunucuya bağlanılamadı.");
    }
  };

  const drawRoute = async () => {
    try {
      setRouteMsg("");
      setPath([]);

      const fromId = Number(fromStationId);
      const toId = Number(toStationId);

      if (!fromId || !toId) {
        setRouteMsg("Başlangıç ve bitiş istasyonu seçiniz.");
        return;
      }

      if (fromId === toId) {
        setRouteMsg("Başlangıç ve bitiş farklı olmalıdır.");
        return;
      }

      const url = `http://localhost:5000/api/routing/route?fromStationId=${fromId}&toStationId=${toId}`;
      const response = await fetch(url);
      const data = await response.json().catch(() => null);

      if (!response.ok) {
        setRouteMsg("Hata: " + (data?.mesaj || "Rota çizilemedi."));
        return;
      }

      setPath(data.polyline || []);
      setRouteMsg(
        `OK: ${Number(data.distanceKm).toFixed(2)} km | Yol maliyeti: ${Number(
          data.roadCost
        ).toFixed(2)}`
      );
    } catch (e) {
      console.error(e);
      setRouteMsg("Sunucuya bağlanılamadı.");
    }
  };

  // Yeni: admin butonu -> backend trip planner'ı çağırır, dönen planları trips'e koyar
  const planAllShipments = async () => {
    try {
      setPlanMsg("");
      setTrips([]);

      const adminEmail = localStorage.getItem("userEmail");
      if (!adminEmail) {
        setPlanMsg("Önce admin olarak giriş yapmalısınız.");
        return;
      }

      // Araç kiralama izni yoksa mode=limited, maxVehicles=3 gönder
      const mode = allowRental ? "unlimited" : "limited";
      const maxVehicles = allowRental ? 0 : 3;
      
      const response = await fetch(`http://localhost:5000/api/tripplanner/plan-next-day?mode=${mode}&maxVehicles=${maxVehicles}`, {
        method: "POST",
      });

      const data = await response.json().catch(() => null);

      if (!response.ok) {
        setPlanMsg("Hata: " + (data?.mesaj || "Planlama başarısız."));
        return;
      }

      setTrips(data.plans || []);
      
      // Toplam maliyet hesapla
      const totalCost = (data.plans || []).reduce((sum, plan) => sum + (plan.totalCost || 0), 0);
      const totalDistance = (data.plans || []).reduce((sum, plan) => sum + (plan.distanceKm || 0), 0);
      const totalVehicles = (data.plans || []).length;
      
      setPlanMsg(
        `${data.mesaj || "Planlama tamamlandı."}\n` +
        `📦 ${totalVehicles} sefer | 📏 ${totalDistance.toFixed(2)} km | 💰 Toplam: ${totalCost.toFixed(2)} TL`
      );
    } catch (e) {
      console.error(e);
      setPlanMsg("Sunucuya bağlanılamadı.");
    }
  };

  const clearPlans = () => {
    setTrips([]);
    setPlanMsg("");
  };

  // Test senaryolarını yükle
  const loadTestScenario = async (scenarioNumber) => {
    try {
      setPlanMsg(`Senaryo ${scenarioNumber} yükleniyor...`);
      
      const email = localStorage.getItem("userEmail");
      if (!email) {
        setPlanMsg("Önce giriş yapmalısınız.");
        return;
      }

      // Senaryo verileri
      const scenarios = {
        1: [
          { station: "Başiskele", quantity: 10, weight: 120, content: "Test Senaryo 1" },
          { station: "Çayırova", quantity: 8, weight: 80, content: "Test Senaryo 1" },
          { station: "Darıca", quantity: 15, weight: 200, content: "Test Senaryo 1" },
          { station: "Derince", quantity: 10, weight: 150, content: "Test Senaryo 1" },
          { station: "Dilovası", quantity: 12, weight: 180, content: "Test Senaryo 1" },
          { station: "Gebze", quantity: 5, weight: 70, content: "Test Senaryo 1" },
          { station: "Gölcük", quantity: 7, weight: 90, content: "Test Senaryo 1" },
          { station: "Kandıra", quantity: 6, weight: 60, content: "Test Senaryo 1" },
          { station: "Karamürsel", quantity: 9, weight: 110, content: "Test Senaryo 1" },
          { station: "Kartepe", quantity: 11, weight: 130, content: "Test Senaryo 1" },
          { station: "Körfez", quantity: 6, weight: 75, content: "Test Senaryo 1" },
          { station: "İzmit", quantity: 14, weight: 160, content: "Test Senaryo 1" }
        ],
        2: [
          { station: "Başiskele", quantity: 40, weight: 200, content: "Test Senaryo 2" },
          { station: "Çayırova", quantity: 35, weight: 175, content: "Test Senaryo 2" },
          { station: "Darıca", quantity: 10, weight: 150, content: "Test Senaryo 2" },
          { station: "Derince", quantity: 5, weight: 100, content: "Test Senaryo 2" },
          { station: "Gebze", quantity: 8, weight: 120, content: "Test Senaryo 2" },
          { station: "İzmit", quantity: 20, weight: 160, content: "Test Senaryo 2" }
        ],
        3: [
          { station: "Çayırova", quantity: 3, weight: 700, content: "Test Senaryo 3" },
          { station: "Dilovası", quantity: 4, weight: 800, content: "Test Senaryo 3" },
          { station: "Gebze", quantity: 5, weight: 900, content: "Test Senaryo 3" },
          { station: "İzmit", quantity: 5, weight: 300, content: "Test Senaryo 3" }
        ],
        4: [
          { station: "Başiskele", quantity: 30, weight: 300, content: "Test Senaryo 4" },
          { station: "Gölcük", quantity: 15, weight: 220, content: "Test Senaryo 4" },
          { station: "Kandıra", quantity: 5, weight: 250, content: "Test Senaryo 4" },
          { station: "Karamürsel", quantity: 20, weight: 180, content: "Test Senaryo 4" },
          { station: "Kartepe", quantity: 10, weight: 200, content: "Test Senaryo 4" },
          { station: "Körfez", quantity: 8, weight: 400, content: "Test Senaryo 4" }
        ]
      };

      const scenarioData = scenarios[scenarioNumber];
      if (!scenarioData) {
        setPlanMsg("Geçersiz senaryo numarası.");
        return;
      }

      let successCount = 0;
      let failCount = 0;

      for (const item of scenarioData) {
        // İstasyon ID'sini bul
        const station = stations.find(s => s.name === item.station);
        if (!station) {
          console.warn(`İstasyon bulunamadı: ${item.station}`);
          failCount++;
          continue;
        }

        try {
          const response = await fetch("http://localhost:5000/api/shipments", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
              email,
              stationId: station.id,
              weightKg: item.weight,
              content: item.content,
              quantity: item.quantity
            })
          });

          if (response.ok) {
            successCount++;
          } else {
            failCount++;
            console.error(`Kargo eklenemedi: ${item.station}`);
          }
        } catch (e) {
          failCount++;
          console.error(`Kargo ekleme hatası (${item.station}):`, e);
        }
      }

      setPlanMsg(`Senaryo ${scenarioNumber} yüklendi! ✅ ${successCount} başarılı, ❌ ${failCount} hatalı`);
    } catch (e) {
      console.error(e);
      setPlanMsg("Senaryo yükleme hatası: " + e.message);
    }
  };

  // Çıkış yap
  const handleLogout = () => {
    localStorage.removeItem("userEmail");
    localStorage.removeItem("userName");
    window.location.href = "/";
  };

  // Kullanıcının kargolarını getir
  const fetchMyShipments = async () => {
    try {
      setMyShipmentsMsg("");
      setMyShipments([]);

      const email = localStorage.getItem("userEmail");
      if (!email) {
        setMyShipmentsMsg("Önce giriş yapmalısınız.");
        return;
      }

      const response = await fetch(`http://localhost:5000/api/shipments/my-shipments?email=${encodeURIComponent(email)}`);
      const data = await response.json().catch(() => null);

      if (!response.ok) {
        const errorMsg = data?.mesaj || "Kargolar yüklenemedi.";
        const errorDetail = data?.detay ? ` (${data.detay})` : "";
        setMyShipmentsMsg("Hata: " + errorMsg + errorDetail);
        console.error("Backend hatası:", data);
        return;
      }

      setMyShipments(data.shipments || []);
      setMyShipmentsMsg(data.mesaj || "Kargolar yüklendi.");
    } catch (e) {
      console.error("Fetch hatası:", e);
      setMyShipmentsMsg("Sunucuya bağlanılamadı: " + e.message);
    }
  };

  // Tüm oluşturulan rotaları backend'den çek
  const fetchAllRoutes = async () => {
    try {
      setRoutesMsg("");
      setAllRoutes([]);

      const response = await fetch("http://localhost:5000/api/tripplanner/all-routes");
      const data = await response.json().catch(() => null);

      if (!response.ok) {
        const errorMsg = data?.mesaj || "Rotalar yüklenemedi.";
        const errorDetail = data?.detay ? ` (${data.detay})` : "";
        setRoutesMsg("Hata: " + errorMsg + errorDetail);
        console.error("Backend hatası:", data);
        return;
      }

      setAllRoutes(data.routes || []);
      setRoutesMsg(data.mesaj || "Rotalar yüklendi.");
    } catch (e) {
      console.error("Fetch hatası:", e);
      setRoutesMsg("Sunucuya bağlanılamadı: " + e.message);
    }
  };

  // Belirli bir rotayı haritada göster
  const showRouteOnMap = async (route) => {
    try {
      setPath([]);
      setRoutesMsg("Rota çiziliyor...");
      
      console.log("Rota bilgisi:", route);

      // Backend'den gelen stationOrder'ı kullan (istasyon isimleri yerine ID'lere çevir)
      if (!route.stationOrder || route.stationOrder.length === 0) {
        setRoutesMsg("Bu rotada istasyon sırası bulunamadı.");
        return;
      }

      // Eğer stationOrder string ise (istasyon isimleri), ID'lere çevir
      let routeStationIds;
      if (typeof route.stationOrder[0] === 'string') {
        // İstasyon isimlerinden ID'lere çevir
        routeStationIds = route.stationOrder
          .map(name => stations.find(s => s.name === name)?.id)
          .filter(id => id !== undefined);
      } else {
        // Zaten ID formatında
        routeStationIds = route.stationOrder;
      }
      
      console.log("Backend'den gelen istasyon sırası:", routeStationIds);
      
      if (routeStationIds.length === 0) {
        setRoutesMsg("İstasyon bilgisi bulunamadı.");
        return;
      }

      // Birden fazla istasyon varsa, sırayla rotaları çiz
      let allPoints = [];
      let totalDistance = 0;
      
      for (let i = 0; i < routeStationIds.length - 1; i++) {
        const fromId = routeStationIds[i];
        const toId = routeStationIds[i + 1];

        console.log(`Rota çiziliyor: ${fromId} -> ${toId}`);

        const url = `http://localhost:5000/api/routing/route?fromStationId=${fromId}&toStationId=${toId}`;
        const response = await fetch(url);
        const data = await response.json().catch(() => null);

        console.log(`Rota sonucu (${fromId} -> ${toId}):`, data);

        if (response.ok && data.polyline && data.polyline.length > 0) {
          totalDistance += data.distanceKm || 0;
          
          if (allPoints.length > 0) {
            // Son nokta ile ilk nokta aynıysa, ilk noktayı atla
            const lastPoint = allPoints[allPoints.length - 1];
            const firstPoint = data.polyline[0];
            if (lastPoint[0] === firstPoint[0] && lastPoint[1] === firstPoint[1]) {
              allPoints = allPoints.concat(data.polyline.slice(1));
            } else {
              allPoints = allPoints.concat(data.polyline);
            }
          } else {
            allPoints = data.polyline;
          }
        } else {
          console.error(`Rota çizilemedi: ${fromId} -> ${toId}`);
        }
      }

      // Son istasyondan KOU MERKEZ'e dönüş rotasını çiz
      if (routeStationIds.length > 0) {
        const lastStationId = routeStationIds[routeStationIds.length - 1];
        const depotStation = stations.find(s => s.name === "KOU MERKEZ") || stations[0];
        
        if (depotStation) {
          console.log(`Dönüş rotası çiziliyor: ${lastStationId} -> ${depotStation.id} (KOU MERKEZ)`);
          
          const url = `http://localhost:5000/api/routing/route?fromStationId=${lastStationId}&toStationId=${depotStation.id}`;
          const response = await fetch(url);
          const data = await response.json().catch(() => null);

          console.log(`Dönüş rotası sonucu:`, data);

          if (response.ok && data.polyline && data.polyline.length > 0) {
            totalDistance += data.distanceKm || 0;
            
            if (allPoints.length > 0) {
              const lastPoint = allPoints[allPoints.length - 1];
              const firstPoint = data.polyline[0];
              if (lastPoint[0] === firstPoint[0] && lastPoint[1] === firstPoint[1]) {
                allPoints = allPoints.concat(data.polyline.slice(1));
              } else {
                allPoints = allPoints.concat(data.polyline);
              }
            } else {
              allPoints = data.polyline;
            }
          } else {
            console.error(`Dönüş rotası çizilemedi`);
          }
        }
      }

      console.log("Toplam nokta sayısı:", allPoints.length);

      if (allPoints.length > 0) {
        setPath(allPoints);
        setRoutesMsg(`Rota haritada gösteriliyor (${routeStationIds.length} istasyon, ${totalDistance.toFixed(2)} km)`);
      } else {
        setRoutesMsg("Rota çizilemedi. Konsolu kontrol edin.");
      }
    } catch (e) {
      console.error("Rota gösterme hatası:", e);
      setRoutesMsg("Rota gösterilirken hata oluştu: " + e.message);
    }
  };

  // Kullanıcı kargosunun rotasını haritada göster
  const showShipmentRouteOnMap = async (shipment) => {
    try {
      setMyShipmentsMsg("🔄 Rota yükleniyor...");
      
      if (!shipment.tripId) {
        setMyShipmentsMsg("⚠️ Bu kargo henüz bir araca atanmadı.");
        return;
      }

      // Eğer polyline varsa direkt kullan
      if (shipment.polyline) {
        try {
          const decodedPolyline = JSON.parse(shipment.polyline);
          setPath(decodedPolyline);
          setMyShipmentsMsg(`🚗 Araç rotası haritada gösteriliyor (${Number(shipment.totalDistanceKm || 0).toFixed(2)} km)`);
          setOpenMyShipmentsPanel(false);
          return;
        } catch (parseError) {
          console.error("Polyline parse hatası:", parseError);
          // Eğer parse başarısız olursa aşağıdaki alternatif yönteme geç
        }
      }

      // Alternatif: Kargonun istasyonu ile depot arasındaki rotayı çiz
      setMyShipmentsMsg("🔄 Rota hesaplanıyor...");
      
      // KOU MERKEZ (depot) bulma
      const depotStation = stations.find(s => s.name === "KOU MERKEZ") || stations[0];
      if (!depotStation) {
        setMyShipmentsMsg("❌ Depot istasyonu bulunamadı.");
        return;
      }

      // Eğer kargo zaten depot'taysa rota çizmeye gerek yok
      if (shipment.stationId === depotStation.id) {
        setPath([]);
        setMyShipmentsMsg("📍 Bu kargo KOU MERKEZ (Depot) istasyonuna ait, rota gösterilemiyor.");
        return;
      }

      // Cargo station ID'den rota hesaplama
      const url = `http://localhost:5000/api/routing/route?fromStationId=${depotStation.id}&toStationId=${shipment.stationId}`;
      const response = await fetch(url);
      
      if (!response.ok) {
        const errorData = await response.json().catch(() => ({}));
        setMyShipmentsMsg(`❌ Rota bilgisi alınamadı: ${errorData.mesaj || response.statusText}`);
        return;
      }

      const data = await response.json();
      if (data.polyline && data.polyline.length > 0) {
        setPath(data.polyline);
        setMyShipmentsMsg(`🚗 Kargo rotası haritada gösteriliyor (${data.distanceKm.toFixed(2)} km)`);
        setOpenMyShipmentsPanel(false);
      } else {
        setMyShipmentsMsg("❌ Rota çizilemedi.");
      }
    } catch (e) {
      console.error("Rota gösterme hatası:", e);
      setMyShipmentsMsg("❌ Rota gösterilirken hata oluştu: " + e.message);
    }
  };


  // renk paleti her sefer için farklı renk
  const colors = [
    "#ef4444", "#f97316", "#f59e0b", "#10b981", "#06b6d4", "#3b82f6", "#8b5cf6", "#ec4899"
  ];

  // Panel yönetimi - sadece bir panel açık olmalı
  const togglePanel = (panelName) => {
    if (panelName === 'shipment') {
      if (openShipmentPanel) {
        setOpenShipmentPanel(false);
      } else {
        setOpenShipmentPanel(true);
        setOpenAdminPanel(false);
        setOpenRoutePanel(false);
        setOpenRoutesPanel(false);
        setOpenMyShipmentsPanel(false);
        setOpenVehiclesPanel(false);
      }
    } else if (panelName === 'admin') {
      if (openAdminPanel) {
        setOpenAdminPanel(false);
      } else {
        setOpenShipmentPanel(false);
        setOpenAdminPanel(true);
        setOpenRoutePanel(false);
        setOpenRoutesPanel(false);
        setOpenMyShipmentsPanel(false);
        setOpenVehiclesPanel(false);
      }
    } else if (panelName === 'vehicles') {
      if (openVehiclesPanel) {
        setOpenVehiclesPanel(false);
      } else {
        setOpenShipmentPanel(false);
        setOpenAdminPanel(false);
        setOpenRoutePanel(false);
        setOpenRoutesPanel(false);
        setOpenMyShipmentsPanel(false);
        setOpenVehiclesPanel(true);
      }
    } else if (panelName === 'route') {
      if (openRoutePanel) {
        setOpenRoutePanel(false);
      } else {
        setOpenShipmentPanel(false);
        setOpenAdminPanel(false);
        setOpenRoutePanel(true);
        setOpenRoutesPanel(false);
        setOpenMyShipmentsPanel(false);
        setOpenVehiclesPanel(false);
      }
    } else if (panelName === 'routes') {
      if (openRoutesPanel) {
        setOpenRoutesPanel(false);
      } else {
        setOpenShipmentPanel(false);
        setOpenAdminPanel(false);
        setOpenRoutePanel(false);
        setOpenRoutesPanel(true);
        setOpenMyShipmentsPanel(false);
        setOpenVehiclesPanel(false);
        fetchAllRoutes();
      }
    } else if (panelName === 'myshipments') {
      if (openMyShipmentsPanel) {
        setOpenMyShipmentsPanel(false);
      } else {
        setOpenShipmentPanel(false);
        setOpenAdminPanel(false);
        setOpenRoutePanel(false);
        setOpenRoutesPanel(false);
        setOpenMyShipmentsPanel(true);
        setOpenVehiclesPanel(false);
        fetchMyShipments();
      }
    }
  };

  return (
    <div className="map-page-container">
      <nav className="top-navbar">
        <div className="navbar-content">
          <h1 className="navbar-title">📦 Kargo Simülatörü</h1>
          
          <div style={{ display: "flex", gap: "10px", marginLeft: "auto", alignItems: "center" }}>
            <button 
              className="navbar-button" 
              onClick={() => togglePanel('shipment')}
              style={{ 
                backgroundColor: openShipmentPanel ? "#1e3a8a" : "#2563eb",
                opacity: openShipmentPanel ? 1 : 0.9
              }}
            >
              {isAdmin ? "📊 Kargo İstatistikleri" : "📦 Kargo Talebi"}
            </button>

            {!isAdmin && (
              <button 
                className="navbar-button" 
                onClick={() => togglePanel('myshipments')}
                style={{ 
                  backgroundColor: openMyShipmentsPanel ? "#065f46" : "#059669",
                  opacity: openMyShipmentsPanel ? 1 : 0.9
                }}
              >
                📦 Kargolarım
              </button>
            )}

            {isAdmin && (
              <>
                <button 
                  className="navbar-button" 
                  onClick={() => togglePanel('admin')}
                  style={{ 
                    backgroundColor: openAdminPanel ? "#0c4a6e" : "#0284c7",
                    opacity: openAdminPanel ? 1 : 0.9
                  }}
                >
                  ➕ İstasyon Ekle
                </button>
                <button 
                  className="navbar-button" 
                  onClick={() => togglePanel('vehicles')}
                  style={{ 
                    backgroundColor: openVehiclesPanel ? "#7c2d12" : "#ea580c",
                    opacity: openVehiclesPanel ? 1 : 0.9
                  }}
                >
                  🚚 Araçlar
                </button>
                <button 
                  className="navbar-button" 
                  onClick={() => togglePanel('route')}
                  style={{ 
                    backgroundColor: openRoutePanel ? "#1e40af" : "#3b82f6",
                    opacity: openRoutePanel ? 1 : 0.9
                  }}
                >
                  🗺️ Rota Çiz
                </button>
                <button 
                  className="navbar-button" 
                  onClick={() => togglePanel('routes')}
                  style={{ 
                    backgroundColor: openRoutesPanel ? "#1e3a8a" : "#3b82f6",
                    opacity: openRoutesPanel ? 1 : 0.9
                  }}
                >
                  📋 Oluşan Rotalar
                </button>
                <button 
                  className="navbar-button" 
                  onClick={clearPlans}
                  style={{ backgroundColor: "#1e293b" }}
                >
                  🗑️ Planları Temizle
                </button>
              </>
            )}
            
            <button 
              className="navbar-button" 
              onClick={handleLogout}
              style={{ 
                backgroundColor: "#dc2626",
                marginLeft: "10px"
              }}
            >
              🚪 Çıkış
            </button>
          </div>

          {loading && (
            <span style={{ color: "#fff", marginLeft: "20px" }}>
              İstasyonlar yükleniyor...
            </span>
          )}
          {error && (
            <span style={{ color: "#ff6b6b", marginLeft: "20px" }}>
              Hata: {error}
            </span>
          )}
        </div>
      </nav>

      {/* Slide-in yan paneller */}
      <div className={`side-panel ${openShipmentPanel ? "open" : ""}`}>
        <div className="side-panel-header">
          <strong>{isAdmin ? "Kargo İstatistikleri (Admin)" : "Kargo Talebi"}</strong>
        </div>
        <div className="side-panel-body">
          {isAdmin ? (
            <>
              <div style={{ marginBottom: 12 }}>
                <button onClick={fetchStationStats} style={{ width: "100%" }}>
                  📊 İstatistikleri Yükle
                </button>
              </div>

              {statsMsg && <div className="panel-msg">{statsMsg}</div>}

              <div style={{ marginTop: 8, overflowY: "auto", maxHeight: "450px" }}>
                {stationStats.length === 0 && <div style={{ color: "#bbb" }}>İstatistik yok</div>}
                {stationStats.map((stat, idx) => (
                  <div key={`stat-${idx}`} style={{ 
                    padding: 10, 
                    marginBottom: 8,
                    borderBottom: "1px solid rgba(255,255,255,0.1)",
                    backgroundColor: "rgba(0,0,0,0.2)",
                    borderRadius: 4
                  }}>
                    <div style={{ fontSize: "1em", fontWeight: "bold", color: "#60a5fa", marginBottom: 4 }}>
                      📍 {stat.stationName}
                    </div>
                    <div style={{ fontSize: "0.9em", color: "#ddd" }}>
                      <strong>Toplam Kargo:</strong> {stat.totalShipments} adet
                    </div>
                    <div style={{ fontSize: "0.9em", color: "#ddd" }}>
                      <strong>Toplam Ağırlık:</strong> {stat.totalWeightKg} kg
                    </div>
                    <div style={{ fontSize: "0.85em", color: "#aaa", marginTop: 4 }}>
                      • Bekleyen: {stat.pendingCount} | Atanan: {stat.assignedCount}
                    </div>
                  </div>
                ))}
              </div>

              <div style={{ marginTop: 12 }}>
                <button onClick={() => togglePanel('shipment')} style={{ width: "100%" }}>
                  Kapat
                </button>
              </div>
            </>
          ) : (
            <>
              <label>İstasyon:</label>
              <select
                value={selectedStationId}
                onChange={(e) => setSelectedStationId(e.target.value)}
              >
                {stations.map((s) => (
                  <option key={s.id} value={String(s.id)}>
                    {s.name}
                  </option>
                ))}
              </select>

              <label>Kargo İçeriği:</label>
              <input
                type="text"
                value={cargoContent}
                onChange={(e) => setCargoContent(e.target.value)}
                placeholder="Örn: Elektronik, Gıda, Tekstil"
              />

              <label>Adet:</label>
              <input
                type="number"
                value={cargoQuantity}
                onChange={(e) => setCargoQuantity(e.target.value)}
                min="1"
              />

              <label>Ağırlık (kg):</label>
              <input
                type="number"
                value={weightKg}
                onChange={(e) => setWeightKg(e.target.value)}
                min="1"
              />

              <div style={{ marginTop: 8 }}>
                <button onClick={submitShipment}>Kargo Talebi Gönder</button>
                <button onClick={() => togglePanel('shipment')} style={{ marginLeft: 8 }}>
                  Kapat
                </button>
              </div>

              {submitMsg && <div className="panel-msg">{submitMsg}</div>}
            </>
          )}
        </div>
      </div>

      <div className={`side-panel ${openAdminPanel ? "open" : ""}`}>
        <div className="side-panel-header">
          <strong>Admin Araçları</strong>
        </div>
        <div className="side-panel-body">
          <div style={{ marginBottom: 20, padding: 12, backgroundColor: "rgba(59, 130, 246, 0.2)", borderRadius: 6 }}>
            <div style={{ fontSize: "1em", fontWeight: "bold", marginBottom: 8, color: "#60a5fa" }}>
              🧪 Test Senaryoları
            </div>
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "8px" }}>
              <button 
                onClick={() => loadTestScenario(1)} 
                style={{ fontSize: "0.85em", padding: "8px" }}
              >
                📦 Senaryo 1
              </button>
              <button 
                onClick={() => loadTestScenario(2)} 
                style={{ fontSize: "0.85em", padding: "8px" }}
              >
                📦 Senaryo 2
              </button>
              <button 
                onClick={() => loadTestScenario(3)} 
                style={{ fontSize: "0.85em", padding: "8px" }}
              >
                📦 Senaryo 3
              </button>
              <button 
                onClick={() => loadTestScenario(4)} 
                style={{ fontSize: "0.85em", padding: "8px" }}
              >
                📦 Senaryo 4
              </button>
            </div>
            <div style={{ fontSize: "0.75em", color: "#aaa", marginTop: 8 }}>
              Test verilerini otomatik yükler
            </div>
          </div>

          <hr style={{ border: "1px solid rgba(255,255,255,0.1)", margin: "16px 0" }} />

          <div style={{ fontSize: "1em", fontWeight: "bold", marginBottom: 8, color: "#60a5fa" }}>
            ➕ Yeni İstasyon Ekle
          </div>

          <label>İsim:</label>
          <input
            value={newStationName}
            onChange={(e) => setNewStationName(e.target.value)}
            placeholder="Yeni istasyon adı"
          />

          <label>Lat:</label>
          <input
            type="number"
            value={newStationLat}
            onChange={(e) => setNewStationLat(e.target.value)}
            placeholder="40.1234"
          />

          <label>Lng:</label>
          <input
            type="number"
            value={newStationLng}
            onChange={(e) => setNewStationLng(e.target.value)}
            placeholder="29.1234"
          />

          <div style={{ marginTop: 8 }}>
            <button onClick={submitNewStation}>İstasyon Ekle</button>
            <button onClick={() => togglePanel('admin')} style={{ marginLeft: 8 }}>
              Kapat
            </button>
          </div>

          {adminMsg && <div className="panel-msg">{adminMsg}</div>}
        </div>
      </div>

      {/* Araçlar Paneli */}
      <div className={`side-panel ${openVehiclesPanel ? "open" : ""}`}>
        <div className="side-panel-header">
          <strong>Araç Yönetimi (Admin)</strong>
        </div>
        <div className="side-panel-body">
          <div style={{ marginBottom: 12 }}>
            <button onClick={fetchVehicles} style={{ width: "100%" }}>
              🚚 Araçları Yükle
            </button>
          </div>

          {vehiclesMsg && <div className="panel-msg">{vehiclesMsg}</div>}

          <div style={{ marginTop: 8, overflowY: "auto", maxHeight: "450px" }}>
            {vehicles.length === 0 && <div style={{ color: "#bbb" }}>Araç yok</div>}
            {vehicles.map((vehicle) => (
              <div key={`vehicle-${vehicle.id}`} style={{ 
                padding: 10, 
                marginBottom: 8,
                borderBottom: "1px solid rgba(255,255,255,0.1)",
                backgroundColor: editingVehicle?.id === vehicle.id ? "rgba(234, 88, 12, 0.2)" : "rgba(0,0,0,0.2)",
                borderRadius: 4
              }}>
                {editingVehicle?.id === vehicle.id ? (
                  <>
                    <div style={{ fontSize: "0.9em", marginBottom: 8 }}>
                      <label style={{ display: "block", color: "#aaa", fontSize: "0.8em" }}>İsim:</label>
                      <input
                        type="text"
                        value={editingVehicle.name}
                        onChange={(e) => setEditingVehicle({...editingVehicle, name: e.target.value})}
                        style={{ width: "100%", padding: "4px", marginTop: "2px" }}
                      />
                    </div>
                    <div style={{ fontSize: "0.9em", marginBottom: 8 }}>
                      <label style={{ display: "block", color: "#aaa", fontSize: "0.8em" }}>Kapasite (kg):</label>
                      <input
                        type="number"
                        value={editingVehicle.capacityKg}
                        onChange={(e) => setEditingVehicle({...editingVehicle, capacityKg: Number(e.target.value)})}
                        style={{ width: "100%", padding: "4px", marginTop: "2px" }}
                      />
                    </div>
                    <div style={{ display: "flex", gap: "8px", marginTop: 12 }}>
                      <button onClick={() => updateVehicle(vehicle.id)} style={{ flex: 1, fontSize: "0.85em", padding: "6px" }}>
                        ✅ Kaydet
                      </button>
                      <button onClick={() => setEditingVehicle(null)} style={{ flex: 1, fontSize: "0.85em", padding: "6px", backgroundColor: "#666" }}>
                        ❌ İptal
                      </button>
                    </div>
                  </>
                ) : (
                  <>
                    <div style={{ fontSize: "1em", fontWeight: "bold", color: "#ea580c", marginBottom: 4 }}>
                      🚚 {vehicle.name}
                    </div>
                    <div style={{ fontSize: "0.9em", color: "#ddd" }}>
                      <strong>Kapasite:</strong> {vehicle.capacityKg} kg
                    </div>
                    <div style={{ marginTop: 8 }}>
                      <button 
                        onClick={() => setEditingVehicle(vehicle)} 
                        style={{ fontSize: "0.85em", padding: "4px 8px", width: "100%" }}
                      >
                        ✏️ Düzenle
                      </button>
                    </div>
                  </>
                )}
              </div>
            ))}
          </div>

          <div style={{ marginTop: 12 }}>
            <button onClick={() => togglePanel('vehicles')} style={{ width: "100%" }}>
              Kapat
            </button>
          </div>
        </div>
      </div>

      <div className={`side-panel ${openRoutePanel ? "open" : ""}`}>
        <div className="side-panel-header">
          <strong>Rota Çiz (Admin)</strong>
        </div>
        <div className="side-panel-body">
          <label>From:</label>
          <select value={fromStationId} onChange={(e) => setFromStationId(e.target.value)}>
            {stations.map((s) => (
              <option key={`from-${s.id}`} value={String(s.id)}>
                {s.name}
              </option>
            ))}
          </select>

          <label>To:</label>
          <select value={toStationId} onChange={(e) => setToStationId(e.target.value)}>
            {stations.map((s) => (
              <option key={`to-${s.id}`} value={String(s.id)}>
                {s.name}
              </option>
            ))}
          </select>

          <div style={{ marginTop: 8 }}>
            <button onClick={drawRoute}>Rota Çiz</button>
            <button onClick={() => setPath([])} style={{ marginLeft: 8 }}>
              Rotayı Temizle
            </button>
            <button onClick={() => togglePanel('route')} style={{ marginLeft: 8 }}>
              Kapat
            </button>
          </div>

          {routeMsg && <div className="panel-msg">{routeMsg}</div>}

          {/* Admin: plan sonuçlarını göster */}
          {isAdmin && (
            <>
              <hr style={{ margin: "8px 0", borderColor: "rgba(255,255,255,0.06)" }} />
              <strong>Plan Sonuçları</strong>
              
              <div className="toggle-container" style={{ marginTop: 8, marginBottom: 8 }}>
                <label className="toggle-switch">
                  <input 
                    type="checkbox" 
                    checked={allowRental} 
                    onChange={(e) => setAllowRental(e.target.checked)}
                  />
                  <span className="toggle-slider"></span>
                </label>
                <span className="toggle-label">Araç Kiralanabilir</span>
              </div>
              
              <div style={{ marginTop: 8 }}>
                <button onClick={planAllShipments}>Tüm Kargoları Planla</button>
                <button onClick={clearPlans} style={{ marginLeft: 8 }}>Planları Temizle</button>
              </div>
              {planMsg && <div className="panel-msg">{planMsg}</div>}

              <div style={{ marginTop: 8, overflowY: "auto", maxHeight: "220px" }}>
                {trips.length === 0 && <div style={{ color: "#bbb" }}>Plan yok</div>}
                {trips.map((t, idx) => (
                  <div key={`plan-${idx}`} style={{ padding: 6, borderBottom: "1px solid rgba(255,255,255,0.04)" }}>
                    <div><strong>Sefer ID:</strong> {t.tripId}</div>
                    <div><strong>Araç ID:</strong> {t.vehicleId || "Kiralanan/Virtual"}</div>
                    <div><strong>Mesafe (km):</strong> {Number(t.distanceKm).toFixed(2)}</div>
                    <div><strong>Toplam Maliyet:</strong> {Number(t.totalCost).toFixed(2)}</div>
                    <div><strong>Kargolar:</strong> {t.shipmentIds?.length || 0}</div>
                  </div>
                ))}
              </div>
            </>
          )}
        </div>
      </div>

      {/* Oluşan Rotalar Paneli */}
      <div className={`side-panel ${openRoutesPanel ? "open" : ""}`}>
        <div className="side-panel-header">
          <strong>Oluşan Rotalar (Admin)</strong>
        </div>
        <div className="side-panel-body">
          <div style={{ marginTop: 8 }}>
            <button onClick={fetchAllRoutes}>Rotaları Yenile</button>
            <button onClick={() => togglePanel('routes')} style={{ marginLeft: 8 }}>
              Kapat
            </button>
          </div>

          {routesMsg && <div className="panel-msg">{routesMsg}</div>}

          <div style={{ marginTop: 8, overflowY: "auto", maxHeight: "400px" }}>
            {allRoutes.length === 0 && <div style={{ color: "#bbb" }}>Henüz rota oluşturulmamış</div>}
            {allRoutes.map((route, idx) => (
              <div key={`route-${idx}`} style={{ 
                padding: 8, 
                marginBottom: 8,
                borderBottom: "1px solid rgba(255,255,255,0.1)",
                backgroundColor: "rgba(0,0,0,0.2)",
                borderRadius: 4
              }}>
                <div><strong>Sefer ID:</strong> {route.tripId}</div>
                <div><strong>Sefer Tarihi:</strong> {route.tripDate}</div>
                <div><strong>Araç ID:</strong> {route.vehicleId}</div>
                <div><strong>Mesafe:</strong> {Number(route.distanceKm).toFixed(2)} km</div>
                <div><strong>Yol Maliyeti:</strong> {Number(route.roadCost).toFixed(2)} TL</div>
                <div><strong>Kiralama Maliyeti:</strong> {Number(route.rentalCost).toFixed(2)} TL</div>
                <div><strong>Toplam Maliyet:</strong> {Number(route.totalCost).toFixed(2)} TL</div>
                <div><strong>Oluşturulma:</strong> {route.createdAt}</div>
                <div style={{ marginTop: 4 }}>
                  <strong>Ziyaret Edilen İstasyonlar:</strong>
                  <div style={{ fontSize: "0.85em", color: "#aaa" }}>
                    {route.stationOrder?.length > 0 ? route.stationOrder.join(" → ") : "Bilgi yok"}
                  </div>
                </div>
                <div style={{ marginTop: 4 }}>
                  <strong>Kargolar ({route.shipments?.length || 0}):</strong>
                  <div style={{ fontSize: "0.85em", color: "#aaa" }}>
                    {route.shipments?.map((sh, i) => (
                      <div key={`ship-${i}`}>
                        • Kargo #{sh.shipmentId} - {sh.stationName} ({sh.weightKg} kg)
                      </div>
                    )) || "Yok"}
                  </div>
                </div>
                <div style={{ marginTop: 6 }}>
                  <button 
                    onClick={() => showRouteOnMap(route)}
                    style={{ fontSize: "0.85em", padding: "4px 8px" }}
                  >
                    🗺️ Haritada Göster
                  </button>
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* Kargolarım Paneli */}
      <div className={`side-panel ${openMyShipmentsPanel ? "open" : ""}`}>
        <div className="side-panel-header">
          <strong>Kargolarım</strong>
        </div>
        <div className="side-panel-body">
          <div style={{ marginTop: 8 }}>
            <button onClick={fetchMyShipments}>Kargoları Yenile</button>
            <button onClick={() => togglePanel('myshipments')} style={{ marginLeft: 8 }}>
              Kapat
            </button>
          </div>

          {myShipmentsMsg && <div className="panel-msg">{myShipmentsMsg}</div>}

          <div style={{ marginTop: 8, overflowY: "auto", maxHeight: "400px" }}>
            {myShipments.length === 0 && <div style={{ color: "#bbb" }}>Henüz kargo bulunmuyor</div>}
            {myShipments.map((shipment, idx) => (
              <div key={`shipment-${idx}`} style={{ 
                padding: 8, 
                marginBottom: 8,
                borderBottom: "1px solid rgba(255,255,255,0.1)",
                backgroundColor: shipment.tripId ? "rgba(16, 185, 129, 0.1)" : "rgba(239, 68, 68, 0.1)",
                borderRadius: 4,
                borderLeft: shipment.tripId ? "3px solid #10b981" : "3px solid #ef4444"
              }}>
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                  <div><strong>Kargo #{shipment.shipmentId}</strong></div>
                  <div style={{ 
                    fontSize: "0.75em", 
                    padding: "2px 8px", 
                    borderRadius: "12px",
                    backgroundColor: shipment.status === 'Pending' ? "#f59e0b" : 
                                    shipment.status === 'Delivered' ? "#10b981" : "#3b82f6",
                    color: "white"
                  }}>
                    {shipment.status}
                  </div>
                </div>
                <div style={{ fontSize: "0.9em", marginTop: 4 }}>
                  <div><strong>İçerik:</strong> {shipment.content || "Belirtilmemiş"}</div>
                  <div><strong>Hedef İstasyon:</strong> {shipment.stationName}</div>
                  <div><strong>Ağırlık:</strong> {shipment.weightKg} kg</div>
                  <div><strong>Sevk Tarihi:</strong> {shipment.shipDate}</div>
                  <div><strong>Oluşturulma:</strong> {shipment.createdAt}</div>
                </div>
                
                {shipment.tripId ? (
                  <div style={{ marginTop: 8, padding: 6, backgroundColor: "rgba(16, 185, 129, 0.15)", borderRadius: 4 }}>
                    <div style={{ fontSize: "0.85em", fontWeight: "600", color: "#10b981" }}>
                      ✅ Araç Atandı
                    </div>
                    <div style={{ fontSize: "0.8em", marginTop: 4 }}>
                      <div><strong>Araç:</strong> {shipment.vehicleName || `Araç #${shipment.vehicleId}`}</div>
                      <div><strong>Sefer ID:</strong> {shipment.tripId}</div>
                      <div><strong>Sefer Tarihi:</strong> {shipment.tripDate}</div>
                      {shipment.totalDistanceKm && (
                        <div><strong>Mesafe:</strong> {Number(shipment.totalDistanceKm).toFixed(2)} km</div>
                      )}
                      {isAdmin && shipment.totalCost && (
                        <div><strong>Maliyet:</strong> {Number(shipment.totalCost).toFixed(2)} TL</div>
                      )}
                    </div>
                    <button
                      onClick={() => showShipmentRouteOnMap(shipment)}
                      style={{
                        marginTop: 8,
                        width: "100%",
                        padding: "6px 12px",
                        backgroundColor: "#3b82f6",
                        color: "white",
                        border: "none",
                        borderRadius: 4,
                        cursor: "pointer",
                        fontSize: "0.85em",
                        fontWeight: "500"
                      }}
                    >
                      🗺️ Rotayı Haritada Göster
                    </button>
                  </div>
                ) : (
                  <div style={{ marginTop: 8, padding: 6, backgroundColor: "rgba(239, 68, 68, 0.15)", borderRadius: 4 }}>
                    <div style={{ fontSize: "0.85em", fontWeight: "600", color: "#ef4444" }}>
                      ⏳ Araç Bekliyor
                    </div>
                    <div style={{ fontSize: "0.8em", marginTop: 4, color: "#bbb" }}>
                      Kargo henüz bir araca atanmadı. Planlama yapıldığında araç bilgileri görünecektir.
                    </div>
                  </div>
                )}
              </div>
            ))}
          </div>
        </div>
      </div>

      <div className="map-wrapper">
        <MapContainer
          center={KOCAELI_CENTER}
          zoom={11}
          scrollWheelZoom={true}
          style={{ width: "100%", height: "100%" }}
        >
          <TileLayer
            url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
            attribution="© OpenStreetMap contributors"
            updateWhenIdle={true}
            keepBuffer={1}
          />

          {stations.map((station) => (
            <Marker key={station.id} position={[station.latitude, station.longitude]}>
              <Popup>
                <div>
                  <strong>{station.name}</strong>
                  <br />
                  <small>
                    Lat: {station.latitude.toFixed(4)}, Lng:{" "}
                    {station.longitude.toFixed(4)}
                  </small>
                </div>
              </Popup>
            </Marker>
          ))}

          {points.map((p, i) => (
            <Marker key={`user-${i}`} position={[p.lat, p.lng]} />
          ))}

          {path.length > 0 && (
            <Polyline positions={path} color="#8b5cf6" weight={6} opacity={0.9} />
          )}

          {/* Planlanan seferleri çiz */}
          {trips.map((t, i) => (
            <Polyline
              key={`trip-${i}`}
              positions={t.polyline || []}
              color={colors[i % colors.length]}
              weight={4}
              opacity={0.85}
            >
            </Polyline>
          ))}
        </MapContainer>
      </div>
    </div>
  );
}

export default MapPage;
