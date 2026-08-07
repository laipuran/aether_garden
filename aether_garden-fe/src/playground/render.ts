import { samplePalette, type Palette } from './palettes'

export type Engine = 'wasm' | 'js'

export interface RenderRequest {
  id: number
  engine: Engine
  cx: number
  cy: number
  scale: number
  width: number
  height: number
  maxIters: number
}

export function escapeTimeJs(cx: number, cy: number, maxIters: number): number {
  let zx = 0
  let zy = 0
  let zx2 = 0
  let zy2 = 0
  for (let i = 0; i < maxIters; i++) {
    zy = 2 * zx * zy + cy
    zx = zx2 - zy2 + cx
    zx2 = zx * zx
    zy2 = zy * zy
    if (zx2 + zy2 > 4) {
      return i
    }
  }
  return maxIters
}

export function renderJs(req: RenderRequest): Uint32Array {
  const { cx, cy, scale, width, height, maxIters } = req
  const out = new Uint32Array(width * height)
  for (let py = 0; py < height; py++) {
    const y = cy - (py - height * 0.5) * scale
    for (let px = 0; px < width; px++) {
      const x = cx + (px - width * 0.5) * scale
      out[py * width + px] = escapeTimeJs(x, y, maxIters)
    }
  }
  return out
}

export function iterationsToImageData(
  iterations: Uint32Array,
  maxIters: number,
  palette: Palette,
  out: Uint8ClampedArray,
): void {
  for (let i = 0; i < iterations.length; i++) {
    const value = iterations[i]
    const rgb =
      value >= maxIters
        ? palette.interior
        : samplePalette(palette, value / maxIters)
    const o = i * 4
    out[o] = rgb[0]
    out[o + 1] = rgb[1]
    out[o + 2] = rgb[2]
    out[o + 3] = 255
  }
}
