import { useEffect, useState } from 'react'

type LoadableState<T> = {
  data: T | null
  loading: boolean
  error: string | null
}

export function useLoadable<T>(loader: () => Promise<T>) {
  const [state, setState] = useState<LoadableState<T>>({
    data: null,
    loading: true,
    error: null,
  })

  useEffect(() => {
    let cancelled = false

    loader()
      .then((data) => {
        if (!cancelled) {
          setState({ data, loading: false, error: null })
        }
      })
      .catch((error: Error) => {
        if (!cancelled) {
          setState({ data: null, loading: false, error: error.message })
        }
      })

    return () => {
      cancelled = true
    }
  }, [loader])

  return state
}
