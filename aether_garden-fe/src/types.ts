export type Profile = {
  name: string
  title: string
  bio: string
  location: string
  school: string
  website: string
  github: string
  interests: string[]
  contactEmail: string
}

export type PostSummary = {
  slug: string
  title: string
  excerpt: string
  date: string
  tags: string[]
}

export type PostDetail = PostSummary & {
  content: string[]
}

export type GithubRepo = {
  name: string
  description: string
  url: string
  language: string
  stars: number
}

export type GithubOverview = {
  username: string
  profileUrl: string
  publicRepos: number
  followers: number
  following: number
  picked: GithubRepo[]
  source: string
}
