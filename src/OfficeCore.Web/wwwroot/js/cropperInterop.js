window.cropperInterop = {
    cropper: null,
    init: function (imgElement) {
        if (this.cropper) {
            this.cropper.destroy();
        }
        this.cropper = new Cropper(imgElement, {
            aspectRatio: 1,
            viewMode: 1,
            dragMode: 'move',
            autoCropArea: 1,
            restore: false,
            guides: false,
            center: false,
            highlight: false,
            cropBoxMovable: true,
            cropBoxResizable: true,
            toggleDragModeOnDblclick: false,
        });
    },
    getCroppedCanvasData: function () {
        if (!this.cropper) return null;
        return this.cropper.getCroppedCanvas({
            width: 256,
            height: 256
        }).toDataURL('image/jpeg', 0.9);
    },
    destroy: function () {
        if (this.cropper) {
            this.cropper.destroy();
            this.cropper = null;
        }
    }
};
