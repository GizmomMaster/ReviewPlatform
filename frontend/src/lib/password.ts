const LOWER = 'abcdefghijkmnpqrstuvwxyz'
const UPPER = 'ABCDEFGHJKLMNPQRSTUVWXYZ'
const DIGITS = '23456789'

/** Временный пароль: 12 символов, гарантированно есть строчная, заглавная и цифра. */
export function generatePassword(length = 12): string {
  const all = LOWER + UPPER + DIGITS
  const random = crypto.getRandomValues(new Uint32Array(length))
  const chars = Array.from(random, (n) => all[n % all.length] ?? 'x')
  chars[0] = LOWER[(random[0] ?? 0) % LOWER.length] ?? 'a'
  chars[1] = UPPER[(random[1] ?? 0) % UPPER.length] ?? 'A'
  chars[2] = DIGITS[(random[2] ?? 0) % DIGITS.length] ?? '2'
  return chars.sort(() => (crypto.getRandomValues(new Uint8Array(1))[0] ?? 0) - 128).join('')
}
