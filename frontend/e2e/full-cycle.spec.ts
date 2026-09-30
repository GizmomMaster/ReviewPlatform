import { devices, expect, test } from '@playwright/test'
import { adminApi, createUser, json, mailsTo, password, signIn, unique } from './support'

interface Named {
  id: string
  code: string
}

test('полный цикл: запуск → опрос → отчёт → решение', async ({ page, browser }) => {
  const admin = await adminApi()
  const manager = await createUser(admin, 'Manager', 'Руководитель E2E')
  const track = (await json<Named[]>(await admin.get('/api/tracks'))).find((t) => t.code === 'backend')!
  const e3 = (await json<Named[]>(await admin.get('/api/grades'))).find((g) => g.code === 'E3')!
  const employeeName = `Сотрудник ${unique('e2e')}`
  const employee = await json<{ id: string }>(
    await admin.post('/api/employees', {
      data: { fullName: employeeName, email: `${unique('emp')}@e2e.test`, trackId: track.id, gradeId: e3.id, managerUserId: manager.id },
    }),
  )
  const respondents = [
    { name: 'Коллега E2E', email: `${unique('peer')}@e2e.test` },
    { name: 'Лид E2E', email: `${unique('lead')}@e2e.test` },
    { name: 'Менеджер E2E', email: `${unique('mgr')}@e2e.test` },
  ]

  await test.step('руководитель входит и меняет временный пароль', async () => {
    await signIn(page, manager.email, password)
    await expect(page.getByRole('heading', { name: 'Сессии оценки' })).toBeVisible()
  })

  await test.step('создаёт черновик сессии', async () => {
    await page.goto('/admin/sessions/new')
    await page.getByRole('combobox').first().click()
    await page.getByRole('option', { name: new RegExp(employeeName) }).click()
    for (const r of respondents) {
      await page.getByRole('button', { name: 'Добавить респондента' }).click()
      await page.getByPlaceholder('ФИО').last().fill(r.name)
      await page.getByPlaceholder('Email').last().fill(r.email)
    }
    await page.getByRole('button', { name: 'Сохранить черновик' }).click()
    await page.waitForURL(/\/admin\/sessions\/[0-9a-f-]{36}$/)
    await expect(page.getByText('Черновик', { exact: true })).toBeVisible()
  })
  const sessionUrl = new URL(page.url()).pathname

  let links: string[] = []
  await test.step('запускает опрос и получает ссылки', async () => {
    await page.getByRole('button', { name: 'Запустить' }).click()
    await page.getByRole('alertdialog').getByRole('button', { name: 'Запустить' }).click()
    const dialog = page.getByRole('dialog', { name: 'Ссылки на анкеты' })
    await expect(dialog).toBeVisible()
    links = await dialog.locator('.font-mono').allTextContents()
    expect(links).toHaveLength(4)
    await dialog.getByRole('button', { name: 'Готово' }).click()
    await expect(page.getByText('Идёт опрос', { exact: true })).toBeVisible()
  })

  await test.step('приглашения приходят на почту', async () => {
    test.skip(!process.env.E2E_MAILPIT_URL, 'Mailpit не настроен')
    for (const r of respondents) {
      await expect.poll(async () => (await mailsTo(r.email))?.map((m) => m.Subject) ?? [], { timeout: 60_000 }).toContainEqual(
        expect.stringContaining(`Приглашение оценить сотрудника ${employeeName}`),
      )
    }
  })

  for (const [index, link] of links.entries()) {
    await test.step(`респондент ${index + 1} из ${links.length} заполняет анкету`, async () => {
      // Первый — с телефона: анкета mobile-first
      const context = await browser.newContext(index === 0 ? { ...devices['iPhone 13'] } : {})
      const survey = await context.newPage()
      await survey.goto(new URL(link).pathname)
      await survey.getByRole('button', { name: 'Начать' }).click()
      for (;;) {
        const cards = survey.locator('article')
        const count = await cards.count()
        // 0 Не проявляет · 1 Эпизодически · 2 Как правило · 3 Стабильно · Не могу оценить: «2» не требует комментария
        for (let i = 0; i < count; i++) await cards.nth(i).getByRole('radio').nth(2).click()
        const next = survey.getByRole('button', { name: /Далее|К отправке/ })
        if ((await next.count()) === 0) break
        await next.click()
      }
      await expect(survey.getByText('Все утверждения оценены')).toBeVisible()
      await survey.getByRole('button', { name: 'Отправить оценку' }).click()
      await survey.getByRole('alertdialog').getByRole('button', { name: 'Отправить' }).click()
      await expect(survey.getByText('Спасибо! Оценка отправлена')).toBeVisible()
      await context.close()
    })
  }

  await test.step('сессия ждёт решения, отчёт построен', async () => {
    await page.goto(sessionUrl)
    await expect(page.getByText('Ждёт решения', { exact: true })).toBeVisible()
    await page.getByRole('link', { name: 'Отчёт', exact: true }).click()
    await expect(page.getByRole('heading', { name: `Отчёт: ${employeeName}` })).toBeVisible()
  })

  await test.step('руководитель получает письмо о завершении опроса', async () => {
    test.skip(!process.env.E2E_MAILPIT_URL, 'Mailpit не настроен')
    await expect.poll(async () => (await mailsTo(manager.email))?.map((m) => m.Subject) ?? [], { timeout: 60_000 }).toContainEqual(
      expect.stringContaining(`Все участники завершили опрос по сотруднику ${employeeName}`),
    )
  })

  await test.step('решение: повышение до E4 с планом развития', async () => {
    await page.goto(`${sessionUrl}/decision`)
    await expect(page.getByRole('heading', { name: `Решение: ${employeeName}` })).toBeVisible()
    await page.getByLabel('Обоснование').fill('Стабильно показывает поведение уровня E4')
    await page.getByRole('button', { name: 'Свой пункт' }).click()
    await page.getByLabel('Пункт 1').fill('Провести дизайн-ревью модуля')
    await page.getByRole('button', { name: 'Сохранить решение' }).click()
    await page.getByRole('alertdialog').getByRole('button', { name: 'Сохранить' }).click()
    await page.waitForURL(new RegExp(`${sessionUrl}$`))
    await expect(page.getByText('Закрыта', { exact: true })).toBeVisible()
  })

  const updated = await json<{ gradeCode: string }>(await admin.get(`/api/employees/${employee.id}`))
  expect(updated.gradeCode).toBe('E4')
})
