import init, { render as wasmRender } from '../../../aether_garden-wasm/mandelbrot/pkg/mandelbrot.js'
import wasmUrl from '../../../aether_garden-wasm/mandelbrot/pkg/mandelbrot_bg.wasm?url'
import { renderJs, type RenderRequest } from './render'

const ctx = self as unknown as {
  postMessage(message: unknown, transfer?: Transferable[]): void
}

let wasmReady: Promise<void> | null = null
function ensureWasm(): Promise<void> {
  if (!wasmReady) {
    wasmReady = init(wasmUrl).then(() => undefined)
  }
  return wasmReady
}

addEventListener('message', async (event: MessageEvent<RenderRequest>) => {
  const req = event.data
  const start = performance.now()
  let iterations: Uint32Array
  if (req.engine === 'js') {
    iterations = renderJs(req)
  } else {
    await ensureWasm()
    iterations = wasmRender(
      req.cx,
      req.cy,
      req.scale,
      req.width,
      req.height,
      req.maxIters,
    )
  }
  const timeMs = performance.now() - start
  ctx.postMessage(
    { id: req.id, timeMs, buffer: iterations.buffer },
    [iterations.buffer],
  )
})
