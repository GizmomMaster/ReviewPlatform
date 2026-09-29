import { expect, test } from '@playwright/test'
import { adminApi, createUser, json, password, signIn, unique } from './support'

interface Matrix {
  groups: { name: string; indicators: { text: string }[] }[]
}

// Отдельное направление: seed-матрица «backend» нужна другим тестам нетронутой
test('администратор правит матрицу: группа, индикаторы, порядок, архив, экспорт и импорт', async ({ page }) => {
  const admin = await adminApi()
  const adminUser = await createUser(admin, 'Admin', 'Администратор E2E')
  const code = unique('t').slice(0, 20)
  const trackName = `Направление ${code}`
  const track = await json<{ id: string }>(await admin.post('/api/tracks', { data: { code, name: trackName } }))
  const matrix = async () => json<Matrix>(await admin.get(`/api/tracks/${track.id}/matrix`))

  await signIn(page, adminUser.email, password)
  await page.goto('/admin/matrix')
  await page.getByRole('combobox').first().click()
  await page.getByRole('option', { name: trackName }).click()

  await test.step('группа и два индикатора E1', async () => {
    await page.getByRole('button', { name: 'Группа', exact: true }).click()
    await page.getByRole('dialog').getByLabel('Название').fill('Коммуникация')
    await page.getByRole('dialog').getByRole('button', { name: 'Создать' }).click()
    await expect(page.getByRole('cell', { name: /Коммуникация/ })).toBeVisible()

    for (const text of ['Слушает собеседника', 'Задаёт уточняющие вопросы']) {
      await page.getByRole('button', { name: 'Добавить', exact: true }).first().click()
      await page.getByRole('dialog').getByLabel('Текст').fill(text)
      await page.getByRole('dialog').getByRole('button', { name: 'Добавить' }).click()
      await expect(page.getByText(text)).toBeVisible()
    }
  })

  await test.step('порядок меняется перетаскиванием', async () => {
    await page.getByText('Задаёт уточняющие вопросы').dragTo(page.getByText('Слушает собеседника'))
    await expect.poll(async () => (await matrix()).groups[0]?.indicators.map((i) => i.text)).toEqual(['Задаёт уточняющие вопросы', 'Слушает собеседника'])
  })

  await test.step('индикатор уходит в архив', async () => {
    await page.getByText('Слушает собеседника').hover()
    await page.getByRole('listitem').filter({ hasText: 'Слушает собеседника' }).getByRole('button', { name: 'Действия с индикатором' }).click()
    await page.getByRole('menuitem', { name: 'В архив' }).click()
    await page.getByRole('alertdialog').getByRole('button', { name: 'В архив' }).click()
    await expect(page.getByText('Слушает собеседника')).toHaveCount(0)
  })

  await test.step('экспорт и повторный импорт того же файла ничего не меняют', async () => {
    const downloading = page.waitForEvent('download')
    await page.getByRole('button', { name: 'Экспорт' }).click()
    const file = await (await downloading).path()

    await page.getByRole('button', { name: 'Импорт' }).click()
    const dialog = page.getByRole('dialog', { name: 'Импорт матрицы из Excel' })
    await dialog.getByLabel('Файл .xlsx по шаблону').setInputFiles(file)
    await dialog.getByRole('button', { name: 'Проверить' }).click()
    await expect(dialog.getByText('Файл совпадает с текущей матрицей')).toBeVisible()
    await expect(dialog.getByRole('button', { name: 'Применить изменения' })).toBeDisabled()
    await dialog.getByRole('button', { name: 'Отмена' }).click()
  })

  await test.step('правила ролей открываются', async () => {
    await page.getByRole('tab', { name: 'Правила ролей' }).click()
    await expect(page.getByRole('checkbox', { name: 'E8, Коллега: роль допустима' })).not.toBeChecked()
    await expect(page.getByRole('checkbox', { name: 'E3, Коллега: роль допустима' })).toBeChecked()
  })
})
