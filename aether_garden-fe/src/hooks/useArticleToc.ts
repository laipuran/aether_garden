import { useEffect, useState } from 'react'
import type { RefObject } from 'react'

export type TocHeading = {
  id: string
  text: string
  level: number
}

// Derives the table of contents from the rendered article DOM instead of a
// parallel markdown parse: whatever id the rendered heading actually carries
// is the one the TOC anchor targets, so the two can never drift apart.
export function useArticleToc(
  containerRef: RefObject<HTMLElement | null>,
  dep: unknown,
): TocHeading[] {
  const [headings, setHeadings] = useState<TocHeading[]>([])

  useEffect(() => {
    const container = containerRef.current
    if (!container) {
      setHeadings([])
      return
    }

    const next = Array.from(
      container.querySelectorAll<HTMLElement>('.markdown-body h2, .markdown-body h3'),
    )
      .map((node) => ({
        id: node.id,
        text: node.textContent ?? '',
        level: Number(node.tagName[1]),
      }))
      .filter((heading) => heading.id !== '')

    setHeadings(next)
  }, [containerRef, dep])

  return headings
}
