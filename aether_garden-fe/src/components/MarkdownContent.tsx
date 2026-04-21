import type { CSSProperties } from 'react'
import ReactMarkdown from 'react-markdown'
import type { Components } from 'react-markdown'
import rehypeSanitize from 'rehype-sanitize'
import remarkGfm from 'remark-gfm'
import { PrismLight as SyntaxHighlighter } from 'react-syntax-highlighter'
import bash from 'react-syntax-highlighter/dist/esm/languages/prism/bash'
import cpp from 'react-syntax-highlighter/dist/esm/languages/prism/cpp'
import csharp from 'react-syntax-highlighter/dist/esm/languages/prism/csharp'
import go from 'react-syntax-highlighter/dist/esm/languages/prism/go'
import json from 'react-syntax-highlighter/dist/esm/languages/prism/json'
import markup from 'react-syntax-highlighter/dist/esm/languages/prism/markup'

SyntaxHighlighter.registerLanguage('csharp', csharp)
SyntaxHighlighter.registerLanguage('cs', csharp)
SyntaxHighlighter.registerLanguage('cpp', cpp)
SyntaxHighlighter.registerLanguage('c++', cpp)
SyntaxHighlighter.registerLanguage('go', go)
SyntaxHighlighter.registerLanguage('bash', bash)
SyntaxHighlighter.registerLanguage('json', json)
SyntaxHighlighter.registerLanguage('xaml', markup)

const vscodeInspiredLightTheme: Record<string, CSSProperties> = {
  'pre[class*="language-"]': {
    background: '#eff5f1',
    color: '#24352f',
    margin: 0,
  },
  'code[class*="language-"]': {
    background: 'transparent',
    color: '#24352f',
    fontFamily: 'var(--font-code)',
    fontSize: '0.92rem',
    lineHeight: 1.6,
  },
  comment: { color: '#6f7f79' },
  prolog: { color: '#6f7f79' },
  doctype: { color: '#6f7f79' },
  cdata: { color: '#6f7f79' },
  punctuation: { color: '#576b63' },
  property: { color: '#0f6e9d' },
  tag: { color: '#0f6e9d' },
  boolean: { color: '#0f6e9d' },
  number: { color: '#0f6e9d' },
  constant: { color: '#0f6e9d' },
  symbol: { color: '#0f6e9d' },
  attrName: { color: '#0f6e9d' },
  selector: { color: '#0f6e9d' },
  string: { color: '#2f7d48' },
  char: { color: '#2f7d48' },
  builtin: { color: '#2f7d48' },
  inserted: { color: '#2f7d48' },
  operator: { color: '#824f9c' },
  entity: { color: '#824f9c' },
  url: { color: '#824f9c' },
  atrule: { color: '#824f9c' },
  keyword: { color: '#004f9f' },
  function: { color: '#7a3e9d' },
  regex: { color: '#9a6f00' },
  important: { color: '#993322', fontWeight: 600 },
}

const languageAliasMap: Record<string, string> = {
  'c#': 'csharp',
  cs: 'csharp',
  csharp: 'csharp',
  'c++': 'cpp',
  cc: 'cpp',
  cxx: 'cpp',
  cpp: 'cpp',
  go: 'go',
  golang: 'go',
  bash: 'bash',
  sh: 'bash',
  shell: 'bash',
  zsh: 'bash',
  json: 'json',
  xaml: 'xaml',
}

const markdownComponents: Components = {
  pre({ children }) {
    return <>{children}</>
  },
  code({ className, children, ...props }) {
    const languageMatch = /language-([\w#+-]+)/.exec(className || '')
    const rawLanguage = languageMatch?.[1]?.toLowerCase()
    const language = rawLanguage ? languageAliasMap[rawLanguage] : undefined
    const value = String(children)
    const isBlock = Boolean(className) || value.includes('\n')

    if (!isBlock) {
      return (
        <code className={className} {...props}>
          {children}
        </code>
      )
    }

    if (!language) {
      return (
        <pre>
          <code className={className} {...props}>
            {value.replace(/\n$/, '')}
          </code>
        </pre>
      )
    }

    return (
      <SyntaxHighlighter
        language={language}
        style={vscodeInspiredLightTheme}
        PreTag="pre"
        customStyle={{
          margin: '1rem 0',
          border: '1px solid var(--line)',
          borderRadius: '14px',
          padding: '0.9rem 1rem',
          overflowX: 'auto',
          background: '#eff5f1',
        }}
        codeTagProps={{ style: { fontFamily: 'var(--font-code)' } }}
      >
        {value.replace(/\n$/, '')}
      </SyntaxHighlighter>
    )
  },
}

type Props = {
  markdown: string
  fallbackParagraphs: string[]
}

export function MarkdownContent({ markdown, fallbackParagraphs }: Props) {
  const source = markdown.trim() ? markdown : fallbackParagraphs.join('\n\n')

  return (
    <div className="markdown-body">
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        rehypePlugins={[rehypeSanitize]}
        components={markdownComponents}
      >
        {source}
      </ReactMarkdown>
    </div>
  )
}
