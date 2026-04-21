import { Link } from 'react-router-dom'

type Props = {
  to: string
  title: string
  label: string
  badge: string
}

export function EntryCard({ to, title, label, badge }: Props) {
  return (
    <Link className="entry-card" to={to}>
      <div>
        <div className="entry-label">{label}</div>
        <h3 className="entry-title">{title}</h3>
      </div>
      <span className="badge">{badge}</span>
    </Link>
  )
}
