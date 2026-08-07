import type { CSSProperties, ReactNode } from 'react'
import { useState } from 'react'
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
    margin: 0,
  },
  'code[class*="language-"]': {
    background: 'transparent',
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

type CodeBlockProps = {
  value: string
  children: ReactNode
}

function CodeBlock({ value, children }: CodeBlockProps) {
  const [copied, setCopied] = useState(false)

  const handleCopy = async () => {
    const trimmedValue = value.replace(/\n$/, '')

    try {
      await navigator.clipboard.writeText(trimmedValue)
      setCopied(true)
    } catch {
      const textarea = document.createElement('textarea')
      textarea.value = trimmedValue
      textarea.setAttribute('readonly', '')
      textarea.style.position = 'absolute'
      textarea.style.left = '-9999px'
      document.body.appendChild(textarea)
      textarea.select()
      const success = document.execCommand('copy')
      document.body.removeChild(textarea)
      if (success) {
        setCopied(true)
      }
    }

    if (typeof window !== 'undefined') {
      window.setTimeout(() => setCopied(false), 1600)
    }
  }

  return (
    <div className="code-block">
      <button
        className="code-copy"
        type="button"
        onClick={handleCopy}
        aria-label={copied ? 'Copied code' : 'Copy code'}
      >
        {copied ? 'Copied' : 'Copy'}
      </button>
      {children}
    </div>
  )
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
        <CodeBlock value={value}>
          <pre>
            <code className={className} {...props}>
              {value.replace(/\n$/, '')}
            </code>
          </pre>
        </CodeBlock>
      )
    }

    return (
      <CodeBlock value={value}>
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
            background: 'var(--code-block-bg)',
            color: 'var(--code-block-text)',
          }}
          codeTagProps={{
            style: {
              fontFamily: 'var(--font-code)',
              color: 'var(--code-block-text)',
            },
          }}
        >
          {value.replace(/\n$/, '')}
        </SyntaxHighlighter>
      </CodeBlock>
    )
  },
}

type Props = {
  markdown: string
}

export function MarkdownContent({ markdown }: Props) {
  return (
    <div className="markdown-body">
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        rehypePlugins={[rehypeSanitize]}
        components={markdownComponents}
      >
        {markdown}
      </ReactMarkdown>
    </div>
  )
}
