.PHONY: gen-api build format run-be run-fe

# Regenerate the backend OpenAPI contract and the frontend types derived from it.
# Rebuild is required so GetDocument always runs (incremental build skips it
# when only openapi.json is missing).
gen-api:
	cd aether_garden-be && dotnet build -t:Rebuild
	cd aether_garden-fe && pnpm gen:api

# Build both apps.
build:
	cd aether_garden-be && dotnet build
	cd aether_garden-fe && pnpm build

# Lint and format both apps.
format:
	cd aether_garden-fe && pnpm lint
	cd aether_garden-be && dotnet format

# Backend dev server (launchSettings -> http://localhost:5109).
run-be:
	cd aether_garden-be && dotnet run

# Frontend dev server (http://localhost:5173).
run-fe:
	cd aether_garden-fe && pnpm dev
