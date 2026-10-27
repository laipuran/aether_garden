import type { MusicTrack } from '../types'

type Props = {
  tracks: MusicTrack[]
}

export function FavoriteTracks({ tracks }: Props) {
  if (!tracks.length) {
    return <p className="status">暂时无法读取喜欢的歌曲。</p>
  }

  return (
    <div className="music-grid">
      {tracks.map((track) => (
        <a
          className="music-card"
          href={track.neteaseUrl || track.appleMusicUrl}
          target="_blank"
          rel="noreferrer"
          key={`${track.name}-${track.artist}`}
        >
          <div className="music-art">
            {track.artworkUrl ? (
              <img src={track.artworkUrl} alt={`${track.name} 封面`} />
            ) : (
              <div className="music-art-fallback" aria-hidden="true" />
            )}
          </div>
          <div>
            <strong className="music-title">{track.name}</strong>
            <div className="meta">{track.artist}</div>
          </div>
        </a>
      ))}
    </div>
  )
}
