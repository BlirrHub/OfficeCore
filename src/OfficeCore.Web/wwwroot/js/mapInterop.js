window.OfficeCoreMap = {
    mapInstance: null,
    markersLayer: null,
    searchMarker: null,
    userMarker: null,

    initializeMap: function (elementId, initialLat, initialLng, zoomLevel) {
        var container = document.getElementById(elementId);
        if (!container) return false;

        try {
            if (this.mapInstance) {
                try { this.mapInstance.remove(); } catch (e) {}
                this.mapInstance = null;
                this.markersLayer = null;
                this.searchMarker = null;
                this.userMarker = null;
            }

            var lat = initialLat || 14.5995;
            var lng = initialLng || 120.9842;
            var zoom = zoomLevel || 13;

            this.mapInstance = L.map(elementId, {
                center: [lat, lng],
                zoom: zoom,
                zoomControl: false,
                attributionControl: true
            });

            // Zoom control at bottom-right — away from all UI overlays
            L.control.zoom({ position: 'topright' }).addTo(this.mapInstance);

            // Free OpenStreetMap tiles — no API key, no cost
            L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
                maxZoom: 20,
                attribution: '&copy; <a href="https://www.openstreetmap.org/copyright" target="_blank">OpenStreetMap</a>'
            }).addTo(this.mapInstance);

            // Dark CSS filter on the tile pane
            var applyDark = function () {
                var panes = document.querySelectorAll('#' + elementId + ' .leaflet-tile-pane');
                panes.forEach(function (p) {
                    p.style.filter = 'invert(100%) hue-rotate(180deg) brightness(90%) contrast(88%) saturate(0.7)';
                });
            };
            setTimeout(applyDark, 250);
            setTimeout(applyDark, 800);

            this.markersLayer = L.layerGroup().addTo(this.mapInstance);

            var self = this;
            setTimeout(function () { if (self.mapInstance) self.mapInstance.invalidateSize(); }, 150);
            setTimeout(function () { if (self.mapInstance) self.mapInstance.invalidateSize(); }, 700);

            return true;
        } catch (err) {
            console.error('[OfficeCoreMap] init error:', err);
            return false;
        }
    },

    showUserLocation: function (profilePictureUrl, initials, displayName) {
        if (!this.mapInstance) return;
        if (!navigator.geolocation) return;

        var self = this;
        var userInitials = (initials || 'ME').toUpperCase();
        var nameLabel = displayName || 'You';

        navigator.geolocation.getCurrentPosition(function (pos) {
            var lat = pos.coords.latitude;
            var lng = pos.coords.longitude;

            if (self.userMarker) {
                self.mapInstance.removeLayer(self.userMarker);
                self.userMarker = null;
            }

            var avatarInner = profilePictureUrl
                ? `<img src="${profilePictureUrl}"
                        style="width:32px;height:32px;border-radius:50%;object-fit:cover;display:block;"
                        onerror="this.style.display='none';this.nextElementSibling.style.display='flex';" />
                   <div style="display:none;width:32px;height:32px;border-radius:50%;background:linear-gradient(135deg,#581c87,#18181b);align-items:center;justify-content:center;color:#e879f9;font-weight:700;font-size:12px;font-family:system-ui,sans-serif;letter-spacing:0.5px;">${userInitials}</div>`
                : `<div style="width:32px;height:32px;border-radius:50%;background:linear-gradient(135deg,#581c87,#18181b);display:flex;align-items:center;justify-content:center;color:#e879f9;font-weight:700;font-size:12px;font-family:system-ui,sans-serif;letter-spacing:0.5px;">${userInitials}</div>`;

            var userIcon = L.divIcon({
                className: '',
                html: `
                    <div style="position:relative;width:44px;height:44px;display:flex;align-items:center;justify-content:center;">
                        <div style="position:absolute;width:44px;height:44px;border-radius:50%;background:#c084fc;opacity:0.35;animation:oc-ping 2s cubic-bezier(0,0,0.2,1) infinite;"></div>
                        <div style="position:relative;width:34px;height:34px;border-radius:50%;border:2.5px solid #a855f7;box-shadow:0 0 14px rgba(168,85,247,0.6), 0 4px 12px rgba(0,0,0,0.8);background:#18181b;display:flex;align-items:center;justify-content:center;overflow:hidden;">
                            ${avatarInner}
                        </div>
                        <span style="position:absolute;bottom:0px;right:0px;width:11px;height:11px;border-radius:50%;background:#a855f7;border:2px solid #09090b;box-shadow:0 0 6px #a855f7;"></span>
                    </div>
                `,
                iconSize: [44, 44],
                iconAnchor: [22, 22],
                popupAnchor: [0, -24]
            });

            var acc = pos.coords.accuracy ? Math.round(pos.coords.accuracy) + 'm' : 'unknown';

            self.userMarker = L.marker([lat, lng], { icon: userIcon, zIndexOffset: 1000 })
                .bindPopup(`
                    <div style="font-family:system-ui,sans-serif;min-width:160px;padding:2px;">
                        <div style="display:flex;align-items:center;gap:8px;margin-bottom:6px;">
                            <div style="width:28px;height:28px;border-radius:50%;background:linear-gradient(135deg,#581c87,#18181b);border:1.5px solid #a855f7;display:flex;align-items:center;justify-content:center;font-size:10px;font-weight:700;color:#e879f9;overflow:hidden;shrink-0;">
                                ${profilePictureUrl ? `<img src="${profilePictureUrl}" style="width:100%;height:100%;object-fit:cover;" onerror="this.parentElement.innerHTML='${userInitials}'"/>` : userInitials}
                            </div>
                            <div>
                                <div style="font-weight:700;font-size:13px;color:#f4f4f5;line-height:1.2;">${nameLabel}</div>
                                <div style="font-size:10px;color:#c084fc;font-weight:600;">Your Location</div>
                            </div>
                        </div>
                        <div style="font-size:10px;color:#a1a1aa;">Accuracy: ±${acc}</div>
                        <div style="font-size:10px;color:#71717a;margin-top:2px;">${lat.toFixed(5)}, ${lng.toFixed(5)}</div>
                    </div>
                `, { className: 'oc-popup', maxWidth: 240 })
                .addTo(self.mapInstance)
                .openPopup();

            self.mapInstance.setView([lat, lng], 15, { animate: true });

        }, function (err) {
            console.warn('[OfficeCoreMap] Geolocation error:', err.message);
        }, { enableHighAccuracy: true, timeout: 8000 });
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

            var pulse = staff.isOnline ? '#10b981' : '#71717a';
            var border = staff.isOnline ? '#34d399' : '#52525b';

            // Use profile picture if available, fall back to 🛵 emoji
            var avatarInner = staff.profilePictureUrl
                ? `<img src="${staff.profilePictureUrl}"
                         style="width:30px;height:30px;border-radius:50%;object-fit:cover;display:block;"
                         onerror="this.style.display='none';this.nextSibling.style.display='flex';" />
                   <span style="display:none;width:30px;height:30px;align-items:center;justify-content:center;font-size:15px;">&#x1F6F5;</span>`
                : `<span style="font-size:15px;">&#x1F6F5;</span>`;

            var icon = L.divIcon({
                className: '',
                html: `
                    <div style="position:relative;width:44px;height:44px;display:flex;align-items:center;justify-content:center;">
                        <div style="position:absolute;width:44px;height:44px;border-radius:50%;background:${pulse};opacity:0.25;animation:oc-ping 1.8s cubic-bezier(0,0,0.2,1) infinite;"></div>
                        <div style="position:relative;width:34px;height:34px;border-radius:50%;background:#18181b;border:2.5px solid ${border};display:flex;align-items:center;justify-content:center;box-shadow:0 4px 14px rgba(0,0,0,0.7);overflow:hidden;">
                            ${avatarInner}
                        </div>
                        <span style="position:absolute;bottom:-1px;right:-1px;width:11px;height:11px;border-radius:50%;background:${pulse};border:2px solid #09090b;"></span>
                    </div>
                `,
                iconSize: [44, 44],
                iconAnchor: [22, 22],
                popupAnchor: [0, -24]
            });

            var last = new Date(staff.lastPingTime).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
            var popup = `
                <div style="font-family:system-ui,sans-serif;min-width:200px;padding:2px;">
                    <div style="font-weight:700;font-size:14px;color:#f4f4f5;margin-bottom:2px;">${staff.employeeName}</div>
                    <div style="font-size:11px;color:#a1a1aa;margin-bottom:8px;">${staff.department} &bull; ${staff.role}</div>
                    <div style="display:inline-flex;align-items:center;gap:4px;font-size:10px;font-weight:700;background:${staff.isOnline ? '#052e16' : '#1c1c1e'};color:${staff.isOnline ? '#34d399' : '#71717a'};padding:3px 8px;border-radius:6px;margin-bottom:8px;">
                        <span style="width:6px;height:6px;border-radius:50%;background:${staff.isOnline ? '#34d399' : '#71717a'};"></span>
                        ${staff.isOnline ? 'ACTIVE OUTSIDE' : 'IDLE'}
                    </div>
                    <div style="font-size:12px;color:#d4d4d8;"><b style="color:#a1a1aa;">Task:</b> ${staff.statusNote || 'Standard Delivery'}</div>
                    ${staff.speedKmh ? `<div style="font-size:11px;color:#a1a1aa;margin-top:4px;"><b>Speed:</b> ~${staff.speedKmh.toFixed(1)} km/h</div>` : ''}
                    <div style="font-size:10px;color:#71717a;margin-top:6px;border-top:1px solid #27272a;padding-top:6px;">
                        ${lat.toFixed(4)}, ${lng.toFixed(4)} &bull; ${last}
                    </div>
                </div>
            `;

            L.marker([lat, lng], { icon: icon })
                .bindPopup(popup, { className: 'oc-popup', maxWidth: 260 })
                .addTo(OfficeCoreMap.markersLayer);
        });

        if (bounds.length > 0) {
            bounds.length === 1
                ? this.mapInstance.setView(bounds[0], 15)
                : this.mapInstance.fitBounds(bounds, { padding: [60, 60], maxZoom: 16 });
        }
    },

    panToStaff: function (lat, lng) {
        if (!this.mapInstance) return;
        this.mapInstance.setView([lat, lng], 16, { animate: true, duration: 0.8 });
    },

    searchLocation: async function (query, dotNetHelper) {
        if (!query || query.trim().length < 2) return;
        try {
            var url = 'https://nominatim.openstreetmap.org/search?q=' + encodeURIComponent(query) + '&format=json&limit=5&addressdetails=1';
            var resp = await fetch(url, { headers: { 'Accept-Language': 'en', 'User-Agent': 'OfficeCoreApp/1.0' } });
            var data = resp.ok ? await resp.json() : [];
            var results = data.map(function (r) {
                return { displayName: r.display_name, lat: parseFloat(r.lat), lng: parseFloat(r.lon) };
            });
            await dotNetHelper.invokeMethodAsync('OnSearchResults', results);
        } catch (e) {
            console.warn('[OfficeCoreMap] search error:', e);
            await dotNetHelper.invokeMethodAsync('OnSearchResults', []);
        }
    },

    goToLocation: function (lat, lng, label) {
        if (!this.mapInstance) return;
        if (this.searchMarker) { this.mapInstance.removeLayer(this.searchMarker); this.searchMarker = null; }

        var pin = L.divIcon({
            className: '',
            html: `<div style="width:20px;height:20px;border-radius:50%;background:#a855f7;border:3px solid #fff;box-shadow:0 0 0 3px rgba(168,85,247,0.4),0 4px 12px rgba(168,85,247,0.5);"></div>`,
            iconSize: [20, 20],
            iconAnchor: [10, 10],
            popupAnchor: [0, -14]
        });

        this.searchMarker = L.marker([lat, lng], { icon: pin })
            .bindPopup('<div style="font-family:system-ui;font-size:12px;color:#f4f4f5;max-width:220px;">' + label + '</div>', { className: 'oc-popup', maxWidth: 260 })
            .addTo(this.mapInstance)
            .openPopup();

        this.mapInstance.setView([lat, lng], 15, { animate: true, duration: 0.9 });
    },

    invalidate: function () { if (this.mapInstance) this.mapInstance.invalidateSize(); }
};

(function () {
    if (document.getElementById('oc-map-styles')) return;
    var s = document.createElement('style');
    s.id = 'oc-map-styles';
    s.textContent = `
        @keyframes oc-ping {
            0%   { transform:scale(1); opacity:0.4; }
            75%, 100% { transform:scale(2.4); opacity:0; }
        }
        .oc-popup .leaflet-popup-content-wrapper {
            background:#18181b; border:1px solid #27272a; border-radius:14px;
            box-shadow:0 20px 40px rgba(0,0,0,0.7); color:#f4f4f5;
        }
        .oc-popup .leaflet-popup-tip { background:#18181b; }
        .leaflet-container { font-family:system-ui,sans-serif; background:#09090b; }
        .leaflet-control-attribution {
            background:rgba(9,9,11,0.75)!important; color:#52525b!important;
            font-size:9px!important; border-radius:6px 0 0 0!important;
        }
        .leaflet-control-attribution a { color:#71717a!important; }
        .leaflet-control-zoom a {
            background:#18181b!important; color:#d4d4d8!important; border-color:#27272a!important;
        }
        .leaflet-control-zoom a:hover { background:#27272a!important; color:#fff!important; }
    `;
    document.head.appendChild(s);
})();
