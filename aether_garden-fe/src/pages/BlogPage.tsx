import { useCallback } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api } from '../api'
import { MarkdownContent } from '../components/MarkdownContent'
import { useLoadable } from '../hooks/useLoadable'

export function BlogPage() {
  const loadBlogs = useCallback(() => api.getBlogs(), [])
  const { data, loading, error } = useLoadable(loadBlogs)

  return (
    <section className="section">
      <header className="page-header">
        <div className="eyebrow">Blog</div>
        <h1>博客</h1>
        <p className="lead">这里放项目复盘、学习记录和较完整的技术文章。</p>
      </header>

      {loading ? <p className="status">正在加载博客列表...</p> : null}
      {error ? <p className="status">博客列表加载失败。</p> : null}

      <ul className="list">
        {data?.map((post) => (
          <li className="list-item" key={post.slug}>
            <Link to={`/blog/${post.slug}`}>
              <strong>{post.title}</strong>
            </Link>
            <p className="meta">{post.excerpt}</p>
            <div className="meta mono">
              {post.date} · {post.tags.join(' / ')}
            </div>
          </li>
        ))}
      </ul>
    </section>
  )
}

export function BlogDetailPage() {
  const { slug = '' } = useParams()
  const loadBlog = useCallback(() => api.getBlogBySlug(slug), [slug])
  const { data, loading, error } = useLoadable(loadBlog)

  if (loading) {
    return <p className="status">正在加载文章...</p>
  }

  if (error || !data) {
    return <p className="status">文章不存在或暂时无法访问。</p>
  }

  return (
    <article className="article">
      <header className="article-header">
        <div className="eyebrow">Blog Article</div>
        <h1>{data.title}</h1>
        <div className="meta mono">
          {data.date} · {data.tags.join(' / ')}
        </div>
      </header>
      <MarkdownContent markdown={data.markdown} />
    </article>
  )
}
