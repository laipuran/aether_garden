import { Navigate, Route, Routes } from 'react-router-dom'
import { Layout } from './components/Layout'
import { AboutPage } from './pages/AboutPage'
import { BlogDetailPage, BlogPage } from './pages/BlogPage'
import { HomePage } from './pages/HomePage'
import { NoteDetailPage, NotesPage } from './pages/NotesPage'

function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route path="/" element={<HomePage />} />
        <Route path="/blog" element={<BlogPage />} />
        <Route path="/blog/:slug" element={<BlogDetailPage />} />
        <Route path="/notes" element={<NotesPage />} />
        <Route path="/notes/:slug" element={<NoteDetailPage />} />
        <Route path="/about" element={<AboutPage />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  )
}

export default App
