import { useCallback } from 'react'
import { api } from '../api'
import { useLoadable } from '../hooks/useLoadable'

export function AboutPage() {
  const loadProfile = useCallback(() => api.getProfile(), [])
  const { data, loading, error } = useLoadable(loadProfile)

  if (loading) {
    return <p className="status">正在加载个人资料...</p>
  }

  if (error || !data) {
    return <p className="status">暂时无法获取个人资料。</p>
  }

  return (
    <section className="article">
      <header className="article-header">
        <div className="eyebrow">About</div>
        <h1>{data.name}</h1>
        <p className="lead">{data.title}</p>
      </header>

      <div className="article-body">
        <p>{data.bio}</p>
        <p>
          学校：<span className="mono">{data.school}</span>，常驻：
          <span className="mono">{data.location}</span>。
        </p>
        <p>兴趣方向：{data.interests.join('、')}。</p>
        <p>
          GitHub：
          <a href={data.github} target="_blank" rel="noreferrer" className="mono">
            {data.github}
          </a>
        </p>
        <p>
          网站：
          <a href={data.website} target="_blank" rel="noreferrer" className="mono">
            {data.website}
          </a>
        </p>
        <p>
          联系方式：<span className="mono">{data.contactEmail}</span>
        </p>
      </div>
    </section>
  )
}
