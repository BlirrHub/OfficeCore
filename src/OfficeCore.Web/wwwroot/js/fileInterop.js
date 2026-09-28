window.fileInterop = {
    downloadFile: function(url, fileName) {
        fetch(url)
            .then(response => {
                if (!response.ok) throw new Error('Download request failed');
                return response.blob();
            })
            .then(blob => {
                const blobUrl = window.URL.createObjectURL(blob);
                const a = document.createElement('a');
                a.style.display = 'none';
                a.href = blobUrl;
                a.download = fileName || 'download';
                document.body.appendChild(a);
                a.click();
                setTimeout(() => {
                    document.body.removeChild(a);
                    window.URL.revokeObjectURL(blobUrl);
                }, 300);
            })
            .catch(err => {
                console.warn('Blob download fallback to direct anchor:', err);
                const a = document.createElement('a');
                a.target = '_blank';
                a.href = url;
                a.download = fileName || 'download';
                document.body.appendChild(a);
                a.click();
                setTimeout(() => document.body.removeChild(a), 300);
            });
    }
};
