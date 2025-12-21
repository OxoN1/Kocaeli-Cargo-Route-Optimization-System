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

  // İstasyonları backend'den çek
  useEffect(() => {
    const fetchStations = async () => {
      try {
        setLoading(true);
        const response = await fetch('http://localhost:5000/api/station/stations');
        
        if (!response.ok) {
          throw new Error('İstasyonlar yüklenemedi');
        }
        
        const data = await response.json();
        setStations(data);
        setError(null);
      } catch (err) {
        console.error('İstasyon yükleme hatası:', err);
        setError(err.message);
      } finally {
        setLoading(false);
      }
    };

    fetchStations();
  }, []);


  return (
    <div className="map-page-container">
      {/* Üst Menü */}
      <nav className="top-navbar">
        <div className="navbar-content">
          <h1 className="navbar-title">📦 Kargo Simülatörü</h1>
          {loading && <span style={{color: '#fff', marginLeft: '20px'}}>İstasyonlar yükleniyor...</span>}
          {error && <span style={{color: '#ff6b6b', marginLeft: '20px'}}>Hata: {error}</span>}
          {!loading && !error && <span style={{color: '#51cf66', marginLeft: '20px'}}>✓ {stations.length} istasyon yüklendi</span>}
        </div>
      </nav>

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

          {/* Veritabanından gelen istasyonlar */}
          {stations.map((station) => (
            <Marker 
              key={station.id} 
              position={[station.latitude, station.longitude]}
            >
              <Popup>
                <div>
                  <strong>{station.name}</strong>
                  <br />
                  <small>
                    Lat: {station.latitude.toFixed(4)}, 
                    Lng: {station.longitude.toFixed(4)}
                  </small>
                </div>
              </Popup>
            </Marker>
          ))}

          {/* Kullanıcı tarafından eklenen noktalar */}
          {points.map((p, i) => (
            <Marker key={`user-${i}`} position={[p.lat, p.lng]} />
          ))}

          {path.length > 0 && (
            <Polyline
              positions={path}
              color="#8b5cf6"
              weight={6}
              opacity={0.9}
            />
          )}
        </MapContainer>
      </div>
    </div>
  );
}

export default MapPage;
