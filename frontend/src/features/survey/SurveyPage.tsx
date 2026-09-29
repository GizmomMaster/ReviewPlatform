import { useParams } from 'react-router'

export function SurveyPage() {
  const { token } = useParams<{ token: string }>()

  return (
    <div className="mx-auto min-h-svh max-w-2xl p-4">
      <h1 className="text-xl font-semibold">Оценка компетенций</h1>
      <p className="text-sm text-muted-foreground">Анкета появится на этапе 4{token ? '' : ' (нет токена)'}.</p>
    </div>
  )
}
