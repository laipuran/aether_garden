import { useCallback, useEffect, useRef, useState } from 'react'
import { PALETTES } from '../playground/palettes'
import {
  iterationsToImageData,
  type Engine,
  type RenderRequest,
} from '../playground/render'

interface View {
  cx: number
  cy: number
  scale: number
}

const DEFAULT_VIEW: View = { cx: -0.75, cy: 0, scale: 0.0025 }
const DEFAULT_ITERS = 500
const MIN_SCALE = 1e-11
const MAX_SCALE = 1
const ZOOM_STEP = 1.25
const CANVAS_HEIGHT = 520

const clamp = (value: number, min: number, max: number) =>
  Math.min(Math.max(value, min), max)

export function PlaygroundPage() {
  const canvasRef = useRef<HTMLCanvasElement>(null)
  const workerRef = useRef<Worker | null>(null)
  const imageDataRef = useRef<ImageData | null>(null)

  const viewRef = useRef<View>({ ...DEFAULT_VIEW })
  const engineRef = useRef<Engine>('wasm')
  const maxItersRef = useRef(DEFAULT_ITERS)
  const paletteIdxRef = useRef(0)
  const renderIdRef = useRef(0)
  const pendingRef = useRef(false)
  const dirtyRef = useRef(false)
  const lastIterationsRef = useRef<Uint32Array | null>(null)
  const lastMaxItersRef = useRef(DEFAULT_ITERS)
  const dragRef = useRef<{
    startX: number
    startY: number
    view: View
  } | null>(null)

  const [engine, setEngine] = useState<Engine>('wasm')
  const [maxIters, setMaxIters] = useState(DEFAULT_ITERS)
  const [paletteIdx, setPaletteIdx] = useState(0)
  const [lastTimeMs, setLastTimeMs] = useState(0)
  const [zoom, setZoom] = useState(1)
  const [ready, setReady] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const paint = useCallback((iterations: Uint32Array, iters: number) => {
    const canvas = canvasRef.current
    const ctx = canvas?.getContext('2d')
    if (!canvas || !ctx) {
      return
    }
    if (iterations.length !== canvas.width * canvas.height) {
      return
    }
    let image = imageDataRef.current
    if (!image || image.width !== canvas.width || image.height !== canvas.height) {
      image = new ImageData(canvas.width, canvas.height)
      imageDataRef.current = image
    }
    iterationsToImageData(iterations, iters, PALETTES[paletteIdxRef.current], image.data)
    ctx.putImageData(image, 0, 0)
  }, [])

  const requestRender = useCallback(() => {
    const worker = workerRef.current
    const canvas = canvasRef.current
    if (!worker || !canvas) {
      return
    }
    if (pendingRef.current) {
      dirtyRef.current = true
      return
    }
    pendingRef.current = true
    renderIdRef.current += 1
    const req: RenderRequest = {
      id: renderIdRef.current,
      engine: engineRef.current,
      cx: viewRef.current.cx,
      cy: viewRef.current.cy,
      scale: viewRef.current.scale,
      width: canvas.width,
      height: canvas.height,
      maxIters: maxItersRef.current,
    }
    worker.postMessage(req)
  }, [])

  useEffect(() => {
    const worker = new Worker(
      new URL('../playground/mandelbrotWorker.ts', import.meta.url),
      { type: 'module' },
    )
    workerRef.current = worker
    worker.addEventListener('message', (event) => {
      const { id, buffer, timeMs } = event.data as {
        id: number
        buffer: ArrayBuffer
        timeMs: number
      }
      if (id !== renderIdRef.current) {
        return
      }
      const iterations = new Uint32Array(buffer)
      lastIterationsRef.current = iterations
      lastMaxItersRef.current = maxItersRef.current
      paint(iterations, maxItersRef.current)
      setLastTimeMs(Math.round(timeMs))
      setReady(true)
      pendingRef.current = false
      if (dirtyRef.current) {
        dirtyRef.current = false
        requestRender()
      }
    })
    worker.addEventListener('error', (event) => {
      setError(event.message || 'Worker 错误')
    })
    return () => worker.terminate()
  }, [paint, requestRender])

  const syncCanvasSize = useCallback(() => {
    const canvas = canvasRef.current
    if (!canvas) {
      return
    }
    const width = Math.max(1, Math.floor(canvas.clientWidth))
    if (canvas.width !== width) {
      canvas.width = width
    }
    if (canvas.height !== CANVAS_HEIGHT) {
      canvas.height = CANVAS_HEIGHT
    }
  }, [])

  useEffect(() => {
    syncCanvasSize()
    const observer = new ResizeObserver(() => {
      syncCanvasSize()
      requestRender()
    })
    if (canvasRef.current) {
      observer.observe(canvasRef.current)
    }
    return () => observer.disconnect()
  }, [requestRender, syncCanvasSize])

  useEffect(() => {
    const canvas = canvasRef.current
    if (!canvas) {
      return
    }
    const handleWheel = (event: WheelEvent) => {
      event.preventDefault()
      const rect = canvas.getBoundingClientRect()
      const px = event.clientX - rect.left
      const py = event.clientY - rect.top
      const { cx, cy, scale } = viewRef.current
      const factor = event.deltaY > 0 ? 1 / ZOOM_STEP : ZOOM_STEP
      const nextScale = clamp(scale * factor, MIN_SCALE, MAX_SCALE)
      if (nextScale === scale) {
        return
      }
      const x = cx + (px - canvas.width / 2) * scale
      const y = cy - (py - canvas.height / 2) * scale
      viewRef.current = {
        cx: x - (px - canvas.width / 2) * nextScale,
        cy: y + (py - canvas.height / 2) * nextScale,
        scale: nextScale,
      }
      setZoom(Math.round(DEFAULT_VIEW.scale / nextScale))
      requestRender()
    }
    canvas.addEventListener('wheel', handleWheel, { passive: false })
    return () => canvas.removeEventListener('wheel', handleWheel)
  }, [requestRender])

  const zoomAt = (px: number, py: number, factor: number) => {
    const canvas = canvasRef.current
    if (!canvas) {
      return
    }
    const { cx, cy, scale } = viewRef.current
    const nextScale = clamp(scale * factor, MIN_SCALE, MAX_SCALE)
    if (nextScale === scale) {
      return
    }
    const x = cx + (px - canvas.width / 2) * scale
    const y = cy - (py - canvas.height / 2) * scale
    viewRef.current = {
      cx: x - (px - canvas.width / 2) * nextScale,
      cy: y + (py - canvas.height / 2) * nextScale,
      scale: nextScale,
    }
    setZoom(Math.round(DEFAULT_VIEW.scale / nextScale))
    requestRender()
  }

  const handlePointerDown = (event: React.PointerEvent<HTMLCanvasElement>) => {
    const canvas = canvasRef.current
    if (!canvas) {
      return
    }
    canvas.setPointerCapture(event.pointerId)
    dragRef.current = {
      startX: event.clientX,
      startY: event.clientY,
      view: { ...viewRef.current },
    }
  }

  const handlePointerMove = (event: React.PointerEvent<HTMLCanvasElement>) => {
    const drag = dragRef.current
    if (!drag) {
      return
    }
    const dx = event.clientX - drag.startX
    const dy = event.clientY - drag.startY
    viewRef.current = {
      cx: drag.view.cx - dx * drag.view.scale,
      cy: drag.view.cy + dy * drag.view.scale,
      scale: drag.view.scale,
    }
    requestRender()
  }

  const handlePointerUp = (event: React.PointerEvent<HTMLCanvasElement>) => {
    dragRef.current = null
    canvasRef.current?.releasePointerCapture(event.pointerId)
  }

  const switchEngine = (next: Engine) => {
    engineRef.current = next
    setEngine(next)
    requestRender()
  }

  const changeIters = (next: number) => {
    maxItersRef.current = next
    setMaxIters(next)
    requestRender()
  }

  const changePalette = (index: number) => {
    paletteIdxRef.current = index
    setPaletteIdx(index)
    const iterations = lastIterationsRef.current
    if (iterations) {
      paint(iterations, lastMaxItersRef.current)
    }
  }

  const reset = () => {
    viewRef.current = { ...DEFAULT_VIEW }
    maxItersRef.current = DEFAULT_ITERS
    setMaxIters(DEFAULT_ITERS)
    setZoom(1)
    requestRender()
  }

  return (
    <section className="article playground">
      <header className="article-header">
        <div className="eyebrow">Playground · WASM</div>
        <h1>Mandelbrot 缩放</h1>
        <p className="lead">
          每一帧由 WASM 或纯 JS 逐像素迭代计算，计算跑在 Web Worker 里，主线程不卡。
          切到 JS 引擎，看右下角的耗时，就是「为什么用 WASM」的答案。
        </p>
      </header>

      <div className="mb-body">
        <div className="mb-hud mono">
          <span>
            引擎 <b className={engine === 'wasm' ? 'accent' : ''}>{engine.toUpperCase()}</b>
          </span>
          <span>上次渲染 <b>{lastTimeMs}</b> ms</span>
          <span>缩放 ×{zoom.toLocaleString('en-US')}</span>
        </div>

        <div className="mb-canvas-shell">
          <canvas
            ref={canvasRef}
            className="mb-canvas"
            style={{ height: CANVAS_HEIGHT }}
            onPointerDown={handlePointerDown}
            onPointerMove={handlePointerMove}
            onPointerUp={handlePointerUp}
            onPointerCancel={handlePointerUp}
            onDoubleClick={(event) => {
              const rect = event.currentTarget.getBoundingClientRect()
              zoomAt(event.clientX - rect.left, event.clientY - rect.top, ZOOM_STEP)
            }}
            aria-label="Mandelbrot 集合画布"
          />
          {!ready && (
            <div className="mb-loading">正在计算第一帧…</div>
          )}
          <div className="mb-note">拖拽平移 · 滚轮缩放 · 双击放大</div>
        </div>

        <div className="mb-controls">
          <div className="mb-row">
            <span className="mb-row-label">引擎</span>
            <div className="mb-segmented" role="group" aria-label="渲染引擎">
              <button
                type="button"
                className={engine === 'wasm' ? 'active' : ''}
                onClick={() => switchEngine('wasm')}
              >
                WASM
              </button>
              <button
                type="button"
                className={engine === 'js' ? 'active' : ''}
                onClick={() => switchEngine('js')}
              >
                JS
              </button>
            </div>
            <button type="button" className="mb-button" onClick={reset}>
              重置
            </button>
          </div>

          <div className="mb-row">
            <span className="mb-row-label">调色板</span>
            {PALETTES.map((palette, index) => (
              <button
                key={palette.name}
                type="button"
                className={`mb-swatch ${index === paletteIdx ? 'active' : ''}`}
                onClick={() => changePalette(index)}
                title={palette.name}
                aria-label={palette.name}
              >
                <span
                  className="mb-swatch-bar"
                  style={{
                    background: `linear-gradient(90deg, rgb(${palette.stops.join(
                      ') , rgb(',
                    )}))`,
                  }}
                />
                {palette.name}
              </button>
            ))}
          </div>

          <label className="mb-row">
            <span className="mb-row-label">最大迭代</span>
            <input
              type="range"
              min={100}
              max={3000}
              step={50}
              value={maxIters}
              onChange={(event) => changeIters(Number(event.target.value))}
            />
            <span className="mono">{maxIters}</span>
          </label>
        </div>

        {error && <p className="status">渲染出错：{error}</p>}
        <p className="mb-footnote">
          深缩放精度受 f64 限制（约 ×10¹¹）；调色板在浏览器端着色，切换无需重编译 wasm。
        </p>
      </div>
    </section>
  )
}
