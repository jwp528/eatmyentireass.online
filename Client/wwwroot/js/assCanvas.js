// Canvas-based eating animation system
// Erases jagged bite-shaped chunks from the image border using destination-out compositing.
window.assCanvas = (function () {
    const states = {};

    // Fast seeded RNG (xorshift32) — deterministic jaggedness per bite
    function seededRng(seed) {
        let s = ((seed ^ 0x9e3779b9) >>> 0) || 1;
        return () => {
            s ^= s << 13;
            s ^= s >>> 17;
            s ^= s << 5;
            return (s >>> 0) / 0x100000000;
        };
    }

    // Draw one jagged-circle bite centered at (cx, cy) with radius r.
    // The shape alternates between tooth-tips and gap-valleys for a bitten look.
    function drawJaggedBite(ctx, cx, cy, r, seed) {
        const rng = seededRng(seed);
        const numTeeth = 9 + Math.floor(rng() * 4); // 9–12 teeth
        const steps = numTeeth * 2;
        ctx.beginPath();
        for (let i = 0; i <= steps; i++) {
            const angle = (i / steps) * Math.PI * 2;
            const isTip = i % 2 === 0;
            const jitter = rng() * 0.12;
            // Tooth tips protrude slightly beyond r; gaps dip inward
            const rr = isTip
                ? r * (0.96 + jitter)
                : r * (0.62 + jitter * 0.4);
            const x = cx + Math.cos(angle) * rr;
            const y = cy + Math.sin(angle) * rr;
            i === 0 ? ctx.moveTo(x, y) : ctx.lineTo(x, y);
        }
        ctx.closePath();
        ctx.fill();
    }

    // Generate n bite descriptors whose centers are distributed uniformly
    // around all four edges of the image (bites always eat from outside in).
    function generateBites(n, w, h) {
        const minDim = Math.min(w, h);
        // Radius: large enough to reach well inward, but visually bite-sized.
        // 38% of the shorter dimension is a good balance.
        const r = minDim * 0.38;
        const perimeter = 2 * (w + h);
        const bites = [];

        for (let i = 0; i < n; i++) {
            // Uniform base position with a small random jitter so bites don't
            // look perfectly evenly spaced, but stay ordered around the border.
            const baseT = (i / n) * perimeter;
            const jitter = (Math.random() - 0.5) * (perimeter / n) * 0.6;
            const t = ((baseT + jitter) + perimeter) % perimeter;

            let cx, cy;
            if (t < w) {
                // Top edge — center sits ON the border
                cx = t;
                cy = 0;
            } else if (t < w + h) {
                // Right edge
                cx = w;
                cy = t - w;
            } else if (t < 2 * w + h) {
                // Bottom edge
                cx = 2 * w + h - t;
                cy = h;
            } else {
                // Left edge
                cx = 0;
                cy = 2 * (w + h) - t;
            }

            const seed = (Math.random() * 0x7fffffff) | 0;
            bites.push({ cx, cy, r, seed });
        }

        // Fisher-Yates shuffle so consecutive bites are spatially spread out
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
            const { cx, cy, r, seed } = bites[i];
            drawJaggedBite(ctx, cx, cy, r, seed);
        }

        ctx.globalCompositeOperation = 'source-over';
    }

    function init(canvasId, imageUrl, biteCount) {
        const canvas = document.getElementById(canvasId);
        if (!canvas) return;

        // Create a lightweight pending-state placeholder immediately.
        // If setBites is called while the image is still loading, it updates
        // pending.count. When the image loads, that count is applied so no
        // click is ever dropped.
        const pending = { count: -1 };
        states[canvasId] = { pending };

        const img = new Image();
        img.onload = function () {
            canvas.width = img.naturalWidth;
            canvas.height = img.naturalHeight;

            const state = {
                canvas,
                img,
                biteCount,
                bites: generateBites(biteCount, canvas.width, canvas.height),
            };
            states[canvasId] = state;
            draw(state, pending.count >= 0 ? pending.count : 0);
        };
        img.src = imageUrl;
    }

    function setBites(canvasId, count) {
        const state = states[canvasId];
        if (!state) return;
        if (state.pending) {
            // Image still loading — queue this count for when it finishes
            state.pending.count = count;
            return;
        }
        draw(state, count);
    }

    function cleanup(canvasId) {
        delete states[canvasId];
    }

    return { init, setBites, cleanup };
})();

