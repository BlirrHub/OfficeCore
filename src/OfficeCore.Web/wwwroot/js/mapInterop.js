window.OfficeCoreMap = {
    mapInstance: null,
    markersLayer: null,
    searchMarker: null,

    initializeMap: function (elementId, initialLat, initialLng, zoomLevel) {
        var container = document.getElementById(elementId);
        if (!container) return false;

        try {
            // Destroy existing instance cleanly
            if (this.mapInstance) {
                try { this.mapInstance.remove(); } catch (e) {}
                this.mapInstance = null;
                this.markersLayer = null;
                this.searchMarker = null;
            }

            var lat = initialLat || 14.5995;
            var lng = initialLng || 120.9842;
            var zoom = zoomLevel || 13;

            this.mapInstance = L.map(elementId, {
                center: [lat, lng],
                zoom: zoom,
                zoomControl: true,
                attributionControl: true
            });

            // Dark-themed CartoDB Voyager tiles
            L.tileLayer('https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png', {
                attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> &copy; <a href="https://carto.com/">CARTO</a>',
                subdomains: 'abcd',
                maxZoom: 20
            }).addTo(this.mapInstance);

            this.markersLayer = L.layerGroup().addTo(this.mapInstance);

            // Ensure correct render size after container layout settles
            var self = this;
            setTimeout(function () { if (self.mapInstance) self.mapInstance.invalidateSize(); }, 100);
            setTimeout(function () { if (self.mapInstance) self.mapInstance.invalidateSize(); }, 500);

            return true;
        } catch (err) {
            console.error('[OfficeCoreMap] Leaflet init error:', err);
            return false;
        }
    },

    updateStaffMarkers: function (staffList) {
        if (!this.mapInstance || !this.markersLayer) return;

        this.markersLayer.clearLayers();

        if (!staffList || staffList.length === 0) return;

        var bounds = [];

        staffList.forEach(function (staff) {
            var lat = staff.latitude;
            var lng = staff.longitude;
            if (!lat || !lng) return;
            bounds.push([lat, lng]);

            var pulseColor = staff.isOnline ? '#10b981' : '#71717a';
            var borderColor = staff.isOnline ? '#34d399' : '#52525b';

            var customIcon = L.divIcon({
                className: '',
                html: `
                    <div style="position:relative;width:44px;height:44px;display:flex;align-items:center;justify-content:center;">
                        <div style="position:absolute;width:44px;height:44px;border-radius:50%;background:${pulseColor};opacity:0.25;animation:oc-ping 1.8s cubic-bezier(0,0,0.2,1) infinite;"></div>
                        <div style="position:relative;width:34px;height:34px;border-radius:50%;background:#18181b;border:2.5px solid ${borderColor};display:flex;align-items:center;justify-content:center;box-shadow:0 4px 14px rgba(0,0,0,0.7);font-size:16px;">
                            🛵
                        </div>
                        <span style="position:absolute;bottom:-1px;right:-1px;width:11px;height:11px;border-radius:50%;background:${pulseColor};border:2px solid #09090b;"></span>
                    </div>
                `,
                iconSize: [44, 44],
                iconAnchor: [22, 22],
                popupAnchor: [0, -24]
            });

            var lastActive = new Date(staff.lastPingTime).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
            var popupContent = `
                <div style="font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;min-width:200px;padding:4px 2px;">
                    <div style="font-weight:700;font-size:14px;color:#f4f4f5;margin-bottom:2px;">${staff.employeeName}</div>
                    <div style="font-size:11px;color:#a1a1aa;margin-bottom:8px;">${staff.department} &bull; ${staff.role}</div>
                    <div style="display:inline-flex;align-items:center;gap:4px;font-size:10px;font-weight:700;background:${staff.isOnline ? '#052e16' : '#1c1c1e'};color:${staff.isOnline ? '#34d399' : '#71717a'};padding:3px 8px;border-radius:6px;margin-bottom:8px;">
                        <span style="width:6px;height:6px;border-radius:50%;background:${staff.isOnline ? '#34d399' : '#71717a'};"></span>
                        ${staff.isOnline ? 'ACTIVE OUTSIDE' : 'IDLE'}
                    </div>
                    <div style="font-size:12px;color:#d4d4d8;margin-bottom:4px;"><b style="color:#a1a1aa;">Task:</b> ${staff.statusNote || 'Standard Delivery / Route'}</div>
                    ${staff.speedKmh ? `<div style="font-size:11px;color:#a1a1aa;"><b>Speed:</b> ~${staff.speedKmh.toFixed(1)} km/h</div>` : ''}
                    <div style="font-size:10px;color:#71717a;margin-top:6px;border-top:1px solid #27272a;padding-top:6px;">
                        GPS: ${lat.toFixed(4)}, ${lng.toFixed(4)} &bull; Last ping: ${lastActive}
                    </div>
                </div>
            `;

            var marker = L.marker([lat, lng], { icon: customIcon })
                .bindPopup(popupContent, {
                    className: 'oc-popup',
                    maxWidth: 260
                });

            OfficeCoreMap.markersLayer.addLayer(marker);
        });

        if (bounds.length > 0) {
            if (bounds.length === 1) {
                this.mapInstance.setView(bounds[0], 15);
            } else {
                this.mapInstance.fitBounds(bounds, { padding: [60, 60], maxZoom: 16 });
            }
        }
    },

    panToStaff: function (lat, lng) {
        if (!this.mapInstance) return;
        this.mapInstance.setView([lat, lng], 16, { animate: true, duration: 0.8 });
    },

    searchLocation: async function (query, dotNetHelper) {
        if (!query || query.trim().length < 2) return;
        try {
            var url = `https://nominatim.openstreetmap.org/search?q=${encodeURIComponent(query)}&format=json&limit=5&addressdetails=1`;
            var resp = await fetch(url, { headers: { 'Accept-Language': 'en' } });
            if (!resp.ok) {
                await dotNetHelper.invokeMethodAsync('OnSearchResults', []);
                return;
            }
            var data = await resp.json();
            var results = data.map(r => ({
                displayName: r.display_name,
                lat: parseFloat(r.lat),
                lng: parseFloat(r.lon)
            }));
            await dotNetHelper.invokeMethodAsync('OnSearchResults', results);
        } catch (e) {
            console.warn('[OfficeCoreMap] Search error:', e);
            await dotNetHelper.invokeMethodAsync('OnSearchResults', []);
        }
    },

    goToLocation: function (lat, lng, label) {
        if (!this.mapInstance) return;

        if (this.searchMarker) {
            this.mapInstance.removeLayer(this.searchMarker);
            this.searchMarker = null;
        }

        var pinIcon = L.divIcon({
            className: '',
            html: `
                <div style="display:flex;flex-direction:column;align-items:center;">
                    <div style="width:32px;height:32px;border-radius:50% 50% 50% 0;transform:rotate(-45deg);background:linear-gradient(135deg,#a855f7,#7c3aed);border:3px solid #fff;box-shadow:0 4px 14px rgba(0,0,0,0.5);"></div>
                    <div style="width:6px;height:6px;border-radius:50%;background:#a855f7;margin-top:2px;box-shadow:0 0 6px #a855f7;"></div>
                </div>
            `,
            iconSize: [32, 42],
            iconAnchor: [16, 42],
            popupAnchor: [0, -44]
        });

        this.searchMarker = L.marker([lat, lng], { icon: pinIcon })
            .bindPopup(`<div style="font-family:sans-serif;font-size:13px;color:#18181b;max-width:240px;">${label}</div>`)
            .addTo(this.mapInstance)
            .openPopup();

        this.mapInstance.setView([lat, lng], 15, { animate: true, duration: 0.9 });
    },

    invalidate: function () {
        if (this.mapInstance) this.mapInstance.invalidateSize();
    }
};

// Ping animation keyframe injection
(function () {
    if (!document.getElementById('oc-map-styles')) {
        var style = document.createElement('style');
        style.id = 'oc-map-styles';
        style.textContent = `
            @keyframes oc-ping {
                0%   { transform: scale(1); opacity: 0.4; }
                75%, 100% { transform: scale(2.2); opacity: 0; }
            }
            .oc-popup .leaflet-popup-content-wrapper {
                background: #18181b;
                border: 1px solid #27272a;
                border-radius: 14px;
                box-shadow: 0 20px 40px rgba(0,0,0,0.7);
                color: #f4f4f5;
            }
            .oc-popup .leaflet-popup-tip {
                background: #18181b;
            }
            .leaflet-container {
                font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            }
            .leaflet-control-attribution {
                background: rgba(9,9,11,0.7) !important;
                color: #52525b !important;
                font-size: 9px !important;
            }
            .leaflet-control-attribution a {
                color: #71717a !important;
            }
        `;
        document.head.appendChild(style);
    }
})();
