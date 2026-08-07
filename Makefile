.PHONY: gen-api build wasm format run-be run-fe

# Regenerate the backend OpenAPI contract and the frontend types derived from it.
# Rebuild is required so GetDocument always runs (incremental build skips it
# when only openapi.json is missing).
gen-api:
	cd aether_garden-be && dotnet build -t:Rebuild
	cd aether_garden-fe && pnpm gen:api

# Compile all WASM crates consumed by the frontend (output goes to
# aether_garden-wasm/<crate>/pkg).
wasm:
	cd aether_garden-wasm && for c in */; do [ -f "$${c}Cargo.toml" ] || continue; wasm-pack build "$${c%/}" --target web --release; done

# Build both apps. Run `make wasm` first if the WASM output is missing.
build: wasm
	cd aether_garden-be && dotnet build
	cd aether_garden-fe && pnpm build

# Lint and format both apps.
format:
	cd aether_garden-fe && pnpm lint
	cd aether_garden-be && dotnet format
	cd aether_garden-wasm && cargo fmt --check && cargo clippy --all-targets -- -D warnings

# Backend dev server (launchSettings -> http://localhost:5109).
run-be:
	cd aether_garden-be && dotnet run

# Frontend dev server (http://localhost:5173).
run-fe:
	cd aether_garden-fe && pnpm dev
