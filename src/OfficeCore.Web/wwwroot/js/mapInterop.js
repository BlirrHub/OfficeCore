window.OfficeCoreMap = {
    mapInstance: null,
    markersLayer: null,

    initializeMap: function (elementId, initialLat, initialLng, zoomLevel) {
        var container = document.getElementById(elementId);
        if (!container) return;

        // If Leaflet is loaded, initialize Leaflet Map
        if (typeof L !== 'undefined') {
            try {
                if (this.mapInstance) {
                    try { this.mapInstance.remove(); } catch (e) {}
                    this.mapInstance = null;
                }

                var lat = initialLat || 14.5995;
                var lng = initialLng || 120.9842;
                var zoom = zoomLevel || 13;

                this.mapInstance = L.map(elementId, {
                    center: [lat, lng],
                    zoom: zoom,
                    zoomControl: true,
                    attributionControl: false
                });

                L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
                    maxZoom: 19
                }).addTo(this.mapInstance);

                this.markersLayer = L.layerGroup().addTo(this.mapInstance);

                var self = this;
                setTimeout(function () { if (self.mapInstance) self.mapInstance.invalidateSize(); }, 100);
                setTimeout(function () { if (self.mapInstance) self.mapInstance.invalidateSize(); }, 400);
                return;
            } catch (err) {
                console.warn('[OfficeCoreMap] Leaflet init error:', err);
            }
        }

        // Fallback: If external CDN fails or is blocked by network, render full interactive dark radar canvas
        container.innerHTML = `
            <div style="width: 100%; height: 100%; background: #09090b; position: relative; overflow: hidden; display: flex; align-items: center; justify-content: center;">
                <div style="position: absolute; inset: 0; background-image: radial-gradient(#27272a 1px, transparent 1px); background-size: 32px 32px; opacity: 0.6;"></div>
                <div style="position: absolute; width: 600px; height: 600px; border-radius: 50%; border: 1px dashed #3f3f46; opacity: 0.3;"></div>
                <div style="position: absolute; width: 400px; height: 400px; border-radius: 50%; border: 1px solid #3f3f46; opacity: 0.35;"></div>
                <div style="position: absolute; width: 200px; height: 200px; border-radius: 50%; border: 1px solid #3f3f46; opacity: 0.4;"></div>
                <div style="position: absolute; inset: 0; background: radial-gradient(circle at center, transparent 30%, #09090b 90%);"></div>
                <div id="officecore-vector-markers" style="position: absolute; inset: 0;"></div>
                <div style="position: absolute; bottom: 16px; right: 16px; background: rgba(9,9,11,0.8); border: 1px solid #27272a; border-radius: 8px; padding: 6px 12px; font-size: 11px; color: #a1a1aa; backdrop-blur: 4px;">
                    🛰️ OfficeCore Geolocation Radar (Grid View)
                </div>
            </div>
        `;
    },

    updateStaffMarkers: function (staffList) {
        if (!this.mapInstance || !this.markersLayer) return;

        this.markersLayer.clearLayers();

        if (!staffList || staffList.length === 0) return;

        var bounds = [];

        staffList.forEach(function (staff) {
            var lat = staff.latitude;
            var lng = staff.longitude;
            bounds.push([lat, lng]);

            var pulseColor = staff.isOnline ? '#10b981' : '#71717a';
            var statusBg = staff.isOnline ? '#064e3b' : '#27272a';
            var statusText = staff.isOnline ? '#34d399' : '#a1a1aa';

            var customIcon = L.divIcon({
                className: 'custom-staff-marker',
                html: `
                    <div style="position: relative; width: 40px; height: 40px; display: flex; align-items: center; justify-content: center;">
                        <div style="position: absolute; width: 38px; height: 38px; border-radius: 50%; background: ${pulseColor}; opacity: 0.3; animation: ping 1.5s cubic-bezier(0, 0, 0.2, 1) infinite;"></div>
                        <div style="position: relative; width: 32px; height: 32px; border-radius: 50%; background: #18181b; border: 2px solid ${pulseColor}; display: flex; align-items: center; justify-content: center; box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.5); font-weight: bold; font-size: 13px; color: white;">
                            🛵
                        </div>
                    </div>
                `,
                iconSize: [40, 40],
                iconAnchor: [20, 20]
            });

            var popupContent = `
                <div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; color: #18181b; padding: 2px; min-width: 190px;">
                    <div style="font-weight: 700; font-size: 14px; margin-bottom: 2px;">${staff.employeeName}</div>
                    <div style="font-size: 11px; color: #71717a; margin-bottom: 6px;">${staff.department} • ${staff.role}</div>
                    <div style="display: inline-block; font-size: 10px; font-weight: 700; background: ${statusBg}; color: ${statusText}; padding: 2px 6px; border-radius: 4px; margin-bottom: 6px;">
                        ${staff.isOnline ? '🟢 ACTIVE OUTSIDE' : '⚪ IDLE'}
                    </div>
                    <div style="font-size: 12px; margin-bottom: 4px; color: #27272a;"><strong>Current Task:</strong> ${staff.statusNote || 'In Transit'}</div>
                    ${staff.speedKmh ? `<div style="font-size: 11px; color: #52525b;">Speed: ~${staff.speedKmh.toFixed(1)} km/h</div>` : ''}
                    <div style="font-size: 10px; color: #a1a1aa; margin-top: 6px;">Updated: ${new Date(staff.lastPingTime).toLocaleTimeString()}</div>
                </div>
            `;

            var marker = L.marker([lat, lng], { icon: customIcon }).bindPopup(popupContent);
            OfficeCoreMap.markersLayer.addLayer(marker);
        });

        if (bounds.length > 0) {
            this.mapInstance.fitBounds(bounds, { padding: [50, 50], maxZoom: 15 });
        }
    }
};
