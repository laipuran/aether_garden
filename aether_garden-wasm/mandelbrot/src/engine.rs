use wide::{f64x4, CmpGt};

/// Returns the number of iterations before `c` escapes (or `max_iters` if it
/// stays inside the set). `z_0 = 0`, iterating `z = z² + c`; escape when
/// `|z|² > 4`.
pub fn escape_time(cx: f64, cy: f64, max_iters: u32) -> u32 {
    let mut zx = 0.0;
    let mut zy = 0.0;
    let mut zx2 = 0.0;
    let mut zy2 = 0.0;
    for i in 0..max_iters {
        zy = 2.0 * zx * zy + cy;
        zx = zx2 - zy2 + cx;
        zx2 = zx * zx;
        zy2 = zy * zy;
        if zx2 + zy2 > 4.0 {
            return i;
        }
    }
    max_iters
}

/// Escape times for four points at once, computed with f64x4 lanes. Escaped
/// lanes stop updating (frozen) so each lane records its own first-escape
/// iteration; lanes that never escape return `max_iters`.
fn escape_time4(cxs: [f64; 4], cys: [f64; 4], max_iters: u32) -> [u32; 4] {
    let cx = f64x4::new(cxs);
    let cy = f64x4::new(cys);
    let mut zx = f64x4::ZERO;
    let mut zy = f64x4::ZERO;
    let mut zx2 = f64x4::ZERO;
    let mut zy2 = f64x4::ZERO;
    let mut active = !f64x4::ZERO;
    let mut count = f64x4::splat(max_iters as f64);
    let threshold = f64x4::splat(4.0);

    for i in 0..max_iters {
        let new_zy = (zx + zx) * zy + cy;
        let new_zx = zx2 - zy2 + cx;
        zx = new_zx;
        zy = new_zy;
        zx2 = zx * zx;
        zy2 = zy * zy;

        let escaped = (zx2 + zy2).cmp_gt(threshold);
        let first_escape = escaped & active;
        count = first_escape.blend(f64x4::splat(i as f64), count);
        active &= !escaped;

        if i & 7 == 7 {
            let a = active.to_array();
            if a[0] == 0.0 && a[1] == 0.0 && a[2] == 0.0 && a[3] == 0.0 {
                break;
            }
        }
    }

    let c = count.to_array();
    [c[0] as u32, c[1] as u32, c[2] as u32, c[3] as u32]
}

/// Renders a view of the Mandelbrot set. `scale` is the complex-plane width of
/// one pixel; pixel `(px, py)` maps to `cx + (px - w/2) * scale` on the real
/// axis and `cy - (py - h/2) * scale` on the imaginary axis. Returns one escape
/// time per pixel, row-major.
pub fn render(cx: f64, cy: f64, scale: f64, width: u32, height: u32, max_iters: u32) -> Vec<u32> {
    let total = (width * height) as usize;
    let mut out = Vec::with_capacity(total);
    let w = width as f64;
    let h = height as f64;

    for py in 0..height {
        let y = cy - (py as f64 - h * 0.5) * scale;
        let mut px = 0u32;
        while px + 4 <= width {
            let mut cxs = [0.0f64; 4];
            for k in 0..4u32 {
                cxs[k as usize] = cx + ((px + k) as f64 - w * 0.5) * scale;
            }
            out.extend_from_slice(&escape_time4(cxs, [y; 4], max_iters));
            px += 4;
        }
        while px < width {
            let x = cx + (px as f64 - w * 0.5) * scale;
            out.push(escape_time(x, y, max_iters));
            px += 1;
        }
    }
    out
}

#[cfg(test)]
mod tests {
    use super::*;

    fn scalar_render(
        cx: f64,
        cy: f64,
        scale: f64,
        width: u32,
        height: u32,
        max_iters: u32,
    ) -> Vec<u32> {
        let mut out = Vec::with_capacity((width * height) as usize);
        let w = width as f64;
        let h = height as f64;
        for idx in 0..(width * height) as usize {
            let px = idx as u32 % width;
            let py = idx as u32 / width;
            let x = cx + (px as f64 - w * 0.5) * scale;
            let y = cy - (py as f64 - h * 0.5) * scale;
            out.push(escape_time(x, y, max_iters));
        }
        out
    }

    #[test]
    fn simd_kernel_matches_scalar() {
        let cases = [
            (-0.75, 0.0, 0.005, 67u32, 53u32, 137u32),
            (2.0, 0.0, 0.1, 40, 40, 200),
            (-0.7435, 0.1314, 2e-6, 100, 80, 1500),
            (0.0, 0.0, 0.05, 12, 12, 33),
        ];
        for (cx, cy, scale, width, height, max_iters) in cases {
            assert_eq!(
                render(cx, cy, scale, width, height, max_iters),
                scalar_render(cx, cy, scale, width, height, max_iters),
                "view c=({cx},{cy}) scale={scale} {width}x{height} iters={max_iters}"
            );
        }
    }

    #[test]
    fn interior_points_never_escape() {
        for (x, y) in [(0.0, 0.0), (-0.5, 0.0), (0.2, 0.0)] {
            assert_eq!(escape_time(x, y, 256), 256, "c=({x},{y})");
        }
    }

    #[test]
    fn exterior_points_escape() {
        assert_eq!(escape_time(3.0, 0.0, 256), 0);
        assert_eq!(escape_time(2.0, 0.0, 256), 1);
        assert_eq!(escape_time(1.0, 0.0, 256), 2);
        assert_eq!(escape_time(0.0, 2.0, 256), 1);
    }

    #[test]
    fn render_returns_one_value_per_pixel() {
        let out = render(-0.75, 0.0, 0.0025, 40, 30, 256);
        assert_eq!(out.len(), 40 * 30);
        assert!(out.iter().all(|&v| v <= 256));
        assert_eq!(out[15 * 40 + 20], 256, "center pixel maps to (-0.75, 0)");

        let outside = render(2.0, 0.0, 0.1, 8, 8, 256);
        assert!(outside.iter().all(|&v| v < 256), "all exterior");
    }
}
