// Canvas-based eating animation system
// Erases organic bite-shaped chunks from an ass image using destination-out compositing.
window.assCanvas = (function () {
    const states = {};

    function generateBites(n, w, h) {
        const cols = Math.ceil(Math.sqrt(n));
        const rows = Math.ceil(n / cols);
        const cellW = w / cols;
        const cellH = h / rows;
        const bites = [];

        for (let i = 0; i < n; i++) {
            const col = i % cols;
            const row = Math.floor(i / cols);
            // Random position within the cell (15%–85% range to avoid edge bleed)
            const cx = (col + 0.15 + Math.random() * 0.7) * cellW;
            const cy = (row + 0.15 + Math.random() * 0.7) * cellH;
            // Ellipse radii: 40–75% of cell dimensions for organic variation
            const rx = cellW * (0.4 + Math.random() * 0.35);
            const ry = cellH * (0.4 + Math.random() * 0.35);
            const rotation = Math.random() * Math.PI;
            bites.push({ cx, cy, rx, ry, rotation });
        }

        // Fisher-Yates shuffle so bite order is spatially non-sequential
        for (let i = bites.length - 1; i > 0; i--) {
            const j = Math.floor(Math.random() * (i + 1));
            [bites[i], bites[j]] = [bites[j], bites[i]];
        }

        return bites;
    }

    function draw(state, count) {
        const { canvas, img, bites } = state;
        const ctx = canvas.getContext('2d');

        ctx.clearRect(0, 0, canvas.width, canvas.height);
        ctx.globalCompositeOperation = 'source-over';
        ctx.drawImage(img, 0, 0, canvas.width, canvas.height);

        if (count <= 0) return;

        ctx.globalCompositeOperation = 'destination-out';
        const limit = Math.min(count, bites.length);
        for (let i = 0; i < limit; i++) {
            const { cx, cy, rx, ry, rotation } = bites[i];
            ctx.save();
            ctx.translate(cx, cy);
            ctx.rotate(rotation);
            ctx.beginPath();
            ctx.ellipse(0, 0, rx, ry, 0, 0, Math.PI * 2);
            ctx.fill();
            ctx.restore();
        }

        ctx.globalCompositeOperation = 'source-over';
    }

    function init(canvasId, imageUrl, biteCount) {
        const canvas = document.getElementById(canvasId);
        if (!canvas) return;

        delete states[canvasId];

        const img = new Image();
        img.onload = function () {
            canvas.width = img.naturalWidth;
            canvas.height = img.naturalHeight;

            const state = { canvas, img, biteCount, bites: generateBites(biteCount, canvas.width, canvas.height) };
            states[canvasId] = state;
            draw(state, 0);
        };
        img.src = imageUrl;
    }

    function setBites(canvasId, count) {
        const state = states[canvasId];
        if (state) draw(state, count);
    }

    function cleanup(canvasId) {
        delete states[canvasId];
    }

    return { init, setBites, cleanup };
})();
