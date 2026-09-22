// OfficeCore Theme Management Interop
(function() {
    window.themeInterop = {
        getTheme: function() {
            try {
                return localStorage.getItem('officecore_theme') || 'dark';
            } catch(e) {
                return 'dark';
            }
        },
        setTheme: function(theme) {
            try {
                localStorage.setItem('officecore_theme', theme);
                if (theme === 'light') {
                    document.documentElement.classList.add('light');
                    document.documentElement.classList.remove('dark');
                } else {
                    document.documentElement.classList.add('dark');
                    document.documentElement.classList.remove('light');
                }
                window.dispatchEvent(new CustomEvent('officecore-theme-changed', { detail: theme }));
            } catch(e) {}
        },
        toggleTheme: function() {
            var current = window.themeInterop.getTheme();
            var next = current === 'dark' ? 'light' : 'dark';
            window.themeInterop.setTheme(next);
            return next;
        }
    };
})();
