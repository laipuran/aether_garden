export type Rgb = readonly [number, number, number]

export interface Palette {
  name: string
  stops: readonly Rgb[]
  interior: Rgb
}

export function samplePalette(palette: Palette, t: number): Rgb {
  const n = palette.stops.length
  const s = Math.min(Math.max(t, 0), 1) * (n - 1)
  const i = Math.min(Math.floor(s), n - 2)
  const f = s - i
  const a = palette.stops[i]
  const b = palette.stops[i + 1]
  return [
    Math.round(a[0] + (b[0] - a[0]) * f),
    Math.round(a[1] + (b[1] - a[1]) * f),
    Math.round(a[2] + (b[2] - a[2]) * f),
  ]
}

export const PALETTES: readonly Palette[] = [
  {
    name: '经典',
    stops: [
      [16, 32, 80],
      [70, 160, 255],
      [240, 250, 255],
      [255, 180, 60],
    ],
    interior: [0, 0, 0],
  },
  {
    name: '火焰',
    stops: [
      [30, 5, 0],
      [150, 40, 0],
      [255, 120, 20],
      [255, 230, 140],
    ],
    interior: [10, 0, 0],
  },
  {
    name: '海洋',
    stops: [
      [5, 20, 60],
      [30, 120, 200],
      [120, 220, 255],
      [240, 255, 255],
    ],
    interior: [5, 10, 30],
  },
  {
    name: '灰度',
    stops: [
      [0, 0, 0],
      [90, 90, 90],
      [220, 220, 220],
      [255, 255, 255],
    ],
    interior: [20, 20, 20],
  },
]
