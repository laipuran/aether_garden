import { useEffect, useState } from 'react'

type LoadableState<T> = {
  data: T | null
  loading: boolean
  error: string | null
}

type UseLoadableOptions = {
  cacheKey?: string
  cacheTtlMs?: number
}

type CachePayload<T> = {
  data: T
  expiresAt: number
}

function readCache<T>(cacheKey: string): T | null {
  if (typeof window === 'undefined') {
    return null
  }

  const raw = window.sessionStorage.getItem(cacheKey)
  if (!raw) {
    return null
  }

  try {
    const payload = JSON.parse(raw) as CachePayload<T>
    if (Date.now() > payload.expiresAt) {
      window.sessionStorage.removeItem(cacheKey)
      return null
    }

    return payload.data
  } catch {
    window.sessionStorage.removeItem(cacheKey)
    return null
  }
}

function writeCache<T>(cacheKey: string, data: T, cacheTtlMs: number) {
  if (typeof window === 'undefined') {
    return
  }

  const payload: CachePayload<T> = {
    data,
    expiresAt: Date.now() + cacheTtlMs,
  }
  window.sessionStorage.setItem(cacheKey, JSON.stringify(payload))
}

export function useLoadable<T>(loader: () => Promise<T>, options: UseLoadableOptions = {}) {
  const { cacheKey, cacheTtlMs = 10 * 60 * 1000 } = options
  const cachedData = cacheKey ? readCache<T>(cacheKey) : null

  const [state, setState] = useState<LoadableState<T>>({
    data: cachedData,
    loading: !cachedData,
    error: null,
  })

  useEffect(() => {
    let cancelled = false

    loader()
      .then((data) => {
        if (!cancelled) {
          if (cacheKey) {
            writeCache(cacheKey, data, cacheTtlMs)
          }
          setState({ data, loading: false, error: null })
        }
      })
      .catch((error: Error) => {
        if (!cancelled) {
          setState((currentState) => {
            if (currentState.data) {
              return { ...currentState, loading: false, error: null }
            }

            return { data: null, loading: false, error: error.message }
          })
        }
      })

    return () => {
      cancelled = true
    }
  }, [loader, cacheKey, cacheTtlMs])

  return state
}
