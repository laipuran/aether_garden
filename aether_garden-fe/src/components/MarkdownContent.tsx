import ReactMarkdown from 'react-markdown'
import rehypeSanitize from 'rehype-sanitize'
import remarkGfm from 'remark-gfm'

type Props = {
  markdown: string
  fallbackParagraphs: string[]
}

export function MarkdownContent({ markdown, fallbackParagraphs }: Props) {
  const source = markdown.trim() ? markdown : fallbackParagraphs.join('\n\n')

  return (
    <div className="markdown-body">
      <ReactMarkdown remarkPlugins={[remarkGfm]} rehypePlugins={[rehypeSanitize]}>
        {source}
      </ReactMarkdown>
    </div>
  )
}
