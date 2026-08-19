import { useCallback } from 'react'
import { EntryCard } from '../components/EntryCard'
import { FavoriteTracks } from '../components/FavoriteTracks'
import { api } from '../api'
import { useLoadable } from '../hooks/useLoadable'

export function HomePage() {
  const loadProfile = useCallback(() => api.getProfile(), [])
  const loadGithub = useCallback(() => api.getGithubOverview(), [])
  const profileState = useLoadable(loadProfile, {
    cacheKey: 'home.profile',
    cacheTtlMs: 10 * 60 * 1000,
  })
  const githubState = useLoadable(loadGithub, {
    cacheKey: 'home.github-overview',
    cacheTtlMs: 10 * 60 * 1000,
  })
  const loadFavorites = useCallback(() => api.getAppleMusicFavorites(), [])
  const favoritesState = useLoadable(loadFavorites, {
    cacheKey: 'home.music-favorites',
    cacheTtlMs: 6 * 60 * 60 * 1000,
  })

  return (
    <>
      <section className="hero">
        <div className="eyebrow">Personal Website</div>
        <h1 className="hero-title">在这里记录代码、生活与正在发生的想法。</h1>
        <p className="lead hero-lead">
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
            to="/note"
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
        <h2>最近喜欢</h2>
        <div className="music-section">
          {favoritesState.loading ? (
            <p className="status">正在加载喜欢的歌曲...</p>
          ) : favoritesState.error ? (
            <p className="status">暂时无法读取喜欢的歌曲。</p>
          ) : (
            <FavoriteTracks tracks={favoritesState.data ?? []} />
          )}
        </div>
      </section>

      <section className="section">
        <h2>GitHub 概览</h2>
        <div className="github-overview-content">
          {profileState.loading ? (
            <p className="status">正在加载公开信息...</p>
          ) : profileState.error ? (
            <p className="status">暂时无法读取个人信息，请稍后刷新重试。</p>
          ) : (
            <p className="lead">
              {profileState.data?.name}
              {githubState.data ? `（${githubState.data.username}）` : ''}
              {githubState.data
                ? `目前公开仓库 ${githubState.data.publicRepos} 个，关注者 ${githubState.data.followers} 人。`
                : '的 GitHub 仓库信息正在加载中。'}
            </p>
          )}

          {githubState.loading ? (
            <ul className="list" aria-hidden="true">
              {Array.from({ length: 3 }).map((_, index) => (
                <li className="list-item skeleton-item" key={index}>
                  <div className="skeleton skeleton-title" />
                  <div className="skeleton skeleton-text" />
                  <div className="skeleton skeleton-meta" />
                </li>
              ))}
            </ul>
          ) : githubState.error ? (
            <p className="status">暂时无法读取 GitHub 仓库信息，请稍后刷新重试。</p>
          ) : (
            <ul className="list">
              {githubState.data?.picked.map((repo) => (
                <li className="list-item" key={repo.name}>
                  <a href={repo.url} target="_blank" rel="noreferrer">
                    <strong>{repo.name}</strong>
                    {!repo.isOwned && <span className="badge github-repo-tag">外部贡献</span>}
                  </a>
                  <p className="meta">{repo.description || 'No description'}</p>
                  <div className="meta mono">
                    {repo.language || 'Unknown'} · ★ {repo.stars} · 最近 {repo.contributions} 次贡献
                  </div>
                </li>
              ))}
            </ul>
          )}
        </div>
      </section>
    </>
  )
}
