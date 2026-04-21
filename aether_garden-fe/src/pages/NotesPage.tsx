import { useCallback } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api } from '../api'
import { MarkdownContent } from '../components/MarkdownContent'
import { useLoadable } from '../hooks/useLoadable'

export function NotesPage() {
  const loadNotes = useCallback(() => api.getNotes(), [])
  const { data, loading, error } = useLoadable(loadNotes)

  return (
    <section className="section">
      <div className="eyebrow">Notes</div>
      <h1>随笔</h1>
      <p className="lead">简短但真实的记录，可能是一句感想，也可能是一个正在萌芽的点子。</p>

      {loading ? <p className="status">正在加载随笔列表...</p> : null}
      {error ? <p className="status">随笔列表加载失败。</p> : null}

      <ul className="list">
        {data?.map((note) => (
          <li className="list-item" key={note.slug}>
            <Link to={`/notes/${note.slug}`}>
              <strong>{note.title}</strong>
            </Link>
            <p className="meta">{note.excerpt}</p>
            <div className="meta mono">
              {note.date} · {note.tags.join(' / ')}
            </div>
          </li>
        ))}
      </ul>
    </section>
  )
}

export function NoteDetailPage() {
  const { slug = '' } = useParams()
  const loadNote = useCallback(() => api.getNoteBySlug(slug), [slug])
  const { data, loading, error } = useLoadable(loadNote)

  if (loading) {
    return <p className="status">正在加载随笔...</p>
  }

  if (error || !data) {
    return <p className="status">随笔不存在或暂时无法访问。</p>
  }

  return (
    <article className="article">
      <header className="article-header">
        <div className="eyebrow">Note</div>
        <h1>{data.title}</h1>
        <div className="meta mono">
          {data.date} · {data.tags.join(' / ')}
        </div>
      </header>
      <MarkdownContent markdown={data.markdown} fallbackParagraphs={data.content} />
    </article>
  )
}
