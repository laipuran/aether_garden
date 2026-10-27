import { Link } from 'react-router-dom'
import type { TocHeading } from '../hooks/useArticleToc'
import type { RelatedContent } from '../types'

type Props = {
  headings: TocHeading[]
  related: RelatedContent[]
}

export function ArticleRail({ headings, related }: Props) {
  const toc = headings.filter((heading) => heading.level === 2 || heading.level === 3)
  const hasToc = toc.length > 0
  const hasRelated = related.length > 0

  if (!hasToc && !hasRelated) {
    return null
  }

  return (
    <aside className="article-rail">
      {hasToc && (
        <nav className="rail-block rail-toc" aria-label="文章目录">
          <h2 className="rail-title">目录</h2>
          <ul className="toc-list">
            {toc.map((heading) => (
              <li key={heading.id} className={`toc-item toc-level-${heading.level}`}>
                <a href={`#${heading.id}`}>{heading.text}</a>
              </li>
            ))}
          </ul>
        </nav>
      )}

      {hasRelated && (
        <div className="rail-block rail-related">
          <h2 className="rail-title">相关文章</h2>
          <ul className="related-list">
            {related.map((item) => (
              <li key={`${item.kind}-${item.slug}`}>
                <Link to={`/${item.kind}/${item.slug}`}>{item.title}</Link>
                <span className="meta mono">{item.date}</span>
              </li>
            ))}
          </ul>
        </div>
      )}
    </aside>
  )
}
