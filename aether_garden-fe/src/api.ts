import type { GithubOverview, PostDetail, PostSummary, Profile } from './types'

const baseUrl =
  import.meta.env.VITE_API_BASE_URL?.trim() || 'http://localhost:5109/api'

async function fetchJson<T>(path: string): Promise<T> {
  const response = await fetch(`${baseUrl}${path}`)
  if (!response.ok) {
    throw new Error(`Request failed: ${response.status}`)
  }
  return response.json() as Promise<T>
}

export const api = {
  getProfile: () => fetchJson<Profile>('/profile'),
  getBlogs: () => fetchJson<PostSummary[]>('/blog'),
  getBlogBySlug: (slug: string) =>
    fetchJson<PostDetail>(`/blog/${encodeURIComponent(slug)}`),
  getNotes: () => fetchJson<PostSummary[]>('/notes'),
  getNoteBySlug: (slug: string) =>
    fetchJson<PostDetail>(`/notes/${encodeURIComponent(slug)}`),
  getGithubOverview: () => fetchJson<GithubOverview>('/github/overview'),
}
