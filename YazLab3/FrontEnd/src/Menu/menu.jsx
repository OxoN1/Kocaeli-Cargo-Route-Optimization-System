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

  // Shipment panel
  const [selectedStationId, setSelectedStationId] = useState("");
  const [weightKg, setWeightKg] = useState("");
  const [submitMsg, setSubmitMsg] = useState("");

  // Admin station add panel
  const [newStationName, setNewStationName] = useState("");
  const [newStationLat, setNewStationLat] = useState("");
  const [newStationLng, setNewStationLng] = useState("");
  const [adminMsg, setAdminMsg] = useState("");

  // Yeni: Route draw panel
  const [fromStationId, setFromStationId] = useState("");
  const [toStationId, setToStationId] = useState("");
  const [routeMsg, setRouteMsg] = useState("");

  // UI control: panel açık/kapalı
  const [openShipmentPanel, setOpenShipmentPanel] = useState(false);
  const [openAdminPanel, setOpenAdminPanel] = useState(false);
  const [openRoutePanel, setOpenRoutePanel] = useState(false);

  // Kullanıcı admin mi?
  const [isAdmin, setIsAdmin] = useState(false);

  // herhangi bir panel açık mı? -> FAB'ları gizlemek için
  const anyPanelOpen = openShipmentPanel || openAdminPanel || openRoutePanel;

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

      if (!stationId || stationId <= 0) {
        setSubmitMsg("İstasyon seçiniz.");
        return;
      }

      if (!kg || kg <= 0) {
        setSubmitMsg("Ağırlık 0'dan büyük olmalıdır.");
        return;
      }

      const response = await fetch("http://localhost:5000/api/shipments", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          email,
          stationId,
          weightKg: kg,
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

      const response = await fetch("http://localhost:5000/api/tripplanner/plan-next-day", {
        method: "POST",
      });

      const data = await response.json().catch(() => null);

      if (!response.ok) {
        setPlanMsg("Hata: " + (data?.mesaj || "Planlama başarısız."));
        return;
      }

      setTrips(data.plans || []);
      setPlanMsg(data.mesaj || "Planlama tamamlandı.");
    } catch (e) {
      console.error(e);
      setPlanMsg("Sunucuya bağlanılamadı.");
    }
  };

  const clearPlans = () => {
    setTrips([]);
    setPlanMsg("");
  };

  // renk paleti her sefer için farklı renk
  const colors = [
    "#ef4444", "#f97316", "#f59e0b", "#10b981", "#06b6d4", "#3b82f6", "#8b5cf6", "#ec4899"
  ];

  return (
    <div className="map-page-container">
      <nav className="top-navbar">
        <div className="navbar-content">
          <h1 className="navbar-title">📦 Kargo Simülatörü</h1>
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
          {!loading && !error && (
            <span style={{ color: "#51cf66", marginLeft: "20px" }}>
              ✓ {stations.length} istasyon yüklendi
            </span>
          )}
        </div>
      </nav>

      {/* Floating action buttons (sağ/sol yerine üst sağ) */}
      {/* FAB'ları panel açık olduğunda gizle */}
      <div className="fab-container" style={{ display: anyPanelOpen ? "none" : "flex" }}>
        <button className="fab-button" onClick={() => setOpenShipmentPanel((v) => !v)}>
          Kargo Talebi
        </button>

        {isAdmin && (
          <>
            <button className="fab-button" onClick={() => setOpenAdminPanel((v) => !v)}>
              İstasyon Ekle (Admin)
            </button>
            <button className="fab-button" onClick={() => setOpenRoutePanel((v) => !v)}>
              Rota Çiz (Admin)
            </button>
            <button className="fab-button" onClick={clearPlans}>
              Planları Temizle
            </button>
          </>
        )}
      </div>

      {/* Slide-in yan paneller */}
      <div className={`side-panel ${openShipmentPanel ? "open" : ""}`}>
        <div className="side-panel-header">
          <strong>Kargo Talebi</strong>
        </div>
        <div className="side-panel-body">
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

          <label>Ağırlık (kg):</label>
          <input
            type="number"
            value={weightKg}
            onChange={(e) => setWeightKg(e.target.value)}
            min="1"
          />

          <div style={{ marginTop: 8 }}>
            <button onClick={submitShipment}>Kargo Talebi Gönder</button>
            <button onClick={() => setOpenShipmentPanel(false)} style={{ marginLeft: 8 }}>
              Kapat
            </button>
          </div>

          {submitMsg && <div className="panel-msg">{submitMsg}</div>}
        </div>
      </div>

      <div className={`side-panel ${openAdminPanel ? "open" : ""}`}>
        <div className="side-panel-header">
          <strong>İstasyon Ekle (Admin)</strong>
        </div>
        <div className="side-panel-body">
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
            <button onClick={() => setOpenAdminPanel(false)} style={{ marginLeft: 8 }}>
              Kapat
            </button>
          </div>

          {adminMsg && <div className="panel-msg">{adminMsg}</div>}
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
            <button onClick={() => setOpenRoutePanel(false)} style={{ marginLeft: 8 }}>
              Kapat
            </button>
          </div>

          {routeMsg && <div className="panel-msg">{routeMsg}</div>}

          {/* Admin: plan sonuçlarını göster */}
          {isAdmin && (
            <>
              <hr style={{ margin: "8px 0", borderColor: "rgba(255,255,255,0.06)" }} />
              <strong>Plan Sonuçları</strong>
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
