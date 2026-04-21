import { useCallback } from 'react'
import { EntryCard } from '../components/EntryCard'
import { api } from '../api'
import { useLoadable } from '../hooks/useLoadable'

export function HomePage() {
  const loadProfile = useCallback(() => api.getProfile(), [])
  const loadGithub = useCallback(() => api.getGithubOverview(), [])
  const profileState = useLoadable(loadProfile)
  const githubState = useLoadable(loadGithub)

  return (
    <>
      <section className="hero">
        <div className="eyebrow">Personal Website</div>
        <h1>在 aether_garden 记录代码、生活与正在发生的想法。</h1>
        <p className="lead">
          这个站点留出足够的空间，把技术学习与日常表达平衡地放在一起。你可以从下面三个入口快速浏览博客、随笔和个人介绍。
        </p>

        <div className="entry-grid">
          <EntryCard
            to="/blog"
            label="长文与项目记录"
            title="博客"
            badge="Blog"
          />
          <EntryCard
            to="/notes"
            label="短想法与片段"
            title="随笔"
            badge="Notes"
          />
          <EntryCard
            to="/about"
            label="背景与联系"
            title="个人介绍"
            badge="About"
          />
        </div>
      </section>

      <section className="section">
        <h2>GitHub 概览</h2>
        {profileState.loading || githubState.loading ? (
          <p className="status">正在加载公开信息...</p>
        ) : profileState.error || githubState.error ? (
          <p className="status">暂时无法读取 GitHub 信息，请稍后刷新重试。</p>
        ) : (
          <>
            <p className="lead">
              {profileState.data?.name}（{githubState.data?.username}）目前公开仓库
              {githubState.data?.publicRepos} 个，关注者 {githubState.data?.followers}
              人。
            </p>
            <ul className="list">
              {githubState.data?.picked.map((repo) => (
                <li className="list-item" key={repo.name}>
                  <a href={repo.url} target="_blank" rel="noreferrer">
                    <strong>{repo.name}</strong>
                  </a>
                  <p className="meta">{repo.description || 'No description'}</p>
                  <div className="meta mono">
                    {repo.language || 'Unknown'} · ★ {repo.stars}
                  </div>
                </li>
              ))}
            </ul>
          </>
        )}
      </section>
    </>
  )
}
