pub mod engine;

use engine::render as engine_render;
use wasm_bindgen::prelude::*;

#[wasm_bindgen]
pub fn render(cx: f64, cy: f64, scale: f64, width: u32, height: u32, max_iters: u32) -> Vec<u32> {
    engine_render(cx, cy, scale, width, height, max_iters)
}
